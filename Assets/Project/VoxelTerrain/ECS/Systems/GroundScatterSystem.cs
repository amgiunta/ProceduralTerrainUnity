using UnityEngine;
using UnityEngine.Profiling;
using System;
using System.Collections;
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

namespace VoxelTerrain.ECS.Systems {
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true, OrderLast = false)]
    public class GroundScatterSystem : SystemBase {

        protected BeginPresentationEntityCommandBufferSystem ecbSystem;
        protected World defaultWorld;
        protected EntityManager entityManager;
        protected Biome[] biomes;
        protected ClimateSettings climateSettings;
        protected int terrainSeed;

        protected int framesToExecution = 5;
        private int frame;
        private int lastExecution;

        protected override void OnCreate() {
            ecbSystem = World.GetOrCreateSystem<BeginPresentationEntityCommandBufferSystem>();
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
        }

        protected override void OnStartRunning()
        {
            climateSettings = TerrainManager.instance.terrainSettings;
            terrainSeed = TerrainManager.instance.terrainSettings.seed;
            biomes = new Biome[TerrainManager.instance.biomes.Count];
            int count = 0;
            foreach (Biome biome in TerrainManager.instance.biomes) {
                biomes[count] = biome;
                count++;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            //prefabs.Clear();
        }

        protected virtual Entity GetNearestChunk(out ChunkComponent chunkComponent, out Translation chunkTranslation)
        {
            Camera cam = Camera.main;
            float closestDistance = float.MaxValue;
            Entity closestChunkEntity = default;
            ChunkComponent closestChunk = default;
            Translation closestTranslation = default;

            Entities.WithAll<ChunkComponent>().WithNone<ScatterApplied>().WithoutBurst().ForEach((Entity chunk) =>
            {
                Translation translation = entityManager.GetComponentData<Translation>(chunk);
                float distance = math.distance(translation.Value, cam.transform.position);

                if (distance >= closestDistance) { return; }

                closestDistance = distance;
                closestTranslation = translation;
                closestChunk = entityManager.GetComponentData<ChunkComponent>(chunk);
                closestChunkEntity = chunk;
            }).Run();

            chunkComponent = closestChunk;
            chunkTranslation = closestTranslation;

            return closestChunkEntity;
        }

        protected override void OnUpdate()
        {
            frame = UnityEngine.Time.frameCount;
            if (frame == 0 || frame - lastExecution < framesToExecution) { return; }
            lastExecution = frame;

            Translation closestChunkTranslation = default;
            ChunkComponent closestChunk = default;
            Entity closestChunkEntity = GetNearestChunk(out closestChunk, out closestChunkTranslation);
            if (closestChunkEntity == default) { return; }

            NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(GroundScatterEntityManager.convertedPrefabs.Count, Allocator.Temp);
            uint seedFrame = (uint)frame;
            
            int count = 0;
            foreach (var scatterPrefab in GroundScatterEntityManager.convertedPrefabs)
            {
                var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();
                Entity prefab = scatterPrefab.Value;
                GroundScatter groundScatter = scatterPrefab.Key;
                ClimateSettings localClimateSettings = climateSettings;
                Scale prefabScale = entityManager.GetComponentData<Scale>(scatterPrefab.Value);
                RotationConstraints rotationConstraints = entityManager.GetComponentData<RotationConstraints>(scatterPrefab.Value);
                int localTerrainSeed = terrainSeed;
                NativeArray<Biome> nativeBiomes = new NativeArray<Biome>(biomes, Allocator.TempJob);

                var spawnJob = Job.WithBurst().
                WithReadOnly(nativeBiomes).WithDisposeOnCompletion(nativeBiomes).
                WithCode(() =>
                {
                    ecb.AddComponent<ScatterApplied>(0, closestChunkEntity);
                    uint processSeed = (uint) seedFrame;
                    Unity.Mathematics.Random rng = new Unity.Mathematics.Random(processSeed + (uint) count);

                    for (int i = 0; i < groundScatter.scatterDensity; i++)
                    {
                        float x = math.lerp(0, closestChunk.grid.chunkSize, rng.NextFloat(0, 1));
                        processSeed += (uint) count + (uint) i * 100;
                        rng = new Unity.Mathematics.Random(processSeed);
                        float z = math.lerp(0, closestChunk.grid.chunkSize, rng.NextFloat(0, 1));

                        float2 climate = TerrainNoise.Climate(x, z, localClimateSettings, closestChunk.gridPosition, closestChunk.grid.chunkSize, localTerrainSeed);
                        
                        float climateIdealness = TerrainNoise.ClimateIdealness(new float2(groundScatter.minTemperature, groundScatter.minMoisture), new float2(groundScatter.maxTemperature, groundScatter.maxMoisture), climate, groundScatter.heartiness);
                        float random = rng.NextFloat(0, 1);

                        if (random > climateIdealness) {
                            continue;
                        }

                        float height = TerrainNoise.GetHeightAtPoint(x, z, climate, nativeBiomes, 1, closestChunk.gridPosition, closestChunk.grid.chunkSize, localTerrainSeed);
                        if (height > groundScatter.maxHeight || height < groundScatter.minHeight) {
                            continue;
                        }

                        var scale = prefabScale.Value + prefabScale.Value * (rng.NextFloat(-0.3f, 0.3f));

                        quaternion rot = quaternion.identity;

                        if (!rotationConstraints.x)
                        {
                            rot = math.mul(rot, quaternion.AxisAngle(new float3(1, 0, 0), rng.NextFloat(0, 360)));
                        }
                        else if (!rotationConstraints.y)
                        {
                            rot = math.mul(rot, quaternion.AxisAngle(new float3(0, 1, 0), rng.NextFloat(0, 360)));
                        }
                        else if (!rotationConstraints.z) {
                            rot = math.mul(rot, quaternion.AxisAngle(new float3(0, 0, 1), rng.NextFloat(0, 360)));
                        }

                        float3 position = new float3(x, height, z);
                        float3 worldPosition = new float3(closestChunkTranslation.Value.x + (position.x * closestChunk.grid.voxelSize), height, closestChunkTranslation.Value.z + (position.z * closestChunk.grid.voxelSize));

                        Entity scatterEntity = ecb.Instantiate(i, prefab);
                        ecb.SetComponent(i, scatterEntity, new Translation { Value = worldPosition});
                        ecb.SetComponent(i, scatterEntity, new Scale { Value = scale });
                        ecb.SetComponent(i, scatterEntity, new Rotation { Value = rot });
                    }
                }).Schedule(Dependency);

                jobs[count] = spawnJob;
                count++;
                ecbSystem.AddJobHandleForProducer(spawnJob);
            }

            Dependency = JobHandle.CombineDependencies(jobs);
            jobs.Dispose();
        }
    }
}