using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Events;
using Unity.Jobs;
using UnityEngine.Jobs;
using Unity.Mathematics;
using VoxelTerrain.Generators;

namespace VoxelTerrain
{
    public class TerrainManager : MonoBehaviour
    {
        [HideInInspector] public Grid grid;


        [Min(1)] public int seed = 0;
        [Range(0.001f, 2f)] public float voxelSize = 1;
        [Range(8, 256)] public int chunkWidth = 64;
        [Range(-512, 512)] public int minTerrainHeight = 0;
        [Range(-512, 512)] public int maxTerrainHeight = 64;
        public Vector2 generatorNoiseScale;
        public Vector2 heightNormalNoiseScale;
        [Range(0,1)] public float heightNormalIntensity;

        [Range(1, 50)] public int generationStartSize = 5;

        public UnityEvent OnStartGeneration;

        public Dictionary<Vector2Int, Chunk> chunks;
        public Dictionary<Vector2Int, TerrainChunk> chunkObjects;

        private PerlinTerrainGenerator generator {
            get {
                if (_generator == null) {
                    _generator = new PerlinTerrainGenerator(
                        minTerrainHeight, 
                        maxTerrainHeight, 
                        seed, 
                        chunkWidth,
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
            
        }

        // Start is called before the first frame update
        void Start()
        {
            Generate();
        }

        // Update is called once per frame
        void Update()
        {
        }

        private void LateUpdate()
        {
            generator?.ResolveJobs(UpdateChunk);
        }

        public void Generate() {
            if (Application.isEditor)
                _generator = new PerlinTerrainGenerator(
                        minTerrainHeight,
                        maxTerrainHeight,
                        seed,
                        chunkWidth,
                        generatorNoiseScale,
                        heightNormalNoiseScale,
                        heightNormalIntensity
                    );

            if (chunkObjects != null) {
                if (chunkObjects.Count != 0) {
                    foreach (var chunkObject in chunkObjects) {
                        try
                        {
                            if (Application.isEditor)
                                DestroyImmediate(chunkObject.Value.gameObject);
                            else
                                Destroy(chunkObject.Value.gameObject);
                        }
                        catch {
                            Debug.LogWarning("Could not delete the chunk.");
                        }
                    }
                }
            }

            chunks = new Dictionary<Vector2Int, Chunk>();
            chunkObjects = new Dictionary<Vector2Int, TerrainChunk>();
            
            grid = new Grid
            {
                voxelSize = voxelSize
            };

            for (int x = 0; x < generationStartSize; x++) {
                for (int y = 0; y < generationStartSize; y++) {
                    InitializeChunk(new Vector2Int(x, y));
                }
            }

            if (!Application.isEditor)
                OnStartGeneration.Invoke();
            else
                generator.ResolveJobs(UpdateChunk);
        }

        public void InitializeChunk(Vector2Int gridPosition) {
            if (!chunks.ContainsKey(gridPosition)) {
                Chunk chunk = new Chunk();
                chunk.gridPosition.x = gridPosition.x;
                chunk.gridPosition.y = gridPosition.y;
                chunk.grid = grid;
                chunk.chunkWidth = chunkWidth;
                chunk.voxels = new Voxel[chunkWidth * chunkWidth];
                chunk.leftEdge = new Voxel[chunkWidth];
                chunk.bottomEdge = new Voxel[chunkWidth];

                chunks.Add(gridPosition, chunk);

                float randomNormIndex = UnityEngine.Random.Range(0f, 1f);
                if (generator == null) {
                    Debug.LogWarning("generator is null");
                }
                generator.QueueChunk(chunk);

                InstantiateChunkObject(chunks[gridPosition]);
            }
        }

        private void UpdateChunk(int2 gridPosition, Voxel[] chunkData, Voxel[] leftEdgeData, Voxel[] bottomEdgeData) {
            Chunk chunk = chunks[new Vector2Int(gridPosition.x, gridPosition.y)];

            chunk.voxels = chunkData;
            chunk.leftEdge = leftEdgeData;
            chunk.bottomEdge = bottomEdgeData;
            chunkObjects[new Vector2Int(gridPosition.x, gridPosition.y)].SetChunk(chunk);
        }

        private void InstantiateChunkObject(Chunk chunk) {
            Vector2Int gridPosition = new Vector2Int(chunk.gridPosition.x, chunk.gridPosition.y);
            if (!chunkObjects.ContainsKey(gridPosition)) {
                GameObject chunkObject = Instantiate<GameObject>(Resources.Load<GameObject>("VoxelTerrain/Chunk"), transform);

                TerrainChunk chunkScript = chunkObject.GetComponent<TerrainChunk>();
                //chunkScript.SetChunk(chunk);

                chunkObjects.Add(gridPosition, chunkScript);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            //Gizmos.DrawWireCube(collider.bounds.min, collider.bounds.size);
        }
#endif
    }
}
