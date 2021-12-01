using UnityEngine.Jobs;
using Unity.Collections;
using System.Linq;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

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
            new public Dictionary<JobHandle, PerlinTerrainGeneratorJob> runningJobs;

            public Biome[] biomes;
            public int seed;
            public Vector2 temperatureNoiseScale;
            public Vector2 moistureNoiseScale;
            public Vector2 biomeNoiseScaleNormal;

            public PerlinTerrainGenerator(
                int seed,
                Biome[] biomes,
                Vector2 temperatureNoiseScale,
                Vector2 moistureNoiseScale,
                Vector2 biomeNoiseScaleNormal,
                int chunkWidth = 64
             ) {
                this.chunkWidth = chunkWidth;
                this.seed = seed;
                this.biomes = biomes;
                this.temperatureNoiseScale = temperatureNoiseScale;
                this.moistureNoiseScale = moistureNoiseScale;
                this.biomeNoiseScaleNormal = biomeNoiseScaleNormal;

                runningJobs = new Dictionary<JobHandle, PerlinTerrainGeneratorJob>();
            }
            public float GetTemperature(float x, float y, int stride = 1)
            {
                float tempx = math.clamp((x * stride + seed) * temperatureNoiseScale.x, -float.MaxValue, float.MaxValue);
                float tempy = math.clamp((y * stride + seed) * temperatureNoiseScale.y, -float.MaxValue, float.MaxValue);
                //float normal = Mathf.PerlinNoise((x * stride + seed) * biomeNoiseScaleNormal.x, (y * stride + seed) * biomeNoiseScaleNormal.y);
                float temperature = Mathf.PerlinNoise(tempx * biomeNoiseScaleNormal.x, tempy * biomeNoiseScaleNormal.y);

                return Mathf.Clamp(temperature, 0f, 1f);
            }

            public float GetMoisture(float x, float y, int stride = 1)
            {
                float moisx = math.clamp((x * stride + seed) * moistureNoiseScale.x, -float.MaxValue, float.MaxValue);
                float moisy = math.clamp((y * stride + seed) * moistureNoiseScale.y, -float.MaxValue, float.MaxValue);
                float moisture = Mathf.PerlinNoise(moisx * biomeNoiseScaleNormal.x, moisy * biomeNoiseScaleNormal.y);

                return Mathf.Clamp(moisture, 0f, 1f);
            }

            public override void QueueChunk(Chunk chunk)
            {
                int lodWidth = chunk.chunkWidth;

                for (int i = 0; lodWidth >= 8 ; i++)
                {
                    PerlinTerrainGeneratorJob job = new PerlinTerrainGeneratorJob
                    {
                        chunkData = new NativeArray<Voxel>(new Voxel[lodWidth * lodWidth], Allocator.Persistent),
                        biomes = new NativeArray<Biome>(biomes, Allocator.Persistent),
                        temperatureNoiseScale = new float2(temperatureNoiseScale.x, temperatureNoiseScale.y),
                        moistureNoiseScale = new float2(moistureNoiseScale.x, moistureNoiseScale.y),
                        biomeNoiseScaleNormal = biomeNoiseScaleNormal,
                        chunkWidth = chunkWidth,
                        lodWidth = lodWidth,
                        lodIndex = i,
                        chunkPosition = chunk.gridPosition,
                        seed = seed
                    };


                    JobHandle handle = job.Schedule(lodWidth * lodWidth, lodWidth * lodWidth);
                    runningJobs.Add(handle, job);

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

            public virtual void ResolveJob(JobHandle key, chunkProcessCallback onChunkComplete) {
                var job = runningJobs[key];

                key.Complete();
                onChunkComplete(
                        job.chunkPosition,
                        job.chunkData.ToArray(),
                        job.lodIndex
                    );
            }

            public virtual void ResolveJob(chunkProcessCallback onChunkComplete) {
                foreach (var process in runningJobs) {
                    if (process.Key.IsCompleted)
                    {
                        ResolveJob(process.Key, onChunkComplete);
                        process.Value.chunkData.Dispose();
                        runningJobs.Remove(process.Key);
                        return;
                    }
                }
            }

            public virtual void ResolveClosestJob(int2 point, chunkProcessCallback onChunkComplete) {
                if (runningJobs.Count == 0) { return; }

                JobHandle closest = runningJobs.First().Key;
                int2 closestPosition = runningJobs[closest].chunkPosition;
                float closestDistance = math.distance(closestPosition, point);
                foreach (var process in runningJobs) {
                    int2 position = process.Value.chunkPosition;
                    float distance = math.distance(position, point);
                    if (distance < closestDistance) {
                        closestDistance = distance;
                        closestPosition = position;
                        closest = process.Key;
                    }
                }

                ResolveJob(closest, onChunkComplete);
                runningJobs[closest].chunkData.Dispose();
                runningJobs[closest].biomes.Dispose();
                runningJobs.Remove(closest);
            }

            public override void ResolveJobs(chunkProcessCallback onChunkComplete)
            {
                foreach (var process in runningJobs)
                {
                    process.Key.Complete();

                    onChunkComplete(
                        process.Value.chunkPosition,
                        process.Value.chunkData.ToArray(),
                        process.Value.lodIndex
                    );

                    process.Value.chunkData.Dispose();
                    process.Value.biomes.Dispose();
                }

                runningJobs.Clear();
            }

            public virtual void DisposeJobs() {
                foreach (var process in runningJobs)
                {
                    process.Key.Complete();

                    process.Value.chunkData.Dispose();
                    process.Value.biomes.Dispose();
                }

                runningJobs.Clear();
            }
        }

        [BurstCompile]
        public struct PerlinTerrainGeneratorJob : IJobParallelFor {
            public NativeArray<Voxel> chunkData;
            public NativeArray<Biome> biomes;
            public float2 temperatureNoiseScale;
            public float2 moistureNoiseScale;
            public float2 biomeNoiseScaleNormal;
            public int chunkWidth;
            public int lodWidth;
            public int lodIndex;
            public int seed;

            public int2 chunkPosition;

            private float GetHeightAtPosition(Biome biome, float x, float y) {
                int stride = Mathf.Max(1, chunkWidth / lodWidth);

                float heightNormal = biome.Noise(x, y, biome.persistance, biome.lancunarity, stride, chunkPosition * chunkWidth, biome.heightNormalNoiseScale, biome.octaves, seed);
                heightNormal = math.remap(-1, 1, 0, biome.heightNormalIntensity, heightNormal);

                float fHeight = biome.Noise(x, y, biome.persistance, biome.lancunarity, stride, chunkPosition * chunkWidth, biome.generatorNoiseScale, biome.octaves, seed) * heightNormal;

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
                float tempx = math.clamp((x * stride + seed) * temperatureNoiseScale.x, -float.MaxValue, float.MaxValue);
                float tempy = math.clamp((y * stride + seed) * temperatureNoiseScale.y, -float.MaxValue, float.MaxValue);
                //float normal = Mathf.PerlinNoise((x * stride + seed) * biomeNoiseScaleNormal.x, (y * stride + seed) * biomeNoiseScaleNormal.y);
                float temperature = Mathf.PerlinNoise(tempx * biomeNoiseScaleNormal.x, tempy * biomeNoiseScaleNormal.y);

                return Mathf.Clamp(temperature, 0f, 1f);
            }

            public float GetMoisture(float x, float y, int stride = 1)
            {
                float moisx = math.clamp((x * stride + seed) * moistureNoiseScale.x, -float.MaxValue, float.MaxValue);
                float moisy = math.clamp((y * stride + seed) * moistureNoiseScale.y, -float.MaxValue, float.MaxValue);
                float moisture = Mathf.PerlinNoise(moisx * biomeNoiseScaleNormal.x, moisy * biomeNoiseScaleNormal.y);

                return Mathf.Clamp(moisture, 0f, 1f);
            }

            
        }
    }
}
