using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

namespace VoxelTerrain
{
    public static class TerrainNoise
    {
        public static float Noise(float x, float y, float persistance, float lancunarity, int stride = 1, float2 offset = default, float2 scale = default, int octaves = 1, int seed = 0)
        {
            if (seed == 0) { seed = 1; }
            Unity.Mathematics.Random random = new Unity.Mathematics.Random((uint) seed);

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
                float2 octOffset = (offset + random.NextFloat2());
                float2 sample = new float2(
                    (x * stride + octOffset.x) / scale.x * frequency,
                    (y * stride + octOffset.y) / scale.y * frequency
                );

                float perlinValue = noise.snoise(sample);
                noiseHeight += perlinValue * amplitude;

                amplitude *= persistance;
                frequency *= lancunarity;
            }

            return math.remap(-1f, 1f, 0f, 1f, noiseHeight);
        }

        public static float Noise(float x, float y, float persistance, float lancunarity, int stride = 1, float2 offset = default, float2 scale = default, int octaves = 1)
        {
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
                float2 octOffset = offset;
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

        public static void CreateNoiseMap(int chunkWidth, TerrainSettings terrainSettings, Biome biome, ref float[] noiseMap, int startIndex = 0, int stride = 1, float2 offset = default)
        {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    noiseMap[(y * chunkWidth + x) + startIndex] = Noise(x, y, biome.persistance, biome.lancunarity, stride, offset, biome.generatorNoiseScale, biome.octaves, terrainSettings.seed);
                }
            }
        }
        public static void CreateNoiseMap(int chunkWidth, Biome biome, int seed, ref float[] noiseMap, int startIndex = 0, int stride = 1, float2 offset = default)
        {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    noiseMap[(y * chunkWidth + x) + startIndex] = Noise(x, y, biome.persistance, biome.lancunarity, stride, offset, biome.generatorNoiseScale, biome.octaves, seed);
                }
            }
        }

        public static void CreateNativeNoiseMap(int chunkWidth, TerrainSettings terrainSettings, Biome biome, ref NativeArray<float> noiseMap, int startIndex, int stride = 1, float2 offset = default)
        {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    noiseMap[(y * chunkWidth + x) + startIndex] = Noise(x, y, biome.persistance, biome.lancunarity, stride, offset, biome.generatorNoiseScale, biome.octaves, terrainSettings.seed);
                }
            }
        }

        public static float2 Climate(float x, float y, ClimateSettings climateSettings, float2 gridPosition, int seed) {
            float temperature = Noise(x, y, climateSettings.temperaturePersistance, climateSettings.temperatureLancunarity, 1, climateSettings.temperatureOffset + gridPosition, climateSettings.temperatureScale, climateSettings.temperatureOctaves, seed);
            temperature = math.remap(0, 1, climateSettings.minTemperature, climateSettings.maxTemperature, temperature);

            float moisture = Noise(x, y, climateSettings.moisturePersistance, climateSettings.moistureLancunarity, 1, climateSettings.moistureOffset + gridPosition, climateSettings.moistureScale, climateSettings.moistureOctaves, seed);
            moisture = math.remap(0, 1, climateSettings.minMoisture, climateSettings.maxMoisture, moisture);

            return new float2(temperature, moisture);
        }

        public static void CreateClimateMap(int chunkWidth, ref float2[] climateMap, int startIndex, TerrainSettings settings, float2 gridPosition = default)
        {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    /*
                    float temperature = Noise(x, y, settings.temperaturePersistance, settings.temperatureLancunarity, 1, settings.temperatureOffset + gridPosition, settings.temperatureScale, settings.temperatureOctaves, settings.seed);
                    temperature = math.remap(0, 1, settings.minTemperature, settings.maxTemperature, temperature);

                    float moisture = Noise(x, y, settings.moisturePersistance, settings.moistureLancunarity, 1, settings.moistureOffset + gridPosition, settings.moistureScale, settings.moistureOctaves, settings.seed);
                    moisture = math.remap(0, 1, settings.minMoisture, settings.maxMoisture, moisture);
                    */

                    climateMap[(y * chunkWidth + x) + startIndex] = Climate(x, y, settings, gridPosition, settings.seed);
                }
            }
        }

        public static void CreateNativeClimateMap(int chunkWidth, ref NativeArray<float2> climateMap, int startIndex, TerrainSettings settings, float2 gridPosition = default) {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    float temperature = Noise(x, y, settings.temperaturePersistance, settings.temperatureLancunarity, 1, settings.temperatureOffset + gridPosition, settings.temperatureScale, settings.temperatureOctaves, settings.seed);
                    temperature = math.remap(0, 1, settings.minTemperature, settings.maxTemperature, temperature);

                    float moisture = Noise(x, y, settings.moisturePersistance, settings.moistureLancunarity, 1, settings.moistureOffset + gridPosition, settings.moistureScale, settings.moistureOctaves, settings.seed);
                    moisture = math.remap(0, 1, settings.minMoisture, settings.maxMoisture, moisture);

                    climateMap[(y * chunkWidth + x) + startIndex] = new float2(temperature, moisture);
                }
            }
        }

        public static Texture2D CreateNoiseTexture(float[] noiseMap, int chunkWidth)
        {
            Color[] values = new Color[noiseMap.Length];

            for (int i = 0; i < noiseMap.Length; i++)
            {
                values[i] = Color.Lerp(Color.black, Color.white, noiseMap[i]);
            }

            Texture2D tex = new Texture2D(chunkWidth, chunkWidth);
            tex.SetPixels(values);
            tex.Apply();

            return tex;
        }

        public static Texture2D CreateClimateTexture(float2[] climateMap, int chunkWidth) {
            Color[] values = new Color[climateMap.Length];

            for (int i = 0; i < climateMap.Length; i++)
            {
                //Debug.Log("Climate: " + climateMap[i]);
                Color temperatureColor = Color.Lerp(Color.white, Color.red, climateMap[i].x);
                Color moistureColor = Color.Lerp(Color.white, Color.blue, climateMap[i].y);
                values[i] = temperatureColor * moistureColor;
            }

            Texture2D tex = new Texture2D(chunkWidth, chunkWidth);
            tex.SetPixels(values);
            tex.Apply();

            return tex;
        }
    }
}
