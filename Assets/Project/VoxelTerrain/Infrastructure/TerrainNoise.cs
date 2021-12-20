using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

namespace VoxelTerrain
{
    public class MapPreview {
        public enum MapLense { Noise, Height, Climate, Color, ColoredNoise, ColoredHeight};

        public Dictionary<MapLense, Texture2D> mapLenses;

        private int2 size;
        private int chunkWidth;
        private TerrainSettings settings;
        private Biome[] biomes;

        public MapPreview(int2 size, int chunkWidth, TerrainSettings settings, Biome[] biomes) {
            mapLenses = new Dictionary<MapLense, Texture2D>();

            this.size = size;
            this.chunkWidth = chunkWidth;
            this.settings = settings;
            this.biomes = biomes;
        }
        
        public void Generate() {
            int flatMapSize = chunkWidth * chunkWidth;

            float[] noiseMap = new float[(size.x * chunkWidth) * (size.y * chunkWidth)];
            float[] heightMap = new float[(size.x * chunkWidth) * (size.y * chunkWidth)];
            float2[] climateMap = new float2[(size.x * chunkWidth) * (size.y * chunkWidth)];
            Color[] colorMap = new Color[(size.x * chunkWidth) * (size.y * chunkWidth)];

            for (int y = 0; y < size.y; y++) {
                for (int x = 0; x < size.x; x++) {
                    int2 gridPosition = new int2(x, y);
                    foreach (int i in System.Enum.GetValues(typeof(MapLense))) {
                        switch ((MapLense) i) {
                            case MapLense.Climate:
                                TerrainNoise.CreateClimateMap(chunkWidth, ref climateMap, (y * (size.x * flatMapSize)) + x * flatMapSize, settings, gridPosition);
                                break;
                            case MapLense.Color:
                                TerrainNoise.CreateColorMap(gridPosition, chunkWidth, ref colorMap, (y * (size.x * flatMapSize)) + x * flatMapSize, settings, biomes);
                                break;
                            case MapLense.Noise:
                                TerrainNoise.CreateNoiseMap(gridPosition, chunkWidth, ref noiseMap, (y * (size.x * flatMapSize)) + x * flatMapSize, settings, biomes);
                                break;
                            case MapLense.Height:
                                TerrainNoise.CreateHeightMap(gridPosition, chunkWidth, ref heightMap, (y * (size.x * flatMapSize)) + x * flatMapSize, settings, biomes);
                                break;
                        }
                    }
                }
            }

            SetLenseTexture(MapLense.Climate, TerrainNoise.CreateClimateTexture(climateMap, chunkWidth, size));
            SetLenseTexture(MapLense.Color, TerrainNoise.CreateColorTexture(colorMap, chunkWidth, size));
            SetLenseTexture(MapLense.Noise, TerrainNoise.CreateNoiseTexture(noiseMap, chunkWidth, size));
            SetLenseTexture(MapLense.Height, TerrainNoise.CreateHeightTexture(heightMap, chunkWidth, size));
            SetLenseTexture(MapLense.ColoredNoise, TerrainNoise.CreateColoredNoiseTexture(colorMap, noiseMap, chunkWidth, size));
            SetLenseTexture(MapLense.ColoredHeight, TerrainNoise.CreateColoredHeightTexture(colorMap, heightMap, chunkWidth, size));
        }

        public Texture2D GetLenseTexture(MapLense lense) {
            if (!mapLenses.ContainsKey(lense)) {
                mapLenses.Add(lense, new Texture2D(size.x * chunkWidth, size.x * chunkWidth));
            }

            return mapLenses[lense];
        }

        public void SetLenseTexture(MapLense lense, Texture2D texture) {

            if (!mapLenses.ContainsKey(lense))
            {
                mapLenses.Add(lense, texture);
            }
            else {
                mapLenses[lense] = texture;
            }
        }
    }

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
            if (scale.y == 0)
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

        [System.Obsolete("Not Necessary")]
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
        
        public static float GetHeightAtPoint(float x, float y, float2 climate, IEnumerable biomes, int stride, float2 chunkPosition, int chunkWidth, int seed) {
            float totalHeight = 0;
            float totalWeight = 0;
            foreach (Biome biome in biomes)
            {
                float noise = biome.GetNoiseAtPoint(x, y, stride, chunkPosition * chunkWidth, seed);
                float height = math.remap(0, 1, biome.minTerrainHeight, biome.maxTerrainHeight, noise);
                float weight = biome.Idealness(climate.x, climate.y);

                totalHeight += height * weight;
                totalWeight += weight;
            }

            return totalHeight / totalWeight;
        }

        public static float GetHeightAtPoint(float x, float y, float2 climate, NativeArray<Biome> biomes, int stride, float2 chunkPosition, int chunkWidth, int seed)
        {
            float totalHeight = 0;
            float totalWeight = 0;
            foreach (Biome biome in biomes)
            {
                float noise = biome.GetNoiseAtPoint(x, y, stride, chunkPosition * chunkWidth, seed);
                float height = math.remap(0, 1, biome.minTerrainHeight, biome.maxTerrainHeight, noise);
                float weight = biome.Idealness(climate.x, climate.y);

                totalHeight += height * weight;
                totalWeight += weight;
            }

            return totalHeight / totalWeight;
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

        public static void CreateNoiseMap(float2 chunkPosition, int chunkWidth, ref float[] noiseMap, int startIndex, TerrainSettings settings, IEnumerable biomes)
        {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    float totalNoise = 0;
                    float totalWeight = 0;
                    float2 climate = Climate(x, y, settings, chunkPosition, chunkWidth, settings.seed);
                    foreach (Biome biome in biomes)
                    {
                        float noise = biome.GetNoiseAtPoint(x, y, 1, chunkPosition * chunkWidth, settings.seed);
                        float weight = biome.Idealness(climate.x, climate.y);

                        totalNoise += noise * weight;
                        totalWeight += weight;
                    }

                    noiseMap[startIndex + (y * chunkWidth + x)] = totalNoise / totalWeight;
                }
            }
        }

        public static void CreateHeightMap(float2 chunkPosition, int chunkWidth, ref float[] heightMap, int startIndex, TerrainSettings settings, Biome[] biomes)
        {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    float totalHeight = 0;
                    float totalWeight = 0;
                    float2 climate = Climate(x, y, settings, chunkPosition, chunkWidth, settings.seed);

                    float height = GetHeightAtPoint(x, y, climate, biomes, 1, chunkPosition, chunkWidth, settings.seed);

                    heightMap[startIndex + (y * chunkWidth + x)] = height;
                }
            }
        }

        public static float2 Climate(float x, float y, ClimateSettings climateSettings, float2 gridPosition, int chunkWidth, int seed) {
            float temperature = Noise(x, y, climateSettings.temperaturePersistance, climateSettings.temperatureLancunarity, 1, (climateSettings.temperatureOffset + gridPosition) * chunkWidth, climateSettings.temperatureScale, climateSettings.temperatureOctaves, seed);
            temperature = math.remap(0, 1, climateSettings.minTemperature, climateSettings.maxTemperature, temperature);

            float moisture = Noise(x, y, climateSettings.moisturePersistance, climateSettings.moistureLancunarity, 1, (climateSettings.moistureOffset + gridPosition) * chunkWidth, climateSettings.moistureScale, climateSettings.moistureOctaves, seed);
            moisture = math.remap(0, 1, climateSettings.minMoisture, climateSettings.maxMoisture, moisture);

            return new float2(temperature, moisture);
        }

        public static void CreateClimateMap(int chunkWidth, ref float2[] climateMap, int startIndex, TerrainSettings settings, float2 gridPosition = default)
        {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {

                    climateMap[(y * chunkWidth + x) + startIndex] = Climate(x, y, settings, gridPosition, chunkWidth, settings.seed);
                }
            }
        }

        public static void CreateColorMap(float2 chunkPosition, int chunkWidth, ref Color[] colorMap, int startIndex, TerrainSettings settings, Biome[] biomes) {
            for (int y = 0; y < chunkWidth; y++)
            {
                for (int x = 0; x < chunkWidth; x++)
                {
                    Color totalColor = Color.black;
                    float totalWeight = 0;
                    float2 climate = Climate(x, y, settings, chunkPosition, chunkWidth, settings.seed);
                    
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

                        Color temperatureColor = Color.Lerp(Color.black, Color.red, climate.x);
                        Color moistureColor = Color.Lerp(Color.black, Color.blue, climate.y);

                        values[mapOffet + (i % chunkWidth)] = (temperatureColor + moistureColor) / (climate.x + climate.y);
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
