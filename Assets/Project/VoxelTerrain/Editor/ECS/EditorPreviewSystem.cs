using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

using VoxelTerrain.ECS.Components;

namespace VoxelTerrain.ECS.Systems
{
    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true, OrderLast = false)]
    public class EditorSpawnVoxelTerrainChunkSystem : SystemBase
    {
        public NativeHashMap<int2, Entity> chunks;

        private EndInitializationEntityCommandBufferSystem ecbSystem;
        private World defaultWorld;
        private EntityManager entityManager;


        protected override void OnCreate()
        {
            chunks = new NativeHashMap<int2, Entity>(1, Allocator.Persistent);

            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            ecbSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
        }

        protected override void OnDestroy()
        {
            chunks.Dispose();
        }

        protected override void OnUpdate()
        {
            if (TerrainChunkConversionManager.chunkPrefab == default) { return; }

            var ecb = ecbSystem.CreateCommandBuffer();

            int radius = (int)TerrainManager.instance.terrainSettings.renderDistance;
            int2 center = new int2(0, 0);
            Grid grid = TerrainManager.instance.terrainSettings.grid;
            Entity prefab = TerrainChunkConversionManager.chunkPrefab;

            NativeHashMap<int2, Entity> localChunks = chunks;

            Job.
            WithBurst().
            WithCode(() =>
            {
                int x = 0;
                int y = 0;
                int dx = 0;
                int dy = -1;
                for (int i = 0; i < (radius * radius); i++)
                {
                    if (((-radius / 2) < x && x <= (radius / 2)) && ((-radius / 2) < y && y <= (radius / 2)))
                    {
                        int2 gridPosition = new int2(x, y) + center;
                        if (!localChunks.ContainsKey(gridPosition))
                        {

                            Entity entity = ecb.Instantiate(prefab);
                            localChunks.Add(gridPosition, entity);

                            ecb.SetComponent(entity, new ChunkComponent
                            {
                                grid = grid,
                                gridPosition = gridPosition
                            });

                            ecb.SetComponent(entity, new Translation
                            {
                                Value = new float3(gridPosition.x, 0, gridPosition.y) * (grid.voxelSize * grid.chunkSize)
                            });
                        }
                    }

                    if (x == y || (x < 0 && x == -y) || (x > 0 && x == 1 - y))
                    {
                        int temp = dx;
                        dx = -dy;
                        dy = temp;
                    }

                    x += dx;
                    y += dy;
                }
            }).Schedule();

            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
