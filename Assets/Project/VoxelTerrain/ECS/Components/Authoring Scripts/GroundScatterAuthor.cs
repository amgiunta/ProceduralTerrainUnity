using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VoxelTerrain.ECS.Components;

namespace VoxelTerrain
{
    public class GroundScatterAuthor : MonoBehaviour
    {
        public float maxRenderDistance;
        [Range(0.001f, 5000)] public float scatterDensity;
        [Range(0, 1)] public float heartiness;
        [Range(0, 1)] public float minTemperature;
        [Range(0, 1)] public float maxTemperature;
        [Range(0, 1)] public float minMoisture;
        [Range(0, 1)] public float maxMoisture;
        public float minHeight = 1;
        public float maxHeight = 1000;
        public Vector3 offset;
        [Range(0, 1)]public float jitterFactor = 1;
        public float uniformScale;
        public Mesh mesh;
        public Material material;

        protected Vector3 lastScale;

        public virtual Type GetScatterType() {
            return typeof(IGroundScatter);
        }

        public GroundScatter GetComponentData() {
            GroundScatter groundScatter = new GroundScatter
            {
                maxRenderDistance = maxRenderDistance,
                scatterDensity = scatterDensity,
                heartiness = heartiness,
                minTemperature = minTemperature,
                maxTemperature = maxTemperature,
                minMoisture = minMoisture,
                maxMoisture = maxMoisture,
                minHeight = minHeight,
                maxHeight = maxHeight,
                offset = offset,
                jitterFactor = jitterFactor,
                uniformScale = uniformScale
            };
            return groundScatter;
        }
    }

    
}
