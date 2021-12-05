using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

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
        [Range(1, 20)] public int octaves;

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

        public static float Noise(float x, float y, float persistance, float lancunarity, int stride = 1, float2 offset = default, float2 scale = default, int octaves = 1, int seed = 0)
        {

            System.Random rand = new System.Random(seed);

            if (scale.x == 0)
            {
                scale.x = 0.00001f;
            }
            else if (scale.y == 0)
            {
                scale.y = 0.00001f;
            }

            float amplitude = 1;
            float frequency = 1;
            float noiseHeight = 0;

            for (int i = 0; i < octaves; i++)
            {
                float2 octOffset = (offset + new float2(rand.Next(-100000, 100000), rand.Next(-100000, 100000)));
                float2 sample = new float2(
                    (x * stride + octOffset.x) / scale.x * frequency,
                    (y * stride + octOffset.y) / scale.y * frequency
                );

                float perlinValue = noise.cnoise(sample);
                noiseHeight += perlinValue * amplitude;

                amplitude *= persistance;
                frequency *= lancunarity;
            }

            return math.remap(-1, 1, 0, 1, noiseHeight);
        }

        public static float[] CreateNoiseMap(int chunkWidth, float persistance, float lancunarity, int stride = 1, float2 offset = default, float2 scale = default, int octaves = 1, int seed = 0) {
            float[] noise = new float[chunkWidth * chunkWidth];
            
            for (int y = 0; y < chunkWidth; y++) {
                for (int x = 0; x < chunkWidth; x++) {
                    noise[y * chunkWidth + x] = Noise(x, y, persistance, lancunarity, stride, offset, scale, octaves, seed);

                }
            }

            return noise;
        }

        public static Texture2D CreateNoiseTexture(float[] noiseMap, int chunkWidth) {
            Color[] values = new Color[noiseMap.Length];

            for (int i = 0; i < noiseMap.Length; i++) {
                values[i] = Color.Lerp(Color.black, Color.white, noiseMap[i]);
            }

            Texture2D tex = new Texture2D(chunkWidth, chunkWidth);
            tex.SetPixels(values);
            tex.Apply();

            return tex;
        }
    }
}
