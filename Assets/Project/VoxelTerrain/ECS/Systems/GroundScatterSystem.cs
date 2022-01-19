using UnityEngine;
using UnityEngine.Profiling;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Collections;
using Unity.Rendering.HybridV2;
using Unity.Jobs;
using VoxelTerrain.ECS.Components;

namespace VoxelTerrain
{
    namespace ECS
    {
        namespace Systems
        {
            public class GroundScatterSystem<Component, Author> : SystemBase where Component : struct, IGroundScatter where Author : GroundScatterAuthor {
                public static GroundScatterSystem<Component, Author> instance;

                protected BeginInitializationEntityCommandBufferSystem ecbSystem;
                protected World defaultWorld;
                protected EntityManager entityManager;
                protected Biome[] biomes;

                protected Component groundScatterData;
                protected Author groundScatterAuthor;
                protected DistanceCullAutor distanceCullAutor;

                protected Entity prefab;

                protected EntityArchetype NewGroundScatterArchetype()
                {
                    if (entityManager == null) { return default; }
                    return entityManager.CreateArchetype(
                        typeof(Component),
                        typeof(Translation),
                        typeof(Rotation),
                        typeof(Scale),
                        typeof(LocalToWorld),
                        typeof(RenderMesh),
                        typeof(RenderBounds),
                        typeof(ReadyToSpawn),
                        typeof(FrustumCull),
                        typeof(DistanceCull),
                        typeof(DisableRendering)
                    );
                }

                protected override void OnCreate()
                {
                    instance = this;
                    ecbSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
                    defaultWorld = World.DefaultGameObjectInjectionWorld;
                    entityManager = defaultWorld.EntityManager;

                    prefab = entityManager.CreateEntity(NewGroundScatterArchetype());
                }

                protected override void OnStartRunning()
                {
                    groundScatterAuthor = TerrainManager.instance.groundScatter.OfType<Author>().FirstOrDefault();
                    //Debug.Log(groundScatterAuthor.GetType());
                    if (groundScatterAuthor == null) { Enabled = false; return; }
                    distanceCullAutor = groundScatterAuthor.GetComponent<DistanceCullAutor>();

                    DistanceCull distanceCull = new DistanceCull
                    {
                        distance = distanceCullAutor.distance
                    };

                    Debug.Log($"Mesh: {groundScatterAuthor.mesh}");

                    RenderMesh meshData = new RenderMesh {
                        mesh = groundScatterAuthor.mesh,
                        material = groundScatterAuthor.material,
                        castShadows = UnityEngine.Rendering.ShadowCastingMode.On,
                        receiveShadows = true
                    };
                    Unity.Mathematics.AABB aabb = new AABB();
                    aabb.Center = meshData.mesh.bounds.center;
                    aabb.Extents = meshData.mesh.bounds.extents;
                    RenderBounds renderBounds = new RenderBounds
                    {
                        Value = aabb
                    };

                    entityManager.SetSharedComponentData(prefab, meshData);
                    entityManager.SetComponentData(prefab, renderBounds);
                    entityManager.SetComponentData(prefab, distanceCull);
                    entityManager.SetComponentData(prefab, groundScatterData);

                    biomes = new Biome[TerrainManager.instance.biomes.Count];
                    for (int i = 0; i < biomes.Length; i++) {
                        biomes[i] = TerrainManager.instance.biomes[i];
                    }
                }

                protected override void OnUpdate()
                {
                    
                }
            }

            [UpdateInGroup(typeof(InitializationSystemGroup))]
            public class RockScatterSystem : GroundScatterSystem<RockGroundScatter, RockGroundScatterAuthor> {

                private Unity.Mathematics.Random rand;

                protected override void OnCreate()
                {
                    base.OnCreate();
                }

                protected override void OnStartRunning()
                {
                    base.OnStartRunning();
                    if (!groundScatterAuthor) { Enabled = false; return; }
                    groundScatterData = groundScatterAuthor.GetComponentData();
                }

                protected override void OnUpdate()
                {
                    ChunkComponent closestChunk = default;
                    Entity closestChunkEntity = default;
                    float closestChunkDistance = float.MaxValue;
                    int chunkEntityInQueryIndex = 0;
                    Entities.WithoutBurst().WithAll<ChunkInitialized>().ForEach((Entity e, int entityInQueryIndex, in ChunkComponent chunk) => {
                        float distance = math.distance(TerrainManager.instance.loadingFromChunk, chunk.gridPosition);
                        if (distance < TerrainManager.instance.renderDistance) {
                            if (distance < closestChunkDistance) {
                                closestChunkDistance = distance;
                                closestChunk = chunk;
                                closestChunkEntity = e;
                                chunkEntityInQueryIndex = entityInQueryIndex;
                            }
                        }
                    }).Run();

                    Translation chunkTranslation = entityManager.GetComponentData<Translation>(closestChunkEntity);
                    RockGroundScatter scatterData = groundScatterData;
                    Entity scatterPrefab = prefab;
                    ClimateSettings settings = TerrainManager.instance.terrainSettings;
                    int seed = TerrainManager.instance.terrainSettings.seed;

                    float increment = ((float) closestChunk.grid.chunkSize / groundScatterData.scatterDensity);

                    JobHandle previous = default;

                    for (float h = 0; h < closestChunk.grid.chunkSize; h += increment)
                    {
                        for (float j = 0; j < closestChunk.grid.chunkSize; j += increment)
                        {
                            var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();
                            Unity.Mathematics.Random rand = new Unity.Mathematics.Random((uint) math.abs(h + closestChunk.gridPosition.x * j + closestChunk.gridPosition.y) + 1);
                            float x = (j + rand.NextFloat(-increment, increment)) % closestChunk.grid.chunkSize;
                            float y = (h + rand.NextFloat(-increment, increment)) % closestChunk.grid.chunkSize;

                            float2 climate = TerrainNoise.Climate(x, y, settings, closestChunk.gridPosition, closestChunk.grid.chunkSize, seed);
                            float idealness = TerrainNoise.ClimateIdealness(new float2(scatterData.minTemperature, scatterData.minMoisture), new float2(scatterData.maxTemperature, scatterData.maxMoisture), climate);
                            if (rand.NextFloat(0, 1) > idealness) {
                                continue;
                            }

                            float height = TerrainNoise.GetHeightAtPoint(x, y, climate, this.biomes, 1, closestChunk.gridPosition, closestChunk.grid.chunkSize, seed);
                            if (height < scatterData.minHeight || height > scatterData.maxHeight) { continue; }

                            JobHandle job = Job.WithBurst().WithCode(() =>
                            {
                                float3 localPosition = new float3(x, height, y) + (scatterData.offset * scatterData.uniformScale);
                                float3 worldDelta = new float3(localPosition.x * closestChunk.grid.voxelSize, height, localPosition.z * closestChunk.grid.voxelSize);
                                float3 worldPosition = chunkTranslation.Value + worldDelta;

                                float3 randomEuler = rand.NextFloat3(float3.zero, new float3(360f, 360f, 360f));
                                Rotation randomRotation = new Rotation
                                {
                                    Value = quaternion.Euler(randomEuler)
                                };

                                Entity scatterEntity = ecb.Instantiate(chunkEntityInQueryIndex, scatterPrefab);
                                RockGroundScatter newScatterData = scatterData;
                                newScatterData.chunk = closestChunk;
                                newScatterData.localPosition = localPosition;

                                ecb.SetComponent(chunkEntityInQueryIndex, scatterEntity, new Translation
                                {
                                    Value = worldPosition
                                });
                                ecb.SetComponent(chunkEntityInQueryIndex, scatterEntity, randomRotation);
                                ecb.SetComponent(chunkEntityInQueryIndex, scatterEntity, new Scale
                                {
                                    Value = scatterData.uniformScale
                                });
                                ecb.SetComponent(chunkEntityInQueryIndex, scatterEntity, scatterData);
                                ecb.RemoveComponent<Prefab>(chunkEntityInQueryIndex, scatterEntity);
                                ecb.RemoveComponent<ChunkInitialized>(chunkEntityInQueryIndex, closestChunkEntity);
                            }).Schedule(Dependency);

                            ecbSystem.AddJobHandleForProducer(job);
                            previous = JobHandle.CombineDependencies(previous, job);
                        }
                    }

                    Dependency = previous;

                    //entityManager.RemoveComponent<ChunkInitialized>(closestChunkEntity);
                }
            }

            [UpdateInGroup(typeof(InitializationSystemGroup))]
            public class TreeGroundScatterSystem : GroundScatterSystem<TreeGroundScatter, TreeGroundScatterAuthor>
            {

                private Unity.Mathematics.Random rand;

                protected override void OnCreate()
                {
                    base.OnCreate();
                }

                protected override void OnStartRunning()
                {
                    base.OnStartRunning();
                    if (!Enabled) { return; }
                    groundScatterData = groundScatterAuthor.GetComponentData();
                }

                protected override void OnUpdate()
                {
                    Profiler.BeginSample("TreeGroundScatterSystem");
                    Profiler.BeginSample("Get Closest Chunk");
                    ChunkComponent closestChunk = default;
                    Entity closestChunkEntity = default;
                    float closestChunkDistance = float.MaxValue;
                    int chunkEntityInQueryIndex = 0;
                    Entities.WithoutBurst().WithAll<ChunkInitialized>().ForEach((Entity e, int entityInQueryIndex, in ChunkComponent chunk) => {
                        float distance = math.distance(TerrainManager.instance.loadingFromChunk, chunk.gridPosition);
                        if (distance < TerrainManager.instance.renderDistance)
                        {
                            if (distance < closestChunkDistance)
                            {
                                closestChunkDistance = distance;
                                closestChunk = chunk;
                                closestChunkEntity = e;
                                chunkEntityInQueryIndex = entityInQueryIndex;
                            }
                        }
                    }).Run();
                    Profiler.EndSample();

                    Profiler.BeginSample("Initialize Job");
                    Translation chunkTranslation = entityManager.GetComponentData<Translation>(closestChunkEntity);
                    EntityArchetype archetype = NewGroundScatterArchetype();
                    TreeGroundScatter scatterData = groundScatterData;
                    Entity scatterPrefab = prefab;
                    ClimateSettings settings = TerrainManager.instance.terrainSettings;
                    int seed = TerrainManager.instance.terrainSettings.seed;

                    float increment = ((float)closestChunk.grid.chunkSize / groundScatterData.scatterDensity);

                    JobHandle previous = new JobHandle();
                    Profiler.EndSample();

                    Profiler.BeginSample("Job Loop");
                    for (float h = 0; h < closestChunk.grid.chunkSize; h += increment)
                    {
                        for (float j = 0; j < closestChunk.grid.chunkSize; j += increment)
                        {
                            var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();
                            Unity.Mathematics.Random rand = new Unity.Mathematics.Random((uint)math.abs(h + closestChunk.gridPosition.x * j + closestChunk.gridPosition.y) + 1);
                            NativeArray<Biome> nativeBiomes = new NativeArray<Biome>(biomes, Allocator.Persistent);

                            Profiler.BeginSample("Schedule Job");
                            JobHandle job = Job.WithBurst().WithReadOnly(nativeBiomes).WithDisposeOnCompletion(nativeBiomes).WithCode(() =>
                            {
                                float x = (j + rand.NextFloat(-increment, increment)) % closestChunk.grid.chunkSize;
                                float y = (h + rand.NextFloat(-increment, increment)) % closestChunk.grid.chunkSize;

                                float2 climate = TerrainNoise.Climate(x, y, settings, closestChunk.gridPosition, closestChunk.grid.chunkSize, seed);
                                float idealness = scatterData.scatterDensity * TerrainNoise.ClimateIdealness(new float2(scatterData.minTemperature, scatterData.minMoisture), new float2(scatterData.maxTemperature, scatterData.maxMoisture), climate);
                                float rng = rand.NextFloat(0, 1);
                                if (rng > idealness)
                                {
                                    return;
                                }

                                float height = TerrainNoise.GetHeightAtPoint(x, y, climate, nativeBiomes, 1, closestChunk.gridPosition, closestChunk.grid.chunkSize, seed);
                                if (height < scatterData.minHeight || height > scatterData.maxHeight) { return; }

                                float3 localPosition = new float3(x, height, y) + (scatterData.offset * scatterData.uniformScale);
                                float3 worldDelta = new float3(localPosition.x * closestChunk.grid.voxelSize, height, localPosition.z * closestChunk.grid.voxelSize);
                                float3 worldPosition = chunkTranslation.Value + worldDelta;

                                float3 randomEuler = rand.NextFloat3(float3.zero, new float3(0f, 360f, 0f));
                                Rotation randomRotation = new Rotation
                                {
                                    Value = quaternion.Euler(randomEuler)
                                };

                                Entity scatterEntity = ecb.Instantiate(chunkEntityInQueryIndex, scatterPrefab);
                                TreeGroundScatter newScatterData = scatterData;
                                newScatterData.chunk = closestChunk;
                                newScatterData.localPosition = localPosition;

                                ecb.SetComponent(chunkEntityInQueryIndex, scatterEntity, new Translation
                                {
                                    Value = worldPosition
                                });
                                ecb.SetComponent(chunkEntityInQueryIndex, scatterEntity, randomRotation);
                                ecb.SetComponent(chunkEntityInQueryIndex, scatterEntity, new Scale
                                {
                                    Value = scatterData.uniformScale
                                });
                                ecb.SetComponent(chunkEntityInQueryIndex, scatterEntity, scatterData);
                                ecb.RemoveComponent<ChunkInitialized>(chunkEntityInQueryIndex, closestChunkEntity);
                            }).Schedule(Dependency);

                            previous = JobHandle.CombineDependencies(previous, job);
                            ecbSystem.AddJobHandleForProducer(job);
                            Profiler.EndSample();
                        }
                    }
                    Profiler.EndSample();
                    Dependency = previous;

                    //entityManager.RemoveComponent<ChunkInitialized>(closestChunkEntity);
                    Profiler.EndSample();
                }
            }
        }
    }
}
