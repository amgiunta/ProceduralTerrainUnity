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

                    dstManager.AddComponent<Scale>(prefabEntity);
                    dstManager.SetComponentData(prefabEntity, new Scale { Value = prefabGo.transform.localScale.x});

                    dstManager.AddComponent<VoxelTerrainGroundScatterNewTag>(prefabEntity);
                    //dstManager.AddComponent<DisableRendering>(prefabEntity);

                    if (!dstManager.HasComponent<RotationConstraints>(prefabEntity)) {
                        dstManager.AddComponent<RotationConstraints>(prefabEntity);
                        dstManager.SetComponentData(prefabEntity, new RotationConstraints { x = true, y = true, z = true });
                    }

                    if (dstManager.HasComponent<NonUniformScale>(prefabEntity)) {
                        dstManager.RemoveComponent<NonUniformScale>(prefabEntity);
                    }

                    convertedPrefabs.Add(scatter, prefabEntity);
                    count++;
                }
            }
        }
    }
}
