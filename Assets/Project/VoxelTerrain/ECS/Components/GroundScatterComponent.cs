using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace VoxelTerrain.ECS.Components
{
    public interface IGroundScatter : IComponentData {
        public float maxRenderDistance { get; set; }
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

    [GenerateAuthoringComponent]
    public struct GroundScatter: IComponentData {
        public float maxRenderDistance;
        public float scatterDensity;
        public float heartiness;
        public float minTemperature;
        public float maxTemperature;
        public float minMoisture;
        public float maxMoisture;
        public float maxHeight;
        public float minHeight;
        public float3 offset;
        public float uniformScale;
        public float jitterFactor;
        public float3 localPosition;
        public ChunkComponent chunk;
    }
}
