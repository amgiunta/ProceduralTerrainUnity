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
    [GenerateAuthoringComponent]
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
    public class SpawnVoxelTerrainChunkSystem : SystemBase
    {
        public NativeHashMap<int2, Entity> chunks;

        private EndInitializationEntityCommandBufferSystem ecbSystem;
        private World defaultWorld;
        private EntityManager entityManager;

        private Camera cam;


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

        protected override void OnStartRunning()
        {
            cam = Camera.main;
        }

        protected override void OnUpdate()
        {
            if (TerrainChunkConversionManager.chunkPrefab == default) { return; }

            var ecb = ecbSystem.CreateCommandBuffer();

            int radius = (int)TerrainManager.instance.terrainSettings.renderDistance;
            int2 center = WorldToGridSpace(cam.transform.position);
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
    public class ClosestVoxelTerrainChunkFinderSystem : SystemBase
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

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true, OrderLast = false)]
    [UpdateAfter(typeof(ClosestVoxelTerrainChunkFinderSystem))]
    public class GenerateVoxelTerrainChunkSystem : SystemBase
    {
        protected EndSimulationEntityCommandBufferSystem bufferSystem;
        protected BeginPresentationEntityCommandBufferSystem tagSystem;
        protected World defaultWorld;
        protected EntityManager entityManager;

        protected Biome[] terrainBiomes;

        private Unity.Mathematics.Random terrainRandom;

        protected override void OnCreate()
        {
            bufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            tagSystem = World.GetOrCreateSystem<BeginPresentationEntityCommandBufferSystem>();
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
            var tagEcb = tagSystem.CreateCommandBuffer();

            Profiler.BeginSample("Create Voxel Generator Job");
            #region GenerateVoxelsJob

            int chunkWidth = TerrainManager.instance.terrainSettings.grid.chunkSize;
            NativeArray<VoxelTerrainChunkVoxelBufferElement> voxelBuffer = new NativeArray<VoxelTerrainChunkVoxelBufferElement>(chunkWidth * chunkWidth, Allocator.TempJob);
            NativeArray<VoxelTerrainChunkClimateBufferElement> climateBuffer = new NativeArray<VoxelTerrainChunkClimateBufferElement>(chunkWidth * chunkWidth, Allocator.TempJob);
            NativeArray<VoxelTerrainChunkClimateColorBufferElement> climateColorBuffer = new NativeArray<VoxelTerrainChunkClimateColorBufferElement>(chunkWidth * chunkWidth, Allocator.TempJob);
            NativeArray<VoxelTerrainChunkTerrainColorBufferElement> terrainColorBuffer = new NativeArray<VoxelTerrainChunkTerrainColorBufferElement>(chunkWidth * chunkWidth, Allocator.TempJob);

            NativeArray<Biome> biomes = new NativeArray<Biome>(terrainBiomes, Allocator.TempJob);
            ClimateSettings climateSettings = TerrainManager.instance.terrainSettings;
            Unity.Mathematics.Random random = terrainRandom;
            int seed = TerrainManager.instance.terrainSettings.seed;
            ChunkComponent closestChunk = ClosestVoxelTerrainChunkData.closestChunk.Data;

            var bufferEcb = bufferSystem.CreateCommandBuffer();

            JobHandle generatorJob = Job.
            WithBurst().WithCode(() =>
            {
                for (int i = 0; i < chunkWidth * chunkWidth; i++)
                {
                    int2 chunkPosition = closestChunk.gridPosition;

                    int stride = 1;

                    Voxel voxel = new Voxel();
                    voxel.x = i % chunkWidth;
                    voxel.y = i / chunkWidth;

                    float2 climate = TerrainNoise.Climate(voxel.x * stride, voxel.y * stride, climateSettings, chunkPosition, chunkWidth, seed);
                    climateBuffer[i] = climate;
                    climateColorBuffer[i] = new Color(climate.x, 0, climate.y, 1);

                    float3 normal = new float3(0, 1, 0);

                    voxel.height = (int)TerrainNoise.GetHeightAndNormalAtPoint(voxel.x, voxel.y, climate, biomes, stride, chunkPosition, chunkWidth, seed, out normal);
                    voxel.normal = normal;

                    terrainColorBuffer[i] = TerrainNoise.GetColorAtPoint(biomes, climate);
                    voxelBuffer[i] = voxel;
                }
            }).Schedule(Dependency);

            #endregion
            Profiler.EndSample();

            Profiler.BeginSample("Create Buffer Attacher Job");
            #region AttachBuffersJob

            Entity closestChunkEntity = ClosestVoxelTerrainChunkData.closestChunkEntity.Data;

            Dependency = Job.
            WithReadOnly(voxelBuffer).WithDisposeOnCompletion(voxelBuffer).
            WithReadOnly(terrainColorBuffer).WithDisposeOnCompletion(terrainColorBuffer).
            WithReadOnly(climateColorBuffer).WithDisposeOnCompletion(climateColorBuffer).
            WithReadOnly(climateBuffer).WithDisposeOnCompletion(climateBuffer).
            WithReadOnly(biomes).WithDisposeOnCompletion(biomes).
            WithBurst().
            WithCode(() =>
            {
                int biomeLength = biomes.Length;

                var dynamicVoxelBuffer = bufferEcb.SetBuffer<VoxelTerrainChunkVoxelBufferElement>(closestChunkEntity);
                dynamicVoxelBuffer.AddRange(voxelBuffer);

                var dynamicColorBuffer = bufferEcb.SetBuffer<VoxelTerrainChunkTerrainColorBufferElement>(closestChunkEntity);
                dynamicColorBuffer.AddRange(terrainColorBuffer);

                var dynamicClimateColorBuffer = bufferEcb.SetBuffer<VoxelTerrainChunkClimateColorBufferElement>(closestChunkEntity);
                dynamicClimateColorBuffer.AddRange(climateColorBuffer);

                var dynamicClimateBuffer = bufferEcb.SetBuffer<VoxelTerrainChunkClimateBufferElement>(closestChunkEntity);
                dynamicClimateBuffer.AddRange(climateBuffer);

                tagEcb.AddComponent<VoxelTerrainChunkGeneratedTag>(closestChunkEntity);
            }).Schedule(generatorJob);
            #endregion
            Profiler.EndSample();

            bufferSystem.AddJobHandleForProducer(Dependency);
            tagSystem.AddJobHandleForProducer(Dependency);
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup), OrderFirst = true, OrderLast = false)]
    [UpdateAfter(typeof(GenerateVoxelTerrainChunkSystem))]
    public class GenerateVoxelTerrainMeshSystem : SystemBase
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

        private Texture2D GetClimateColors() {
            NativeArray<VoxelTerrainChunkClimateColorBufferElement> climateBuffer = GetBuffer<VoxelTerrainChunkClimateColorBufferElement>(ClosestVoxelTerrainChunkData.closestChunkEntity.Data).AsNativeArray();
            Color[] colorBuffer = new Color[climateBuffer.Length];

            for (int i = 0; i < climateBuffer.Length; i++) {
                colorBuffer[i] = climateBuffer[i];
            }

            Texture2D tex = new Texture2D(TerrainManager.instance.terrainSettings.grid.chunkSize, TerrainManager.instance.terrainSettings.grid.chunkSize);
            tex.SetPixels(colorBuffer);
            tex.Apply();
            return tex;
        }

        private Texture2D GetBiomeColors()
        {
            NativeArray<VoxelTerrainChunkTerrainColorBufferElement> climateBuffer = GetBuffer<VoxelTerrainChunkTerrainColorBufferElement>(ClosestVoxelTerrainChunkData.closestChunkEntity.Data).AsNativeArray();
            Color[] colorBuffer = new Color[climateBuffer.Length];

            for (int i = 0; i < climateBuffer.Length; i++)
            {
                colorBuffer[i] = climateBuffer[i];
            }

            Texture2D tex = new Texture2D(TerrainManager.instance.terrainSettings.grid.chunkSize, TerrainManager.instance.terrainSettings.grid.chunkSize);
            tex.SetPixels(colorBuffer);
            tex.Apply();
            return tex;
        }

        protected override void OnUpdate()
        {
            if (ClosestVoxelTerrainChunkData.closestChunkEntity.Data == default) { return; }
            var ecb = ecbSystem.CreateCommandBuffer();

            ComputeShader meshGenerator = TerrainChunkConversionManager.terrainGeneratorShader;
            NativeArray<VoxelTerrainChunkVoxelBufferElement> terrainVoxels = GetBuffer<VoxelTerrainChunkVoxelBufferElement>(ClosestVoxelTerrainChunkData.closestChunkEntity.Data).AsNativeArray();

            if (terrainVoxels.Length == 0) { return; }

            Mesh mesh = new Mesh();
            Entity closestEntity = ClosestVoxelTerrainChunkData.closestChunkEntity.Data;
            ChunkComponent chunkComponent = ClosestVoxelTerrainChunkData.closestChunk.Data;
            RenderMesh meshInstance = TerrainChunkConversionManager.renderMesh;

            mesh.name = $"chunk {chunkComponent.gridPosition}";

            int sizeVector3 = sizeof(float) * 3;
            int sizeVector2 = sizeof(float) * 2;
            int sizeVoxel = (sizeof(int) * 3) + (sizeVector3);
            int vertCount = (terrainVoxels.Length * 12) + (chunkComponent.grid.chunkSize * 8);
            int indexCount = (terrainVoxels.Length * 18) + (chunkComponent.grid.chunkSize * 12);

            ComputeBuffer voxels = new ComputeBuffer(terrainVoxels.Length, sizeVoxel);
            voxels.SetData(terrainVoxels);

            ComputeBuffer verts = new ComputeBuffer(vertCount, sizeVector3);
            ComputeBuffer tris = new ComputeBuffer(indexCount, sizeof(int));
            ComputeBuffer normals = new ComputeBuffer(vertCount, sizeVector3);
            ComputeBuffer uv0 = new ComputeBuffer(vertCount, sizeVector2);
            ComputeBuffer uv1 = new ComputeBuffer(vertCount, sizeVector2);

            meshGenerator.SetFloat("voxelSize", chunkComponent.grid.voxelSize);
            meshGenerator.SetInt("chunkWidth", chunkComponent.grid.chunkSize);

            meshGenerator.SetBuffer(0, "voxels", voxels);
            meshGenerator.SetBuffer(0, "verts", verts);
            meshGenerator.SetBuffer(0, "tris", tris);
            meshGenerator.SetBuffer(0, "normals", normals);
            meshGenerator.SetBuffer(0, "uv0", uv0);
            meshGenerator.SetBuffer(0, "uv1", uv1);

            // Run computation
            meshGenerator.Dispatch(0, chunkComponent.grid.chunkSize, chunkComponent.grid.chunkSize, 1);

            AABB aabb = new AABB();
            aabb.Center = new float3((chunkComponent.grid.chunkSize * chunkComponent.grid.voxelSize) / 2, 500, (chunkComponent.grid.chunkSize * chunkComponent.grid.voxelSize) / 2);
            aabb.Extents = new float3(chunkComponent.grid.chunkSize * chunkComponent.grid.voxelSize, 1000, chunkComponent.grid.chunkSize * chunkComponent.grid.voxelSize);

            ecb.SetComponent(closestEntity, new RenderBounds { Value = aabb });

            meshInstance.mesh = mesh;

            ecb.SetSharedComponent(closestEntity, meshInstance);
            ecb.AddComponent<VoxelTerrainChunkInitializedTag>(closestEntity);

            AsyncGPUReadback.Request(verts, (AsyncGPUReadbackRequest request) => {
                var tagEcb = ecbSystem.CreateCommandBuffer();

                if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
                else if (!request.done) { Debug.Log("Not done yet..."); return; }

                mesh.SetVertices(request.GetData<Vector3>());
                verts.Dispose();
                voxels.Dispose();

                tagEcb.AddComponent<VoxelTerrainChunkRenderTag>(closestEntity);
                tagEcb.AddComponent<RenderInstanced>(closestEntity);

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
            });

            Texture2D climateTex = GetClimateColors();
            Texture2D colorTex = GetBiomeColors();

            meshInstance.material.SetTexture("climate_texture", climateTex);
            meshInstance.material.SetTexture("color_texture", colorTex);

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
