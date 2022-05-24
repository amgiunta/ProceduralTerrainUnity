using UnityEngine.Jobs;
using Unity.Collections;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace VoxelTerrain {
    [System.Serializable]
    public struct Grid {
        public float voxelSize;
        public int chunkSize;
    }

    [System.Serializable]
    public struct Voxel {
        public float3 position;
        public float3 normal;
    }
}
