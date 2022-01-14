using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VoxelTerrain.ECS.Components;

namespace VoxelTerrain
{
    public class GroundScatterAuthor : MonoBehaviour
    {
        [Range(0.001f, 100)] public float scatterDensity;
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

        public IGroundScatter GetComponentData() {
            return default(IGroundScatter);
        }
    }

    
}
