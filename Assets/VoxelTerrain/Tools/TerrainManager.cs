using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Mathematics;
using VoxelTerrain.Generators;
using UnityEngine.Profiling;

namespace VoxelTerrain
{
    public class TerrainManager : MonoBehaviour
    {
        public static TerrainManager instance;

        public Grid grid;

        public float generatorFrequency = 0.02f;
        [Min(1)] public int seed = 0;
        [Range(-512, 512)] public int minTerrainHeight = 0;
        [Range(-512, 512)] public int maxTerrainHeight = 64;
        public Vector2 generatorNoiseScale;
        public Vector2 heightNormalNoiseScale;
        [Range(0,1)] public float heightNormalIntensity;
        [Range(1, 50)] public float renderDistance;
        public List<float> lodRanges;

        public UnityEvent OnStartGeneration;

        private float lastUpdate = 0;
        private float elapsedTime = 0;
        public Dictionary<int2, Chunk> chunks;
        public Dictionary<int2, TerrainChunk> chunkObjects;

        private PerlinTerrainGenerator generator {
            get {
                if (_generator == null) {
                    _generator = new PerlinTerrainGenerator(
                        minTerrainHeight, 
                        maxTerrainHeight, 
                        seed, 
                        grid.chunkSize,
                        generatorNoiseScale,
                        heightNormalNoiseScale,
                        heightNormalIntensity
                    );
                }
                return _generator;
            }
        }

        private PerlinTerrainGenerator _generator;

        private void Awake()
        {
            if (instance) {
                Destroy(instance.gameObject);
            }
            instance = this;
        }

        // Start is called before the first frame update
        void Start()
        {
            StartGenerator();
        }

        // Update is called once per frame
        void Update()
        {
            if (PlayerController.instance) {
                LoadChunks(PlayerController.instance.gridPosition);
            }
        }

        private void FixedUpdate()
        {
            
        }

        private void LateUpdate()
        {
            elapsedTime += Time.fixedDeltaTime;
            if (elapsedTime - lastUpdate > generatorFrequency)
            {
                generator?.ResolveJob(UpdateChunkObject);
                lastUpdate = elapsedTime;
            }
        }

        public void LoadChunks(int2 gridPosition) {
            Profiler.BeginSample("Generate Chunk Data");

            int2 chunkLocation;
            for (int y = (int)-renderDistance; y < renderDistance; y++)
            {
                for (int x = (int)-renderDistance; x < renderDistance; x++)
                {
                    chunkLocation = gridPosition + new int2(x, y);
                    if (math.distance(chunkLocation, gridPosition) > renderDistance) { continue; }
                    else if (chunks.ContainsKey(chunkLocation)) { continue; }

                    InitializeChunk(chunkLocation);
                }
            }

            Profiler.EndSample();
        }

        public void StartGenerator() {
            chunks = new Dictionary<int2, Chunk>();
            chunkObjects = new Dictionary<int2, TerrainChunk>();

            LoadChunks(default);

            OnStartGeneration.Invoke();
        }

        public void InitializeChunk(int2 gridPosition) {
            Profiler.BeginSample("Initialize Chunk Data");

            if (!chunks.ContainsKey(gridPosition)) {
                Chunk chunk = new Chunk();
                chunk.gridPosition.x = gridPosition.x;
                chunk.gridPosition.y = gridPosition.y;
                chunk.grid = grid;
                chunk.chunkWidth = grid.chunkSize;
                int lodWidth = grid.chunkSize;
                int lods = 0;
                while (lodWidth >= 8) {
                    lods++;
                    lodWidth /= 2;
                }
                chunk.lods = new Dictionary<int, ChunkLod>();

                chunks.Add(gridPosition, chunk);

                if (generator == null) {
                    Debug.LogWarning("generator is null");
                }
                generator.QueueChunk(chunk);

                InstantiateChunkObject(chunks[gridPosition]);
            }

            Profiler.EndSample();
        }

        private void UpdateChunkObject(int2 gridPosition, Voxel[] chunkData, int lodIndex) {
            Chunk chunk = chunkObjects[gridPosition].chunk;

            ChunkLod lod = new ChunkLod()
            {
                voxels = chunkData,
                width = (int) Mathf.Sqrt(chunkData.Length)
            };

            chunk.SetChunkLod(lodIndex, lod);

            chunkObjects[gridPosition].SetChunk(chunk);
        }

        private void InstantiateChunkObject(Chunk chunk) {
            Profiler.BeginSample("Create Chunk Object");

            if (!chunkObjects.ContainsKey(chunk.gridPosition)) {
                GameObject chunkObject = Instantiate<GameObject>(Resources.Load<GameObject>("VoxelTerrain/Chunk"), transform);
                chunkObject.name = $"Chunk ( {chunk.gridPosition.x}, {chunk.gridPosition.y} )";

                TerrainChunk chunkScript = chunkObject.GetComponent<TerrainChunk>();
                chunkScript.SetChunk(chunk, false);

                chunkObjects.Add(chunk.gridPosition, chunkScript);
            }

            Profiler.EndSample();
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            //Gizmos.DrawWireCube(collider.bounds.min, collider.bounds.size);
        }
#endif
    }
}
