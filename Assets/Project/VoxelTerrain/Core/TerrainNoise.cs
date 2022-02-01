using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace VoxelTerrain
{
    public class MapPreview {
        public enum MapLense { Height, Climate, Color, ColoredHeight };

        public Dictionary<MapLense, Texture2D> mapLenses;
        public bool isRunning {
            get {
                return running;
            }
        }

        private int2 size;
        private int chunkWidth;
        private TerrainSettings settings;
        private Biome[] biomes;

        private bool running = false;
        private Dictionary<JobHandle, IJobParallelFor> runningJobs;

        private NativeArray<float> heightMap;
        private NativeArray<Color> colorMap;
        private int2 pixelSize {
            get {
                return new int2(size.x * chunkWidth, size.y * chunkWidth);
            }
        }

        public MapPreview(int2 size, int chunkWidth, TerrainSettings settings, Biome[] biomes) {
            mapLenses = new Dictionary<MapLense, Texture2D>();

            runningJobs = new Dictionary<JobHandle, IJobParallelFor>();

            this.size = size;
            this.chunkWidth = chunkWidth / 8;
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
                        switch ((MapLense)i) {
                            case MapLense.Climate:
                                TerrainNoise.CreateClimateMap(chunkWidth, ref climateMap, (y * (size.x * flatMapSize)) + x * flatMapSize, settings, gridPosition);
                                break;
                            case MapLense.Color:
                                TerrainNoise.CreateColorMap(gridPosition, chunkWidth, ref colorMap, (y * (size.x * flatMapSize)) + x * flatMapSize, settings, biomes);
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
            SetLenseTexture(MapLense.Height, TerrainNoise.CreateHeightTexture(heightMap, chunkWidth, size));
            SetLenseTexture(MapLense.ColoredHeight, TerrainNoise.CreateColoredHeightTexture(colorMap, heightMap, chunkWidth, size));
        }

        public void GenerateAsync() {
            if (running)
                return;

            CompleteRunningJobs();

            float lowest = float.MaxValue;
            float highest = float.MinValue;
            foreach (Biome biome in biomes) {
                if (biome.maxTerrainHeight > highest)
                    highest = biome.maxTerrainHeight;
                if (biome.minTerrainHeight < lowest)
                    lowest = biome.minTerrainHeight;
            }

            int bufferLength = (size.x * chunkWidth) * (size.y * chunkWidth);

            JobHandle heightMapHandle = default;
            JobHandle colorHandle = default;

            foreach (int i in System.Enum.GetValues(typeof(MapLense)))
            {
                switch ((MapLense)i)
                {
                    case MapLense.Height:
                        heightMap = new NativeArray<float>(new float[bufferLength], Allocator.TempJob);
                        HeightMapJob firstJob = new HeightMapJob
                        {
                            biomes = new NativeArray<Biome>(biomes, Allocator.Persistent),
                            heights = heightMap,
                            climateSettings = settings,
                            chunkPosition = new float2(0, 0),
                            textureSize = size,
                            chunkWidth = chunkWidth,
                            seed = settings.seed
                        };
                        heightMapHandle = firstJob.Schedule(bufferLength, chunkWidth);

                        if (!mapLenses.ContainsKey(MapLense.Height)) {
                            SetLenseTexture(MapLense.Height, new Texture2D(pixelSize.x, pixelSize.y));
                        }

                        HeightTextureJob secondJob = new HeightTextureJob
                        {
                            heights = heightMap,
                            colors = new NativeArray<Color>(new Color[bufferLength], Allocator.Persistent),
                            textureSize = size,
                            chunkWidth = chunkWidth,
                            lowest = lowest,
                            highest = highest
                        };
                        JobHandle secondHandle = secondJob.Schedule(bufferLength, chunkWidth, heightMapHandle);
                        runningJobs.Add(secondHandle, secondJob);
                        break;
                    case MapLense.Color:
                        colorMap = new NativeArray<Color>(new Color[bufferLength], Allocator.TempJob);
                        ColorTextureJob colorJob = new ColorTextureJob
                        {
                            biomes = new NativeArray<Biome>(biomes, Allocator.Persistent),
                            colors = colorMap,
                            climateSettings = settings,
                            chunkPosition = new float2(0, 0),
                            textureSize = size,
                            chunkWidth = chunkWidth,
                            seed = settings.seed
                        };
                        colorHandle = colorJob.Schedule(bufferLength, chunkWidth);
                        runningJobs.Add(colorHandle, colorJob);
                        break;
                    case MapLense.Climate:
                        ClimateTextureJob climateJob = new ClimateTextureJob {
                            colors = new NativeArray<Color>(bufferLength, Allocator.Persistent),
                            textureSize = size,
                            climateSettings = settings,
                            chunkPosition = new float2(0, 0),
                            chunkWidth = chunkWidth,
                            seed = settings.seed
                        };
                        JobHandle climateHandle = climateJob.Schedule(bufferLength, chunkWidth);
                        runningJobs.Add(climateHandle, climateJob);
                        break;
                    case MapLense.ColoredHeight:
                        ColoredHeightTextureJob coloredHeightJob = new ColoredHeightTextureJob
                        {
                            heightMap = heightMap,
                            colorMap = colorMap,
                            colors = new NativeArray<Color>(new Color[bufferLength], Allocator.Persistent),
                            lowest = lowest,
                            highest = highest
                        };

                        JobHandle dependancies = JobHandle.CombineDependencies(heightMapHandle, colorHandle);

                        JobHandle coloredHeightHandle = coloredHeightJob.Schedule(bufferLength, chunkWidth, dependancies);
                        runningJobs.Add(coloredHeightHandle, coloredHeightJob);
                        break;
                    default:
                        continue;
                }
            }

            running = true;
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

        public void CompleteRunningJobs() {
            while (runningJobs.Count > 0) {
                var process = runningJobs.First();
                if (process.Value is HeightTextureJob)
                {
                    HeightTextureJob job = (HeightTextureJob)process.Value;
                    process.Key.Complete();
                    Texture2D tex = new Texture2D(pixelSize.x, pixelSize.y);
                    tex.SetPixels(job.colors.ToArray());
                    tex.Apply();
                    SetLenseTexture(MapLense.Height, tex);

                    job.colors.Dispose();
                    runningJobs.Remove(process.Key);
                }
                else if (process.Value is ColorTextureJob)
                {
                    ColorTextureJob job = (ColorTextureJob)process.Value;
                    process.Key.Complete();
                    Texture2D tex = new Texture2D(pixelSize.x, pixelSize.y);
                    tex.SetPixels(job.colors.ToArray());
                    tex.Apply();
                    SetLenseTexture(MapLense.Color, tex);

                    job.biomes.Dispose();
                    runningJobs.Remove(process.Key);
                }
                else if (process.Value is ClimateTextureJob) {
                    ClimateTextureJob job = (ClimateTextureJob)process.Value;
                    process.Key.Complete();
                    Texture2D tex = new Texture2D(pixelSize.x, pixelSize.y);
                    tex.SetPixels(job.colors.ToArray());
                    tex.Apply();
                    SetLenseTexture(MapLense.Climate, tex);

                    job.colors.Dispose();
                    runningJobs.Remove(process.Key);
                }
                else if (process.Value is ColoredHeightTextureJob)
                {
                    ColoredHeightTextureJob job = (ColoredHeightTextureJob)process.Value;
                    process.Key.Complete();
                    Texture2D tex = new Texture2D(pixelSize.x, pixelSize.y);
                    tex.SetPixels(job.colors.ToArray());
                    tex.Apply();
                    SetLenseTexture(MapLense.ColoredHeight, tex);

                    job.heightMap.Dispose();
                    job.colorMap.Dispose();
                    job.colors.Dispose();
                    runningJobs.Remove(process.Key);
                }
            }

            running = false;
        }

        [BurstCompile(Debug = true)]
        public struct HeightMapJob : IJobParallelFor {
            [ReadOnly] public NativeArray<Biome> biomes;
            public NativeArray<float> heights;
            public ClimateSettings climateSettings;
            public float2 chunkPosition;
            public int2 textureSize;
            public int chunkWidth;
            public int seed;

            public void Execute(int id)
            {
                int y = id / (textureSize.x * chunkWidth);
                int x = id % (textureSize.x * chunkWidth);
                float2 climate = TerrainNoise.Climate(x * 8, y * 8, climateSettings, chunkPosition, chunkWidth, seed);

                heights[y * (textureSize.x * chunkWidth) + x] = TerrainNoise.GetHeightAtPoint(x, y, climate, biomes, 8, chunkPosition, chunkWidth, seed);
            }
        }

        [BurstCompile(Debug = true)]
        public struct HeightTextureJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<float> heights;
            public NativeArray<Color> colors;
            public int2 textureSize;
            public int chunkWidth;
            public float highest;
            public float lowest;

            public void Execute(int id)
            {
                float normalized = math.unlerp(lowest, highest, heights[id]);

                colors[id] = Color.Lerp(Color.black, Color.white, normalized);
            }
        }

        [BurstCompile(Debug = true)]
        public struct ColorTextureJob : IJobParallelFor {
            [ReadOnly] public NativeArray<Biome> biomes;
            public NativeArray<Color> colors;
            public ClimateSettings climateSettings;
            public float2 chunkPosition;
            public int2 textureSize;
            public int chunkWidth;
            public int seed;

            public void Execute(int id) {
                int y = id / (textureSize.x * chunkWidth);
                int x = id % (textureSize.x * chunkWidth);
                float2 climate = TerrainNoise.Climate(x * 8, y * 8, climateSettings, chunkPosition, chunkWidth, seed);

                Color totalColor = Color.black;
                float totalWeight = 0;

                foreach (Biome biome in biomes) {
                    float weight = biome.Idealness(climate.x, climate.y);
                    Color color = Color.Lerp(Color.black, biome.color, weight);

                    totalColor += color;
                    totalWeight += weight;
                }

                colors[y * (textureSize.x * chunkWidth) + x] = totalColor / totalWeight;
            }
        }

        [BurstCompile(Debug = true)]
        public struct ClimateTextureJob : IJobParallelFor
        {
            public NativeArray<Color> colors;
            public int2 textureSize;
            public ClimateSettings climateSettings;
            public float2 chunkPosition;
            public int chunkWidth;
            public int seed;

            public void Execute(int id) {
                int y = id / (textureSize.x * chunkWidth);
                int x = id % (textureSize.x * chunkWidth);
                float2 climate = TerrainNoise.Climate(x * 8, y * 8, climateSettings, chunkPosition, chunkWidth, seed);

                Color temperatureColor = Color.Lerp(Color.black, Color.red, climate.x);
                Color moistureColor = Color.Lerp(Color.black, Color.blue, climate.y);

                colors[y * (textureSize.x * chunkWidth) + x] = (temperatureColor + moistureColor) / (climate.x + climate.y);
            }
        }

        [BurstCompile(Debug = true)]
        public struct ColoredHeightTextureJob : IJobParallelFor {
            [ReadOnly] public NativeArray<float> heightMap;
            [ReadOnly] public NativeArray<Color> colorMap;
            public NativeArray<Color> colors;
            public float highest;
            public float lowest;

            public void Execute(int id) {
                colors[id] = Color.Lerp(Color.black, colorMap[id], math.unlerp(lowest, highest, heightMap[id]));
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

            for (int i = 0; i < biomes.Length; i++)
            {
                Biome biome = biomes[i];

                float noise = biome.GetNoiseAtPoint(x, y, stride, chunkPosition * chunkWidth, seed);
                float height = math.remap(0, 1, biome.minTerrainHeight, biome.maxTerrainHeight, noise);
                float weight = biome.Idealness(climate.x, climate.y);

                totalHeight += height * weight;
                totalWeight += weight;
            }

            return totalHeight / totalWeight;
        }

        public static float GetHeightAtPoint(float x, float y, float2 climate, NativeArray<Biome>.ReadOnly biomes, int stride, float2 chunkPosition, int chunkWidth, int seed)
        {
            float totalHeight = 0;
            float totalWeight = 0;

            for (int i = 0; i < biomes.Length; i++) {
                Biome biome = biomes[i];

                float noise = biome.GetNoiseAtPoint(x, y, stride, chunkPosition * chunkWidth, seed);
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
                    float2 climate = Climate(x, y, settings, chunkPosition, chunkWidth, settings.seed);

                    float height = GetHeightAtPoint(x, y, climate, biomes, 1, chunkPosition, chunkWidth, settings.seed);

                    heightMap[startIndex + (y * chunkWidth + x)] = height;
                }
            }
        }

        public static float2 Climate(float x, float y, ClimateSettings climateSettings, float2 gridPosition, int chunkWidth, int seed) {
            float temperature = Noise(x, y, climateSettings.temperaturePersistance, climateSettings.temperatureLancunarity, 1, (climateSettings.temperatureOffset + gridPosition) * chunkWidth, climateSettings.temperatureScale, climateSettings.temperatureOctaves, seed);
            temperature = math.clamp(math.remap(0, 1, climateSettings.minTemperature, climateSettings.maxTemperature, temperature), 0, 1);

            float moisture = Noise(x, y, climateSettings.moisturePersistance, climateSettings.moistureLancunarity, 1, (climateSettings.moistureOffset + gridPosition) * chunkWidth, climateSettings.moistureScale, climateSettings.moistureOctaves, seed);
            moisture = math.clamp(math.remap(0, 1, climateSettings.minMoisture, climateSettings.maxMoisture, moisture), 0, 1);

            return new float2(temperature, moisture);
        }

        public static float ClimateIdealness(float2 minClimate, float2 maxClimate, float2 climate, float heartiness = 0) {
            if (minClimate.Equals(maxClimate)) { return 0; }
            else if (climate.x > maxClimate.x || climate.y > maxClimate.y || climate.x < minClimate.x || climate.y < minClimate.y) { return 0; }

            float tempRange = maxClimate.x - minClimate.x;
            float moisRange = maxClimate.y - minClimate.y;

            float idealTempRange = tempRange * heartiness;
            float idealMoisRange = moisRange * heartiness;

            float idealTemperature = minClimate.x + (tempRange / 2);
            float idealMoisture = minClimate.y + (moisRange / 2);

            float idealTempStart = idealTemperature - (idealTempRange / 2);
            float idealTempEnd = idealTemperature + (idealTempRange / 2);
            float idealMoisStart = idealMoisture - (idealMoisRange / 2);
            float idealMoisEnd = idealMoisture + (idealMoisRange / 2);


            if (climate.x < idealTempEnd && climate.x > idealMoisStart && climate.y < idealMoisEnd && climate.y > idealMoisStart) { return 1; }

            float tempIdealness;
            float moisIdealness;

            if (climate.x < idealTempStart)
            {
                tempIdealness = math.unlerp(minClimate.x, idealTempStart, climate.x);
            }
            else if (climate.x > idealTempEnd)
            {
                tempIdealness = math.unlerp(maxClimate.x, idealTempEnd, climate.x);
            }
            else { tempIdealness = 1; }

            if (climate.y < idealMoisStart)
            {
                moisIdealness = math.unlerp(minClimate.y, idealMoisStart, climate.y);
            }
            else if (climate.y > idealMoisEnd)
            {
                moisIdealness = math.unlerp(maxClimate.y, idealMoisEnd, climate.y);
            }
            else { moisIdealness = 1; }

            return tempIdealness * moisIdealness;
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
