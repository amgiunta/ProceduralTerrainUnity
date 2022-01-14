using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using UnityEngine.Events;
using Unity.Mathematics;
using VoxelTerrain.Generators;
using VoxelTerrain.ECS.Components;
using UnityEngine.Profiling;
using Unity.Jobs.LowLevel.Unsafe;

namespace VoxelTerrain
{
    public class TerrainManager : MonoBehaviour
    {
        public static TerrainManager instance;

        public int chunkQueueLimit = 100;
        public string worldName = "NewWorld";
        public Grid grid;

        public float generatorFrequency = 0.02f;

        public List<GroundScatterAuthor> groundScatter;
        public List<BiomeObject> biomes;
        public TerrainSettings terrainSettings;

        [Range(1, 500)] public float renderDistance;
        public List<float> lodRanges;

        public UnityEvent OnStartGeneration;

        public Dictionary<int2, Chunk> chunks;
        public Dictionary<int2, TerrainChunk> chunkObjects;

        private PerlinTerrainGenerator generator {
            get {

                if (_generator == null) {
                    _generator = new PerlinTerrainGenerator(
                        biomes.ToArray(),
                        terrainSettings,
                        grid.chunkSize,
                        (uint) chunkQueueLimit
                    );
                }
                return _generator;
            }
        }
        public WorldSaveData saveData;

        private PerlinTerrainGenerator _generator;
        public int2 loadingFromChunk = new int2(int.MaxValue, int.MaxValue);
        private Coroutine loadingRoutine = null;

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
            Debug.Log(groundScatter.Count);
            Debug.Log(Application.persistentDataPath);
            chunks = new Dictionary<int2, Chunk>();
            chunkObjects = new Dictionary<int2, TerrainChunk>();
            Debug.Log("Max Jobs: " + JobsUtility.MaxJobThreadCount);

            //LoadWorld();

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
        }

        private void LateUpdate()
        {
            ResolveChunks();
            //generator.DisposeJobs();
        }

        public void StartLoadingChunks(int2 gridPosition) {
            if (!gridPosition.Equals(loadingFromChunk)) {
                
                if (loadingRoutine != null) {
                    StopCoroutine(loadingRoutine);
                    loadingRoutine = null;
                }
                
                loadingFromChunk = gridPosition;
                loadingRoutine = StartCoroutine(LoadChunks());
            }
        }

        public void ResolveChunks() {
            //generator?.ResolveClosestJob(gridPosition, UpdateChunkObject, this);
            //generator?.ResolveAllCompleteJobs(UpdateChunkObject, this);
            //Debug.Log($"current: {loadingFromChunk}");
            generator?.ResolveAllCloseJobs(loadingFromChunk, renderDistance, UpdateChunkObject);
        }

        public void StartGenerator() {
            OnStartGeneration.Invoke();
        }

        public bool InitializeChunk(int2 gridPosition) {
            Profiler.BeginSample("Initialize Chunk Data");

            bool queued = false;

            if (!chunks.ContainsKey(gridPosition))
            {
                Profiler.BeginSample("Create Data Structure");
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

                Profiler.EndSample();

                Profiler.BeginSample("Enqueing");
                if (generator == null)
                {
                    Debug.LogWarning("generator is null");
                }
                queued = generator.QueueChunk(chunk);
                Profiler.EndSample();

                if (queued)
                {
                    chunks.Add(gridPosition, chunk);
                    InstantiateChunkObject(chunks[gridPosition]);
                }
            }
            Profiler.EndSample();

            return queued;
        }

        private void UpdateChunkObject(int2 gridPosition, ref Voxel[] chunkData, Texture2D climateTexture, Texture2D colorTexture, int lodIndex) {
            Profiler.BeginSample("Update Chunk");
            Chunk chunk = chunkObjects[gridPosition].chunk;

            ChunkLod lod = new ChunkLod()
            {
                voxels = chunkData,
                width = (int) Mathf.Sqrt(chunkData.Length),
                climateTexture = climateTexture,
                colorTexture = colorTexture
            };

            chunk.SetChunkLod(lodIndex, lod);
            Profiler.EndSample();

            Profiler.BeginSample("Set Chunk");
            chunkObjects[gridPosition].SetChunk(chunk);
            Profiler.EndSample();
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

        public IEnumerator LoadChunks() {
            int x = 0;
            int y = 0;
            int dx = 0;
            int dy = -1;

            int maxTries = 100;
            int tryCount = 0;

            List<int> missed = new List<int>();

            bool done = false;
            for (int i = 0; i < (renderDistance * renderDistance); i++)
            {
                //Profiler.BeginSample("Queue Chunk");
                tryCount++;
                bool queued = false;
                if (((-renderDistance / 2) < x && x <= (renderDistance / 2)) && ((-renderDistance / 2) < y && y <= (renderDistance / 2)))
                {
                    int2 chunkLocation = loadingFromChunk + new int2(x, y);
                    if (math.distance(chunkLocation, loadingFromChunk) < renderDistance)
                    {
                        if (chunks.ContainsKey(chunkLocation))
                        {
                            queued = true;
                            tryCount = 0;
                            if (!chunkObjects.ContainsKey(chunkLocation))
                            {
                                InstantiateChunkObject(chunks[chunkLocation]);
                            }
                        }
                        else
                        {
                            queued = InitializeChunk(chunkLocation);
                            tryCount = 0;
                            yield return new WaitForSeconds(1 / generatorFrequency);
                        }
                    }
                }
                //Profiler.EndSample();

                //Profiler.BeginSample("Iterate Pattern");
                if (queued || tryCount >= maxTries)
                {
                    if (x == y || (x < 0 && x == -y) || (x > 0 && x == 1 - y))
                    {
                        int temp = dx;
                        dx = -dy;
                        dy = temp;
                    }

                    x += dx;
                    y += dy;
                }
                else if (!missed.Contains(i)) {
                    missed.Add(i);
                }
                //Profiler.EndSample();

                if (i == (renderDistance * renderDistance) - 1) { done = true; }
                
                if (done && missed.Count > 0) {
                    i = missed.First();
                    yield return new WaitForEndOfFrame();
                }
            }
            yield return null;
        }
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
