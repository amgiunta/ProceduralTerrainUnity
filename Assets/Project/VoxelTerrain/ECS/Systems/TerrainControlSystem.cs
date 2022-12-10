using System;
using System.Collections.Generic;

using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

using VoxelTerrain.ECS.Components;

namespace VoxelTerrain.ECS.Components {

    public struct VoxelTerrainChunkNewTag : IComponentData { }

    public struct VoxelTerrainChunkGeneratedTag : IComponentData { }

    public struct VoxelTerrainChunkInitializedTag : IComponentData { }

    public struct VoxelTerrainChunkLoadedTag : IComponentData { }

    public struct VoxelTerrainChunkRenderTag : IComponentData { }

    public struct VoxelTerrainChunkGroundScatterBufferElement : IBufferElementData {
        public static implicit operator GroundScatter(VoxelTerrainChunkGroundScatterBufferElement e) { return e.value; }
        public static implicit operator VoxelTerrainChunkGroundScatterBufferElement(GroundScatter e) { return new VoxelTerrainChunkGroundScatterBufferElement { value = e }; }

        public GroundScatter value;
    }

    public struct VoxelTerrainChunkVoxelBufferElement : IBufferElementData {
        public static implicit operator Voxel(VoxelTerrainChunkVoxelBufferElement e) { return e.value; }
        public static implicit operator VoxelTerrainChunkVoxelBufferElement(Voxel e) { return new VoxelTerrainChunkVoxelBufferElement { value = e }; }

        public Voxel value;
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

    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true, OrderLast = false)]
    [UpdateAfter(typeof(BeginInitializationEntityCommandBufferSystem))]
    public partial class SpawnVoxelTerrainChunkSystem : SystemBase
    {
        public NativeParallelHashMap<int2, Entity> chunks;

        private BeginInitializationEntityCommandBufferSystem ecbSystem;
        private World defaultWorld;
        private EntityManager entityManager;

        private Camera cam;


        protected override void OnCreate()
        {
            chunks = new NativeParallelHashMap<int2, Entity>(1, Allocator.Persistent);

            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            ecbSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        protected override void OnDestroy()
        {
            chunks.Dispose();
        }

        protected override void OnStartRunning()
        {
            cam = Camera.main;
            
        }

        protected override void OnUpdate()
        {
            //if (TerrainChunkConversionManager.chunkPrefab == default) { return; }

            var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            int radius = (int)TerrainManager.instance.terrainSettings.renderDistance;
            int2 center = WorldToGridSpace(cam.transform.position);
            EntityQuery query = GetEntityQuery(ComponentType.ReadOnly<ChunkParent>());
            var localChunks = query.ToComponentDataArray<ChunkParent>(Allocator.TempJob);

            var job1 = Entities.
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
                for (int i = 0; i < (radius * radius) && newCount < 10; i++)
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
            }).ScheduleParallel(Dependency);

            var ecb2 = ecbSystem.CreateCommandBuffer().AsParallelWriter();

            Dependency = Entities.
            WithAll<Parent, ChunkComponent>().
            WithNone<VoxelTerrainChunkInitializedTag, VoxelTerrainChunkGeneratedTag, VoxelTerrainChunkLoadedTag>().
            WithBurst().
            ForEach((int entityInQueryIndex, Entity e, ref ChunkComponent chunk, in Parent parent) =>
            {
                ChunkParent parentChunk = GetComponent<ChunkParent>(parent.Value);

                chunk.gridPosition = parentChunk.gridPosition;
            }).ScheduleParallel(job1);

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

    [AlwaysUpdateSystem]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SpawnVoxelTerrainChunkSystem))]
    public partial class ClosestVoxelTerrainChunkFinderSystem : SystemBase
    {
        protected World defaultWorld;
        protected EntityManager entityManager;

        protected override void OnCreate()
        {
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
        }

        protected override void OnUpdate()
        {
            Profiler.BeginSample("Create Closest Chunk Job");

            ClosestVoxelTerrainChunkData.closestChunkDistance.Data = float.MaxValue;
            ClosestVoxelTerrainChunkData.closestChunk.Data = default;
            ClosestVoxelTerrainChunkData.closestChunkEntity.Data = default;
            ClosestVoxelTerrainChunkData.closestChunkTranslation.Data = default;
            ClosestVoxelTerrainChunkData.closestChunkRenderBounds.Data = default;

            #region GetClosestChunk
            Camera cam = Camera.main;
            float3 camPosition = cam.transform.position;

            Entities.WithAll<ChunkComponent, VoxelTerrainChunkNewTag>().
            WithNone<VoxelTerrainChunkInitializedTag, VoxelTerrainChunkGeneratedTag, VoxelTerrainChunkLoadedTag>().
            WithoutBurst().
            ForEach((Entity entity, in Translation translation, in ChunkComponent chunk, in RenderBounds bounds) =>
            {
                float distance = math.distance(translation.Value, camPosition);

                if (
                    distance < ClosestVoxelTerrainChunkData.closestChunkDistance.Data
                )
                {

                    ClosestVoxelTerrainChunkData.closestChunkDistance.Data = distance;
                    ClosestVoxelTerrainChunkData.closestChunkEntity.Data = entity;
                    ClosestVoxelTerrainChunkData.closestChunk.Data = chunk;
                    ClosestVoxelTerrainChunkData.closestChunkTranslation.Data = translation;
                    ClosestVoxelTerrainChunkData.closestChunkRenderBounds.Data = bounds;
                }
            }).Schedule();
            #endregion
            Profiler.EndSample();
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true, OrderLast = false)]
    [UpdateAfter(typeof(BeginInitializationEntityCommandBufferSystem))]
    public partial class GenerateVoxelTerrainChunkSystem : SystemBase
    {
        protected BeginInitializationEntityCommandBufferSystem tagSystem;
        protected World defaultWorld;
        protected EntityManager entityManager;

        protected Biome[] terrainBiomes;

        private Unity.Mathematics.Random terrainRandom;

        protected override void OnCreate()
        {
            tagSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
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
            if (ClosestVoxelTerrainChunkData.closestChunkEntity.Data == default)
            {
                return;
            }
            var tagEcb = tagSystem.CreateCommandBuffer().AsParallelWriter();

            Profiler.BeginSample("Create Voxel Generator Job");
            #region GenerateVoxelsJob            

            NativeArray<Biome> biomes = new NativeArray<Biome>(terrainBiomes, Allocator.TempJob);
            ClimateSettings climateSettings = TerrainManager.instance.terrainSettings;
            Unity.Mathematics.Random random = terrainRandom;
            int seed = TerrainManager.instance.terrainSettings.seed;
            float3 camPosition = Camera.main.transform.position;
            camPosition.y = 0;

            float renderDistance = TerrainManager.instance.terrainSettings.renderDistance;

            Entities.
            WithReadOnly(biomes).
            WithDisposeOnCompletion(biomes).
            WithAll<VoxelTerrainChunkNewTag>().WithNone<VoxelTerrainChunkGeneratedTag>().
            WithBurst().
            ForEach((Entity entity, int entityInQueryIndex, in ChunkComponent closestChunk, in Translation translation) =>
            {
                if (math.distance(translation.Value, camPosition) > (renderDistance * closestChunk.grid.chunkSize * closestChunk.grid.voxelSize)) { return; }

                var voxelBuffer = tagEcb.SetBuffer<VoxelTerrainChunkVoxelBufferElement>(entityInQueryIndex, entity);
                var climateColorBuffer = tagEcb.SetBuffer<VoxelTerrainChunkClimateColorBufferElement>(entityInQueryIndex, entity);
                var terrainColorBuffer = tagEcb.SetBuffer<VoxelTerrainChunkTerrainColorBufferElement>(entityInQueryIndex, entity);
                var climateBuffer = tagEcb.SetBuffer<VoxelTerrainChunkClimateBufferElement>(entityInQueryIndex, entity);
                var topEdgeBuffer = tagEcb.SetBuffer<VoxelTerrainChunkTopEdgeBufferElement>(entityInQueryIndex, entity);
                var rightEdgeBuffer = tagEcb.SetBuffer<VoxelTerrainChunkRightEdgeBufferElement>(entityInQueryIndex, entity);

                voxelBuffer.ResizeUninitialized(closestChunk.grid.chunkSize * closestChunk.grid.chunkSize);
                topEdgeBuffer.ResizeUninitialized(closestChunk.grid.chunkSize + 1);
                rightEdgeBuffer.ResizeUninitialized(closestChunk.grid.chunkSize);
                climateBuffer.ResizeUninitialized(closestChunk.grid.chunkSize * closestChunk.grid.chunkSize);
                climateColorBuffer.ResizeUninitialized(closestChunk.grid.chunkSize * closestChunk.grid.chunkSize);
                terrainColorBuffer.ResizeUninitialized(closestChunk.grid.chunkSize * closestChunk.grid.chunkSize);


                for (int i = 0; i < (closestChunk.grid.chunkSize * closestChunk.grid.chunkSize) + (2 * closestChunk.grid.chunkSize + 1); i++)
                {
                    int2 chunkPosition = closestChunk.gridPosition;

                    Voxel voxel = new Voxel();
                    int x = i % (closestChunk.grid.chunkSize + 1);
                    int y = i / (closestChunk.grid.chunkSize + 1);

                    int index = y * closestChunk.grid.chunkSize + x;
                    
                    float2 climate = TerrainNoise.Climate(x, y, climateSettings, closestChunk.gridPosition, closestChunk.grid.chunkSize, closestChunk.grid.voxelSize, seed);                    

                    float3 normal = new float3(0, 1, 0);
                    float height = TerrainNoise.GetHeightAndNormalAtPoint(x, y, climate, biomes, closestChunk.gridPosition, closestChunk.grid.chunkSize, closestChunk.grid.voxelSize, seed, out normal);
                    voxel.normal = normal;
                    
                    voxel.position = new float3(x, height, y);
                    if (x < closestChunk.grid.chunkSize && y < closestChunk.grid.chunkSize)
                    {
                        climateBuffer[index] = climate;
                        climateColorBuffer[index] = new Color(climate.x, 0, climate.y, 1);
                        terrainColorBuffer[index] = TerrainNoise.GetColorAtPoint(biomes, climate);
                        voxelBuffer[index] = voxel;
                    }
                    else if (y >= closestChunk.grid.chunkSize) {
                        topEdgeBuffer[x] = voxel;
                    }
                    else if (x == closestChunk.grid.chunkSize)
                    {
                        rightEdgeBuffer[y] = voxel;
                    }                    
                }

                tagEcb.AddComponent<VoxelTerrainChunkGeneratedTag>(entityInQueryIndex, entity);
            }).ScheduleParallel();

            tagSystem.AddJobHandleForProducer(Dependency);

            #endregion
            Profiler.EndSample();
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true, OrderLast = false)]
    [UpdateAfter(typeof(BeginInitializationEntityCommandBufferSystem))]
    public partial class GenerateVoxelTerrainMeshSystem : SystemBase
    {
        protected BeginInitializationEntityCommandBufferSystem ecbSystem;
        protected World defaultWorld;
        protected EntityManager entityManager;

        protected override void OnCreate()
        {
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            ecbSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        private static Texture2D GetClimateColors(ChunkComponent chunk, in NativeArray<VoxelTerrainChunkClimateColorBufferElement> climateBuffer) {
            Profiler.BeginSample("Climate Color Texture");
            Texture2D tex = new Texture2D(chunk.grid.chunkSize, chunk.grid.chunkSize);
            var texPixels = tex.GetRawTextureData<Color32>();

            for (int i = 0; i < climateBuffer.Length; i++)
            {
                texPixels[i] = (Color32)climateBuffer[i].value;
            }

            tex.Apply(false);
            Profiler.EndSample();
            return tex;
        }

        private static Texture2D GetBiomeColors(ChunkComponent chunk, in NativeArray<VoxelTerrainChunkTerrainColorBufferElement> terrainColorBuffer)
        {
            Profiler.BeginSample("Biome Color Texture");
            Texture2D tex = new Texture2D(chunk.grid.chunkSize, chunk.grid.chunkSize);
            var texPixels = tex.GetRawTextureData<Color32>();

            for (int i = 0; i < terrainColorBuffer.Length; i++) {
                texPixels[i] = (Color32) terrainColorBuffer[i].value;
            }

            tex.Apply(false);
            Profiler.EndSample();
            return tex;
        }

        private static void CreateCallbacks(
            Mesh mesh, ComputeBuffer verts, ComputeBuffer tris, 
            ComputeBuffer voxels, ComputeBuffer topEdge, ComputeBuffer rightEdge, 
            ComputeBuffer normals, ComputeBuffer uv0, ComputeBuffer uv1
        ) {
            AsyncGPUReadback.Request(verts, (AsyncGPUReadbackRequest request) => {
                Profiler.BeginSample("Perform Mesh Callbacks");
                if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
                else if (!request.done) { Debug.Log("Not done yet..."); return; }

                AsyncGPUReadback.Request(tris, (AsyncGPUReadbackRequest request) =>
                {
                    if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
                    else if (!request.done) { Debug.Log("Not done yet..."); return; }

                    mesh.SetIndexBufferParams(tris.count, IndexFormat.UInt32);
                    mesh.SetIndexBufferData(request.GetData<int>(), 0, 0, tris.count);
                    mesh.SetSubMesh(0, new SubMeshDescriptor(0, tris.count));
                    tris.Dispose();
                });

                AsyncGPUReadback.Request(normals, (AsyncGPUReadbackRequest request) =>
                {
                    if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
                    else if (!request.done) { Debug.Log("Not done yet..."); return; }

                    mesh.SetNormals(request.GetData<Vector3>());
                    normals.Dispose();
                });

                AsyncGPUReadback.Request(uv0, (AsyncGPUReadbackRequest request) =>
                {
                    if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
                    else if (!request.done) { Debug.Log("Not done yet..."); return; }

                    mesh.SetUVs(0, request.GetData<Vector2>());
                    uv0.Dispose();
                });

                AsyncGPUReadback.Request(uv1, (AsyncGPUReadbackRequest request) =>
                {
                    if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
                    else if (!request.done) { Debug.Log("Not done yet..."); return; }

                    mesh.SetUVs(1, request.GetData<Vector2>());
                    uv1.Dispose();
                });

                mesh.SetVertices(request.GetData<Vector3>());
                Profiler.EndSample();

                Profiler.BeginSample("Dispose Mesh Buffers");
                verts.Dispose();
                voxels.Dispose();
                topEdge.Dispose();
                rightEdge.Dispose();
                Profiler.EndSample();
            });
        }

        protected override void OnUpdate()
        {
            var ecb = ecbSystem.CreateCommandBuffer();

            TerrainSettings terrainSettings = TerrainManager.instance.terrainSettings;
            ComputeShader meshGenerator = TerrainChunkConversionManager.terrainGeneratorShader;

            int renderCount = 0;

            Entities.
            WithAll<VoxelTerrainChunkGeneratedTag, ChunkComponent>().
            WithNone<VoxelTerrainChunkRenderTag>().
            WithoutBurst().
            ForEach((int entityInQueryIndex, Entity e, in RenderMesh meshInstance, in ChunkComponent chunk) => {
                if (renderCount >= 100) { return; }

                Profiler.BeginSample("Init Render Buffers");
                NativeArray<VoxelTerrainChunkVoxelBufferElement> terrainVoxels = GetBuffer<VoxelTerrainChunkVoxelBufferElement>(e).AsNativeArray();
                NativeArray<VoxelTerrainChunkTopEdgeBufferElement> topEdgeVoxels = GetBuffer<VoxelTerrainChunkTopEdgeBufferElement>(e).AsNativeArray();
                NativeArray<VoxelTerrainChunkRightEdgeBufferElement> rightEdgeVoxels = GetBuffer<VoxelTerrainChunkRightEdgeBufferElement>(e).AsNativeArray();
                NativeArray<VoxelTerrainChunkClimateColorBufferElement> climateBuffer = GetBuffer<VoxelTerrainChunkClimateColorBufferElement>(e).AsNativeArray();
                NativeArray<VoxelTerrainChunkTerrainColorBufferElement> terrainColorBuffer = GetBuffer<VoxelTerrainChunkTerrainColorBufferElement>(e).AsNativeArray();
                Profiler.EndSample();

                if (terrainVoxels.Length == 0) { return; }

                Profiler.BeginSample("Init Mesh Generator");
                renderCount++;

                Mesh mesh = new Mesh();

                mesh.name = $"chunk {chunk.gridPosition}";

                int sizeVector3 = sizeof(float) * 3;
                int sizeVector2 = sizeof(float) * 2;
                int sizeVoxel = sizeVector3 * 2;
                int chunkWidth = chunk.grid.chunkSize;
                int vertCount = chunkWidth * chunkWidth * terrainSettings.voxelVertecies;
                int indexCount = chunkWidth * chunkWidth * terrainSettings.voxelIdexies;

                ComputeBuffer voxels = new ComputeBuffer(terrainVoxels.Length, sizeVoxel);
                voxels.SetData(terrainVoxels);

                ComputeBuffer topEdge = new ComputeBuffer(topEdgeVoxels.Length, sizeVoxel);
                topEdge.SetData(topEdgeVoxels);

                ComputeBuffer rightEdge = new ComputeBuffer(rightEdgeVoxels.Length, sizeVoxel);
                rightEdge.SetData(rightEdgeVoxels);

                ComputeBuffer verts = new ComputeBuffer(vertCount, sizeVector3);
                ComputeBuffer tris = new ComputeBuffer(indexCount, sizeof(int));
                ComputeBuffer normals = new ComputeBuffer(vertCount, sizeVector3);
                ComputeBuffer uv0 = new ComputeBuffer(vertCount, sizeVector2);
                ComputeBuffer uv1 = new ComputeBuffer(vertCount, sizeVector2);

                meshGenerator.SetFloat("voxelSize", chunk.grid.voxelSize);
                meshGenerator.SetInt("chunkWidth", chunkWidth);

                meshGenerator.SetBuffer(0, "voxels", voxels);
                meshGenerator.SetBuffer(0, "topEdge", topEdge);
                meshGenerator.SetBuffer(0, "rightEdge", rightEdge);

                meshGenerator.SetBuffer(0, "verts", verts);
                meshGenerator.SetBuffer(0, "tris", tris);
                meshGenerator.SetBuffer(0, "normals", normals);
                meshGenerator.SetBuffer(0, "uv0s", uv0);
                meshGenerator.SetBuffer(0, "uv1s", uv1);
                Profiler.EndSample();

                Profiler.BeginSample("Dispach Mesh Generator");
                // Run computation
                meshGenerator.Dispatch(0, chunkWidth, chunkWidth, 1);
                Profiler.EndSample();

                Profiler.BeginSample("Apply Terrain Mesh");
                AABB aabb = new AABB();
                aabb.Center = new float3((chunk.grid.chunkSize * chunk.grid.voxelSize) / 2, 500, (chunk.grid.chunkSize * chunk.grid.voxelSize) / 2);
                aabb.Extents = new float3(chunk.grid.chunkSize * chunk.grid.voxelSize, 1000, chunk.grid.chunkSize * chunk.grid.voxelSize);

                ecb.SetComponent(e, new RenderBounds { Value = aabb });
                
                ecb.AddComponent<VoxelTerrainChunkRenderTag>(e);

                ecb.RemoveComponent<DisableRendering>(e);

                Profiler.BeginSample("Generate Mesh Textures");
                Texture2D climateTex = GetClimateColors(chunk, climateBuffer);
                Texture2D colorTex = GetBiomeColors(chunk, terrainColorBuffer);
                Profiler.EndSample();

                Profiler.BeginSample("Apply Mesh Material and Textures");
                Material mat = new Material(meshInstance.material);
                mat.SetTexture("climate_texture", climateTex);
                mat.SetTexture("color_texture", colorTex);
                Profiler.EndSample();

                Profiler.BeginSample("Copy Terrain Mesh");
                RenderMesh meshCopy = meshInstance;
                meshCopy.mesh = mesh;
                meshCopy.material = mat;
                Profiler.EndSample();

                ecb.SetSharedComponent(e, meshCopy);
                Profiler.EndSample();

                CreateCallbacks(
                    mesh, verts, tris,
                    voxels, topEdge, rightEdge,
                    normals, uv0, uv1
                );

            }).Run();
            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }

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
