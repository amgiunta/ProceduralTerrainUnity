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

        public void SetChunk(Chunk chunk) {
            this.chunk = chunk;

            mesh.name = $"chunk ( {chunk.gridPosition.x}, {chunk.gridPosition.y} )";
            meshFilter.sharedMesh = mesh;

            transform.position = new Vector3(chunk.gridPosition.x, 0f, chunk.gridPosition.y) * (chunk.grid.voxelSize / 2 * chunk.chunkWidth);
            collider.center = new Vector3((chunk.grid.voxelSize / 2 * chunk.chunkWidth) / 2, 0f, (chunk.grid.voxelSize / 2 * chunk.chunkWidth) / 2);
            collider.size = new Vector3((chunk.grid.voxelSize / 2 * chunk.chunkWidth), 0f, (chunk.grid.voxelSize / 2 * chunk.chunkWidth));

            if (meshCollider != null) {
                meshCollider.sharedMesh = mesh;
            }

            UpdateChunkMesh();
        }

        public void UpdateChunkMesh() {
            // Initializations
            int sizeVector3 = sizeof(float) * 3;
            int sizeVoxel = sizeof(int) * 3;

            Vector3[] verts = new Vector3[chunk.voxels.Length * 12];
            int[] tris = new int[chunk.voxels.Length * 18];

            // Chunk data buffers
            ComputeBuffer chunkVoxelBuffer = new ComputeBuffer(chunk.voxels.Length, sizeVoxel);
            ComputeBuffer leftEdgeVoxelBuffer = new ComputeBuffer(chunk.leftEdge.Length, sizeVoxel);
            ComputeBuffer bottomEdgeVoxelBuffer = new ComputeBuffer(chunk.bottomEdge.Length, sizeVoxel);
            chunkVoxelBuffer.SetData(chunk.voxels);
            leftEdgeVoxelBuffer.SetData(chunk.leftEdge);
            bottomEdgeVoxelBuffer.SetData(chunk.bottomEdge);

            // Mesh data buffers
            ComputeBuffer vertsBuffer = new ComputeBuffer(verts.Length, sizeVector3);
            ComputeBuffer trisBuffer = new ComputeBuffer(tris.Length, sizeof(int));
            vertsBuffer.SetData(verts);
            trisBuffer.SetData(tris);

            meshGenerator.SetFloat("voxelSize", chunk.grid.voxelSize / 2 );
            meshGenerator.SetInt("chunkWidth", chunk.chunkWidth);

            meshGenerator.SetBuffer(0, "voxels", chunkVoxelBuffer);
            meshGenerator.SetBuffer(0, "leftEdgeVoxels", leftEdgeVoxelBuffer);
            meshGenerator.SetBuffer(0, "bottomEdgeVoxels", bottomEdgeVoxelBuffer);

            meshGenerator.SetBuffer(0, "verts", vertsBuffer);
            meshGenerator.SetBuffer(0, "tris", trisBuffer);

            // Run computation
            meshGenerator.Dispatch(0, chunk.chunkWidth / 8, chunk.chunkWidth / 8, 1);

            // Read back data
            vertsBuffer.GetData(verts);
            trisBuffer.GetData(tris);
            Debug.Log($"Verts: {verts.Length} tris: {tris.Length}");
            
            /*
            for (int ti = 0; ti < tris.Length; ti++)
            {
                int i = tris[ti];
                try {
                    Debug.Log($"Vertex Index {i % 12} at tri {ti} is valid and point to {verts[i]}.");
                }
                catch {
                    Debug.LogError($"Index {i} at tri {ti} is not valid.");
                    break;
                }
            }
            */
            

            // Use Data
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.triangles = new int[chunk.voxels.Length * 18];
            mesh.triangles = tris;
            mesh.RecalculateNormals();

            

            // Dispose all buffers
            chunkVoxelBuffer.Dispose();
            leftEdgeVoxelBuffer.Dispose();
            bottomEdgeVoxelBuffer.Dispose();
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
            }
            */
        }
    }
}
