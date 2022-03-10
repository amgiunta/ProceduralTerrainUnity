using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Events;
using Unity.Mathematics;
using VoxelTerrain.ECS.Components;
using UnityEngine.Profiling;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Entities;

namespace VoxelTerrain
{
    public class TerrainManager : MonoBehaviour
    {
        public static TerrainManager instance;
        public TerrainSettings terrainSettings;

        private void Awake()
        {
            if (instance) {
                Destroy(instance.gameObject);
            }
            instance = this;
        }
    }
}
