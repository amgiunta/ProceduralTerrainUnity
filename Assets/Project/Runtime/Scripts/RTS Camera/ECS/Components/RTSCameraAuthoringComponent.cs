using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;

using Unity.Mathematics;
using UnityEngine;

namespace RTSCamera.Components {
    [DisallowMultipleComponent]
    public class RTSCameraAuthoringComponent : MonoBehaviour, IConvertGameObjectToEntity
    {
        // Add fields to your component here. Remember that:
        //
        // * The purpose of this class is to store data for authoring purposes - it is not for use while the game is
        //   running.
        // 
        // * Traditional Unity serialization rules apply: fields must be public or marked with [SerializeField], and
        //   must be one of the supported types.
        //
        // For example,
        //    public float scale;

        public float movementSpeed;
        public float rotationSpeed;
        [Range(0f, 1f)] public float dampening = 1;
        [Range(0f, 10f)] public float sensetivity = 1;
        [Range(0f, 10f)] public float zoomSensetivity = 1;

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            // Call methods on 'dstManager' to create runtime components on 'entity' here. Remember that:
            //
            // * You can add more than one component to the entity. It's also OK to not add any at all.
            //
            // * If you want to create more than one entity from the data in this class, use the 'conversionSystem'
            //   to do it, instead of adding entities through 'dstManager' directly.
            //
            // For example,
            //   dstManager.AddComponentData(entity, new Unity.Transforms.Scale { Value = scale });

            dstManager.AddComponentData(entity, new RTSCameraAttributesComponent { 
                movementSpeed = this.movementSpeed,
                rotationSpeed = this.rotationSpeed,
                dampening = this.dampening,
                sensetivity = this.sensetivity,
                zoomSensetivity = this.zoomSensetivity
            });

            dstManager.AddComponentData(entity, new RTSInputPointer());
            dstManager.AddComponentData(entity, new RTSInputDelta());
            dstManager.AddComponentData(entity, new RTSInputTranslation());
            dstManager.AddComponentData(entity, new RTSInputZoom());
            dstManager.AddComponentData(entity, new RTSInputSelectPrimary());
            dstManager.AddComponentData(entity, new RTSInputSelectSecondary());
            dstManager.AddComponentData(entity, new RTSInputSelectTerciary());
        }
    }
}
