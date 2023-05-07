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
    [AlwaysUpdateSystem]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(SpawnVoxelTerrainChunkSystemV2))]
    public partial class EnableDisableVoxelTerrainSystem : SystemBase {
        protected World defaultWorld;
        protected EntityManager entityManager;
        protected EndSimulationEntityCommandBufferSystem ecbSystem;

        private Camera cam;

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
        protected override void OnCreate()
        {
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        protected override void OnStartRunning()
        {
            cam = Camera.main;
        }

        [BurstCompile]
        protected override void OnUpdate() {
            float radius = TerrainManager.instance.terrainSettings.renderDistance;
            EntityQuery allChunkPrefabsQuery = GetEntityQuery(
                new EntityQueryDesc{
                    All = new ComponentType[] {typeof(Prefab), typeof(ChunkParent)}
                }
            );
            if (allChunkPrefabsQuery.CalculateEntityCount() == 0) {return;}

            var chunkPrefabs = allChunkPrefabsQuery.ToEntityArray(Allocator.Temp);

            Entity chunkPrefab = chunkPrefabs[0];
            chunkPrefabs.Dispose();

            ChunkParent prefabChunkComponent = GetComponent<ChunkParent>(chunkPrefab);
            int2 camGridPosition = WorldToGridSpace(cam.transform.position, prefabChunkComponent.grid);

            var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            Entities.
            WithAll<ChunkComponent, VoxelTerrainChunkRenderTag>().
            WithNone<Disabled>().
            ForEach((int entityInQueryIndex, Entity e, in ChunkComponent chunk) => {
                if (math.distance(camGridPosition, chunk.gridPosition) <= radius) {return;}

                ecb.AddComponent<Disabled>(entityInQueryIndex, e);
            }).ScheduleParallel();

            Entities.
            WithAll<ChunkComponent, VoxelTerrainChunkRenderTag, Disabled>().
            ForEach((int entityInQueryIndex, Entity e, in ChunkComponent chunk) => {
                if (math.distance(camGridPosition, chunk.gridPosition) > radius) {return;}

                ecb.RemoveComponent<Disabled>(entityInQueryIndex, e);
            }).ScheduleParallel();

            ecbSystem.AddJobHandleForProducer(Dependency);
        }

    }
    

    [BurstCompile]
    [AlwaysUpdateSystem]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial class SpawnVoxelTerrainChunkSystemV2 : SystemBase {
        protected World defaultWorld;
        protected EntityManager entityManager;
        protected EndSimulationEntityCommandBufferSystem ecbSystem;

        private Camera cam;

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
        protected override void OnCreate()
        {
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            ecbSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        protected override void OnStartRunning()
        {
            cam = Camera.main;
        }

        [BurstCompile]
        protected override void OnUpdate() {
            EntityQuery allChunkPrefabsQuery = GetEntityQuery(
                new EntityQueryDesc{
                    All = new ComponentType[] {typeof(Prefab), typeof(ChunkParent)}
                }
            );
            if (allChunkPrefabsQuery.CalculateEntityCount() == 0) {return;}

            var chunkPrefabs = allChunkPrefabsQuery.ToEntityArray(Allocator.Temp);

            Entity chunkPrefab = chunkPrefabs[0];
            chunkPrefabs.Dispose();

            ChunkParent prefabChunkComponent = GetComponent<ChunkParent>(chunkPrefab);
            int2 camGridPosition = WorldToGridSpace(cam.transform.position, prefabChunkComponent.grid);

            EntityQuery allChunksQuery = GetEntityQuery(
                new EntityQueryDesc{
                    All = new ComponentType[] {typeof(ChunkParent)},
                    None = new ComponentType[] {typeof(Prefab)}
                }
            );
            var allChunks = allChunksQuery.ToComponentDataArray<ChunkParent>(Allocator.TempJob);
            
            bool found = false;

            foreach(var chunk in allChunks) {
                if (!chunk.gridPosition.Equals(camGridPosition)) {continue;}

                found = true;
                break;
            }


            var ecb = ecbSystem.CreateCommandBuffer();

            if (!found) {
                ChunkParent newChunkParent = new ChunkParent
                {
                    grid = prefabChunkComponent.grid,
                    gridPosition = camGridPosition
                };
                Entity entity = ecb.Instantiate(chunkPrefab);
                
                ecb.SetComponent(entity, newChunkParent);                
                ecb.SetComponent(entity, new Translation
                {
                    Value = new float3(camGridPosition.x, 0, camGridPosition.y) * (prefabChunkComponent.grid.voxelSize * prefabChunkComponent.grid.chunkSize)
                });
            }

            float radius = TerrainManager.instance.terrainSettings.renderDistance;

            //allChunks.Dispose();
            //allChunkEntities.Dispose();
            var ecb1 = ecbSystem.CreateCommandBuffer().AsParallelWriter();
            int chunkCount = 0;
            Entities.
            WithAll<ChunkParent>().
            WithNone<Prefab, VoxelTerrainChunkInitializedTag>().
            WithDisposeOnCompletion(allChunks).
            WithReadOnly(allChunks).
            WithBurst().
            ForEach((int entityInQueryIndex, Entity e, in ChunkParent chunkParent) => {
                if (math.distance(chunkParent.gridPosition, camGridPosition) > radius || chunkCount > 4) {return;}

                for (int y = -1; y <= 1; y++) {
                    for (int x = -1; x <= 1; x++) {
                        int2 gridPosition = new int2(x, y) + chunkParent.gridPosition;
                        if (math.abs(x) == math.abs(y) || math.distance(camGridPosition, gridPosition) > radius) {continue;}

                        bool exists = false;
                        foreach (var existingChunk in allChunks) {
                            if (existingChunk.gridPosition.Equals(gridPosition)) {
                                exists = true;
                                break;
                            }
                        }
                        if (exists) {continue;}

                        chunkCount++;

                        ChunkParent newChunkParent = new ChunkParent
                        {
                            grid = chunkParent.grid,
                            gridPosition = gridPosition
                        };
                        Entity entity = ecb1.Instantiate(entityInQueryIndex, chunkPrefab);
                        
                        ecb1.SetComponent(entityInQueryIndex, entity, newChunkParent);                
                        ecb1.SetComponent(entityInQueryIndex, entity, new Translation
                        {
                            Value = new float3(gridPosition.x, 0, gridPosition.y) * (chunkParent.grid.voxelSize * chunkParent.grid.chunkSize)
                        });

                    }
                }
                ecb1.AddComponent<VoxelTerrainChunkInitializedTag>(entityInQueryIndex, e);
            }).ScheduleParallel();

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
                typeof(VoxelTerrainVoxelInitializedTag)
            );

            int chunkComponentCount = 0;
            Entities.
            WithNone<VoxelTerrainChunkInitializedTag>().WithAll<VoxelTerrainChunkNewTag>().
            WithBurst().
            ForEach((int entityInQueryIndex, Entity e, in ChunkComponent terrainChunk) => {
                if (math.distance(terrainChunk.gridPosition, camGridPosition) > radius /*|| chunkComponentCount > 4*/) {return;}
                
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
                chunkComponentCount++;
                ecb2.AddComponent(entityInQueryIndex, e, new VoxelTerrainChunkInitializedTag());
            }).ScheduleParallel();

            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }

    //[DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial class SetVoxelTerrainDataSystem : SystemBase {
        protected World defaultWorld;
        protected EntityManager entityManager;
        protected Biome[] terrainBiomes;
        protected EndSimulationEntityCommandBufferSystem ecbSystem;

        private Camera cam;

        Unity.Mathematics.Random rand;
        int2 randomOffset;

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
        protected override void OnCreate()
        {
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;    
            ecbSystem = defaultWorld.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        protected override void OnStartRunning()
        {
            cam = Camera.main;

            terrainBiomes = new Biome[TerrainManager.instance.terrainSettings.biomes.Count];
            
            for (int i = 0; i < TerrainManager.instance.terrainSettings.biomes.Count; i++)
            {
                terrainBiomes[i] = TerrainManager.instance.terrainSettings.biomes[i];
            }

            rand = new Unity.Mathematics.Random((uint) TerrainManager.instance.terrainSettings.seed);
            randomOffset = rand.NextInt2(new int2(int.MinValue, int.MinValue)/2000, new int2(int.MaxValue, int.MaxValue)/2000);
        }
        
        [BurstCompile]
        protected override void OnUpdate() {
            float radius = TerrainManager.instance.terrainSettings.renderDistance;

            Profiler.BeginSample("Get Prefab");
            EntityQuery allChunkPrefabsQuery = GetEntityQuery(
                new EntityQueryDesc{
                    All = new ComponentType[] {typeof(Prefab), typeof(ChunkParent)}
                }
            );
            if (allChunkPrefabsQuery.CalculateEntityCount() == 0) {return;}

            var chunkPrefabs = allChunkPrefabsQuery.ToEntityArray(Allocator.Temp);

            Entity chunkPrefab = chunkPrefabs[0];
            chunkPrefabs.Dispose();

            ChunkParent prefabChunkComponent = GetComponent<ChunkParent>(chunkPrefab);
            Profiler.EndSample();

            int2 camGridPosition = WorldToGridSpace(cam.transform.position, prefabChunkComponent.grid);

            NativeArray<Biome> biomes = new NativeArray<Biome>(terrainBiomes, Allocator.TempJob);
            ClimateSettings climateSettings = TerrainManager.instance.terrainSettings;

            var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            Profiler.BeginSample("Generate Filter");
            EntityQuery generating = GetEntityQuery(
                new EntityQueryDesc() {
                    All = new ComponentType[] {typeof(VoxelComponent), typeof(VoxelTerrainVoxelInitializedTag)},
                    None = new ComponentType[] {typeof(VoxelTerrainVoxelGeneratedTag)}
                }
            );
            Profiler.EndSample();

            int2 localRandomOffset = randomOffset;

            Entities.
            WithReadOnly(biomes).
            WithDisposeOnCompletion(biomes).
            WithAll<VoxelComponent,VoxelTerrainVoxelInitializedTag>().
            WithNone<VoxelTerrainVoxelGeneratedTag>().
            WithBurst().
            ForEach((Entity e, int entityInQueryIndex, ref VoxelComponent voxel) => {
                //generateMarker.Begin();
                VoxelComponent newVoxel = new VoxelComponent() {};

                int chunkSize = voxel.chunkComponent.grid.chunkSize;
                int chunkIndex = ((int) voxel.position.y * chunkSize) + (int) + voxel.position.x;

                TerrainNoise.GetDataAtPoint(
                    biomes, voxel.position.x, voxel.position.z, voxel.chunkComponent.gridPosition, voxel.chunkComponent.grid.chunkSize, voxel.chunkComponent.grid.voxelSize,
                    climateSettings, out voxel.normal, out voxel.position.y, out voxel.climate, out voxel.terrainColor, localRandomOffset
                );

                if (voxel.position.x == chunkSize -1) {
                    TerrainNoise.GetDataAtPoint(
                        biomes, voxel.position.x + 1, voxel.position.z, voxel.chunkComponent.gridPosition, voxel.chunkComponent.grid.chunkSize, voxel.chunkComponent.grid.voxelSize,
                        climateSettings, out voxel.rightNormal, out voxel.rightPosition.y, out voxel.rightClimate, out voxel.rightColor, localRandomOffset
                    );
                }

                if (voxel.position.z == chunkSize -1) {
                    TerrainNoise.GetDataAtPoint(
                        biomes, voxel.position.x, voxel.position.z + 1, voxel.chunkComponent.gridPosition, voxel.chunkComponent.grid.chunkSize, voxel.chunkComponent.grid.voxelSize,
                        climateSettings, out voxel.topNormal, out voxel.topPosition.y, out voxel.topClimate, out voxel.topColor, localRandomOffset
                    );
                }

                if (voxel.position.z == chunkSize -1 && voxel.position.x == chunkSize -1) {
                    TerrainNoise.GetDataAtPoint(
                        biomes, voxel.position.x + 1, voxel.position.z + 1, voxel.chunkComponent.gridPosition, voxel.chunkComponent.grid.chunkSize, voxel.chunkComponent.grid.voxelSize,
                        climateSettings, out voxel.topRightNormal, out voxel.topRightPosition.y, out voxel.topRightClimate, out voxel.topRightColor, localRandomOffset
                    );
                }
                
                ecb.AddComponent<VoxelTerrainVoxelGeneratedTag>(entityInQueryIndex, e);
                ecb.AddComponent<VoxelTerrainChunkGeneratedTag>(entityInQueryIndex, voxel.chunk);
                //generateMarker.End();
            }).ScheduleParallel();
            // filter.Dispose();

            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }

    //[DisableAutoCreation]
    [BurstCompile]
    //[AlwaysUpdateSystem]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(SetVoxelTerrainDataSystem))]
    public partial class GenerateVoxelTerrainMeshSystem : SystemBase {
        public Mesh.MeshDataArray generatedMeshData;
        public NativeArray<Entity> chunksToRender = new NativeArray<Entity>(0, Allocator.Persistent);
        
        protected World defaultWorld;
        protected EntityManager entityManager;
        protected BeginInitializationEntityCommandBufferSystem ecbSystem;

        EntityQuery chunksReadyToRenderQuery;

        public struct vert {
            public float3 pos;
            public float3 norm;
            public Color32 color;
            public float2 uv0;
            public float2 uv1;
            public float2 uv2;
        }

        [BurstCompile]
        protected override void OnCreate()
        {
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            ecbSystem = defaultWorld.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        protected override void OnDestroy()
        {
            Debug.LogError("Destroyed!");
            chunksToRender.Dispose();
        }

        [BurstCompile]
        protected override void OnUpdate() {
            chunksReadyToRenderQuery = GetEntityQuery(
                new EntityQueryDesc() {
                    All = new ComponentType[] {typeof(VoxelTerrainChunkGeneratedTag), typeof(ChunkComponent)},
                    None = new ComponentType[] {typeof(VoxelTerrainChunkRenderTag)}
                }
            );
            if (chunksReadyToRenderQuery.CalculateEntityCount() == 0) {return;}
            generatedMeshData = Mesh.AllocateWritableMeshData(chunksReadyToRenderQuery.CalculateEntityCount());
            
            chunksToRender = chunksReadyToRenderQuery.ToEntityArray(Allocator.Persistent);

            var meshDataArray = generatedMeshData;
            if (generatedMeshData.Length == 0) {return;}
            var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            for (int i = 0; i < meshDataArray.Length; i++) {
                meshDataArray[i].SetVertexBufferParams(
                    289,
                    new VertexAttributeDescriptor(VertexAttribute.Position),
                    new VertexAttributeDescriptor(VertexAttribute.Normal),
                    new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension:2),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord1, dimension:2),
                    new VertexAttributeDescriptor(VertexAttribute.TexCoord2, dimension:2)
                );
            }

            Entities.
            WithAll<VoxelTerrainChunkGeneratedTag>().
            WithNone<VoxelTerrainChunkRenderTag>().
            WithBurst().
            ForEach((Entity e, int entityInQueryIndex, ref RenderBounds bounds, in ChunkComponent chunkComponent, in DynamicBuffer<VoxelTerrainChunkVoxelBufferElement> voxelEntities) => {
                int elementSize = ((chunkComponent.grid.chunkSize + 1) * (chunkComponent.grid.chunkSize + 1));

                int vertIndex = 0;
                var data = meshDataArray[entityInQueryIndex];
                var verts = data.GetVertexData<vert>();

                float highest = 0;
                float lowest = 0;

                data.subMeshCount = 1;
                data.SetIndexBufferParams(elementSize * 6, IndexFormat.UInt32);
                var indecies = data.GetIndexData<int>();
                for(int i = 0; i < indecies.Length; i++) {indecies[i] = 0;}
                
                for (int i = 0; i < voxelEntities.Length; i++) {
                    VoxelComponent voxel = GetComponent<VoxelComponent>(voxelEntities[i]);

                    int chunkSize = voxel.chunkComponent.grid.chunkSize;
                    int y = i / chunkSize;
                    int x = i % chunkSize;

                    vert vert = new vert();

                    vert.pos = voxel.position * new float3(chunkComponent.grid.voxelSize, 1, chunkComponent.grid.voxelSize);
                    vert.norm = voxel.normal;
                    vert.color = voxel.terrainColor;
                    vert.uv0 = new float2(x, y);
                    vert.uv1 = new float2(voxel.position.x / chunkSize, voxel.position.z / chunkSize);
                    vert.uv2 = voxel.climate;

                    if (vert.pos.y > highest) {highest = vert.pos.y;}
                    if (vert.pos.y < lowest) {lowest = vert.pos.y;}

                    verts[vertIndex] = vert;

                    int triStart = (i * 6);

                    indecies[triStart] = vertIndex;
                    indecies[triStart + 1] = vertIndex + chunkSize + 1;
                    indecies[triStart + 2] = vertIndex + 1;
                    indecies[triStart + 3] = vertIndex + 1;
                    indecies[triStart + 4] = vertIndex + chunkSize + 1;
                    indecies[triStart + 5] = vertIndex + 2 + chunkSize;

                    vertIndex++;

                    if (y == (chunkSize -1)) {
                        vert nVert = new vert();
                        int index = vertIndex + chunkSize;
                        nVert.pos = voxel.topPosition * new float3(chunkComponent.grid.voxelSize, 1, chunkComponent.grid.voxelSize);
                        nVert.norm = voxel.topNormal;
                        nVert.color = voxel.topColor;
                        nVert.uv0 = new float2(x, chunkSize);
                        nVert.uv1 = new float2(voxel.position.x / chunkSize, 1);
                        nVert.uv2 = voxel.topClimate;

                        if (nVert.pos.y > highest) {highest = nVert.pos.y;}
                        if (nVert.pos.y < lowest) {lowest = nVert.pos.y;}

                        verts[index] = nVert;
                    }

                    if (x == (chunkSize - 1) && y == (chunkSize - 1)) {
                        vert nVert = new vert();
                        int index = vertIndex + chunkSize + 1;
                        nVert.pos = voxel.topRightPosition;
                        nVert.norm = voxel.topRightNormal;
                        nVert.color = voxel.topRightColor;
                        nVert.uv0 = new float2(chunkSize, chunkSize);
                        nVert.uv1 = new float2(1, 1);
                        nVert.uv2 = voxel.topRightClimate;

                        if (nVert.pos.y > highest) {highest = nVert.pos.y;}
                        if (nVert.pos.y < lowest) {lowest = nVert.pos.y;}

                        verts[index] = nVert;
                    }

                    if (x == (chunkSize -1)) {
                        vert nVert = new vert();
                        nVert.pos = voxel.rightPosition * new float3(chunkComponent.grid.voxelSize, 1, chunkComponent.grid.voxelSize);
                        nVert.norm = voxel.rightNormal;
                        nVert.color = voxel.rightColor;
                        nVert.uv0 = new float2(chunkSize, y);
                        nVert.uv1 = new float2(1, voxel.position.z / chunkSize);
                        nVert.uv2 = voxel.rightClimate;

                        if (nVert.pos.y > highest) {highest = nVert.pos.y;}
                        if (nVert.pos.y < lowest) {lowest = nVert.pos.y;}

                        verts[vertIndex] = nVert;

                        vertIndex++;
                    }
                }

                float height = highest - lowest;

                bounds.Value = new AABB();
                bounds.Value.Center = float3.zero;
                bounds.Value.Extents = new float3(
                    (chunkComponent.grid.chunkSize * chunkComponent.grid.voxelSize) / 2,
                    500,
                    (chunkComponent.grid.chunkSize * chunkComponent.grid.voxelSize) / 2
                );

                data.SetSubMesh(0, new SubMeshDescriptor(0, elementSize * 6));
                ecb.AddComponent<VoxelTerrainChunkRenderTag>(entityInQueryIndex, e);
                
            }).ScheduleParallel();

            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }


    //[DisableAutoCreation]
    [BurstCompile]
    [AlwaysUpdateSystem]
    [UpdateAfter(typeof(BeginInitializationEntityCommandBufferSystem))]
    [UpdateBefore(typeof(EndInitializationEntityCommandBufferSystem))]
    public partial class RenderVoxelTerrainChunkSystem : SystemBase {
        protected World defaultWorld;
        protected EntityManager entityManager;

        EntityQuery chunksReadyToRenderQuery;
        GenerateVoxelTerrainMeshSystem meshSystem;
        EndInitializationEntityCommandBufferSystem ecbSystem;

        protected override void OnCreate()
        {
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            meshSystem = defaultWorld.GetOrCreateSystem<GenerateVoxelTerrainMeshSystem>();
            ecbSystem = defaultWorld.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
        }

        [BurstCompile]
        protected override void OnStartRunning() {
            chunksReadyToRenderQuery = GetEntityQuery(
                new EntityQueryDesc() {
                    All = new ComponentType[] {typeof(VoxelTerrainChunkRenderTag), typeof(ChunkComponent), typeof(DisableRendering)},
                }
            );
        }

        protected override void OnUpdate() {
            var meshDataArray = meshSystem.generatedMeshData;
            if (!meshSystem.chunksToRender.IsCreated) {return;}
            else if (meshSystem.chunksToRender.Length == 0) {
                try {
                    meshDataArray.Dispose();
                }
                catch {}
                    meshSystem.chunksToRender.Dispose();
                
                return;
            }
            var ecb = ecbSystem.CreateCommandBuffer();

            List<Mesh> meshes = new List<Mesh>();

            foreach(var entity in meshSystem.chunksToRender) {
                Mesh mesh = new Mesh();
                RenderMesh renderMesh = entityManager.GetSharedComponentData<RenderMesh>(entity);
                renderMesh.mesh = mesh;
                ecb.SetSharedComponent<RenderMesh>(entity, renderMesh);
                meshes.Add(mesh);
            }

            meshSystem.chunksToRender.Dispose();
            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);

            //foreach(Mesh mesh in meshes) {mesh.RecalculateNormals();}

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
