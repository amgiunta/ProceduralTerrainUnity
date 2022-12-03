using UnityEngine;
using Unity.Mathematics;

namespace VoxelTerrain
{
    [CreateAssetMenu]
    public class BiomeObject : ScriptableObject
    {
        public Color color;

        [Range(0, 1)] public float maxTemperature = 1;
        [Range(0, 1)] public float minTemperature = 0;
        [Range(0, 1)] public float maxMoisture = 1;
        [Range(0, 1)] public float minMoisture = 0;

        [Range(0, 1)] public float heightNormalIntensity;
        [Range(-512, 512)] public int minTerrainHeight = 0;
        [Range(-512, 512)] public int maxTerrainHeight = 64;
        public Vector2 generatorNoiseScale;
        public Vector2 heightNormalNoiseScale;
        [Range(0, 1)]public float noiseRotation;
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
        public Color color;

        public float maxTemperature;
        public float minTemperature;
        public float maxMoisture;
        public float minMoisture;

        public float heightNormalIntensity;
        public int minTerrainHeight;
        public int maxTerrainHeight;
        public float2 generatorNoiseScale;
        public float2 heightNormalNoiseScale;
        public float noiseRotation;
        public float persistance;
        public float lancunarity;
        [Range(1, 20)] public int octaves;

        public static implicit operator Biome(BiomeObject other) {
            if (other == null) {
                return null;
            }

            return new Biome
            {
                color = other.color,
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
                octaves = other.octaves,
                noiseRotation = other.noiseRotation
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

        public float GetNoiseAtPoint(float x, float y, float2 offset, int seed) {
            return TerrainNoise.Noise(
                x, y, 
                persistance,
                lancunarity,
                offset,
                generatorNoiseScale,
                octaves,
                seed
            );
        }

        public float3 GetNoiseAndDerivativeAtPoint(float x, float y, float2 offset, float chunkSize, float voxelSize, int seed)
        {
            float dx;
            float dy;

            float noise = TerrainNoise.Noise(
                x, y,
                persistance,
                lancunarity,
                out dx, out dy,
                offset * chunkSize,
                generatorNoiseScale / voxelSize,
                octaves,
                seed,
                noiseRotation
            );

            return new float3(noise, dx, dy);
        }
    }
}
