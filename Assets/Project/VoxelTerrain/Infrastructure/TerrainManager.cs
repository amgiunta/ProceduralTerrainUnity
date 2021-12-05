using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Events;
using Unity.Mathematics;
using VoxelTerrain.Generators;
using UnityEngine.Profiling;

namespace VoxelTerrain
{
    public class TerrainManager : MonoBehaviour
    {
        public static TerrainManager instance;

        public string worldName = "NewWorld";
        public Grid grid;

        public float generatorFrequency = 0.02f;
        [Min(1)] public int seed = 0;        

        public List<BiomeObject> biomes;
        public Vector2 temperatureNoiseScale;
        public Vector2 moistureNoiseScale;
        public Vector2 biomeNoiseScaleNormal;

        [Range(1, 500)] public float renderDistance;
        public List<float> lodRanges;

        public UnityEvent OnStartGeneration;

        private float lastUpdate = 0;
        private float elapsedTime = 0;
        public Dictionary<int2, Chunk> chunks;
        public Dictionary<int2, TerrainChunk> chunkObjects;

        private PerlinTerrainGenerator generator {
            get {
                Biome[] biomeStructs = new Biome[biomes.Count];

                for (int i = 0; i < biomes.Count; i++) {
                    biomeStructs[i] = biomes[i];
                }

                if (_generator == null) {
                    _generator = new PerlinTerrainGenerator(
                        seed,
                        biomeStructs,
                        temperatureNoiseScale,
                        moistureNoiseScale,
                        biomeNoiseScaleNormal,
                        grid.chunkSize
                    );
                }
                return _generator;
            }
        }
        public WorldSaveData saveData;

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
            Debug.Log(Application.persistentDataPath);
            chunks = new Dictionary<int2, Chunk>();
            chunkObjects = new Dictionary<int2, TerrainChunk>();

            LoadWorld();

            StartGenerator();
        }

        private void OnDisable()
        {
        }

        // Update is called once per frame
        void Update()
        {
            
        }

        private void FixedUpdate()
        {
            elapsedTime += Time.fixedDeltaTime;
        }        

        public void LoadChunks(int2 gridPosition) {
            if (elapsedTime - lastUpdate < generatorFrequency) { return; }

            Profiler.BeginSample("Generate Chunk Data");

            int x = 0;
            int y = 0;
            int dx = 0;
            int dy = -1;

            for (int i = 0; i < (renderDistance * renderDistance); i++) {
                if (((-renderDistance / 2) < x && x <= (renderDistance / 2)) && ((-renderDistance / 2) < y && y <= (renderDistance / 2))) {
                    int2 chunkLocation = gridPosition + new int2(x, y);
                    if (math.distance(chunkLocation, gridPosition) < renderDistance) {
                        if (chunks.ContainsKey(chunkLocation))
                        {
                            InstantiateChunkObject(chunks[chunkLocation]);
                        }
                        else
                        {
                            InitializeChunk(chunkLocation);
                        }
                    }
                }
                if (x == y || (x < 0 && x == -y) || (x > 0 && x == 1 - y)) {
                    int temp = dx;
                    dx = -dy;
                    dy = temp;
                }

                x += dx;
                y += dy;
            }

            Profiler.EndSample();
        }

        public void ResolveChunks(int2 gridPosition) {
            if (elapsedTime - lastUpdate > generatorFrequency)
            {
                generator?.ResolveClosestJob(gridPosition, UpdateChunkObject);
                lastUpdate = elapsedTime;
            }
        }

        public void StartGenerator() {
            LoadChunks(default);

            OnStartGeneration.Invoke();
        }

        public void InitializeChunk(int2 gridPosition) {
            Profiler.BeginSample("Initialize Chunk Data");

            if (!chunks.ContainsKey(gridPosition))
            {
                Chunk chunk = new Chunk();
                chunk.gridPosition.x = gridPosition.x;
                chunk.gridPosition.y = gridPosition.y;
                chunk.grid = grid;
                chunk.chunkWidth = grid.chunkSize;
                int lodWidth = grid.chunkSize;
                int lods = 0;
                while (lodWidth >= 8)
                {
                    lods++;
                    lodWidth /= 2;
                }
                chunk.lods = new Dictionary<int, ChunkLod>();

                chunks.Add(gridPosition, chunk);

                if (generator == null)
                {
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

        private void SaveWorld() {
            saveData.chunks = chunks.Values.ToArray();
            saveData.version = Application.version;
            saveData.name = worldName;
            saveData.Save();
        }

        private void LoadWorld() {
            saveData = WorldSaveData.Load(worldName);
            if (saveData == null)
            {
                saveData = new WorldSaveData(new Chunk[0], Application.version, worldName);
            }
            else {
                foreach (Chunk chunk in saveData.chunks) {
                    chunks.Add(chunk.gridPosition, chunk);
                }
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            //Gizmos.DrawWireCube(collider.bounds.min, collider.bounds.size);
        }
#endif
    }

    [System.Serializable]
    public class WorldSaveData {
        public string version;
        public string name;

        public Chunk[] chunks;

        public WorldSaveData(Chunk[] chunks, string version, string name = "NewWorld") {
            this.chunks = chunks;
            this.version = version;
            this.name = name;
        }

        public void Save() {
            if (!Directory.Exists($"{Application.persistentDataPath}/Saves")) {
                Directory.CreateDirectory($"{Application.persistentDataPath}/Saves");
            }

            try
            {
                using (var file = File.Open($"{Application.persistentDataPath}/Saves/{name}", FileMode.OpenOrCreate))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    formatter.Serialize(file, this);
                }
            }
            catch {
                Debug.LogError($"Failed to save to file {Application.persistentDataPath}/Saves/{name}");
            }
        }

        public static WorldSaveData Load(string name) { 
            if (!Directory.Exists($"{Application.persistentDataPath}/Saves") || !File.Exists($"{Application.persistentDataPath}/Saves/{name}")) {
                Debug.LogError($"Could not find file to load at {Application.persistentDataPath}/Saves/{name}");
                return null;
            }

            try
            {
                using (var file = File.OpenRead($"{Application.persistentDataPath}/Saves/{name}"))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    WorldSaveData newData = (WorldSaveData) formatter.Deserialize(file);

                    return newData;
                }
            }
            catch {
                Debug.LogError($"Could not load file at {Application.persistentDataPath}/Saves/{name}. The file may be corrupted.");
                return null;
            }
        }
    }
}
