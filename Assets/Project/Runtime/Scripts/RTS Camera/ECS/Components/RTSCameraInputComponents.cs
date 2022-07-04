using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;


namespace RTSCamera.Components
{
    [Serializable]
    public struct RTSInputPointer : IComponentData
    {
        public float2 value;
    }

    [Serializable]
    public struct RTSInputDelta : IComponentData {
        public float2 value;
    }

    [Serializable]
    public struct RTSInputTranslation : IComponentData
    {
        public float2 value;
    }

    [Serializable]
    public struct RTSInputZoom : IComponentData
    {
        public float value;
    }

    [Serializable]
    public struct RTSInputSelectPrimary : IComponentData
    {
        public bool value;
    }

    [Serializable]
    public struct RTSInputSelectSecondary : IComponentData
    {
        public bool value;
    }

    [Serializable]
    public struct RTSInputSelectTerciary : IComponentData {
        public bool value;
    }
}
