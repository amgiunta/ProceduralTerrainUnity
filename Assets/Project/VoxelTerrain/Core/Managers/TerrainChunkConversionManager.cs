using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;

using VoxelTerrain.ECS.Components;

namespace VoxelTerrain
{
    public class TerrainChunkConversionManager : MonoBehaviour, IConvertGameObjectToEntity
    {
        public static Entity chunkPrefab;
        public static RenderMesh renderMesh;
        public static ComputeShader terrainGeneratorShader;

        public GameObject chunkGameObjectPrefab;
        public ComputeShader _terrainGeneratorShader;
        public Material terrainShader;

        /*
        public void Awake()
        {
            terrainGeneratorShader = _terrainGeneratorShader;
            Instantiate<GameObject>(chunkGameObjectPrefab);
        }
        */

        
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            terrainGeneratorShader = _terrainGeneratorShader;
            using (BlobAssetStore assetStore = new BlobAssetStore())
            {
                Entity prefabEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(chunkGameObjectPrefab, GameObjectConversionSettings.FromWorld(dstManager.World, assetStore));
                dstManager.AddComponent<Static>(prefabEntity);

                /*
                dstManager.AddComponent<VoxelTerrainChunkNewTag>(prefabEntity);
                dstManager.AddComponent<DisableRendering>(prefabEntity);
                dstManager.AddBuffer<VoxelTerrainChunkGroundScatterBufferElement>(prefabEntity);

                dstManager.AddBuffer<VoxelTerrainChunkVoxelBufferElement>(prefabEntity);
                dstManager.AddBuffer<VoxelTerrainChunkTopEdgeBufferElement>(prefabEntity);
                dstManager.AddBuffer<VoxelTerrainChunkRightEdgeBufferElement>(prefabEntity);

                dstManager.AddBuffer<VoxelTerrainChunkClimateBufferElement>(prefabEntity);
                dstManager.AddBuffer<VoxelTerrainChunkClimateColorBufferElement>(prefabEntity);
                dstManager.AddBuffer<VoxelTerrainChunkTerrainColorBufferElement>(prefabEntity);
                */

                Material shader = new Material(terrainShader);

                renderMesh = new RenderMesh {
                    material = shader,
                    layer = 6,
                    castShadows = UnityEngine.Rendering.ShadowCastingMode.On,
                    receiveShadows = true,
                    layerMask = 1
                };
                //chunkPrefab = prefabEntity;
            }
        }
    }
}
