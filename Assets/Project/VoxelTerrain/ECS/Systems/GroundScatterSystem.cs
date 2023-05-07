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
using Unity.Burst;
using VoxelTerrain.ECS.Components;

namespace VoxelTerrain.ECS.Components {
    public struct VoxelTerrainGroundScatterNewTag : IComponentData { }
    public struct VoxelTerrainGroundScatterInitializedTag : IComponentData { }

    public struct VoxelTerrainGroundScatterMovedTag : IComponentData { }
    public struct VoxelTerrainGroundScatterGeneratedTag : IComponentData { }
}

namespace VoxelTerrain.ECS.Systems {

    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class GroundScatterSpawnSystem : SystemBase {

        protected EndSimulationEntityCommandBufferSystem ecbSystem;
        protected World defaultWorld;
        protected EntityManager entityManager;
        protected Biome[] biomes;
        protected ClimateSettings climateSettings;
        protected Entity[] prefabs;
        protected int terrainSeed;

        protected int framesToExecution = 10;
        private int frame;
        private int lastExecution;

        private Camera cam;

        [BurstCompile]
        protected override void OnCreate() {
            ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            
        }

        [BurstCompile]
        protected override void OnStartRunning()
        {
            prefabs = new Entity[GroundScatterEntityManager.convertedPrefabs.Count()];
            int count = 0;
            foreach(var pair in GroundScatterEntityManager.convertedPrefabs) {
                prefabs[count] = pair.Value;
                count++;
            }

            cam = Camera.main;
        }

        private int2 WorldToGridSpace(Vector3 position, Grid grid)
        {
            int vx = (int) math.floor(position.x * grid.voxelSize);
            int vy = (int) math.floor(position.z * grid.voxelSize);
            return new int2(
                vx / grid.chunkSize,
                vy / grid.chunkSize
            );
        }

        [BurstCompile]
        protected override void OnUpdate()
        {
            float radius = TerrainManager.instance.terrainSettings.renderDistance;
            int2 camGridPos = WorldToGridSpace(cam.transform.position, TerrainManager.instance.terrainSettings.grid);
            float3 camPos = cam.transform.position;

            var localPrefabs = new NativeArray<Entity>(prefabs, Allocator.TempJob);

            var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            Entities.
            WithAll<ChunkComponent, VoxelTerrainChunkRenderTag>().
            WithNone<Prefab, VoxelTerrainGroundScatterGeneratedTag>().
            WithReadOnly(localPrefabs).
            WithDisposeOnCompletion(localPrefabs).
            WithBurst().
            ForEach((int entityInQueryIndex, Entity e, in ChunkComponent chunkComponent, in Parent parentEntity) => {
                if (math.distance(chunkComponent.gridPosition, camGridPos) > radius) {return;}
                foreach (var prefab in localPrefabs) {
                    GroundScatter prefabScatter = GetComponent<GroundScatter>(prefab);
                    prefabScatter.ChunkEntity = e;
                    prefabScatter.chunk = chunkComponent;
                    prefabScatter.chunkPosition = GetComponent<Translation>(parentEntity.Value).Value;
                    
                    NativeArray<Entity> scatterEntities = new NativeArray<Entity>((int) prefabScatter.scatterDensity * (int) prefabScatter.scatterDensity, Allocator.Temp);

                    ecb.Instantiate(entityInQueryIndex, prefab, scatterEntities);

                    int count = 0;
                    foreach (var entity in scatterEntities) {
                        prefabScatter.scatterIndex = count;
                        ecb.SetComponent<GroundScatter>(entityInQueryIndex, entity, prefabScatter);
                        count++;
                    }
                }

                ecb.AddComponent<VoxelTerrainGroundScatterGeneratedTag>(entityInQueryIndex, e);
            }).ScheduleParallel();

            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }


    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    //[UpdateAfter(typeof(GenerateVoxelTerrainChunkSystem))]
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
            terrainRandom = new Unity.Mathematics.Random(TerrainManager.instance.terrainSettings.seed == 0 ? 1 : (uint)TerrainManager.instance.terrainSettings.seed);
        }

        protected override void OnUpdate()
        {
            var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            var localRandom = terrainRandom;

            Entities.
            WithAll<GroundScatter, VoxelTerrainGroundScatterNewTag>().
            WithNone<Prefab, VoxelTerrainGroundScatterMovedTag>().
            WithBurst().
            ForEach((int entityInQueryIndex, Entity e, ref Translation translation, in GroundScatter scatter) => {
                var voxels = GetBuffer<VoxelTerrainChunkVoxelBufferElement>(scatter.ChunkEntity);

                float chunkWidth = scatter.chunk.grid.voxelSize * scatter.chunk.grid.chunkSize;

                int voxelIndex = (int) math.round((scatter.scatterIndex / (scatter.scatterDensity * scatter.scatterDensity) * voxels.Length));
                VoxelComponent voxel = GetComponent<VoxelComponent>(voxels[voxelIndex].value);

                float3 position = voxel.position + scatter.chunkPosition;
                position += localRandom.NextFloat3(
                    new float3(-scatter.jitterFactor, 0, -scatter.jitterFactor) * (chunkWidth / scatter.scatterDensity),
                    new float3(scatter.jitterFactor, 0, scatter.jitterFactor) * (chunkWidth / scatter.scatterDensity)
                );
                position += scatter.offset;

                float idealness = TerrainNoise.ClimateIdealness(
                    new float2(scatter.minTemperature, scatter.minMoisture), new float2(scatter.maxTemperature, scatter.maxMoisture),
                    voxel.climate, scatter.heartiness
                );

                if (position.y > scatter.maxHeight || position.y <= 0 || localRandom.NextFloat(0, 1) > idealness) {
                    ecb.DestroyEntity(entityInQueryIndex, e);
                    return;
                }

                translation.Value = position;

                ecb.AddComponent<VoxelTerrainGroundScatterMovedTag>(entityInQueryIndex, e);
            }).Schedule();

            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
    
}