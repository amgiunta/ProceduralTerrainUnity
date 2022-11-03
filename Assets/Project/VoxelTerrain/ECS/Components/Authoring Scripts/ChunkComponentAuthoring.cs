using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;

using UnityEngine;

namespace VoxelTerrain
{
    namespace ECS
    {
        namespace Components
        {
            [DisallowMultipleComponent]
            public class ChunkComponentAuthoring : MonoBehaviour, IConvertGameObjectToEntity
            {
                public Grid grid;
                public int2 gridPosition;
                public uint lodLevel;
                //public bool prefab = false;

                public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
                {
                    using (BlobAssetStore assetStore = new BlobAssetStore())
                    {
                        /*
                        if (prefab)
                        {
                            //TerrainChunkConversionManager.chunkPrefab = GameObjectConversionUtility.ConvertGameObjectHierarchy(gameObject, GameObjectConversionSettings.FromWorld(dstManager.World, assetStore)); ;
                            TerrainChunkConversionManager.renderMesh = new RenderMesh { material = this.GetComponent<MeshRenderer>().sharedMaterial };
                            dstManager.AddComponent<Prefab>(entity);
                        }
                        */

                        dstManager.AddComponentData(entity, new ChunkComponent
                        {
                            grid = this.grid,
                            gridPosition = this.gridPosition,
                            lodLevel = this.lodLevel
                        });

                        dstManager.AddComponent<DisableRendering>(entity);
                        dstManager.AddComponent<VoxelTerrainChunkNewTag>(entity);

                        var scatterBuffer = dstManager.AddBuffer<VoxelTerrainChunkGroundScatterBufferElement>(entity);
                        scatterBuffer.ResizeUninitialized(10);

                        var voxelBuffer = dstManager.AddBuffer<VoxelTerrainChunkVoxelBufferElement>(entity);
                        voxelBuffer.ResizeUninitialized(grid.chunkSize * grid.chunkSize);
                        var voxelTopBuffer = dstManager.AddBuffer<VoxelTerrainChunkTopEdgeBufferElement>(entity);
                        voxelTopBuffer.ResizeUninitialized(grid.chunkSize + 1);
                        var voxelRightBuffer = dstManager.AddBuffer<VoxelTerrainChunkRightEdgeBufferElement>(entity);
                        voxelRightBuffer.ResizeUninitialized(grid.chunkSize);

                        var climateBuffer = dstManager.AddBuffer<VoxelTerrainChunkClimateBufferElement>(entity);
                        climateBuffer.ResizeUninitialized(grid.chunkSize * grid.chunkSize);
                        var climateColorBuffer = dstManager.AddBuffer<VoxelTerrainChunkClimateColorBufferElement>(entity);
                        climateColorBuffer.ResizeUninitialized(grid.chunkSize * grid.chunkSize);
                        var colorBuffer = dstManager.AddBuffer<VoxelTerrainChunkTerrainColorBufferElement>(entity);
                        colorBuffer.ResizeUninitialized(grid.chunkSize * grid.chunkSize);
                    }
                }
            }

            public struct ChunkComponent : IComponentData {
                public Grid grid;
                public int2 gridPosition;
                public uint lodLevel;
            }
        }
    }
}
