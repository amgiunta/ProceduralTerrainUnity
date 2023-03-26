using System;
using System.Collections.Generic;

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Profiling;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

using VoxelTerrain.ECS;
using VoxelTerrain.ECS.Components;

namespace VoxelTerrain.ECS.Components {

    public struct VoxelComponent: IComponentData {
        public Entity chunk;
        public ChunkComponent chunkComponent;
        public float3 position;
        public float3 normal;
        public float2 climate;
        public Color terrainColor;

        public Color topColor;
        public float3 topPosition;
        public float3 topNormal;
        public float2 topClimate;

        public Color rightColor;
        public float3 rightPosition;
        public float3 rightNormal;
        public float2 rightClimate;

        public Color topRightColor;
        public float3 topRightPosition;
        public float3 topRightNormal;
        public float2 topRightClimate;
    }

    public struct VoxelTerrainChunkNewTag : IComponentData { }

    public struct VoxelTerrainChunkGeneratedTag : IComponentData { }

    public struct VoxelTerrainChunkInitializedTag : IComponentData { }

    public struct VoxelTerrainChunkLoadedTag : IComponentData { }

    public struct VoxelTerrainChunkRenderTag : IComponentData { }

    public struct VoxelTerrainVoxelInitializedTag : IComponentData {}

    public struct VoxelTerrainVoxelGeneratedTag : IComponentData{};

    public struct VoxelTerrainVoxelRenderedTag : IComponentData{};

    public struct VoxelTerrainChunkGroundScatterBufferElement : IBufferElementData {
        public static implicit operator GroundScatter(VoxelTerrainChunkGroundScatterBufferElement e) { return e.value; }
        public static implicit operator VoxelTerrainChunkGroundScatterBufferElement(GroundScatter e) { return new VoxelTerrainChunkGroundScatterBufferElement { value = e }; }

        public GroundScatter value;
    }

    public struct VoxelTerrainChunkVoxelBufferElement : IBufferElementData {
        public static implicit operator Entity(VoxelTerrainChunkVoxelBufferElement e) { return e.value; }
        public static implicit operator VoxelTerrainChunkVoxelBufferElement(Entity e) { return new VoxelTerrainChunkVoxelBufferElement { value = e }; }

        public Entity value;
    }

    public struct VoxelTerrainChunkTopEdgeBufferElement : IBufferElementData
    {
        public static implicit operator Voxel(VoxelTerrainChunkTopEdgeBufferElement e) { return e.value; }
        public static implicit operator VoxelTerrainChunkTopEdgeBufferElement(Voxel e) { return new VoxelTerrainChunkTopEdgeBufferElement { value = e }; }

        public Voxel value;
    }

    public struct VoxelTerrainChunkRightEdgeBufferElement : IBufferElementData
    {
        public static implicit operator Voxel(VoxelTerrainChunkRightEdgeBufferElement e) { return e.value; }
        public static implicit operator VoxelTerrainChunkRightEdgeBufferElement(Voxel e) { return new VoxelTerrainChunkRightEdgeBufferElement { value = e }; }

        public Voxel value;
    }

    public struct VoxelTerrainChunkClimateBufferElement : IBufferElementData
    {
        public static implicit operator float2(VoxelTerrainChunkClimateBufferElement e) { return e.value; }
        public static implicit operator VoxelTerrainChunkClimateBufferElement(float2 e) { return new VoxelTerrainChunkClimateBufferElement { value = e }; }

        public float2 value;
    }

    public struct VoxelTerrainChunkClimateColorBufferElement : IBufferElementData {
        public static implicit operator Color(VoxelTerrainChunkClimateColorBufferElement e) { return e.value; }
        public static implicit operator VoxelTerrainChunkClimateColorBufferElement(Color e) { return new VoxelTerrainChunkClimateColorBufferElement { value = e }; }
        
        public Color value;
    }

    public struct VoxelTerrainChunkTerrainColorBufferElement : IBufferElementData
    {
        public static implicit operator Color(VoxelTerrainChunkTerrainColorBufferElement e) { return e.value; }
        public static implicit operator VoxelTerrainChunkTerrainColorBufferElement(Color e) { return new VoxelTerrainChunkTerrainColorBufferElement { value = e }; }

        public Color value;
    }
}

namespace VoxelTerrain.ECS.Systems
{
    
    public abstract class ClosestVoxelTerrainChunkData
    {
        public static SharedStatic<Entity> closestChunkEntity = SharedStatic<Entity>.GetOrCreate<ClosestVoxelTerrainChunkData, VoxelTerrainChunkEntityKey>();
        public static SharedStatic<ChunkComponent> closestChunk = SharedStatic<ChunkComponent>.GetOrCreate<ClosestVoxelTerrainChunkData, VoxelTerrainChunkKey>();
        public static SharedStatic<Translation> closestChunkTranslation = SharedStatic<Translation>.GetOrCreate<ClosestVoxelTerrainChunkData, VoxelTerrainChunkTranslationKey>();
        public static SharedStatic<float> closestChunkDistance = SharedStatic<float>.GetOrCreate<ClosestVoxelTerrainChunkData, VoxelTerrainChunkDistanceKey>();
        public static SharedStatic<RenderBounds> closestChunkRenderBounds = SharedStatic<RenderBounds>.GetOrCreate<ClosestVoxelTerrainChunkData, VoxelTerrainChunkRenderBoundsKey>();

        private class VoxelTerrainChunkKey { }
        private class VoxelTerrainChunkEntityKey { }
        private class VoxelTerrainChunkTranslationKey { }
        private class VoxelTerrainChunkDistanceKey { }
        private class VoxelTerrainChunkRenderBoundsKey { }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true, OrderLast = false)]
    [UpdateAfter(typeof(BeginInitializationEntityCommandBufferSystem))]
    public partial class SpawnVoxelTerrainChunkSystem : SystemBase
    {
        public NativeParallelHashMap<int2, Entity> chunks;

        private EndInitializationEntityCommandBufferSystem ecbSystem;
        private World defaultWorld;
        private EntityManager entityManager;

        private Camera cam;

        [BurstCompile]
        protected override void OnCreate()
        {
            chunks = new NativeParallelHashMap<int2, Entity>(1, Allocator.Persistent);

            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            ecbSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        protected override void OnDestroy()
        {
            chunks.Dispose();
        }

        [BurstCompile]
        protected override void OnStartRunning()
        {
            cam = Camera.main;
            
        }

        [BurstCompile]
        protected override void OnUpdate()
        {
            //if (TerrainChunkConversionManager.chunkPrefab == default) { return; }

            var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            int radius = (int)TerrainManager.instance.terrainSettings.renderDistance;
            int2 center = WorldToGridSpace(cam.transform.position);
            EntityQuery query = GetEntityQuery(ComponentType.ReadOnly<ChunkParent>());
            var localChunks = query.ToComponentDataArray<ChunkParent>(Allocator.TempJob);

            Entities.
            WithReadOnly(localChunks).
            WithDisposeOnCompletion(localChunks).
            WithAll<Prefab, ChunkParent>().
            WithNone<Parent>().
            WithBurst().
            ForEach((int entityInQueryIndex, Entity e, ref ChunkParent chunk, ref Translation translation) => {
                int x = 0;
                int y = 0;
                int dx = 0;
                int dy = -1;

                int newCount = 0;
                for (int i = 0; i < (radius * radius) && newCount < 5; i++)
                {
                    if (((-radius / 2) < x && x <= (radius / 2)) && ((-radius / 2) < y && y <= (radius / 2)))
                    {
                        int2 gridPosition = new int2(x, y) + center;
                        ChunkParent newChunkParent = new ChunkParent
                        {
                            grid = chunk.grid,
                            gridPosition = gridPosition
                        };

                        bool exists = false;
                        foreach (var chunkParent in localChunks) {
                            if (chunkParent.gridPosition.Equals(newChunkParent.gridPosition)) {
                                exists = true;
                                break;
                            }
                        }

                        if(!exists)
                        {
                            newCount++;
                            Entity entity = ecb.Instantiate(i, e);
                            
                            ecb.SetComponent(i, entity, newChunkParent);
                            
                            ecb.SetComponent(i, entity, new Translation
                            {
                                Value = new float3(gridPosition.x, 0, gridPosition.y) * (chunk.grid.voxelSize * chunk.grid.chunkSize)
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
            }).ScheduleParallel();

            var ecb1 = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            Entities.
            WithAll<Parent, ChunkComponent>().
            WithNone<VoxelTerrainChunkInitializedTag, VoxelTerrainChunkGeneratedTag, VoxelTerrainChunkLoadedTag>().
            WithBurst().
            ForEach((int entityInQueryIndex, Entity e, ref ChunkComponent chunk, in Parent parent) =>
            {
                ChunkParent parentChunk = GetComponent<ChunkParent>(parent.Value);

                chunk.gridPosition = parentChunk.gridPosition;
            }).ScheduleParallel();

            var ecb2 = ecbSystem.CreateCommandBuffer().AsParallelWriter();
            EntityArchetype voxelArchetype = EntityManager.CreateArchetype(
                typeof(VoxelComponent),
                ComponentType.ChunkComponent<VoxelTerrainVoxelInitializedTag>()
            );

            Entities.
            WithNone<VoxelTerrainChunkInitializedTag>().WithAll<VoxelTerrainChunkNewTag>().
            WithBurst().
            ForEach((int entityInQueryIndex, Entity e, in ChunkComponent terrainChunk) => {
                for (int y = 0; y < terrainChunk.grid.chunkSize; y++) {
                    for (int x = 0; x < terrainChunk.grid.chunkSize; x++) {
                        VoxelComponent voxel = new VoxelComponent() {
                            chunk = e,
                            chunkComponent = terrainChunk,
                            position = new float3(x, 0, y),
                            topPosition = new float3(x, 0, y + 1),
                            rightPosition = new float3(x + 1, 0, y),
                            topRightPosition = new float3(x + 1, 0, y + 1)
                        };
                        Entity voxelEntity = ecb2.CreateEntity(entityInQueryIndex, voxelArchetype);
                        ecb2.SetComponent<VoxelComponent>(entityInQueryIndex, voxelEntity, voxel);
                        ecb2.AppendToBuffer<VoxelTerrainChunkVoxelBufferElement>(entityInQueryIndex, e, voxelEntity);
                    }                    
                }
                ecb2.AddComponent(entityInQueryIndex, e, new VoxelTerrainChunkInitializedTag());
            }).ScheduleParallel();

            ecbSystem.AddJobHandleForProducer(Dependency);
        }

        private int2 WorldToGridSpace(Vector3 position)
        {
            int vx = (int) math.floor(position.x * TerrainManager.instance.terrainSettings.grid.voxelSize);
            int vy = (int) math.floor(position.z * TerrainManager.instance.terrainSettings.grid.voxelSize);
            return new int2(
                vx / TerrainManager.instance.terrainSettings.grid.chunkSize,
                vy / TerrainManager.instance.terrainSettings.grid.chunkSize
            );
        }
    }

    //[DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SetVoxelTerrainDataSystem : SystemBase {
        protected World defaultWorld;
        protected EntityManager entityManager;
        protected Biome[] terrainBiomes;
        protected EndSimulationEntityCommandBufferSystem ecbSystem;

        private float3 camPosition;

        [BurstCompile]
        protected override void OnCreate()
        {
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;    
            ecbSystem = defaultWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();        
        }

        [BurstCompile]
        protected override void OnStartRunning()
        {
            terrainBiomes = new Biome[TerrainManager.instance.terrainSettings.biomes.Count];
            camPosition = Camera.main.transform.position;

            for (int i = 0; i < TerrainManager.instance.terrainSettings.biomes.Count; i++)
            {
                terrainBiomes[i] = TerrainManager.instance.terrainSettings.biomes[i];
            }
        }
        
        [BurstCompile]
        protected override void OnUpdate() {
            NativeArray<Biome> biomes = new NativeArray<Biome>(terrainBiomes, Allocator.TempJob);
            ClimateSettings climateSettings = TerrainManager.instance.terrainSettings;
            // float renderDistance = TerrainManager.instance.terrainSettings.renderDistance;
            camPosition.y = 0;

            float3 currentCamPosition = new float3(camPosition);

            var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            EntityQuery generating = GetEntityQuery(
                new EntityQueryDesc() {
                    All = new ComponentType[] {ComponentType.ChunkComponent<VoxelTerrainVoxelInitializedTag>()},
                    None = new ComponentType[] {ComponentType.ChunkComponent<VoxelTerrainVoxelGeneratedTag>()}
                }
            );

            NativeArray<Entity> filter = generating.ToEntityArray(Allocator.TempJob);
            //ProfilerMarker generateMarker = new ProfilerMarker("Generate Voxel Data");

            Entities.
            WithReadOnly(biomes).
            WithDisposeOnCompletion(biomes).
            WithFilter(filter).
            WithBurst().
            ForEach((Entity e, int entityInQueryIndex, ref VoxelComponent voxel) => {
                //generateMarker.Begin();
                VoxelComponent newVoxel = new VoxelComponent() {};

                int chunkSize = voxel.chunkComponent.grid.chunkSize;
                int chunkIndex = ((int) voxel.position.y * chunkSize) + (int) + voxel.position.x;

                TerrainNoise.GetDataAtPoint(
                    biomes, voxel.position.x, voxel.position.z, voxel.chunkComponent.gridPosition, voxel.chunkComponent.grid.chunkSize, voxel.chunkComponent.grid.voxelSize,
                    climateSettings, out voxel.normal, out voxel.position.y, out voxel.climate, out voxel.terrainColor
                );

                if (voxel.position.x == chunkSize -1) {
                    TerrainNoise.GetDataAtPoint(
                        biomes, voxel.position.x + 1, voxel.position.z, voxel.chunkComponent.gridPosition, voxel.chunkComponent.grid.chunkSize, voxel.chunkComponent.grid.voxelSize,
                        climateSettings, out voxel.rightNormal, out voxel.rightPosition.y, out voxel.rightClimate, out voxel.rightColor
                    );
                }

                if (voxel.position.z == chunkSize -1) {
                    TerrainNoise.GetDataAtPoint(
                        biomes, voxel.position.x, voxel.position.z + 1, voxel.chunkComponent.gridPosition, voxel.chunkComponent.grid.chunkSize, voxel.chunkComponent.grid.voxelSize,
                        climateSettings, out voxel.topNormal, out voxel.topPosition.y, out voxel.topClimate, out voxel.topColor
                    );
                }

                if (voxel.position.z == chunkSize -1 && voxel.position.x == chunkSize -1) {
                    TerrainNoise.GetDataAtPoint(
                        biomes, voxel.position.x + 1, voxel.position.z + 1, voxel.chunkComponent.gridPosition, voxel.chunkComponent.grid.chunkSize, voxel.chunkComponent.grid.voxelSize,
                        climateSettings, out voxel.topRightNormal, out voxel.topRightPosition.y, out voxel.topRightClimate, out voxel.topRightColor
                    );
                }
                

                ecb.AddComponent(entityInQueryIndex, voxel.chunk, new VoxelTerrainChunkGeneratedTag());
                //generateMarker.End();
            }).ScheduleParallel();
            filter.Dispose();

            ecbSystem.AddJobHandleForProducer(Dependency);

            entityManager.AddChunkComponentData(generating, new VoxelTerrainVoxelGeneratedTag());
        }
    }

    //[DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(SetVoxelTerrainDataSystem))]
    public partial class GenerateVoxelTerrainMeshSystem : SystemBase {
        protected World defaultWorld;
        protected EntityManager entityManager;
        protected BeginInitializationEntityCommandBufferSystem ecbSystem;

        public struct RawMeshData {
            public NativeArray<float3> verts;
            public NativeArray<int> tris;
            public NativeArray<float3> normal;
            public NativeArray<float2> uv0;
            public NativeArray<float2> uv1;
        }

        [BurstCompile]
        protected override void OnCreate()
        {
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            ecbSystem = defaultWorld.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        protected override void OnUpdate() {
            var ecb = ecbSystem.CreateCommandBuffer();
            // Create entity query for chunks that are ready for a mesh
            // Create an empty native dictionary where the key is an entity, and the value is an empty RawMeshData
            EntityQuery chunksReadyToRenderQuery = GetEntityQuery(
                new EntityQueryDesc() {
                    All = new ComponentType[] {typeof(VoxelTerrainChunkGeneratedTag)},
                    None = new ComponentType[] {typeof(VoxelTerrainChunkRenderTag)}
                }
            );
            //NativeArray<Entity> chunksReadyToRender = chunksReadyToRenderQuery.ToEntityArray(Allocator.TempJob);

            Entities.
            WithAll<VoxelTerrainChunkGeneratedTag>().
            WithNone<VoxelTerrainChunkRenderTag>().
            WithoutBurst().
            ForEach((Entity e, int entityInQueryIndex, RenderMesh renderMesh, in ChunkComponent chunkComponent, in DynamicBuffer<VoxelTerrainChunkVoxelBufferElement> voxelEntities) => {
                
                int elementSize = ((chunkComponent.grid.chunkSize + 1) * (chunkComponent.grid.chunkSize + 1));
                
                NativeArray<float3> verts = new NativeArray<float3>(elementSize, Allocator.TempJob);
                NativeArray<int> indecies = new NativeArray<int>(elementSize * 6, Allocator.TempJob);
                NativeArray<float3> normals = new NativeArray<float3>(elementSize, Allocator.TempJob);
                NativeArray<Color> colors = new NativeArray<Color>(elementSize, Allocator.TempJob);
                NativeArray<float2> uv0 = new NativeArray<float2>(elementSize, Allocator.TempJob);
                NativeArray<float2> uv1 = new NativeArray<float2>(elementSize, Allocator.TempJob);
                NativeArray<float2> uv2 = new NativeArray<float2>(elementSize, Allocator.TempJob);

                int vertIndex = 0;

                for (int i = 0; i < voxelEntities.Length; i++) {
                    VoxelComponent voxel = GetComponent<VoxelComponent>(voxelEntities[i]);

                    int chunkSize = voxel.chunkComponent.grid.chunkSize;
                    int y = i / chunkSize;
                    int x = i % chunkSize;

                    verts[vertIndex] = voxel.position * new float3(chunkComponent.grid.voxelSize, 1, chunkComponent.grid.voxelSize);
                    normals[vertIndex] = voxel.normal;
                    colors[vertIndex] = voxel.terrainColor;
                    uv0[vertIndex] = new float2(x, y);
                    uv1[vertIndex] = new float2(voxel.position.x / chunkSize, voxel.position.z / chunkSize);
                    uv2[vertIndex] = voxel.climate;

                    int triStart = (i * 6);

                    indecies[triStart] = vertIndex;
                    indecies[triStart + 1] = vertIndex + chunkSize + 1;
                    indecies[triStart + 2] = vertIndex + 1;
                    indecies[triStart + 3] = vertIndex + 1;
                    indecies[triStart + 4] = vertIndex + chunkSize + 1;
                    indecies[triStart + 5] = vertIndex + 2 + chunkSize;

                    vertIndex++;

                    if (y == (chunkSize -1)) {
                        int index = vertIndex + chunkSize;
                        verts[index] = voxel.topPosition * new float3(chunkComponent.grid.voxelSize, 1, chunkComponent.grid.voxelSize);
                        normals[index] = voxel.topNormal;
                        colors[index] = voxel.topColor;
                        uv0[index] = new float2(x, chunkSize);
                        uv1[index] = new float2(voxel.position.x / chunkSize, 1);
                        uv2[index] = voxel.topClimate;
                    }

                    if (x == (chunkSize - 1) && y == (chunkSize - 1)) {
                        int index = vertIndex + chunkSize + 1;
                        verts[index] = voxel.topRightPosition;
                        normals[index] = voxel.topRightNormal;
                        colors[index] = voxel.topRightColor;
                        uv0[index] = new float2(chunkSize, chunkSize);
                        uv1[index] = new float2(1, 1);
                        uv2[index] = voxel.topRightClimate;
                    }

                    if (x == (chunkSize -1)) {
                        verts[vertIndex] = voxel.rightPosition * new float3(chunkComponent.grid.voxelSize, 1, chunkComponent.grid.voxelSize);
                        normals[vertIndex] = voxel.rightNormal;
                        colors[vertIndex] = voxel.rightColor;
                        uv0[vertIndex] = new float2(chunkSize, y);
                        uv1[vertIndex] = new float2(1, voxel.position.z / chunkSize);
                        uv2[vertIndex] = voxel.rightClimate;

                        vertIndex++;
                    }


                }

                renderMesh.mesh = new Mesh();
                renderMesh.mesh.name = $"Chunk Mesh: {chunkComponent.gridPosition}";
                renderMesh.mesh.SetVertices(verts);
                renderMesh.mesh.SetNormals(normals);
                renderMesh.mesh.SetColors(colors);
                renderMesh.mesh.SetUVs(0, uv0);
                renderMesh.mesh.SetUVs(1, uv1);
                renderMesh.mesh.SetUVs(2, uv2);
                renderMesh.mesh.SetIndexBufferParams(indecies.Length, IndexFormat.UInt32);
                renderMesh.mesh.SetIndexBufferData(indecies, 0, 0, indecies.Length);
                renderMesh.mesh.SetSubMesh(0, new SubMeshDescriptor(0, indecies.Length));

                ecb.SetSharedComponent<RenderMesh>(e, renderMesh);
                ecb.AddComponent<VoxelTerrainChunkRenderTag>(e);
                ecb.RemoveComponent<DisableRendering>(e);

                verts.Dispose();
                indecies.Dispose();
                normals.Dispose();
                colors.Dispose();
                uv0.Dispose();
                uv1.Dispose();
                uv2.Dispose();
            }).Run();

            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }

    // [DisableAutoCreation]
    // [BurstCompile]
    // [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true, OrderLast = false)]
    // [UpdateAfter(typeof(SpawnVoxelTerrainChunkSystem))]
    // public partial class GenerateVoxelTerrainChunkSystem : SystemBase
    // {
    //     protected EndInitializationEntityCommandBufferSystem tagSystem;
    //     protected World defaultWorld;
    //     protected EntityManager entityManager;

    //     protected Biome[] terrainBiomes;

    //     private Unity.Mathematics.Random terrainRandom;

    //     private ProfilerMarker startMarker;
        

    //     [BurstCompile]
    //     protected override void OnCreate()
    //     {
    //         tagSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
    //         defaultWorld = World.DefaultGameObjectInjectionWorld;
    //         entityManager = defaultWorld.EntityManager;

    //         startMarker = new ProfilerMarker("Start mesh generation");
            
    //     }

    //     [BurstCompile]
    //     protected override void OnStartRunning()
    //     {
    //         terrainBiomes = new Biome[TerrainManager.instance.terrainSettings.biomes.Count];

    //         for (int i = 0; i < TerrainManager.instance.terrainSettings.biomes.Count; i++)
    //         {
    //             terrainBiomes[i] = TerrainManager.instance.terrainSettings.biomes[i];
    //         }

    //         terrainRandom = new Unity.Mathematics.Random(TerrainManager.instance.terrainSettings.seed == 0 ? 1 : (uint)TerrainManager.instance.terrainSettings.seed);
    //     }

    //     [BurstCompile]
    //     protected override void OnUpdate()
    //     {
    //         startMarker.Begin();
    //         var tagEcb = tagSystem.CreateCommandBuffer().AsParallelWriter();

    //         ProfilerMarker generatorLoopMarker;
    //         ProfilerMarker noiseMarker;
    //         generatorLoopMarker = new ProfilerMarker("Generate Mesh Loop");
    //         noiseMarker = new ProfilerMarker("Mesh Noise");

    //         NativeArray<Biome> biomes = new NativeArray<Biome>(terrainBiomes, Allocator.TempJob);
    //         ClimateSettings climateSettings = TerrainManager.instance.terrainSettings;
    //         float renderDistance = TerrainManager.instance.terrainSettings.renderDistance;

    //         //Unity.Mathematics.Random random = terrainRandom;
    //         //int seed = TerrainManager.instance.terrainSettings.seed;
    //         float3 camPosition = Camera.main.transform.position;
    //         camPosition.y = 0;

    //         startMarker.End();

    //         Entities.
    //         WithReadOnly(biomes).
    //         WithDisposeOnCompletion(biomes).
    //         WithAll<VoxelTerrainChunkInitializedTag>().WithNone<VoxelTerrainChunkGeneratedTag>().
    //         WithBurst().
    //         ForEach((
    //             Entity entity, int entityInQueryIndex,
    //             ref DynamicBuffer<VoxelTerrainChunkVoxelBufferElement> voxelBuffer,
    //             ref DynamicBuffer<VoxelTerrainChunkClimateColorBufferElement> climateColorBuffer,
    //             ref DynamicBuffer<VoxelTerrainChunkTerrainColorBufferElement> terrainColorBuffer,
    //             ref DynamicBuffer<VoxelTerrainChunkClimateBufferElement> climateBuffer,
    //             ref DynamicBuffer<VoxelTerrainChunkTopEdgeBufferElement> topEdgeBuffer,
    //             ref DynamicBuffer<VoxelTerrainChunkRightEdgeBufferElement> rightEdgeBuffer,
    //             in ChunkComponent closestChunk, in Translation translation
    //         ) =>
    //         {
    //             if (math.distance(translation.Value, camPosition) > (renderDistance * closestChunk.grid.chunkSize * closestChunk.grid.voxelSize)) { return; }
                
    //             generatorLoopMarker.Begin();

    //             for (int i = 0; i < (closestChunk.grid.chunkSize * closestChunk.grid.chunkSize) + (2 * closestChunk.grid.chunkSize + 1); i++)
    //             {
    //                 int2 chunkPosition = closestChunk.gridPosition;

    //                 Voxel voxel = new Voxel();
    //                 int x = i % (closestChunk.grid.chunkSize + 1);
    //                 int y = i / (closestChunk.grid.chunkSize + 1);

    //                 int index = y * closestChunk.grid.chunkSize + x;
                    
    //                 float2 climate;
    //                 float3 normal = new float3(0, 1, 0);
    //                 float height;
    //                 Color color;

    //                 noiseMarker.Begin();
    //                 TerrainNoise.GetDataAtPoint(
    //                     biomes, x, y, closestChunk.gridPosition, closestChunk.grid.chunkSize, closestChunk.grid.voxelSize,
    //                     climateSettings, out normal, out height, out climate, out color
    //                 );
    //                 noiseMarker.End();

    //                 voxel.normal = normal;
                    
    //                 voxel.position = new float3(x, height, y);
    //                 if (x < closestChunk.grid.chunkSize && y < closestChunk.grid.chunkSize)
    //                 {
    //                     climateBuffer[index] = climate;
    //                     climateColorBuffer[index] = new Color(normal.x, 0, normal.y, 1);
    //                     terrainColorBuffer[index] = color;
    //                     voxelBuffer[index] = voxel;
    //                 }
    //                 else if (y >= closestChunk.grid.chunkSize) {
    //                     topEdgeBuffer[x] = voxel;
    //                 }
    //                 else if (x == closestChunk.grid.chunkSize)
    //                 {
    //                     rightEdgeBuffer[y] = voxel;
    //                 }                    
    //             }

    //             tagEcb.AddComponent<VoxelTerrainChunkGeneratedTag>(entityInQueryIndex, entity);
    //             generatorLoopMarker.End();
    //         }).ScheduleParallel();

    //         tagSystem.AddJobHandleForProducer(Dependency);
    //     }
    // }

    // [DisableAutoCreation]
    // [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = false, OrderLast = true)]
    // public partial class GenerateVoxelTerrainMeshSystem : SystemBase
    // {
    //     protected BeginPresentationEntityCommandBufferSystem ecbSystem;
    //     protected World defaultWorld;
    //     protected EntityManager entityManager;

    //     protected override void OnCreate()
    //     {
    //         defaultWorld = World.DefaultGameObjectInjectionWorld;
    //         entityManager = defaultWorld.EntityManager;
    //         ecbSystem = World.GetOrCreateSystem<BeginPresentationEntityCommandBufferSystem>();
    //     }

    //     private static Texture2D GetClimateColors(NativeArray<VoxelTerrainChunkClimateColorBufferElement> climateBuffer, int samples = 8) {
    //         Profiler.BeginSample("Climate Color Texture");
    //         Texture2D tex = new Texture2D(samples, samples);
    //         var texPixels = tex.GetRawTextureData<Color32>();

    //         int c = 0;
    //         for (int y = 0; y < samples; y++) {
    //             for (int x = 0; x < samples; x++) {
    //                 texPixels[c] = (Color32) climateBuffer[(y * samples) + x].value;
    //                 c++;
    //             }
    //         }
    //         tex.Apply(false);
    //         Profiler.EndSample();

    //         return tex;
    //     }

    //     private static Texture2D GetBiomeColors(NativeArray<VoxelTerrainChunkTerrainColorBufferElement> terrainColorBuffer, int samples = 8) {
    //         Profiler.BeginSample("Biome Color Texture");
    //         Texture2D tex = new Texture2D(samples, samples);
    //         var texPixels = tex.GetRawTextureData<Color32>();

    //         int c = 0;
    //         for (int y = 0; y < samples; y++) {
    //             for (int x = 0; x < samples; x++) {
    //                 texPixels[c] = (Color32) terrainColorBuffer[(y * samples) + x].value;
    //                 c++;
    //             }
    //         }
    //         tex.Apply(false);
    //         Profiler.EndSample();

    //         return tex;
    //     }

    //     private static void CreateCallbacks(
    //         Mesh mesh, ComputeBuffer verts, ComputeBuffer tris, 
    //         ComputeBuffer voxels, ComputeBuffer topEdge, ComputeBuffer rightEdge, 
    //         ComputeBuffer normals, ComputeBuffer uv0, ComputeBuffer uv1
    //     ) {
    //         AsyncGPUReadback.Request(verts, (AsyncGPUReadbackRequest request) => {
    //             Profiler.BeginSample("Perform Mesh Callbacks");
    //             if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
    //             else if (!request.done) { Debug.Log("Not done yet..."); return; }

    //             AsyncGPUReadback.Request(tris, (AsyncGPUReadbackRequest request) =>
    //             {
    //                 if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
    //                 else if (!request.done) { Debug.Log("Not done yet..."); return; }

    //                 mesh.SetIndexBufferParams(tris.count, IndexFormat.UInt32);
    //                 mesh.SetIndexBufferData(request.GetData<int>(), 0, 0, tris.count);
    //                 mesh.SetSubMesh(0, new SubMeshDescriptor(0, tris.count));
    //                 tris.Dispose();
    //             });

    //             AsyncGPUReadback.Request(normals, (AsyncGPUReadbackRequest request) =>
    //             {
    //                 if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
    //                 else if (!request.done) { Debug.Log("Not done yet..."); return; }

    //                 mesh.SetNormals(request.GetData<Vector3>());
    //                 normals.Dispose();
    //             });

    //             AsyncGPUReadback.Request(uv0, (AsyncGPUReadbackRequest request) =>
    //             {
    //                 if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
    //                 else if (!request.done) { Debug.Log("Not done yet..."); return; }

    //                 mesh.SetUVs(0, request.GetData<Vector2>());
    //                 uv0.Dispose();
    //             });

    //             AsyncGPUReadback.Request(uv1, (AsyncGPUReadbackRequest request) =>
    //             {
    //                 if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
    //                 else if (!request.done) { Debug.Log("Not done yet..."); return; }

    //                 mesh.SetUVs(1, request.GetData<Vector2>());
    //                 uv1.Dispose();
    //             });

    //             mesh.SetVertices(request.GetData<Vector3>());
    //             Profiler.EndSample();

    //             Profiler.BeginSample("Dispose Mesh Buffers");
    //             verts.Dispose();
    //             voxels.Dispose();
    //             topEdge.Dispose();
    //             rightEdge.Dispose();
    //             Profiler.EndSample();
    //         });
    //     }

    //     protected override void OnUpdate()
    //     {
    //         var ecb = ecbSystem.CreateCommandBuffer();

    //         TerrainSettings terrainSettings = TerrainManager.instance.terrainSettings;
    //         ComputeShader meshGenerator = TerrainChunkConversionManager.terrainGeneratorShader;

    //         int renderCount = 0;

    //         Entities.
    //         WithAll<VoxelTerrainChunkGeneratedTag, ChunkComponent>().
    //         WithNone<VoxelTerrainChunkRenderTag>().
    //         WithoutBurst().
    //         ForEach((
    //             int entityInQueryIndex, Entity e,
    //             DynamicBuffer<VoxelTerrainChunkVoxelBufferElement> terrainVoxels,
    //             DynamicBuffer<VoxelTerrainChunkClimateColorBufferElement> climateColorBuffer,
    //             DynamicBuffer<VoxelTerrainChunkTerrainColorBufferElement> terrainColorBuffer,
    //             DynamicBuffer<VoxelTerrainChunkClimateBufferElement> climateBuffer,
    //             DynamicBuffer<VoxelTerrainChunkTopEdgeBufferElement> topEdgeVoxels,
    //             DynamicBuffer<VoxelTerrainChunkRightEdgeBufferElement> rightEdgeVoxels,
    //             in RenderMesh meshInstance, in ChunkComponent chunk
    //         ) => {
    //             if (renderCount >= 25) { return; }
    //             if (terrainVoxels.Length == 0) { return; }

    //             Profiler.BeginSample("Init Mesh Generator");
    //             renderCount++;

    //             Mesh mesh = new Mesh();

    //             mesh.name = $"chunk {chunk.gridPosition}";

    //             int sizeVector3 = sizeof(float) * 3;
    //             int sizeVector2 = sizeof(float) * 2;
    //             int sizeVoxel = sizeVector3 * 2;
    //             int chunkWidth = chunk.grid.chunkSize;
    //             int vertCount = chunkWidth * chunkWidth * terrainSettings.voxelVertecies;
    //             int indexCount = chunkWidth * chunkWidth * terrainSettings.voxelIdexies;

    //             ComputeBuffer voxels = new ComputeBuffer(terrainVoxels.Length, sizeVoxel);
    //             voxels.SetData(terrainVoxels.AsNativeArray());

    //             ComputeBuffer topEdge = new ComputeBuffer(topEdgeVoxels.Length, sizeVoxel);
    //             topEdge.SetData(topEdgeVoxels.AsNativeArray());

    //             ComputeBuffer rightEdge = new ComputeBuffer(rightEdgeVoxels.Length, sizeVoxel);
    //             rightEdge.SetData(rightEdgeVoxels.AsNativeArray());

    //             ComputeBuffer verts = new ComputeBuffer(vertCount, sizeVector3);
    //             ComputeBuffer tris = new ComputeBuffer(indexCount, sizeof(int));
    //             ComputeBuffer normals = new ComputeBuffer(vertCount, sizeVector3);
    //             ComputeBuffer uv0 = new ComputeBuffer(vertCount, sizeVector2);
    //             ComputeBuffer uv1 = new ComputeBuffer(vertCount, sizeVector2);

    //             meshGenerator.SetFloat("voxelSize", chunk.grid.voxelSize);
    //             meshGenerator.SetInt("chunkWidth", chunkWidth);

    //             meshGenerator.SetBuffer(0, "voxels", voxels);
    //             meshGenerator.SetBuffer(0, "topEdge", topEdge);
    //             meshGenerator.SetBuffer(0, "rightEdge", rightEdge);

    //             meshGenerator.SetBuffer(0, "verts", verts);
    //             meshGenerator.SetBuffer(0, "tris", tris);
    //             meshGenerator.SetBuffer(0, "normals", normals);
    //             meshGenerator.SetBuffer(0, "uv0s", uv0);
    //             meshGenerator.SetBuffer(0, "uv1s", uv1);
    //             Profiler.EndSample();

    //             Profiler.BeginSample("Dispach Mesh Generator");
    //             // Run computation
    //             meshGenerator.Dispatch(0, chunkWidth, chunkWidth, 1);
    //             Profiler.EndSample();

    //             Profiler.BeginSample("Apply Terrain Mesh");
    //             AABB aabb = new AABB();
    //             aabb.Center = new float3((chunk.grid.chunkSize * chunk.grid.voxelSize) / 2, 500, (chunk.grid.chunkSize * chunk.grid.voxelSize) / 2);
    //             aabb.Extents = new float3(chunk.grid.chunkSize * chunk.grid.voxelSize, 1000, chunk.grid.chunkSize * chunk.grid.voxelSize);

    //             ecb.SetComponent(e, new RenderBounds { Value = aabb });
                
    //             ecb.AddComponent<VoxelTerrainChunkRenderTag>(e);

    //             ecb.RemoveComponent<DisableRendering>(e);

    //             Profiler.BeginSample("Generate Mesh Textures");
    //             Texture2D climateTex = GetClimateColors(climateColorBuffer.AsNativeArray(), 4);
    //             Texture2D colorTex = GetBiomeColors(terrainColorBuffer.AsNativeArray(), 4);
    //             Profiler.EndSample();

    //             Profiler.BeginSample("Apply Mesh Material and Textures");
    //             Material mat = new Material(meshInstance.material);
    //             mat.SetTexture("climate_texture", climateTex);
    //             mat.SetTexture("color_texture", colorTex);
    //             Profiler.EndSample();

    //             Profiler.BeginSample("Copy Terrain Mesh");
    //             RenderMesh meshCopy = meshInstance;
    //             meshCopy.mesh = mesh;
    //             meshCopy.material = mat;
    //             Profiler.EndSample();

    //             ecb.SetSharedComponent(e, meshCopy);
    //             Profiler.EndSample();

    //             CreateCallbacks(
    //                 mesh, verts, tris,
    //                 voxels, topEdge, rightEdge,
    //                 normals, uv0, uv1
    //             );

    //         }).Run();
    //         ecbSystem.AddJobHandleForProducer(Dependency);
    //     }
    // }

    /*
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = false)]
    [UpdateAfter(typeof(GenerateVoxelTerrainMeshSystem))]
    public class RenderVoxelTerrainChunkSystem : SystemBase {
        protected World defaultWorld;
        protected EntityManager entityManager;

        protected override void OnCreate()
        {
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
        }

        protected override void OnUpdate()
        {
            Entities.
            WithAll<VoxelTerrainChunkRenderTag>().
            WithoutBurst().
            ForEach((Entity e, in RenderMesh renderMesh, in LocalToWorld matrix) => {
                Graphics.DrawMeshInstanced(renderMesh.mesh, 0, renderMesh.material, new Matrix4x4[] { matrix.Value});
            }).
            Run();
        }
    }
    */
}
