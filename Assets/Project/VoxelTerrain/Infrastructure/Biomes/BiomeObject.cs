using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

namespace VoxelTerrain
{
    [CreateAssetMenu]
    public class BiomeObject : ScriptableObject
    {
        [Range(0, 1)] public float maxTemperature = 1;
        [Range(0, 1)] public float minTemperature = 0;
        [Range(0, 1)] public float maxMoisture = 1;
        [Range(0, 1)] public float minMoisture = 0;

        [Range(0, 1)] public float heightNormalIntensity;
        [Range(-512, 512)] public int minTerrainHeight = 0;
        [Range(-512, 512)] public int maxTerrainHeight = 64;
        public Vector2 generatorNoiseScale;
        public Vector2 heightNormalNoiseScale;
        public float persistance;
        public float lancunarity;
        [Range(1, 20)] public int octaves = 1;

        public Biome biomeProperties {
            get {
                return this;
            }
        }

        public float idealTemperature
        {
            get
            {
                return biomeProperties.idealTemperature;
            }
        }

        public float idealMoisture
        {
            get
            {
                return biomeProperties.idealMoisture;
            }
        }

        public float Idealness(float temperature, float moisture)
        {
            return biomeProperties.Idealness(temperature, moisture);
        }
    }

    public struct Biome {
        public float maxTemperature;
        public float minTemperature;
        public float maxMoisture;
        public float minMoisture;

        public float heightNormalIntensity;
        public int minTerrainHeight;
        public int maxTerrainHeight;
        public float2 generatorNoiseScale;
        public float2 heightNormalNoiseScale;
        public float persistance;
        public float lancunarity;
        [Range(1, 20)] public int octaves;

        public static implicit operator Biome(BiomeObject other) {
            if (other == null) {
                return null;
            }

            return new Biome
            {
                maxTemperature = other.maxTemperature,
                minTemperature = other.minTemperature,
                maxMoisture = other.maxMoisture,
                minMoisture = other.minMoisture,
                heightNormalIntensity = other.heightNormalIntensity,
                minTerrainHeight = other.minTerrainHeight,
                maxTerrainHeight = other.maxTerrainHeight,
                generatorNoiseScale = new float2(other.generatorNoiseScale.x, other.generatorNoiseScale.y),
                heightNormalNoiseScale = new float2(other.heightNormalNoiseScale.x, other.heightNormalNoiseScale.y),
                persistance = other.persistance,
                lancunarity = other.lancunarity,
                octaves = other.octaves
                
            };
        }

        public float idealTemperature
        {
            get
            {
                return minTemperature + ((maxTemperature - minTemperature)/2);
            }
        }

        public float idealMoisture
        {
            get
            {
                return minMoisture + ((maxMoisture - minMoisture)/2);
            }
        }

        public float Idealness(float temperature, float moisture)
        {
            float tempDistance = math.abs(temperature - idealTemperature);
            float moisDistance = math.abs(moisture - idealMoisture);

            float tempIdealness = math.clamp(math.remap(maxTemperature - minTemperature, 0f, 0f, 1f, tempDistance), 0, 1);
            float moisIdealness = math.clamp(math.remap(maxMoisture - minMoisture, 0f, 0f, 1f, moisDistance), 0, 1);

            return tempIdealness * moisIdealness;
        }

        
    }
}
