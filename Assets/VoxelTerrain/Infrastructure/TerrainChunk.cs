using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace VoxelTerrain {

    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshCollider))]
    public class TerrainChunk : MonoBehaviour
    {
        public Chunk chunk;
        public ChunkLod currentLod;
        public int lodIndex = 0;
        public int colliderLodIndex = 3;
        public ComputeShader meshGenerator;

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

        private MeshFilter _meshFilter;
        private MeshCollider meshCollider;

        public void Awake()
        {
            gameCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
            meshRend = GetComponent<MeshRenderer>();
            
            meshCollider = GetComponent<MeshCollider>();
            meshes = new Dictionary<int, Mesh>();
        }

        public void Update()
        {
            SetVisible();
            DetectLod();
        }

        private void SetVisible() {
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(gameCamera);

            visible = GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
            meshRend.enabled = visible;
        }

        private void DetectLod() {
            if (!gameCamera) {
                //Debug.Log("Could not find camera");
                ChangeLod(TerrainManager.instance.lodRanges.Count - 1);
                return;
            }

            float camDist = Vector3.Distance(transform.position, gameCamera.transform.position);

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
            this.lodIndex = lodIndex;

            if (!meshes.ContainsKey(lodIndex))
            {
                //Debug.Log($"Should generate mesh for lod {lodIndex}", this);
                if (GenerateChunkMesh(lodIndex))
                {
                    //Debug.Log($"Should render mesh for lod {lodIndex}", this);
                    if (meshFilter.sharedMesh == meshes[lodIndex]) { return; }

                    meshFilter.sharedMesh = meshes[lodIndex];
                }
            }
            else {
                //Debug.Log($"Should render mesh for lod {lodIndex}", this);
                if (meshFilter.sharedMesh == meshes[lodIndex]) { return; }

                meshFilter.sharedMesh = meshes[lodIndex];
            }

            if (meshes.ContainsKey(colliderLodIndex)) {
                if (meshCollider.sharedMesh != meshes[colliderLodIndex]) {
                    meshCollider.sharedMesh = meshes[colliderLodIndex];
                }
            }
        }

        public void SetChunk(Chunk chunk, bool reRender = true) {
            this.chunk = chunk;


            transform.position = new Vector3(chunk.gridPosition.x, 0f, chunk.gridPosition.y) * (chunk.grid.voxelSize * chunk.chunkWidth);
        }

        public bool GenerateChunkMesh(int lodIndex) {
            Profiler.BeginSample("Generate Chunk Mesh");

            // Initializations
            int sizeVector3 = sizeof(float) * 3;
            int sizeVoxel = sizeof(int) * 3;

            if (!chunk.lods.ContainsKey(lodIndex)) {
                //Debug.Log($"Can't generate mesh for lod {lodIndex}", this);
                Profiler.EndSample();
                return false;
            }

            if (!meshes.ContainsKey(lodIndex))
            {
                meshes.Add(lodIndex, new Mesh());
            }

            //Debug.Log($"Mesh Count {meshes.Count}", this);

            float voxelWidth = (chunk.grid.voxelSize * (chunk.chunkWidth / chunk.lods[lodIndex].width));

            Vector3[] verts = new Vector3[(chunk.lods[lodIndex].voxels.Length * 12) + (chunk.lods[lodIndex].width * 8)];
            int[] tris = new int[(chunk.lods[lodIndex].voxels.Length * 18) + (chunk.lods[lodIndex].width * 12)];

            // Chunk data buffers
            ComputeBuffer chunkVoxelBuffer = new ComputeBuffer(chunk.lods[lodIndex].voxels.Length, sizeVoxel);
            chunkVoxelBuffer.SetData(chunk.lods[lodIndex].voxels);

            // Mesh data buffers
            ComputeBuffer vertsBuffer = new ComputeBuffer(verts.Length, sizeVector3);
            ComputeBuffer trisBuffer = new ComputeBuffer(tris.Length, sizeof(int));
            vertsBuffer.SetData(verts);
            trisBuffer.SetData(tris);

            meshGenerator.SetFloat("voxelSize", voxelWidth);
            meshGenerator.SetInt("chunkWidth", chunk.lods[lodIndex].width);

            meshGenerator.SetBuffer(0, "voxels", chunkVoxelBuffer);

            meshGenerator.SetBuffer(0, "verts", vertsBuffer);
            meshGenerator.SetBuffer(0, "tris", trisBuffer);

            // Run computation
            meshGenerator.Dispatch(0, chunk.lods[lodIndex].width / 8, chunk.lods[lodIndex].width / 8, 1);

            // Read back data
            vertsBuffer.GetData(verts);
            trisBuffer.GetData(tris);


            // Use Data
            meshes[lodIndex].Clear();
            meshes[lodIndex].SetVertices(verts);
            meshes[lodIndex].name = $"Chunk {chunk.gridPosition} LOD {lodIndex}";
            meshes[lodIndex].triangles = new int[chunk.lods[lodIndex].voxels.Length * 18];
            meshes[lodIndex].triangles = tris;
            meshes[lodIndex].RecalculateNormals();

            bounds = new Bounds(transform.position + (meshes[lodIndex].bounds.max / 2), (meshes[lodIndex].bounds.max - meshes[lodIndex].bounds.min));

            // Dispose all buffers
            chunkVoxelBuffer.Dispose();
            vertsBuffer.Dispose();
            trisBuffer.Dispose();

            Profiler.EndSample();
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            /*
            Gizmos.color = Color.blue;
            if (mesh) {
                foreach (Vector3 vert in mesh.vertices) {
                    Gizmos.DrawSphere(transform.position + vert, 0.1f);
                }
            }*/

        }

        void DrawBounds(Bounds b, float delay = 0)
        {
            // bottom
            var p1 = new Vector3(b.min.x, b.min.y, b.min.z);
            var p2 = new Vector3(b.max.x, b.min.y, b.min.z);
            var p3 = new Vector3(b.max.x, b.min.y, b.max.z);
            var p4 = new Vector3(b.min.x, b.min.y, b.max.z);

            Debug.DrawLine(p1, p2, Color.blue, delay);
            Debug.DrawLine(p2, p3, Color.red, delay);
            Debug.DrawLine(p3, p4, Color.yellow, delay);
            Debug.DrawLine(p4, p1, Color.magenta, delay);

            // top
            var p5 = new Vector3(b.min.x, b.max.y, b.min.z);
            var p6 = new Vector3(b.max.x, b.max.y, b.min.z);
            var p7 = new Vector3(b.max.x, b.max.y, b.max.z);
            var p8 = new Vector3(b.min.x, b.max.y, b.max.z);

            Debug.DrawLine(p5, p6, Color.blue, delay);
            Debug.DrawLine(p6, p7, Color.red, delay);
            Debug.DrawLine(p7, p8, Color.yellow, delay);
            Debug.DrawLine(p8, p5, Color.magenta, delay);

            // sides
            Debug.DrawLine(p1, p5, Color.white, delay);
            Debug.DrawLine(p2, p6, Color.gray, delay);
            Debug.DrawLine(p3, p7, Color.green, delay);
            Debug.DrawLine(p4, p8, Color.cyan, delay);
        }
    }
}
