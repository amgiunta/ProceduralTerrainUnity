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

        public static float[] CreateNoiseMap(int chunkWidth, float persistance, float lancunarity, int stride = 1, float2 offset = default, float2 scale = default, int octaves = 1, int seed = 0)
        {
            float[] noise = new float[chunkWidth * chunkWidth];

            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    noise[y * chunkWidth + x] = Noise(x, y, persistance, lancunarity, stride, offset, scale, octaves, seed);
                }
            }

            return noise;
        }

        public static NativeArray<float> CreateNativeNoiseMap(int chunkWidth, float persistance, float lancunarity, int stride = 1, float2 offset = default, float2 scale = default, int octaves = 1, int seed = 0)
        {
            NativeArray<float> noise = new NativeArray<float>(
                CreateNoiseMap(
                    chunkWidth,
                    persistance,
                    lancunarity,
                    stride,
                    offset,
                    scale,
                    octaves,
                    seed
            ), Allocator.Persistent);

            return noise;
        }

        public static float2[] CreateClimateMap(int chunkWidth, TerrainSettings settings, float2 gridPosition)
        {
            float2[] noise = new float2[chunkWidth * chunkWidth];

            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    float temperature = Noise(x, y, settings.temperaturePersistance, settings.temperatureLancunarity, 1, settings.temperatureOffset + gridPosition, settings.temperatureScale, settings.temperatureOctaves, settings.seed);
                    temperature = math.remap(0, 1, settings.minTemperature, settings.maxTemperature, temperature);

                    float moisture = Noise(x, y, settings.moisturePersistance, settings.moistureLancunarity, 1, settings.moistureOffset + gridPosition, settings.moistureScale, settings.moistureOctaves, settings.seed);
                    moisture = math.remap(0, 1, settings.minMoisture, settings.maxMoisture, moisture);

                    noise[y * chunkWidth + x] = new float2(temperature, moisture);
                }
            }

            return noise;
        }

        public static NativeArray<float2> CreateNativeClimateMap(int chunkWidth, TerrainSettings settings, float2 gridPosition) {
            NativeArray<float2> noise = new NativeArray<float2>(
                CreateClimateMap(
                    chunkWidth,
                    settings,
                    gridPosition
            ), Allocator.Persistent);

            return noise;
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
