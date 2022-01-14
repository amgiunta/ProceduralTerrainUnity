using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
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

                protected EndInitializationEntityCommandBufferSystem endInitializationEntityCommandBufferSystem;
                protected World defaultWorld;
                protected EntityManager entityManager;
                protected Biome[] biomes;

                protected Component groundScatterData;
                protected Author groundScatterAuthor;
                protected RenderMesh meshData;

                protected Entity prefab;

                protected uint frameScatterLimit;

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
                        typeof(ReadyToSpawn)
                    );
                }

                protected override void OnCreate()
                {
                    instance = this;
                    endInitializationEntityCommandBufferSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
                    defaultWorld = World.DefaultGameObjectInjectionWorld;
                    entityManager = defaultWorld.EntityManager;

                    frameScatterLimit = 10;

                    prefab = entityManager.CreateEntity(NewGroundScatterArchetype());
                }

                protected override void OnStartRunning()
                {
                    groundScatterAuthor = TerrainManager.instance.groundScatter.OfType<Author>().FirstOrDefault();
                    
                    meshData = new RenderMesh {
                        mesh = groundScatterAuthor.mesh,
                        material = groundScatterAuthor.material,
                        castShadows = UnityEngine.Rendering.ShadowCastingMode.On,
                        receiveShadows = true
                    };

                    entityManager.SetSharedComponentData(prefab, meshData);

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

                    var ecb = endInitializationEntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
                    Translation chunkTranslation = entityManager.GetComponentData<Translation>(closestChunkEntity);
                    EntityArchetype archetype = NewGroundScatterArchetype();
                    RockGroundScatter scatterData = groundScatterData;
                    Entity scatterPrefab = prefab;
                    ClimateSettings settings = TerrainManager.instance.terrainSettings;
                    int seed = TerrainManager.instance.terrainSettings.seed;

                    float increment = ((float) closestChunk.grid.chunkSize / groundScatterData.scatterDensity);

                    JobHandle previous = new JobHandle();

                    for (float h = 0; h < closestChunk.grid.chunkSize; h += increment)
                    {
                        for (float j = 0; j < closestChunk.grid.chunkSize; j += increment)
                        {
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
                            }).Schedule(previous);

                            previous = job;
                        }
                    }

                    entityManager.RemoveComponent<ChunkInitialized>(closestChunkEntity);
                    endInitializationEntityCommandBufferSystem.AddJobHandleForProducer(previous);                    
                }
            }
        }
    }
}
