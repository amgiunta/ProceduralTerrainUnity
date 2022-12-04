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

namespace VoxelTerrain.ECS.Components {
    public struct VoxelTerrainGroundScatterNewTag : IComponentData { }
    public struct VoxelTerrainGroundScatterInitializedTag : IComponentData { }

    public struct VoxelTerrainGroundScatterMovedTag : IComponentData { }
}

namespace VoxelTerrain.ECS.Systems {
    
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(ClosestVoxelTerrainChunkData))]
    public partial class GroundScatterSpawnSystem : SystemBase {

        protected EndInitializationEntityCommandBufferSystem ecbSystem;
        protected World defaultWorld;
        protected EntityManager entityManager;
        protected Biome[] biomes;
        protected ClimateSettings climateSettings;
        protected int terrainSeed;

        protected int framesToExecution = 10;
        private int frame;
        private int lastExecution;

        protected override void OnCreate() {
            ecbSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
        }

        protected override void OnStartRunning()
        {
            climateSettings = TerrainManager.instance.terrainSettings;
            terrainSeed = TerrainManager.instance.terrainSettings.seed;
            biomes = new Biome[TerrainManager.instance.terrainSettings.biomes.Count];
            framesToExecution = 5 * GroundScatterEntityManager.convertedPrefabs.Count;
            int count = 0;
            foreach (Biome biome in TerrainManager.instance.terrainSettings.biomes) {
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
            if (ClosestVoxelTerrainChunkData.closestChunkEntity.Data == default || GroundScatterEntityManager.convertedPrefabs.Count == 0) { return; }

            NativeArray<VoxelTerrainChunkGroundScatterBufferElement> groundScatterArray = GetBuffer<VoxelTerrainChunkGroundScatterBufferElement>(ClosestVoxelTerrainChunkData.closestChunkEntity.Data).AsNativeArray();
            NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(GroundScatterEntityManager.convertedPrefabs.Count, Allocator.Temp);
            Entity chunkEntity = ClosestVoxelTerrainChunkData.closestChunkEntity.Data;
            ChunkComponent chunkEntityComponent = ClosestVoxelTerrainChunkData.closestChunk.Data;
            Translation chunkEntityTranslation = ClosestVoxelTerrainChunkData.closestChunkTranslation.Data;
            uint seedFrame = (uint)frame;
            
            int count = 0;
            foreach (var scatterPrefab in GroundScatterEntityManager.convertedPrefabs)
            {
                var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();
                Entity prefab = scatterPrefab.Value;
                //GroundScatter groundScatter = scatterPrefab.Key;

                //if (groundScatterArray.Contains(groundScatter)) { continue; }

                ClimateSettings localClimateSettings = climateSettings;
                Scale prefabScale = entityManager.GetComponentData<Scale>(scatterPrefab.Value);
                RotationConstraints rotationConstraints = entityManager.GetComponentData<RotationConstraints>(scatterPrefab.Value);
                int localTerrainSeed = terrainSeed;

                var spawnJob = Entities.WithBurst().
                WithAll<ChunkComponent>().WithNone<VoxelTerrainNoGroundScatterTag>().
                ForEach((int entityInQueryIndex, Entity e, in GroundScatter groundScatter, in ChunkComponent chunk) =>
                {
                    Entity chunkEntity = groundScatter.ChunkEntity;
                    VoxelTerrainChunkGroundScatterBufferElement scatterBufferElement = new VoxelTerrainChunkGroundScatterBufferElement { value = groundScatter };
                    ecb.AppendToBuffer(entityInQueryIndex, groundScatter.ChunkEntity, scatterBufferElement);

                    uint processSeed = (uint)seedFrame;
                    Unity.Mathematics.Random rng = new Unity.Mathematics.Random(processSeed + (uint)count + 1);

                    for (int i = 0; i < groundScatter.scatterDensity; i++)
                    {
                        ecb.Instantiate(i, prefab);
                    }
                }).ScheduleParallel(Dependency);
                /*
                var spawnJob = Job.WithBurst().
                WithCode(() =>
                {
                    VoxelTerrainChunkGroundScatterBufferElement scatterBufferElement = new VoxelTerrainChunkGroundScatterBufferElement { value = groundScatter };
                    ecb.AppendToBuffer(0, chunkEntity, scatterBufferElement);

                    uint processSeed = (uint) seedFrame;
                    Unity.Mathematics.Random rng = new Unity.Mathematics.Random(processSeed + (uint) count +1);

                    for (int i = 0; i < groundScatter.scatterDensity; i++)
                    {
                        Entity scatterEntity = ecb.Instantiate(i, prefab);
                    }
                }).Schedule(Dependency);
                */

                jobs[count] = spawnJob;
                count++;
            }

            groundScatterArray.Dispose();

            ecbSystem.AddJobHandleForProducer(JobHandle.CombineDependencies(jobs));
            jobs.Dispose();
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(GenerateVoxelTerrainChunkSystem))]
    public partial class GroundScatterMoveSystem : SystemBase {
        protected BeginInitializationEntityCommandBufferSystem ecbSystem;
        protected World defaultWorld;
        protected EntityManager entityManager;

        protected Biome[] terrainBiomes;
        private Unity.Mathematics.Random terrainRandom;

        protected override void OnCreate()
        {
            ecbSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
        }

        protected override void OnStartRunning()
        {
            terrainBiomes = new Biome[TerrainManager.instance.terrainSettings.biomes.Count];

            for (int i = 0; i < TerrainManager.instance.terrainSettings.biomes.Count; i++)
            {
                terrainBiomes[i] = TerrainManager.instance.terrainSettings.biomes[i];
            }

            terrainRandom = new Unity.Mathematics.Random(TerrainManager.instance.terrainSettings.seed == 0 ? 1 : (uint)TerrainManager.instance.terrainSettings.seed);
        }

        protected override void OnUpdate()
        {
            if (ClosestVoxelTerrainChunkData.closestChunkEntity.Data == default) { return; }
            var pecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            ClimateSettings climateSettings = TerrainManager.instance.terrainSettings;
            int seed = TerrainManager.instance.terrainSettings.seed;

            var chunkEntityComponent = ClosestVoxelTerrainChunkData.closestChunk.Data;
            var chunkEntityTranslation = ClosestVoxelTerrainChunkData.closestChunkTranslation.Data;

            NativeArray<Biome> biomes = new NativeArray<Biome>(terrainBiomes, Allocator.TempJob);
            float3 camDistance = Camera.main.transform.position;
            float renderDistance = TerrainManager.instance.terrainSettings.renderDistance;

            Entities.
            //WithReadOnly(biomes).
            //WithDisposeOnCompletion(biomes).
            WithAll<VoxelTerrainGroundScatterNewTag>().
            WithNone<VoxelTerrainGroundScatterInitializedTag>().
            WithBurst().
            ForEach((int entityInQueryIndex, Entity e, ref Translation translation, ref Scale scale, ref Rotation rotation, in RotationConstraints rotationConstraints, in GroundScatter groundScatter) => {
                if (math.distance(translation.Value, camDistance) > renderDistance) { return; }

                int voxelLength = groundScatter.chunk.grid.chunkSize * groundScatter.chunk.grid.chunkSize;

                Unity.Mathematics.Random rng = new Unity.Mathematics.Random((uint) (e.Index + entityInQueryIndex));
                rng = new Unity.Mathematics.Random(rng.NextUInt(1, uint.MaxValue));

                float x = rng.NextInt(0, chunkEntityComponent.grid.chunkSize);
                rng = new Unity.Mathematics.Random(rng.NextUInt(1, uint.MaxValue));
                float z = rng.NextInt(0, chunkEntityComponent.grid.chunkSize);

                int bufferIndex = (int)((chunkEntityComponent.grid.chunkSize * z) + x);
                bufferIndex = bufferIndex >= voxelLength ? voxelLength - 1 : bufferIndex;

                int stride = (int)groundScatter.chunk.lodLevel;

                //float2 climate = TerrainNoise.Climate(x, z, climateSettings, groundScatter.chunk.gridPosition, groundScatter.chunk.grid.chunkSize, groundScatter.chunk.grid.voxelSize, seed);
                //float height = TerrainNoise.GetHeightAtPoint(x, z, climate, biomes, groundScatter.chunk.gridPosition, groundScatter.chunk.grid.chunkSize, groundScatter.chunk.grid.voxelSize, seed );

                //float climateIdealness = TerrainNoise.ClimateIdealness(new float2(groundScatter.minTemperature, groundScatter.minMoisture), new float2(groundScatter.maxTemperature, groundScatter.maxMoisture), climate, groundScatter.heartiness);

                rng = new Unity.Mathematics.Random(rng.NextUInt(1, uint.MaxValue));
                float random = rng.NextFloat(0, 1);
                /*
                if (random > climateIdealness)
                {
                    return;
                }

                if (height > groundScatter.maxHeight || height < groundScatter.minHeight)
                {
                    return;
                }

                var scaleValue = scale.Value + scale.Value * (rng.NextFloat(-0.3f, 0.3f));

                quaternion rot = quaternion.identity;

                if (!rotationConstraints.x)
                {
                    rot = math.mul(rot, quaternion.AxisAngle(new float3(1, 0, 0), rng.NextFloat(0, 360)));
                }
                else if (!rotationConstraints.y)
                {
                    rot = math.mul(rot, quaternion.AxisAngle(new float3(0, 1, 0), rng.NextFloat(0, 360)));
                }
                else if (!rotationConstraints.z)
                {
                    rot = math.mul(rot, quaternion.AxisAngle(new float3(0, 0, 1), rng.NextFloat(0, 360)));
                }

                float3 position = new float3(x, height, z);
                float3 worldPosition = new float3(chunkEntityTranslation.Value.x + (position.x * chunkEntityComponent.grid.voxelSize), position.y, chunkEntityTranslation.Value.z + (position.z * chunkEntityComponent.grid.voxelSize));

                translation.Value = worldPosition;
                scale.Value = scaleValue;
                rotation.Value = rot;

                biomes.Dispose();
                */

                pecb.RemoveComponent<DisableRendering>(entityInQueryIndex, e);
                pecb.AddComponent<VoxelTerrainGroundScatterInitializedTag>(entityInQueryIndex, e);
            })
            .ScheduleParallel();

            ecbSystem.AddJobHandleForProducer(Dependency);
            biomes.Dispose();
        }
    }
    
}