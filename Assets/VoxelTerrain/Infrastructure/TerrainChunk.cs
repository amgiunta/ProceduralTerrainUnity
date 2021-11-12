using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelTerrain {

    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshCollider))]
    public class TerrainChunk : MonoBehaviour
    {
        public Chunk chunk;
        public ChunkLod currentLod;
        public int lodIndex = 0;
        public ComputeShader meshGenerator;

        [HideInInspector]
        public bool visible = false;

        private Mesh mesh {
            get {
                if (!_mesh) {
                    _mesh = new Mesh();
                    _mesh.name = "chunk";
                }

                return _mesh;
            }
        }
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

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshCollider meshCollider;

        public void Awake()
        {
            gameCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
            meshRend = GetComponent<MeshRenderer>();
            
            meshFilter.sharedMesh = mesh;
            meshCollider = GetComponent<MeshCollider>();
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
                Debug.Log("Could not find camera");
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
            if (this.lodIndex == lodIndex) { return; }

            this.lodIndex = lodIndex;

            UpdateChunkMesh();
        }

        public void SetChunk(Chunk chunk, bool reRender = true) {
            this.chunk = chunk;

            meshFilter.sharedMesh = mesh;

            transform.position = new Vector3(chunk.gridPosition.x, 0f, chunk.gridPosition.y) * (chunk.grid.voxelSize * chunk.chunkWidth);

            if (meshCollider != null) {
                meshCollider.sharedMesh = mesh;
            }

            if (reRender)
                UpdateChunkMesh();
        }

        public void UpdateChunkMesh() {
            // Initializations
            int sizeVector3 = sizeof(float) * 3;
            int sizeVoxel = sizeof(int) * 3;

            int lodIndex = this.lodIndex;
            if (chunk.lods.Count <= lodIndex) {
                Debug.LogWarning($"Lod index {lodIndex} does not exist. Using lowest lod", this);
                lodIndex = chunk.lods.Count - 1;
            }

            currentLod = chunk.lods[lodIndex];

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
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.triangles = new int[chunk.lods[lodIndex].voxels.Length * 18];
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            bounds = new Bounds(transform.position + (mesh.bounds.max / 2), (mesh.bounds.max - mesh.bounds.min));

            // Dispose all buffers
            chunkVoxelBuffer.Dispose();
            vertsBuffer.Dispose();
            trisBuffer.Dispose();
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
