using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering.HybridV2;

namespace RTSCamera.Components
{
    [Serializable]
    public struct RTSCameraAttributesComponent : IComponentData
    {
        public float movementSpeed;
        public float rotationSpeed;
        public float dampening;
        public float sensetivity;
    }
}
