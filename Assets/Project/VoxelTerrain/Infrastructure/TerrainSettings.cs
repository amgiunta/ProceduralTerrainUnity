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

    public struct ClimateSettings {
        public float minTemperature;
        public float maxTemperature;
        public float temperatureLancunarity;
        public float temperaturePersistance;
        public int temperatureOctaves;
        public float2 temperatureScale;
        public float2 temperatureOffset;

        public float minMoisture;
        public float maxMoisture;
        public float moistureLancunarity;
        public float moisturePersistance;
        public int moistureOctaves;
        public float2 moistureScale;
        public float2 moistureOffset;

        public static implicit operator ClimateSettings(TerrainSettings other) {
            if (other == null) { return null; }

            return new ClimateSettings {
                minTemperature = other.minTemperature,
                maxTemperature = other.maxTemperature,
                temperatureLancunarity = other.temperatureLancunarity,
                temperaturePersistance = other.temperaturePersistance,
                temperatureOctaves = other.temperatureOctaves,
                temperatureScale = other.temperatureScale,
                temperatureOffset = other.temperatureOffset,

                minMoisture = other.minMoisture,
                maxMoisture = other.maxMoisture,
                moistureLancunarity = other.moistureLancunarity,
                moisturePersistance = other.moisturePersistance,
                moistureOctaves = other.moistureOctaves,
                moistureScale = other.moistureScale,
                moistureOffset = other.moistureOffset
            };
        }
    }
}
