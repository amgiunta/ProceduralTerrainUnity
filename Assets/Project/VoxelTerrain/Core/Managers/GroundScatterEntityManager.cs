using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Unity.Collections;
using Unity.Entities;

using VoxelTerrain.ECS.Components;

namespace VoxelTerrain
{
    public class GroundScatterEntityManager : MonoBehaviour, IConvertGameObjectToEntity
    {
        public static Dictionary<GroundScatter, Entity> convertedPrefabs;
        public List<GameObject> prefabsToConvert;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem) {
            convertedPrefabs = new Dictionary<GroundScatter, Entity>();

            int count = 0;
            foreach (GameObject prefabGo in prefabsToConvert)
            {
                using (BlobAssetStore assetStore = new BlobAssetStore())
                {
                    Entity prefabEntity = GameObjectConversionUtility.ConvertGameObjectHierarchy(prefabGo, GameObjectConversionSettings.FromWorld(dstManager.World, assetStore));
                    GroundScatter scatter = dstManager.GetComponentData<GroundScatter>(prefabEntity);
                    convertedPrefabs.Add(scatter, prefabEntity);
                    count++;
                }
            }
        }
    }
}
