using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using VoxelTerrain.ECS.Components;

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
                

        protected override void OnCreate()
        {
            instance = this;
            chunks = new Dictionary<int2, Entity>();

            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            endInitializationEntityCommandBufferSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
        }

        public virtual void CreateChunks(float radius, int2 center, Grid grid) {
            EntityArchetype entityArchetype = entityManager.CreateArchetype(
                typeof(Translation),
                typeof(Rotation),
                typeof(RenderMesh),
                typeof(RenderBounds),
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
