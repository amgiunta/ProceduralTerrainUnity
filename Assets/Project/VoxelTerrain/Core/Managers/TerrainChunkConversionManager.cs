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

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            terrainGeneratorShader = _terrainGeneratorShader;
            using (BlobAssetStore assetStore = new BlobAssetStore())
            {
                Entity prefabEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(chunkGameObjectPrefab, GameObjectConversionSettings.FromWorld(dstManager.World, assetStore));
                dstManager.AddComponent<VoxelTerrainChunkNewTag>(prefabEntity);
                renderMesh = new RenderMesh { material = chunkGameObjectPrefab.GetComponent<MeshRenderer>().sharedMaterial };
                //dstManager.AddSharedComponentData(prefabEntity, renderMesh);
                chunkPrefab = prefabEntity;
            }
        }
    }
}