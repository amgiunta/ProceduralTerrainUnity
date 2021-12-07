using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace VoxelTerrain
{
    [CreateAssetMenu]
    public class TerrainSettings : ScriptableObject
    {
        public int seed;

        [Range(0, 1)] public float minTemperature = 0;
        [Range(0, 1)] public float maxTemperature = 1;
        public float temperatureLancunarity = 1;
        public float temperaturePersistance = 1;
        [Range(1, 20)] public int temperatureOctaves = 1;
        public float2 temperatureScale;
        public float2 temperatureOffset;

        [Range(0, 1)] public float minMoisture = 0;
        [Range(0, 1)] public float maxMoisture = 1;
        public float moistureLancunarity = 1;
        public float moisturePersistance = 1;
        [Range(1, 20)] public int moistureOctaves = 1;
        public float2 moistureScale;
        public float2 moistureOffset;
    }
}
