using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
using Unity.Mathematics;

namespace VoxelTerrain {

    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshCollider))]
    public class TerrainChunk : MonoBehaviour
    {
        [Range(0.01f, 100f)] public float renderFrequency = 1;
        public Chunk chunk;
        public ChunkLod currentLod;
        public int lodIndex = 0;
        public int colliderLodIndex = 3;
        public ComputeShader meshGenerator;
        public ComputeShader colliderGenerator;

        [HideInInspector]
        public bool visible = false;

        public Dictionary<int, Mesh> meshes;
        private MeshFilter meshFilter {
            get {
                if (!_meshFilter) {
                    _meshFilter = GetComponent<MeshFilter>();
                }
                return _meshFilter;
            }
        }
        private MeshRenderer meshRend;
        public Bounds bounds;
        private Camera gameCamera;

        public Vector3[] verts;
        public int[] tris;

        private Dictionary<int, ComputeProcess> computeProcesses;
        private ComputeProcess colliderComputeProcess;

        private MeshFilter _meshFilter;
        private MeshCollider meshCollider;

        public void Awake()
        {
            gameCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
            meshRend = GetComponent<MeshRenderer>();
            
            meshCollider = GetComponent<MeshCollider>();
            meshes = new Dictionary<int, Mesh>();
            computeProcesses = new Dictionary<int, ComputeProcess>();
        }

        public void OnEnable() {
            meshRend.sharedMaterial = Instantiate<Material>(meshRend.sharedMaterial);
        }

        public void Update() { 
            SetVisible();
            DetectLod();
        }

        public void LateUpdate()
        {
            CompleteComputeProcesses();
        }

        private void SetVisible() {
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(gameCamera);

            visible = GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
            meshRend.enabled = visible;
        }

        private void DetectLod() {
            float camDist = Vector3.Distance(transform.position, gameCamera.transform.position);

            if (!gameCamera) {
                ChangeLod(TerrainManager.instance.lodRanges.Count - 1);
                return;
            }


            int i = 0;
            foreach (float lodRange in TerrainManager.instance.lodRanges) {
                if (camDist < lodRange) {
                    ChangeLod(i);
                    return;
                }
                i++;
            }

            ChangeLod(TerrainManager.instance.lodRanges.Count - 1);
        }

        public void ChangeLod(int lodIndex) {
            if (!computeProcesses.ContainsKey(lodIndex) && !meshes.ContainsKey(lodIndex))
            {
                GenerateChunkMesh(lodIndex);
            }
            else if (meshes.ContainsKey(lodIndex)) {
                this.lodIndex = lodIndex;
                meshFilter.sharedMesh = meshes[lodIndex];
                currentLod = chunk.lods[lodIndex];
                meshRend.sharedMaterial.SetTexture("_ClimateTexture", chunk.lods[lodIndex].climateTexture);
                meshRend.sharedMaterial.SetTexture("_ColorTexture", chunk.lods[lodIndex].colorTexture);
            }            
        }

        public void SetChunk(Chunk chunk, bool reRender = true) {
            this.chunk = chunk;

            transform.position = new Vector3(chunk.gridPosition.x, 0f, chunk.gridPosition.y) * (chunk.grid.voxelSize * chunk.chunkWidth);

            if (meshCollider.sharedMesh == null && chunk.lods.ContainsKey(colliderLodIndex) && colliderComputeProcess == null) {
                GenerateColliderMesh();
            }
            
        }

        public bool GenerateChunkMesh(int lodIndex) {
            if (!chunk.lods.ContainsKey(lodIndex)) {
                return false;
            }

            Profiler.BeginSample("Generate Chunk Mesh");
            Profiler.BeginSample("Initialize Chunk Mesh");
            Profiler.BeginSample("Sizing");
            // Initializations
            int sizeVector3 = sizeof(float) * 3;
            int sizeVector2 = sizeof(float) * 2;
            int sizeVoxel = (sizeof(int) * 3) + (sizeVector3 * 4);

            float voxelWidth = (chunk.grid.voxelSize * (chunk.chunkWidth / chunk.lods[lodIndex].width));
            Profiler.EndSample();

            Profiler.BeginSample("Counts");
            int vertCount = (chunk.lods[lodIndex].voxels.Length * 12) + (chunk.lods[lodIndex].width * 8);
            int indexCount = (chunk.lods[lodIndex].voxels.Length * 18) + (chunk.lods[lodIndex].width * 12);
            Profiler.EndSample();

            Profiler.BeginSample("Compute Process Creation");
            if (!computeProcesses.ContainsKey(lodIndex)) {
                computeProcesses.Add(lodIndex, new ComputeProcess(lodIndex));
            }

            ComputeProcess process = computeProcesses[lodIndex];
            Profiler.EndSample();

            Profiler.BeginSample("Process Buffers");
            // Chunk data buffers
            process.SetBuffer("chunk_voxel_data", ref chunk.lods[lodIndex].voxels, sizeVoxel);

            // Mesh data buffers
            process.InitializeBuffer("verts", vertCount, sizeVector3);
            process.InitializeBuffer("tris", indexCount, sizeof(int));
            process.InitializeBuffer("normals", vertCount, sizeVector3);
            process.InitializeBuffer("uv0", vertCount, sizeVector2);
            process.InitializeBuffer("uv1", vertCount, sizeVector2);

            process.SetValue("voxel_size", voxelWidth);
            process.SetValue("chunk_width", chunk.lods[lodIndex].width);

            Profiler.BeginSample("Compute Buffers");
            meshGenerator.SetFloat("voxelSize", voxelWidth);
            meshGenerator.SetInt("chunkWidth", chunk.lods[lodIndex].width);

            meshGenerator.SetBuffer(0, "voxels", process.GetBuffer("chunk_voxel_data"));
            meshGenerator.SetBuffer(0, "verts", process.GetBuffer("verts"));
            meshGenerator.SetBuffer(0, "tris", process.GetBuffer("tris"));
            meshGenerator.SetBuffer(0, "normals", process.GetBuffer("normals"));
            meshGenerator.SetBuffer(0, "uv0", process.GetBuffer("uv0"));
            meshGenerator.SetBuffer(0, "uv1", process.GetBuffer("uv1"));
            Profiler.EndSample();
            Profiler.EndSample();

            Profiler.BeginSample("Generate Mesh");
            // Run computation
            meshGenerator.Dispatch(0, chunk.lods[lodIndex].width / 8, chunk.lods[lodIndex].width / 8, 1);
            process.startTime = Time.realtimeSinceStartup;
            Profiler.EndSample();

            process.Complete += (int lodIndex, ComputeData computeData) =>
            {
                if (!meshes.ContainsKey(lodIndex)) {
                    meshes.Add(lodIndex, new Mesh());
                }

                Profiler.BeginSample("Read Mesh Data");
                // Use Data
                //meshes[lodIndex].Clear();
                meshes[lodIndex].name = $"Chunk {chunk.gridPosition} LOD {lodIndex}";

                
                AsyncGPUReadback.Request(computeData.GetBuffer("verts"), (AsyncGPUReadbackRequest request) => {                    
                    if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
                    else if (!request.done) { Debug.Log("Not done yet..."); return; }

                    Profiler.BeginSample("Read Mesh Verts");
                    meshes[lodIndex].SetVertices(request.GetData<Vector3>());
                    computeData.DisposeBuffer("verts");
                    computeData.DisposeBuffer("chunk_voxel_data");
                    Profiler.EndSample();

                    AsyncGPUReadback.Request(computeData.GetBuffer("tris"), (AsyncGPUReadbackRequest request) =>
                    {
                        if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
                        else if (!request.done) { Debug.Log("Not done yet..."); return; }

                        Profiler.BeginSample("Read Mesh Tris");
                        meshes[lodIndex].SetIndexBufferParams(computeData.GetBuffer("tris").count, IndexFormat.UInt32);
                        meshes[lodIndex].SetIndexBufferData(request.GetData<int>(), 0, 0, computeData.GetBuffer("tris").count);
                        meshes[lodIndex].SetSubMesh(0, new SubMeshDescriptor(0, computeData.GetBuffer("tris").count));
                        computeData.DisposeBuffer("tris");
                        Profiler.EndSample();
                    });

                    AsyncGPUReadback.Request(computeData.GetBuffer("normals"), (AsyncGPUReadbackRequest request) =>
                    {
                        if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
                        else if (!request.done) { Debug.Log("Not done yet..."); return; }

                        Profiler.BeginSample("Read Mesh Normals");
                        meshes[lodIndex].SetNormals(request.GetData<Vector3>());
                        computeData.DisposeBuffer("normals");
                        Profiler.EndSample();
                    });

                    AsyncGPUReadback.Request(computeData.GetBuffer("uv0"), (AsyncGPUReadbackRequest request) =>
                    {
                        if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
                        else if (!request.done) { Debug.Log("Not done yet..."); return; }

                        Profiler.BeginSample("Read Mesh UV0s");
                        meshes[lodIndex].SetUVs(0, request.GetData<Vector2>());
                        computeData.DisposeBuffer("uv0");
                        Profiler.EndSample();
                    });

                    AsyncGPUReadback.Request(computeData.GetBuffer("uv1"), (AsyncGPUReadbackRequest request) =>
                    {
                        if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
                        else if (!request.done) { Debug.Log("Not done yet..."); return; }

                        Profiler.BeginSample("Read Mesh UV1s");
                        meshes[lodIndex].SetUVs(1, request.GetData<Vector2>());
                        computeData.DisposeBuffer("uv1");
                        Profiler.EndSample();
                    });

                    bounds = new Bounds(transform.position + (meshes[lodIndex].bounds.max / 2), (meshes[lodIndex].bounds.max - meshes[lodIndex].bounds.min));
                });
                
                Profiler.EndSample();
            };            

            Profiler.EndSample();
            return true;
        }

        public void GenerateColliderMesh() {
            //Profiler.BeginSample("Generate Collider Mesh");
            //Profiler.BeginSample("Initialize Collider Mesh");
            // Initializations
            int sizeVector3 = sizeof(float) * 3;
            int sizeVoxel = (sizeof(int) * 3) + (sizeVector3 * 4);

            float voxelWidth = (chunk.grid.voxelSize * (chunk.chunkWidth / chunk.lods[colliderLodIndex].width));

            colliderComputeProcess = new ComputeProcess(colliderLodIndex);

            // Chunk data buffers
            colliderComputeProcess.SetBuffer("chunk_voxel_buffer", ref chunk.lods[colliderLodIndex].voxels, sizeVoxel);

            // Mesh data buffers
            colliderComputeProcess.InitializeBuffer("verts", chunk.lods[colliderLodIndex].voxels.Length + (chunk.lods[colliderLodIndex].width * 2) + 1, sizeVector3);
            colliderComputeProcess.InitializeBuffer("tris", chunk.lods[colliderLodIndex].voxels.Length * 6, sizeVector3);

            colliderComputeProcess.SetValue("voxel_size", voxelWidth);
            colliderComputeProcess.SetValue("chunk_width", chunk.lods[colliderLodIndex].width);

            colliderGenerator.SetFloat("voxelSize", voxelWidth);
            colliderGenerator.SetInt("chunkWidth", chunk.lods[colliderLodIndex].width);

            colliderGenerator.SetBuffer(0, "voxels", colliderComputeProcess.GetBuffer("chunk_voxel_buffer"));
            colliderGenerator.SetBuffer(0, "verts", colliderComputeProcess.GetBuffer("verts"));
            colliderGenerator.SetBuffer(0, "tris", colliderComputeProcess.GetBuffer("tris"));
            //Profiler.EndSample();

            //Profiler.BeginSample("Generate Collider");
            // Run computation

            colliderComputeProcess.startTime = Time.realtimeSinceStartup;
            colliderGenerator.Dispatch(0, chunk.lods[colliderLodIndex].width / 8, chunk.lods[colliderLodIndex].width / 8, 1);
            //Profiler.EndSample();
            colliderComputeProcess.Complete += (int lodIndex, ComputeData computeData) =>
            {
                Mesh mesh = new Mesh();
                mesh.name = $"Chunk {chunk.gridPosition} Collider Mesh";

                AsyncGPUReadback.Request(computeData.GetBuffer("verts"), (AsyncGPUReadbackRequest request) => {
                    if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
                    else if (!request.done) { Debug.Log("Not done yet..."); return; }

                    Profiler.BeginSample("Vert Data");
                    mesh?.SetVertices(computeData.GetBuffer<Vector3>("verts"));
                    computeData.DisposeBuffer("verts");
                    computeData.DisposeBuffer("chunk_voxel_buffer");
                    Profiler.EndSample();

                    AsyncGPUReadback.Request(computeData.GetBuffer("tris"), (AsyncGPUReadbackRequest request) => {
                        if (request.hasError) { Debug.LogError($"Could not get buffer data."); return; }
                        else if (!request.done) { Debug.Log("Not done yet..."); return; }

                        Profiler.BeginSample("Tri Data");
                        mesh?.SetIndexBufferParams(computeData.GetBuffer("tris").count, IndexFormat.UInt32);
                        mesh?.SetIndexBufferData(request.GetData<int>(), 0, 0, computeData.GetBuffer("tris").count);
                        mesh?.SetSubMesh(0, new SubMeshDescriptor(0, computeData.GetBuffer("tris").count));
                        computeData.DisposeBuffer("tris");
                        Profiler.EndSample();

                        Profiler.BeginSample("Set Collider Mesh");
                        meshCollider.sharedMesh = mesh;
                        Profiler.EndSample();
                    });
                });
            };

            //Profiler.EndSample();
        }

        public void CompleteComputeProcesses() {
            List<int> complete = new List<int>();

            if (colliderComputeProcess != null)
            {
                if (!colliderComputeProcess.done && (Time.realtimeSinceStartup - colliderComputeProcess.startTime) < (1 / renderFrequency))
                {
                    colliderComputeProcess.CompleteProcess();
                    colliderComputeProcess = null;
                }
            }

            foreach (var process in computeProcesses) {
                //if ((Time.realtimeSinceStartup - process.Value.startTime) < (1 / renderFrequency)) { continue; }
                if (!process.Value.done)
                {
                    process.Value.CompleteProcess();
                }

                complete.Add(process.Key);
            }

            foreach (int key in complete) {
                computeProcesses.Remove(key);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            
            /*
            if (chunk.lods != null) {
                if (chunk.lods.Count != 0) {
                    if (chunk.lods[lodIndex] != null) {
                        if (chunk.lods[lodIndex].voxels.Length != 0) {
                            foreach (var voxel in chunk.lods[lodIndex].voxels) {
                                Vector3 vPos = (new Vector3(voxel.x * TerrainManager.instance.grid.voxelSize, voxel.height, voxel.y * TerrainManager.instance.grid.voxelSize) + transform.position);

                                Gizmos.color = Color.green;
                                Gizmos.DrawSphere(vPos, TerrainManager.instance.grid.voxelSize / 4);

                                //Gizmos.color = Color.blue;
                                //Debug.DrawRay(vPos, voxel.normalNorth, Color.blue);
                                Debug.DrawRay(vPos, voxel.normalSouth, Color.yellow);
                                //Debug.DrawRay(vPos, voxel.normalEast, Color.red);
                                Debug.DrawRay(vPos, voxel.normalWest, Color.green);
                            }
                        }
                    }
                }
            }*/
            

            /*
            Gizmos.color = Color.blue;
            if (mesh) {
                foreach (Vector3 vert in mesh.vertices) {
                    Gizmos.DrawSphere(transform.position + vert, 0.1f);
                }
            }*/

        }

        public class ComputeData {
            public bool done {
                get {
                    return buffers != null ? buffers.Count == 0 : true;
                }
            }

            private Dictionary<string, ComputeBuffer> buffers;
            private Dictionary<string, object> values;

            public ComputeData() {
                buffers = new Dictionary<string, ComputeBuffer>();
                values = new Dictionary<string, object>();
            }

            public void SetValue<T>(string valueName, T value) {
                if (!values.ContainsKey(valueName)) {
                    values.Add(valueName, value);
                    return;
                }

                values[valueName] = value;
            }

            public T GetValue<T>(string valueName) {
                if (!values.ContainsKey(valueName)) {
                    Debug.LogError($"{valueName} is not a compute value.");
                    return default(T);
                }

                return (T)values[valueName];
            }

            public ComputeBuffer InitializeBuffer(string bufferName, int count, int elementSize) {
                ComputeBuffer buffer = new ComputeBuffer(count, elementSize);
                if (buffers.ContainsKey(bufferName))
                {
                    buffers[bufferName].Dispose();
                    buffers.Remove(bufferName);
                }
                buffers.Add(bufferName, buffer);
                return buffer;
            }

            public void SetBuffer<T>(string bufferName, ref T[] data, int elementSize) {
                InitializeBuffer(bufferName, data.Length, elementSize).SetData(data);
            }

            public T[] GetBuffer<T>(string bufferName) {
                if (!buffers.ContainsKey(bufferName))
                {
                    Debug.LogError($"{bufferName} is not a compute buffer.");
                    return null;
                }

                ComputeBuffer buffer = buffers[bufferName];
                T[] bufferData = new T[buffer.count];

                try
                {
                    buffer.GetData(bufferData);
                    return bufferData;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Could not box data for buffer {bufferName} into array of type {typeof(T)}. {e.Message}");
                    Profiler.EndSample();
                    return null;
                }
            }

            public void GetBuffer<T>(string bufferName, ref T[] bufferData) {
                if (!buffers.ContainsKey(bufferName)) {
                    Debug.LogError($"{bufferName} is not a compute buffer.");
                    return;
                }

                ComputeBuffer buffer = buffers[bufferName];

                try
                {
                    buffer.GetData(bufferData);
                }
                catch (Exception e) {
                    Debug.LogError($"Could not box data for buffer {bufferName} into array of type {typeof(T)}. {e.Message}");
                    Profiler.EndSample();
                }
            }

            public ComputeBuffer GetBuffer(string bufferName) {
                if (!buffers.ContainsKey(bufferName))
                {
                    Debug.LogError($"{bufferName} is not a compute buffer.");
                    return null;
                }

                return buffers[bufferName];
            }

            public void DisposeBuffer(string bufferName) {
                if (!buffers.ContainsKey(bufferName))
                {
                    Debug.LogError($"{bufferName} is not a compute buffer. Has it already been disposed?");
                    return;
                }

                buffers[bufferName].Dispose();
                buffers.Remove(bufferName);
            }

            public void Dispose(bool clear = false) {
                foreach (var bufferEntry in buffers) {
                    bufferEntry.Value.Dispose();
                }

                if (clear) {
                    buffers.Clear();
                }
            }
        }

        public class ComputeProcess {
            public int lodIndex;
            public float startTime;
            public event CompleteHandler Complete;

            public bool done {
                get {
                    return computeData != null ? computeData.done : true;
                }
            }

            private ComputeData computeData;
            public delegate void CompleteHandler(int lodIndex, ComputeData computeData);

            public ComputeProcess(int lodIndex) {
                this.lodIndex = lodIndex;
                computeData = new ComputeData();
            }

            public void SetValue<T>(string valueName, T value) {
                computeData.SetValue(valueName, value);
            }

            public ComputeBuffer InitializeBuffer(string bufferName, int count, int elementSize) {
                return computeData.InitializeBuffer(bufferName, count, elementSize);
            }

            public void SetBuffer<T>(string bufferName, ref T[] data, int elementSize) {
                computeData.SetBuffer(bufferName, ref data, elementSize);
            }

            public T GetValue<T>(string valueName) {
                return computeData.GetValue<T>(valueName);
            }

            public ComputeBuffer GetBuffer(string bufferName) {
                return computeData.GetBuffer(bufferName);
            }

            public void DisposeBuffer(string bufferName) {
                computeData.DisposeBuffer(bufferName);
            }

            public void CompleteProcess() {
                foreach (var dele in Complete.GetInvocationList())
                {
                    try
                    {
                        dele.DynamicInvoke(lodIndex, computeData);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Could not process delegate. {e.Message}");
                    }
                }
            }
        }
    }
}
