using UnityEngine.Jobs;
using Unity.Collections;
using System;
using System.Collections;
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
    }

    [System.Serializable]
    public struct ChunkLod {
        public int width;
        public Voxel[] voxels;
        public Voxel[] leftEdge;
        public Voxel[] bottomEdge;
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

        public Voxel[] GetLeftEdge(int lod)
        {
            if (lod >= lods.Count)
            {
                Debug.LogWarning($"Chunk lod {lod} too low and does not exist. Using lowest available lod.");
                return lods[lods.Count - 1].leftEdge;
            }

            return lods[lod].leftEdge;
        }

        public Voxel[] GetBottomEdge(int lod)
        {
            if (lod >= lods.Count)
            {
                Debug.LogWarning($"Chunk lod {lod} too low and does not exist. Using lowest available lod.");
                return lods[lods.Count - 1].bottomEdge;
            }

            return lods[lod].bottomEdge;
        }
    }

    [System.Serializable]
    public struct Voxel {
        public int x;
        public int y;
        public int height;
    }

    namespace Generators {
        public abstract class Generator {
            public Dictionary<JobHandle, IJobParallelFor> runningJobs;
            public int chunkWidth;

            public delegate void chunkProcessCallback(int2 gridPosition, Voxel[] chunkData, Voxel[] leftEdgeData, Voxel[] bottomEdgeData, int lodIndex);

            public abstract void QueueChunk(Chunk chunk);
            public abstract void QueueChunks(List<Chunk> chunks);
            public abstract void ResolveJobs(chunkProcessCallback onProcessComplete);
        }

        public class PerlinTerrainGenerator : Generator {
            new public Dictionary<JobHandle, PerlinTerrainGeneratorJob> runningJobs;

            public int minHeight;
            public int maxHeight;
            public float2 generatorNoiseScale;
            public float2 heightNormalNoiseScale;
            public float heightNormalIntensity;
            public int seed;

            public PerlinTerrainGenerator(
                int minHeight,
                int maxHeight,
                int seed,
                int chunkWidth = 64,
                float2 generatorNoiseScale = default,
                float2 heightNormalNoiseScale = default,
                float heightNormalIntensity = 1
             ) {
                this.chunkWidth = chunkWidth;
                this.minHeight = minHeight;
                this.maxHeight = maxHeight;
                this.seed = seed;
                this.generatorNoiseScale = generatorNoiseScale;
                this.heightNormalNoiseScale = heightNormalNoiseScale;
                this.heightNormalIntensity = heightNormalIntensity;

                runningJobs = new Dictionary<JobHandle, PerlinTerrainGeneratorJob>();
            }

            public override void QueueChunk(Chunk chunk)
            {
                int lodWidth = chunk.chunkWidth;
                for (int i = 0; lodWidth >= 8 ; i++)
                {
                    PerlinTerrainGeneratorJob job = new PerlinTerrainGeneratorJob
                    {
                        chunkData = new NativeArray<Voxel>(new Voxel[lodWidth * lodWidth], Allocator.Persistent),
                        leftEdgeData = new NativeArray<Voxel>(new Voxel[lodWidth], Allocator.Persistent),
                        bottomEdgeData = new NativeArray<Voxel>(new Voxel[lodWidth], Allocator.Persistent),
                        minChunkHeight = minHeight,
                        maxChunkHeight = maxHeight,
                        chunkWidth = chunkWidth,
                        lodWidth = lodWidth,
                        lodIndex = i,
                        chunkPosition = chunk.gridPosition,
                        generatorNoiseScale = new float2(generatorNoiseScale.x, generatorNoiseScale.y),
                        heightNormalNoiseScale = new float2(heightNormalNoiseScale.x, heightNormalNoiseScale.y),
                        heightNormalIntensity = heightNormalIntensity,
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

            public override void ResolveJobs(chunkProcessCallback onChunkComplete)
            {
                foreach (var process in runningJobs)
                {
                    process.Key.Complete();

                    onChunkComplete(
                        process.Value.chunkPosition,
                        process.Value.chunkData.ToArray(),
                        process.Value.leftEdgeData.ToArray(),
                        process.Value.bottomEdgeData.ToArray(),
                        process.Value.lodIndex
                    );

                    process.Value.chunkData.Dispose();
                    process.Value.leftEdgeData.Dispose();
                    process.Value.bottomEdgeData.Dispose();
                }

                runningJobs.Clear();
            }

            
        }

        public struct PerlinTerrainGeneratorJob : IJobParallelFor {
            public NativeArray<Voxel> chunkData;
            public NativeArray<Voxel> leftEdgeData;
            public NativeArray<Voxel> bottomEdgeData;
            public int chunkWidth;
            public int lodWidth;
            public int lodIndex;
            public int minChunkHeight;
            public int maxChunkHeight;
            public float2 generatorNoiseScale;
            public float2 heightNormalNoiseScale;
            public float heightNormalIntensity;
            public int seed;

            public int2 chunkPosition;

            private float GetHeightAtPosition(float x, float y) {
                int stride = Mathf.Max(1, chunkWidth / lodWidth);

                float heightNormal = Noise(x, y, stride, chunkPosition * chunkWidth, heightNormalNoiseScale) * heightNormalIntensity;
                heightNormal = math.remap(-1, 1, 0, 1, heightNormal);

                float fHeight = Noise(x, y, stride, chunkPosition * chunkWidth, generatorNoiseScale) * heightNormal;

                fHeight = math.remap(-1, 1, minChunkHeight, maxChunkHeight, fHeight);
                return fHeight;
            }

            public void Execute(int voxelId) {
                Voxel voxel = chunkData[voxelId];
                voxel.x = voxelId % lodWidth;
                voxel.y = voxelId / lodWidth;

                voxel.height = (int) GetHeightAtPosition(voxel.x, voxel.y);

                chunkData[voxelId] = voxel;

                if (voxel.x == 0)
                {
                    Voxel leftVoxel = leftEdgeData[voxel.y];
                    leftVoxel.x = - 1 * (lodIndex + 1);
                    leftVoxel.y = voxel.y;

                    leftVoxel.height = (int) GetHeightAtPosition(leftVoxel.x, leftVoxel.y);

                    leftEdgeData[voxel.y] = leftVoxel;
                }

                if (voxel.y == 0) { 
                    Voxel bottomVoxel = bottomEdgeData[voxel.x];
                    bottomVoxel.x = voxel.x;
                    bottomVoxel.y = - 1 * (lodIndex + 1);
                    bottomVoxel.height = (int) GetHeightAtPosition(bottomVoxel.x, bottomVoxel.y);

                    bottomEdgeData[voxel.x] = bottomVoxel;
                }
            }

            public float Noise(float x, float y, int stride = 1, float2 offset = default, float2 scale = default)
            {
                float2 pos = new float2(x * stride + Mathf.PI + seed, y * stride + Mathf.PI + seed) + offset;
                if (scale.ToVector2() != Vector2.zero)
                    pos *= scale;
                return math.remap(0, 1, -1, 1, Mathf.PerlinNoise(pos.x, pos.y));
                //return noise.cnoise(pos);
            }
        }
    }
}
