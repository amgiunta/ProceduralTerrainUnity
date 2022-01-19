using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace VoxelTerrain
{
    namespace ECS.Components
    {
        public interface IGroundScatter : IComponentData {
            public float scatterDensity { get; set; }
            public float minTemperature { get; set; }
            public float maxTemperature { get; set; }
            public float minMoisture { get; set; }
            public float maxMoisture { get; set; }
            public float maxHeight { get; set; }
            public float minHeight { get; set; }
            public float3 offset { get; set; }
            public float uniformScale { get; set; }
            public float jitterFactor { get; set; }
            public float3 localPosition { get; set; }
            public ChunkComponent chunk { get; set; }
        }

        public struct RockGroundScatter : IGroundScatter
        {
            public float scatterDensity { get; set; }
            public float minTemperature { get; set; }
            public float maxTemperature { get; set; }
            public float minMoisture { get; set; }
            public float maxMoisture { get; set; }
            public float maxHeight { get; set; }
            public float minHeight { get; set; }
            public float3 offset { get; set; }
            public float uniformScale { get; set; }
            public float jitterFactor { get; set; }
            public float3 localPosition { get; set; }
            public ChunkComponent chunk { get; set; }
        }

        public struct TreeGroundScatter : IGroundScatter
        {
            public float scatterDensity { get; set; }
            public float minTemperature { get; set; }
            public float maxTemperature { get; set; }
            public float minMoisture { get; set; }
            public float maxMoisture { get; set; }
            public float maxHeight { get; set; }
            public float minHeight { get; set; }
            public float3 offset { get; set; }
            public float uniformScale { get; set; }
            public float jitterFactor { get; set; }
            public float3 localPosition { get; set; }
            public ChunkComponent chunk { get; set; }
        }
    }
    
}
