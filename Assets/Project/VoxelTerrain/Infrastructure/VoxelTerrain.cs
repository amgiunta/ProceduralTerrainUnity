using UnityEngine.Jobs;
using Unity.Collections;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

namespace VoxelTerrain {
    [System.Serializable]
    public struct Grid {
        public float voxelSize;
        public int chunkSize;
    }

    [System.Serializable]
    public class ChunkLod {
        public int width;
        public Voxel[] voxels;
    }

    [System.Serializable]
    public struct Chunk {
        public int2 gridPosition;
        public Grid grid;
        public int chunkWidth;
        public Dictionary<int, ChunkLod> lods;
        public void SetChunkLod(int lodIndex, ChunkLod lod)
        {
            if (lods.ContainsKey(lodIndex))
            {
                lods[lodIndex] = lod;
                return;
            }

            lods.Add(lodIndex, lod);
        }

        public Voxel[] GetVoxels (int lod) {
            if (lod >= lods.Count) {
                Debug.LogWarning($"Chunk lod {lod} too low and does not exist. Using lowest available lod.");
                return lods[lods.Count - 1].voxels;
            }

            return lods[lod].voxels;
        }
    }

    [System.Serializable]
    public struct Voxel {
        public int x;
        public int y;
        public int height;
        public float3 normalNorth;
        public float3 normalSouth;
        public float3 normalEast;
        public float3 normalWest;
    }

    namespace Generators {
        public abstract class Generator {
            public Dictionary<JobHandle, IJobParallelFor> runningJobs;
            public int chunkWidth;

            public delegate void chunkProcessCallback(int2 gridPosition, Voxel[] chunkData, int lodIndex);

            public abstract void QueueChunk(Chunk chunk);
            public abstract void QueueChunks(List<Chunk> chunks);
            public abstract void ResolveJobs(chunkProcessCallback onProcessComplete);
        }

        public class PerlinTerrainGenerator : Generator {
            new public Dictionary<JobHandle, PerlinGeneratorJobV2> runningJobs;

            public Biome[] biomes;
            TerrainSettings settings;

            private System.Random rand;

            public PerlinTerrainGenerator(
                Biome[] biomes,
                TerrainSettings settings,
                int chunkWidth = 64
             ) {
                this.chunkWidth = chunkWidth;
                this.biomes = biomes;
                this.settings = settings;

                runningJobs = new Dictionary<JobHandle, PerlinGeneratorJobV2>();
                rand = new System.Random(settings.seed);
            }

            public override void QueueChunk(Chunk chunk)
            {
                int lodWidth = chunk.chunkWidth;

                JobHandle previous = default;
                
                for (int i = 0; lodWidth >= 8; i++)
                {

                    PerlinGeneratorJobV2 job = new PerlinGeneratorJobV2
                    {
                        biomes = new NativeArray<Biome>(biomes, Allocator.Persistent),
                        voxels = new NativeArray<Voxel>(new Voxel[lodWidth * lodWidth], Allocator.Persistent),
                        seed = settings.seed,
                        climateSettings = settings,
                        chunkWidth = chunk.chunkWidth,
                        lodWidth = lodWidth,
                        lodIndex = i,
                        chunkPosition = chunk.gridPosition
                    };

                    JobHandle handle = default;

                    handle = job.Schedule(lodWidth * lodWidth, lodWidth);

                    runningJobs.Add(handle, job);

                    previous = handle;
                    lodWidth /= 2;
                
                }
                
            }

            public override void QueueChunks(List<Chunk> chunks)
            {
                if (chunks == null) { Debug.LogWarning($"Chunks array is empty."); return; }

                foreach (Chunk chunk in chunks)
                {
                    QueueChunk(chunk);
                }
            }

            public virtual void ResolveJob(JobHandle key, chunkProcessCallback onChunkComplete, MonoBehaviour sceneObject) {
                sceneObject.StartCoroutine(CompleteJob(key, onChunkComplete));
            }

            public virtual void ResolveClosestJob(int2 point, chunkProcessCallback onChunkComplete, MonoBehaviour sceneObject) {
                if (runningJobs.Count == 0) { return; }
                Profiler.BeginSample("Resolve Closest Job");
                JobHandle closest = runningJobs.First().Key;
                int2 closestPosition = runningJobs[closest].chunkPosition;
                float closestDistance = math.distance(closestPosition, point);
                foreach (var process in runningJobs) {

                    int2 position = process.Value.chunkPosition;
                    float distance = math.distance(position, point);
                    int lod = process.Value.lodIndex;
                    if (distance < closestDistance) {
                        closestDistance = distance;
                        closestPosition = position;
                        closest = process.Key;
                    }
                }

                ResolveJob(closest, onChunkComplete, sceneObject);
                
                Profiler.EndSample();
            }

            public override void ResolveJobs(chunkProcessCallback onChunkComplete)
            {
                foreach (var process in runningJobs)
                {
                    process.Key.Complete();

                    onChunkComplete(
                        process.Value.chunkPosition,
                        process.Value.voxels.ToArray(),
                        process.Value.lodIndex
                    );

                    process.Value.voxels.Dispose();
                    process.Value.biomes.Dispose();
                }

                runningJobs.Clear();
            }

            public IEnumerator CompleteJob(JobHandle key, chunkProcessCallback onChunkComplete) {
                var job = runningJobs[key];

                yield return new WaitUntil(() => key.IsCompleted);
                runningJobs.Remove(key);

                key.Complete();

                try
                {
                    onChunkComplete(
                            job.chunkPosition,
                            job.voxels.ToArray(),
                            job.lodIndex
                        );

                    job.voxels.Dispose();
                    job.biomes.Dispose();
                }
                catch (Exception e) {
                    Debug.LogWarning($"Attempeted to use job data and dispose, but job was already disposed. Ignoring. {e.Message}");
                }
            }
        }

        
        [BurstCompile(Debug = true)]
        public struct PerlinGeneratorJobV2 : IJobParallelFor {
            [ReadOnly] public NativeArray<Biome> biomes;
            public NativeArray<Voxel> voxels;
            public ClimateSettings climateSettings;
            public int seed;
            public int chunkWidth;
            public int lodWidth;
            public int lodIndex;
            public int2 chunkPosition;

            public void Execute(int id) {
                int stride = Mathf.Max(1, chunkWidth / lodWidth);

                Voxel voxel = voxels[id];
                voxel.x = id % lodWidth;
                voxel.y = id / lodWidth;

                float2 climate = TerrainNoise.Climate(voxel.x * stride, voxel.y * stride, climateSettings, chunkPosition, chunkWidth, seed);
                voxel.height = (int) TerrainNoise.GetHeightAtPoint(voxel.x, voxel.y, climate, biomes, stride, chunkPosition, chunkWidth, seed);

                for (int n = -1; n <= 1; n++) {
                    for (int m = -1; m <= 1; m++) {
                        if (m == n) {
                            continue;
                        }

                        float2 position = new float2(voxel.x + m, voxel.y + n);
                        climate = TerrainNoise.Climate(position.x * stride, position.y * stride, climateSettings, chunkPosition, chunkWidth, seed);
                        float height = TerrainNoise.GetHeightAtPoint(position.x, position.y, climate, biomes, stride, chunkPosition, chunkWidth, seed);
                        float3 heading = (new float3(position.x, height, position.y)) - new float3(voxel.x, voxel.height, voxel.y);

                        if (m == 0)
                        {
                            if (n == -1)
                            {
                                voxel.normalSouth = math.normalizesafe(math.cross(new float3(1, 0, 0), heading));
                            }
                            else
                            {
                                voxel.normalNorth = math.normalizesafe(math.cross(new float3(-1, 0, 0), heading));
                            }
                        }
                        else if (n == 0) {
                            if (m == -1)
                            {
                                voxel.normalWest = math.normalizesafe(math.cross(new float3(0, 0, -1), heading));
                            }
                            else {
                                voxel.normalEast = math.normalizesafe(math.cross(new float3(0, 0, 1), heading));
                            }
                        }
                    }
                }

                voxels[id] = voxel;
            }
        }

        [BurstCompile(Debug = true)]
        public struct PerlinTerrainGeneratorJob : IJobParallelFor {
            public NativeArray<Voxel> chunkData;
            public NativeArray<Biome> biomes;
            public float2 temperatureNoiseScale;
            public float2 moistureNoiseScale;
            public float2 biomeNoiseScaleNormal;
            public int chunkWidth;
            public int lodWidth;
            public int lodIndex;
            public float2 randomOffset;

            public int2 chunkPosition;

            private float GetHeightAtPosition(Biome biome, float x, float y) {
                int stride = Mathf.Max(1, chunkWidth / lodWidth);

                float fHeight = TerrainNoise.Noise(x, y, biome.persistance, biome.lancunarity, stride, (chunkPosition + randomOffset) * chunkWidth , biome.generatorNoiseScale, biome.octaves);

                fHeight = math.remap(-1, 1, biome.minTerrainHeight, biome.maxTerrainHeight, fHeight); 
                return fHeight;
            }

            public void Execute(int voxelId) {

                Voxel voxel = chunkData[voxelId];
                voxel.x = voxelId % lodWidth;
                voxel.y = voxelId / lodWidth;
                int stride = Mathf.Max(1, chunkWidth / lodWidth);

                float temperature = GetTemperature(voxel.x, voxel.y, stride);
                float moisture = GetMoisture(voxel.x, voxel.y, stride);
                float northTemperature = GetTemperature(voxel.x, voxel.y + 1, stride);
                float northMoisture = GetMoisture(voxel.x, voxel.y + 1, stride);
                float southTemperature = GetTemperature(voxel.x, voxel.y - 1, stride);
                float southMoisture = GetMoisture(voxel.x, voxel.y - 1, stride);
                float eastTemperature = GetTemperature(voxel.x + 1, voxel.y, stride);
                float eastMoisture = GetMoisture(voxel.x + 1, voxel.y, stride);
                float westTemperature = GetTemperature(voxel.x - 1, voxel.y, stride);
                float westMoisture = GetMoisture(voxel.x - 1, voxel.y, stride);

                float tempHeight = 0;
                float northHeight = 0;
                float southHeight = 0;
                float eastHeight = 0;
                float westHeight = 0;
                float weight = 0;
                float northWeight = 0;
                float southWeight = 0;
                float eastWeight = 0;
                float westWeight = 0;

                if (biomes.Length == 0) {
                    Debug.Log("No biomes to execute on");
                    return;
                }

                foreach (Biome biome in biomes)
                {
                    tempHeight += (GetHeightAtPosition(biome, voxel.x, voxel.y) * biome.Idealness(temperature, moisture));
                    northHeight += (GetHeightAtPosition(biome, voxel.x, voxel.y + 1) * biome.Idealness(northTemperature, northMoisture));
                    southHeight += (GetHeightAtPosition(biome, voxel.x, voxel.y - 1) * biome.Idealness(southTemperature, southMoisture));
                    eastHeight += (GetHeightAtPosition(biome, voxel.x + 1, voxel.y) * biome.Idealness(eastTemperature, eastMoisture));
                    westHeight += (GetHeightAtPosition(biome, voxel.x - 1, voxel.y) * biome.Idealness(westTemperature, westMoisture));

                    weight += biome.Idealness(temperature, moisture);
                    northWeight += biome.Idealness(northTemperature, northMoisture);
                    southWeight += biome.Idealness(southTemperature, southMoisture);
                    eastWeight += biome.Idealness(eastTemperature, eastMoisture);
                    westWeight += biome.Idealness(westTemperature, westMoisture);
                }

                voxel.height = (int) (tempHeight / weight);
                northHeight = (northHeight / northWeight);
                southHeight = (southHeight / southWeight);
                eastHeight = (eastHeight / eastWeight);
                westHeight = (westHeight / westWeight);

                float3 northHeading = (new float3(voxel.x, northHeight, voxel.y + 1)) - (new float3(voxel.x, voxel.height, voxel.y));
                voxel.normalNorth = voxel.height == northHeight ? new float3(0, 1, 0) : math.normalizesafe(math.cross(new float3(-1, 0, 0), northHeading));

                float3 southHeading =  (new float3(voxel.x, southHeight, voxel.y - 1)) - (new float3(voxel.x, voxel.height, voxel.y));
                voxel.normalSouth = voxel.height == southHeight ? new float3(0, 1, 0) : math.normalizesafe(math.cross(new float3(1, 0, 0), southHeading));

                float3 eastHeading =  (new float3(voxel.x + 1, eastHeight, voxel.y)) - (new float3(voxel.x, voxel.height, voxel.y));
                voxel.normalEast =voxel.height == eastHeight ? new float3(0, 1, 0) : math.normalizesafe(math.cross(new float3(0, 0, 1), eastHeading));

                float3 westHeading =  (new float3(voxel.x - 1, westHeight, voxel.y)) - (new float3(voxel.x, voxel.height, voxel.y));
                voxel.normalWest =voxel.height == westHeight ? new float3(0, 1, 0) : math.normalizesafe(math.cross(new float3(0, 0, -1), westHeading));

                chunkData[voxelId] = voxel;
            }

            public float GetTemperature(float x, float y, int stride = 1)
            {
                float tempx = math.clamp((x * stride + randomOffset.x) * temperatureNoiseScale.x, -float.MaxValue, float.MaxValue);
                float tempy = math.clamp((y * stride + randomOffset.y) * temperatureNoiseScale.y, -float.MaxValue, float.MaxValue);
                //float normal = Mathf.PerlinNoise((x * stride + seed) * biomeNoiseScaleNormal.x, (y * stride + seed) * biomeNoiseScaleNormal.y);
                float temperature = Mathf.PerlinNoise(tempx * biomeNoiseScaleNormal.x, tempy * biomeNoiseScaleNormal.y);

                return Mathf.Clamp(temperature, 0f, 1f);
            }

            public float GetMoisture(float x, float y, int stride = 1)
            {

                float moisx = math.clamp((x * stride + randomOffset.x) * moistureNoiseScale.x, -float.MaxValue, float.MaxValue);
                float moisy = math.clamp((y * stride + randomOffset.y) * moistureNoiseScale.y, -float.MaxValue, float.MaxValue);
                float moisture = Mathf.PerlinNoise(moisx * biomeNoiseScaleNormal.x, moisy * biomeNoiseScaleNormal.y);

                return Mathf.Clamp(moisture, 0f, 1f);
            }

            
        }
    }
}
