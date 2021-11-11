using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelTerrain {

    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(BoxCollider))]
    public class TerrainChunk : MonoBehaviour
    {
        public Chunk chunk;
        public ChunkLod currentLod;
        public int lodIndex = 0;
        public ComputeShader meshGenerator;

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
        new private BoxCollider collider {
            get {
                if (!_collider) {
                    _collider = GetComponent<BoxCollider>();
                }
                return _collider;
            }
        }

        public Vector3[] verts;
        public int[] tris;

        private Mesh _mesh;
        private BoxCollider _collider;
        private MeshFilter _meshFilter;
        private MeshCollider meshCollider;

        public void Awake()
        {
            meshRend = GetComponent<MeshRenderer>();
            
            meshFilter.sharedMesh = mesh;
            meshCollider = GetComponent<MeshCollider>();
        }

        public void ChangeLod(int lodIndex) {
            this.lodIndex = lodIndex;

            UpdateChunkMesh();
        }

        public void SetChunk(Chunk chunk, bool reRender = true) {
            this.chunk = chunk;

            mesh.name = $"chunk ( {chunk.gridPosition.x}, {chunk.gridPosition.y} )";
            meshFilter.sharedMesh = mesh;

            transform.position = new Vector3(chunk.gridPosition.x, 0f, chunk.gridPosition.y) * (chunk.grid.voxelSize * chunk.chunkWidth);
            collider.center = new Vector3((chunk.grid.voxelSize * chunk.chunkWidth), 0f, (chunk.grid.voxelSize * chunk.chunkWidth));
            collider.size = new Vector3((chunk.grid.voxelSize * chunk.chunkWidth), 0f, (chunk.grid.voxelSize * chunk.chunkWidth));

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
            //float voxelWidth = (chunk.grid.voxelSize / 2) * lodIndex + 1;

            Debug.Log($"Voxel width for lod {lodIndex}: {voxelWidth}", this);
            Debug.Log($"Voxels in lod {lodIndex}: {chunk.lods[lodIndex].voxels.Length}", this);

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
            Debug.Log($"Verts: {verts.Length} tris: {tris.Length}");
            

            // Use Data
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.triangles = new int[chunk.lods[lodIndex].voxels.Length * 18];
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            

            // Dispose all buffers
            chunkVoxelBuffer.Dispose();
            vertsBuffer.Dispose();
            trisBuffer.Dispose();
        }

        private void OnDrawGizmosSelected()
        {
            /*
            Gizmos.color = Color.blue;
            if (mesh) {
                foreach (Vector3 vert in mesh.vertices) {
                    Gizmos.DrawSphere(transform.position + vert, 0.1f);
                }
            }*/
            
        }
    }
}
