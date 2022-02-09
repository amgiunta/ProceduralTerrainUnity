using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using VoxelTerrain.ECS.Components;

using UnityEngine;

namespace VoxelTerrain.ECS.Systems
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class TerrainControlSystem : SystemBase
    {
        public static TerrainControlSystem instance;

        public Dictionary<int2, Entity> chunks;

        private EndInitializationEntityCommandBufferSystem endInitializationEntityCommandBufferSystem;
        private World defaultWorld;
        private EntityManager entityManager;

        private Camera cam;
                

        protected override void OnCreate()
        {
            instance = this;
            chunks = new Dictionary<int2, Entity>();

            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            endInitializationEntityCommandBufferSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
        }

        protected override void OnStartRunning()
        {
            cam = Camera.main;
        }

        protected override void OnUpdate()
        {
            CreateChunks(TerrainManager.instance.renderDistance, WorldToGridSpace(cam.transform.position), TerrainManager.instance.grid);
        }

        private int2 WorldToGridSpace(Vector3 position) {
            int vx = Mathf.FloorToInt(position.x * TerrainManager.instance.grid.voxelSize);
            int vy = Mathf.FloorToInt(position.z * TerrainManager.instance.grid.voxelSize);
            return new int2(
                vx / TerrainManager.instance.grid.chunkSize,
                vy / TerrainManager.instance.grid.chunkSize
            );
        }

        public virtual void CreateChunks(float radius, int2 center, Grid grid) {
            EntityArchetype entityArchetype = entityManager.CreateArchetype(
                typeof(Translation),
                typeof(Rotation),
                typeof(LocalToWorld),
                typeof(ChunkComponent),
                typeof(ChunkInitialized)
            );

            for (int y = -((int) radius); y <= (int)radius; y++) {
                for (int x = -((int) radius); x <= (int)radius; x++) {
                    int2 gridPosition = (new int2(x, y)) + center;
                    if (chunks.ContainsKey(gridPosition)) { continue; }

                    Entity entity = entityManager.CreateEntity(entityArchetype);
                    chunks.Add(gridPosition, entity);

                    entityManager.SetComponentData(entity, new ChunkComponent
                    {
                        grid = grid,
                        gridPosition = gridPosition
                    });

                    entityManager.SetComponentData(entity, new Translation
                    {
                        Value = (new float3(x + center.x, 0, y + center.y) * grid.chunkSize) * grid.voxelSize
                    });

                    //entityManager.SetName(entity, $"chunk {gridPosition}");
                }
            }
        }
    }
}
