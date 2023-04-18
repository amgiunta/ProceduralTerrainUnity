using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using Unity.Profiling;

//using UnityEngine.Profiling;

namespace VoxelTerrain
{
    public static class TerrainNoise
    {
        
        public static float Noise(float x, float y, float persistance, float lancunarity, float2 offset = default, float2 scale = default, int octaves = 1)
        {

            if (scale.x == 0)
            {
                scale.x = 0.00001f;
            }
            if (scale.y == 0)
            {
                scale.y = 0.00001f;
            }
            
            return math.remap(-1f, 1f, 0f, 1f, Noise(x, y, persistance, lancunarity, out _, out _, offset, scale, octaves));
        }

        public static float Noise(float x, float y, float persistance, float lancunarity, out float dx, out float dy, float2 offset = default, float2 scale = default, int octaves = 1, float rotationDegrees = 0)
        {
            //ProfilerMarker parentMarker = new ProfilerMarker("Terrain Noise");
            //ProfilerMarker loopMarker = new ProfilerMarker("Terrain Noise Loop");
            //parentMarker.Begin();
            if (scale.x == 0)
            {
                scale.x = 0.00001f;
            }
            if (scale.y == 0)
            {
                scale.y = 0.00001f;
            }

            float amplitude = 1;
            float frequency = 1;
            float noiseHeight = 0;
            dx = 0;
            dy = 0;

            for (int i = 0; i < octaves; i++)
            {
                //loopMarker.Begin();
                float2 octOffset = (offset);

                float2 sample = (new float2(x, y) + octOffset) / (scale) * frequency;

                float3 perlinValue = noise.snoise(sample);
                noiseHeight += perlinValue.x * amplitude;
                dx += perlinValue.y * amplitude;
                dy += perlinValue.z * amplitude;

                amplitude *= persistance;
                frequency *= lancunarity;
                //loopMarker.End();
            }
            float result = math.remap(-1f, 1f, 0f, 1f, noiseHeight);
            //parentMarker.End();
            return result;
        }

        public static float GetHeightAtPoint(float x, float y, float2 climate, IEnumerable biomes, float2 chunkPosition, int chunkWidth, float voxelSize) {
            float totalHeight = 0;
            float totalWeight = 0;
            foreach (Biome biome in biomes)
            {
                float noise = biome.GetNoiseAtPoint(x, y, chunkPosition * chunkWidth * voxelSize);
                float height = math.remap(0, 1, biome.minTerrainHeight, biome.maxTerrainHeight, noise);
                float weight = biome.Idealness(climate.x, climate.y);

                totalHeight += height * weight;
                totalWeight += weight;
            }

            return totalHeight / totalWeight;
        }

        public static float GetHeightAtPoint(float x, float y, float2 climate, NativeArray<Biome> biomes, float2 chunkPosition, int chunkWidth)
        {
            float totalHeight = 0;
            float totalWeight = 0;

            for (int i = 0; i < biomes.Length; i++)
            {
                Biome biome = biomes[i];

                float noise = biome.GetNoiseAtPoint(x, y, chunkPosition * chunkWidth);
                float height = math.remap(0, 1, biome.minTerrainHeight, biome.maxTerrainHeight, noise);
                float weight = biome.Idealness(climate.x, climate.y);

                totalHeight += height * weight;
                totalWeight += weight;
            }

            return totalHeight / totalWeight;
        }

        public static float GetHeightAndNormalAtPoint(float x, float y, float2 climate, in NativeArray<Biome> biomes, float2 chunkPosition, int chunkWidth, float voxelSize, int seed, out float3 normal)
        {   
            float totalHeight = 0;
            float totalDx = 0;
            float totalDy = 0;
            float totalWeight = 0;

            for (int i = 0; i < biomes.Length; i++)
            {
                Biome biome = biomes[i];

                //float3 noise = biome.GetNoiseAndDerivativeAtPoint(x, y, chunkPosition, chunkWidth, voxelSize);
                float3 noise = Noise(
                    x, y, biome.persistance, biome.lancunarity,
                    chunkPosition * chunkWidth, biome.generatorNoiseScale/voxelSize, biome.octaves
                );
                float height = math.remap(0, 1, biome.minTerrainHeight, biome.maxTerrainHeight, noise.x);
                float dx = noise.y;
                float dy = noise.z;
                float weight = biome.Idealness(climate.x, climate.y);

                totalHeight += height * weight;
                totalDx += dx * weight;
                totalDy += dy * weight;
                totalWeight += weight;
            }

            normal = math.normalize(new float3(-(totalDx / totalWeight), 1, -(totalDy / totalWeight)));

            return totalHeight / totalWeight;
        }

        public static float GetHeightAtPoint(float x, float y, float2 climate, NativeArray<Biome>.ReadOnly biomes, float2 chunkPosition, int chunkWidth)
        {
            float totalHeight = 0;
            float totalWeight = 0;

            for (int i = 0; i < biomes.Length; i++) {
                Biome biome = biomes[i];

                float noise = biome.GetNoiseAtPoint(x, y, chunkPosition * chunkWidth);
                float height = math.remap(0, 1, biome.minTerrainHeight, biome.maxTerrainHeight, noise);
                float weight = biome.Idealness(climate.x, climate.y);

                totalHeight += height * weight;
                totalWeight += weight;
            }

            return totalHeight / totalWeight;
        }

        public static Color GetColorAtPoint(NativeArray<Biome> biomes, float2 climate) {
            Color totalColor = Color.black;
            float totalWeight = 0;

            foreach (Biome biome in biomes)
            {
                float weight = biome.Idealness(climate.x, climate.y);
                Color color = Color.Lerp(Color.black, biome.color, weight);

                totalColor += color;
                totalWeight += weight;
            }

            return totalColor / totalWeight;
        }

        public static void GetDataAtPoint( 
            in NativeArray<Biome> biomes, float x, float y, float2 chunkPosition, int chunkWidth, float voxelSize,
            ClimateSettings climateSettings,            
            out float3 normal, out float height, out float2 climate, out Color color, int2 offset = default
        ) {
            //ProfilerMarker dataMarker = new ProfilerMarker("Getting Voxel Data");
            //dataMarker.Begin();
            climate = Climate(x, y, climateSettings, chunkPosition + offset, chunkWidth, voxelSize);

            float totalHeight = 0;
            float totalDx = 0;
            float totalDy = 0;
            float totalWeight = 0;
            Color totalColor = Color.black;

            for (int i = 0; i < biomes.Length; i++)
            {
                Biome biome = biomes[i];
                float ndx; float ndy;

                float3 noise = new float3(
                    Noise(x, y, biome.persistance, biome.lancunarity, out ndx, out ndy, (chunkPosition + (float2) offset) * chunkWidth, biome.generatorNoiseScale / voxelSize, biome.octaves, biome.noiseRotation),
                    ndx, ndy
                );
                height = math.remap(0, 1, biome.minTerrainHeight, biome.maxTerrainHeight, noise.x);
                float dx = noise.y;
                float dy = noise.z;
                float weight = biome.Idealness(climate.x, climate.y);
                color = Color.Lerp(Color.black, biome.color, weight);

                totalColor += color;

                totalHeight += height * weight;
                totalDx += dx * weight;
                totalDy += dy * weight;
                totalWeight += weight;
            }

            normal = math.normalize(new float3(-(totalDx / totalWeight), 1, -(totalDy / totalWeight)));
            color = totalColor / totalWeight;
            height = totalHeight / totalWeight;
            //dataMarker.End();
        }

        public static void CreateNoiseMap(int chunkWidth, TerrainSettings terrainSettings, Biome biome, ref float[] noiseMap, int startIndex = 0, float2 offset = default)
        {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    noiseMap[(y * chunkWidth + x) + startIndex] = Noise(x, y, biome.persistance, biome.lancunarity, out _ , out _ , offset, biome.generatorNoiseScale, biome.octaves, biome.noiseRotation);
                }
            }
        }
        public static void CreateNoiseMap(int chunkWidth, Biome biome, int seed, ref float[] noiseMap, int startIndex = 0, float2 offset = default)
        {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    noiseMap[(y * chunkWidth + x) + startIndex] = Noise(x, y, biome.persistance, biome.lancunarity, offset, biome.generatorNoiseScale, biome.octaves);
                }
            }
        }

        public static void CreateNoiseMap(float2 chunkPosition, int chunkWidth, float voxelSize, ref float[] noiseMap, int startIndex, TerrainSettings settings, IEnumerable biomes)
        {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    float totalNoise = 0;
                    float totalWeight = 0;
                    float2 climate = Climate(x, y, settings, chunkPosition, chunkWidth, voxelSize);
                    foreach (Biome biome in biomes)
                    {
                        float noise = biome.GetNoiseAtPoint(x, y, chunkPosition * chunkWidth);
                        float weight = biome.Idealness(climate.x, climate.y);

                        totalNoise += noise * weight;
                        totalWeight += weight;
                    }

                    noiseMap[startIndex + (y * chunkWidth + x)] = totalNoise / totalWeight;
                }
            }
        }

        public static void CreateHeightMap(float2 chunkPosition, int chunkWidth, float voxelSize, ref float[] heightMap, int startIndex, TerrainSettings settings, Biome[] biomes)
        {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    float2 climate = Climate(x, y, settings, chunkPosition, chunkWidth, voxelSize);

                    float height = GetHeightAtPoint(x, y, climate, biomes, chunkPosition, chunkWidth, voxelSize);

                    heightMap[startIndex + (y * chunkWidth + x)] = height;
                }
            }
        }

        public static float2 Climate(float x, float y, ClimateSettings climateSettings, float2 gridPosition, int chunkWidth, float voxelSize) {
            //ProfilerMarker climate = new ProfilerMarker("Get Climate");
            //climate.Begin();
            float temperature = Noise(x, y, climateSettings.temperaturePersistance, climateSettings.temperatureLancunarity, gridPosition * chunkWidth, climateSettings.temperatureScale / voxelSize, climateSettings.temperatureOctaves);
            temperature = math.clamp(math.unlerp(climateSettings.minTemperature, climateSettings.maxTemperature, temperature), 0, 1);

            float moisture = Noise(x, y, climateSettings.moisturePersistance, climateSettings.moistureLancunarity, gridPosition * chunkWidth, climateSettings.moistureScale / voxelSize, climateSettings.moistureOctaves);
            moisture = math.clamp(math.unlerp(climateSettings.minMoisture, climateSettings.maxMoisture, moisture), 0, 1);
            //climate.End();
            return new float2(temperature, moisture);
        }

        public static float Idealness(float min, float max, float value, float heartiness) {
            float fullRange = max - min;
            float maxDistance = fullRange / 2;
            float ideal = max - maxDistance;

            float heartyRange = fullRange * heartiness;
            float heartyDistance = heartyRange / 2;

            float distance = math.abs(value - ideal);

            if (distance < heartyDistance) { return 1; }

            float idealness = math.clamp(math.unlerp(maxDistance, heartyDistance, distance), 0, 1);

            return idealness;
        }

        public static float ClimateIdealness(float2 minClimate, float2 maxClimate, float2 climate, float heartiness = 0) {
            if (minClimate.Equals(maxClimate)) { return 0; }

            return Idealness(minClimate.x, maxClimate.x, climate.x, heartiness) * Idealness(minClimate.y, maxClimate.y, climate.y, heartiness);
        }

        public static void CreateClimateMap(int chunkWidth, float voxelSize, ref float2[] climateMap, int startIndex, TerrainSettings settings, float2 gridPosition = default)
        {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {

                    climateMap[(y * chunkWidth + x) + startIndex] = Climate(x, y, settings, gridPosition, chunkWidth, voxelSize);
                }
            }
        }

        public static void CreateColorMap(float2 chunkPosition, int chunkWidth, float voxelSize, ref Color[] colorMap, int startIndex, TerrainSettings settings, Biome[] biomes) {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    Color totalColor = Color.black;
                    float totalWeight = 0;
                    float2 climate = Climate(x, y, settings, chunkPosition, chunkWidth, voxelSize);
                    
                    foreach (Biome biome in biomes)
                    {
                        float weight = biome.Idealness(climate.x, climate.y);
                        Color color = Color.Lerp(Color.black, biome.color, weight);
                        
                        totalColor += color;
                        totalWeight += weight;
                    }

                    colorMap[(y * chunkWidth + x) + startIndex] = totalColor / totalWeight;
                }
            }
            
        }

        public static Texture2D CreateNoiseTexture(float[] noiseMap, int chunkWidth, int2 textureSize)
        {
            Color[] values = new Color[noiseMap.Length];

            for (int y = 0; y < textureSize.y; y++)
            {
                for (int x = 0; x < textureSize.x; x++)
                {
                    int yOffset = chunkWidth * chunkWidth * textureSize.x * y;
                    int xOffset = chunkWidth * x;

                    for (int i = 0; i < chunkWidth * chunkWidth; i++)
                    {
                        int mapOffet = yOffset + xOffset + ((i / chunkWidth) * (chunkWidth * textureSize.x));
                        int chunkOffset = yOffset + (x * chunkWidth * chunkWidth);

                        values[mapOffet + (i%chunkWidth)] = Color.Lerp(Color.black, Color.white, noiseMap[chunkOffset + i]);
                    }
                }
            }

            Texture2D tex = new Texture2D(chunkWidth * textureSize.x, chunkWidth * textureSize.y);
            tex.SetPixels(values);
            tex.Apply();

            return tex;
        }

        public static Texture2D CreateNoiseTexture(float[] noiseMap, int chunkWidth) {
            return CreateNoiseTexture(noiseMap, chunkWidth, new int2(1, 1));
        }

        public static Texture2D CreateHeightTexture(float[] heightMap, int chunkWidth, int2 textureSize)
        {
            Color[] values = new Color[heightMap.Length];

            float lowest = float.MaxValue;
            float highest = float.MinValue;

            foreach (float height in heightMap) {
                if (height < lowest)
                    lowest = height;
                else if (height > highest)
                    highest = height;
            }

            for (int y = 0; y < textureSize.y; y++)
            {
                for (int x = 0; x < textureSize.x; x++)
                {
                    int yOffset = chunkWidth * chunkWidth * textureSize.x * y;
                    int xOffset = chunkWidth * x;

                    for (int i = 0; i < chunkWidth * chunkWidth; i++)
                    {
                        int mapOffet = yOffset + xOffset + ((i / chunkWidth) * (chunkWidth * textureSize.x));
                        int chunkOffset = yOffset + (x * chunkWidth * chunkWidth);

                        float wightedNoise = math.unlerp(lowest, highest, heightMap[chunkOffset + i]);

                        values[mapOffet + (i % chunkWidth)] = Color.Lerp(Color.black, Color.white, wightedNoise);
                    }
                }
            }

            Texture2D tex = new Texture2D(chunkWidth * textureSize.x, chunkWidth * textureSize.y);
            tex.SetPixels(values);
            tex.Apply();

            return tex;
        }

        public static Texture2D CreateClimateTexture(float2[] climateMap, int chunkWidth, int2 textureSize) {
            Color[] values = new Color[climateMap.Length];

            for (int y = 0; y < textureSize.y; y++) {
                for (int x = 0; x < textureSize.x; x++) {
                    int yOffset = chunkWidth * chunkWidth * textureSize.x * y;
                    int xOffset = chunkWidth * x;

                    for (int i = 0; i < chunkWidth * chunkWidth; i++) {
                        int mapOffet = yOffset + xOffset + ((i / chunkWidth) * (chunkWidth * textureSize.x));
                        int chunkOffset = yOffset + (x * chunkWidth * chunkWidth);

                        float2 climate = climateMap[chunkOffset + i];
                        values[mapOffet + (i % chunkWidth)] = new Color(climate.x, 0, climate.y, 1);
                    }
                }
            }

            Texture2D tex = new Texture2D(chunkWidth * textureSize.x, chunkWidth * textureSize.y);
            tex.SetPixels(values);
            tex.Apply();

            return tex;
        }

        public static Texture2D CreateClimateTexture(float2[] climateMap, int chunkWidth) {
            return CreateClimateTexture(climateMap, chunkWidth, new int2(1, 1));
        }

        public static Texture2D CreateColorTexture(Color[] colorMap, int chunkWidth, int2 textureSize) {
            Color[] values = new Color[colorMap.Length];

            for (int y = 0; y < textureSize.y; y++)
            {
                for (int x = 0; x < textureSize.x; x++)
                {
                    int yOffset = chunkWidth * chunkWidth * textureSize.x * y;
                    int xOffset = chunkWidth * x;

                    for (int i = 0; i < chunkWidth * chunkWidth; i++)
                    {
                        int mapOffet = yOffset + xOffset + ((i / chunkWidth) * (chunkWidth * textureSize.x));
                        int chunkOffset = yOffset + (x * chunkWidth * chunkWidth);

                        values[mapOffet + (i % chunkWidth)] = colorMap[chunkOffset + i];
                    }
                }
            }

            Texture2D tex = new Texture2D(chunkWidth * textureSize.x, chunkWidth * textureSize.y);
            tex.SetPixels(values);
            tex.Apply();

            return tex;
        }

        public static Texture2D CreateColoredNoiseTexture(Color[] colorMap, float[] noiseMap, int chunkWidth, int2 textureSize) {
            Color[] values = new Color[colorMap.Length];

            for (int y = 0; y < textureSize.y; y++)
            {
                for (int x = 0; x < textureSize.x; x++)
                {
                    int yOffset = chunkWidth * chunkWidth * textureSize.x * y;
                    int xOffset = chunkWidth * x;

                    for (int i = 0; i < chunkWidth * chunkWidth; i++)
                    {
                        int mapOffet = yOffset + xOffset + ((i / chunkWidth) * (chunkWidth * textureSize.x));
                        int chunkOffset = yOffset + (x * chunkWidth * chunkWidth);

                        values[mapOffet + (i%chunkWidth)] = Color.Lerp(Color.black, colorMap[chunkOffset + i], noiseMap[chunkOffset + i]);
                    }
                }
            }

            Texture2D tex = new Texture2D(chunkWidth * textureSize.x, chunkWidth * textureSize.y);
            tex.SetPixels(values);
            tex.Apply();

            return tex;
        }

        public static Texture2D CreateColoredHeightTexture(Color[] colorMap, float[] heightMap, int chunkWidth, int2 textureSize)
        {
            Color[] values = new Color[heightMap.Length];

            float lowest = float.MaxValue;
            float highest = float.MinValue;

            foreach (float height in heightMap)
            {
                if (height < lowest)
                    lowest = height;
                else if (height > highest)
                    highest = height;
            }

            for (int y = 0; y < textureSize.y; y++)
            {
                for (int x = 0; x < textureSize.x; x++)
                {
                    int yOffset = chunkWidth * chunkWidth * textureSize.x * y;
                    int xOffset = chunkWidth * x;

                    for (int i = 0; i < chunkWidth * chunkWidth; i++)
                    {
                        int mapOffet = yOffset + xOffset + ((i / chunkWidth) * (chunkWidth * textureSize.x));
                        int chunkOffset = yOffset + (x * chunkWidth * chunkWidth);

                        float wightedNoise = math.unlerp(lowest, highest, heightMap[chunkOffset + i]);

                        values[mapOffet + (i % chunkWidth)] = Color.Lerp(Color.black, colorMap[chunkOffset + i], wightedNoise);
                    }
                }
            }

            Texture2D tex = new Texture2D(chunkWidth * textureSize.x, chunkWidth * textureSize.y);
            tex.SetPixels(values);
            tex.Apply();

            return tex;
        }
    }
}
