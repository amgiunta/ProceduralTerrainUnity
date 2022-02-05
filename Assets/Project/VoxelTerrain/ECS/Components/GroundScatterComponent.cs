using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace VoxelTerrain.ECS.Components
{
    [GenerateAuthoringComponent]
    public struct GroundScatter: IComponentData {
        public float scatterDensity;
        [Range(0, 1)] public float heartiness;
        [Range(0, 1)] public float minTemperature;
        [Range(0, 1)] public float maxTemperature;
        [Range(0, 1)] public float minMoisture;
        [Range(0, 1)] public float maxMoisture;
        public float maxHeight;
        public float minHeight;
        public float3 offset;
        [Range(0, 1)] public float jitterFactor;
    }
}
