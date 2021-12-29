using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Mathematics;

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

        private MeshFilter _meshFilter;
        private MeshCollider meshCollider;

        public void Awake()
        {
            gameCamera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
            meshRend = GetComponent<MeshRenderer>();
            
            meshCollider = GetComponent<MeshCollider>();
            meshes = new Dictionary<int, Mesh>();
        }

        public void LateUpdate()
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
            this.lodIndex = lodIndex;

            if (!meshes.ContainsKey(lodIndex))
            {
                if (GenerateChunkMesh(lodIndex))
                {
                    if (meshFilter.sharedMesh == meshes[lodIndex]) { return; }

                    meshFilter.sharedMesh = meshes[lodIndex];
                }
            }
            else {
                if (meshFilter.sharedMesh == meshes[lodIndex]) { return; }

                meshFilter.sharedMesh = meshes[lodIndex];
            }
        }

        public void SetChunk(Chunk chunk, bool reRender = true) {
            this.chunk = chunk;

            transform.position = new Vector3(chunk.gridPosition.x, 0f, chunk.gridPosition.y) * (chunk.grid.voxelSize * chunk.chunkWidth);

            if (meshCollider.sharedMesh == null && chunk.lods.ContainsKey(colliderLodIndex)) {
                GenerateColliderMesh();
            }
        }

        public bool GenerateChunkMesh(int lodIndex) {
            if (!chunk.lods.ContainsKey(lodIndex)) {
                return false;
            }

            Profiler.BeginSample("Generate Chunk Mesh");
            Profiler.BeginSample("Initialize Chunk Mesh");
            // Initializations
            int sizeVector3 = sizeof(float) * 3;
            int sizeVector2 = sizeof(float) * 2;
            int sizeVoxel = (sizeof(int) * 3) + (sizeVector3 * 4);


            if (!meshes.ContainsKey(lodIndex))
            {
                meshes.Add(lodIndex, new Mesh());
            }

            float voxelWidth = (chunk.grid.voxelSize * (chunk.chunkWidth / chunk.lods[lodIndex].width));

            float3[] verts = new float3[(chunk.lods[lodIndex].voxels.Length * 12) + (chunk.lods[lodIndex].width * 8)];
            int[] tris = new int[(chunk.lods[lodIndex].voxels.Length * 18) + (chunk.lods[lodIndex].width * 12)];
            float3[] normals = new float3[(chunk.lods[lodIndex].voxels.Length * 12) + (chunk.lods[lodIndex].width * 8)];
            float2[] uvs = new float2[(chunk.lods[lodIndex].voxels.Length * 12) + (chunk.lods[lodIndex].width * 8)];

            // Chunk data buffers
            ComputeBuffer chunkVoxelBuffer = new ComputeBuffer(chunk.lods[lodIndex].voxels.Length, sizeVoxel);
            chunkVoxelBuffer.SetData(chunk.lods[lodIndex].voxels);

            // Mesh data buffers
            ComputeBuffer vertsBuffer = new ComputeBuffer(verts.Length, sizeVector3);
            ComputeBuffer trisBuffer = new ComputeBuffer(tris.Length, sizeof(int));
            ComputeBuffer normalsBuffer = new ComputeBuffer(normals.Length, sizeVector3);
            ComputeBuffer uvsBuffer = new ComputeBuffer(uvs.Length, sizeVector2);
            vertsBuffer.SetData(verts);
            trisBuffer.SetData(tris);
            normalsBuffer.SetData(normals);
            uvsBuffer.SetData(uvs);

            meshGenerator.SetFloat("voxelSize", voxelWidth);
            meshGenerator.SetInt("chunkWidth", chunk.lods[lodIndex].width);

            meshGenerator.SetBuffer(0, "voxels", chunkVoxelBuffer);

            meshGenerator.SetBuffer(0, "verts", vertsBuffer);
            meshGenerator.SetBuffer(0, "tris", trisBuffer);
            meshGenerator.SetBuffer(0, "normals", normalsBuffer);
            meshGenerator.SetBuffer(0, "uvs", uvsBuffer);
            Profiler.EndSample();

            Profiler.BeginSample("Generate Mesh");
            // Run computation
            meshGenerator.Dispatch(0, chunk.lods[lodIndex].width / 8, chunk.lods[lodIndex].width / 8, 1);
            Profiler.EndSample();

            Profiler.BeginSample("Read Mesh Data");
            // Read back data
            vertsBuffer.GetData(verts);
            trisBuffer.GetData(tris);
            normalsBuffer.GetData(normals);
            uvsBuffer.GetData(uvs);

            // Use Data
            meshes[lodIndex].Clear();
            meshes[lodIndex].SetVertices(verts.ToVectorArray());
            meshes[lodIndex].name = $"Chunk {chunk.gridPosition} LOD {lodIndex}";
            meshes[lodIndex].triangles = new int[chunk.lods[lodIndex].voxels.Length * 18];
            meshes[lodIndex].triangles = tris;
            meshes[lodIndex].normals = normals.ToVectorArray();
            meshes[lodIndex].SetUVs(0, uvs.ToVectorArray());

            bounds = new Bounds(transform.position + (meshes[lodIndex].bounds.max / 2), (meshes[lodIndex].bounds.max - meshes[lodIndex].bounds.min));
            Profiler.EndSample();

            Profiler.BeginSample("Dispose Mesh Data");
            // Dispose all buffers
            chunkVoxelBuffer.Dispose();
            vertsBuffer.Dispose();
            trisBuffer.Dispose();
            normalsBuffer.Dispose();
            uvsBuffer.Dispose();
            Profiler.EndSample();

            Profiler.EndSample();
            return true;
        }

        public void GenerateColliderMesh() {
            Profiler.BeginSample("Generate Collider Mesh");
            Profiler.BeginSample("Initialize Collider Mesh");
            // Initializations
            int sizeVector3 = sizeof(float) * 3;
            int sizeVoxel = (sizeof(int) * 3) + (sizeVector3 * 4);

            float voxelWidth = (chunk.grid.voxelSize * (chunk.chunkWidth / chunk.lods[colliderLodIndex].width));

            float3[] verts = new float3[chunk.lods[colliderLodIndex].voxels.Length + (chunk.lods[colliderLodIndex].width * 2) + 1];
            int[] tris = new int[chunk.lods[colliderLodIndex].voxels.Length * 6];

            // Chunk data buffers
            ComputeBuffer chunkVoxelBuffer = new ComputeBuffer(chunk.lods[colliderLodIndex].voxels.Length, sizeVoxel);
            chunkVoxelBuffer.SetData(chunk.lods[colliderLodIndex].voxels);

            // Mesh data buffers
            ComputeBuffer vertsBuffer = new ComputeBuffer(verts.Length, sizeVector3);
            ComputeBuffer trisBuffer = new ComputeBuffer(tris.Length, sizeof(uint));
            vertsBuffer.SetData(verts);
            trisBuffer.SetData(tris);

            colliderGenerator.SetFloat("voxelSize", voxelWidth);
            colliderGenerator.SetInt("chunkWidth", chunk.lods[colliderLodIndex].width);

            colliderGenerator.SetBuffer(0, "voxels", chunkVoxelBuffer);
            colliderGenerator.SetBuffer(0, "verts", vertsBuffer);
            colliderGenerator.SetBuffer(0, "tris", trisBuffer);
            Profiler.EndSample();

            Profiler.BeginSample("Generate Collider");
            // Run computation
            colliderGenerator.Dispatch(0, chunk.lods[colliderLodIndex].width / 8, chunk.lods[colliderLodIndex].width / 8, 1);
            Profiler.EndSample();

            Profiler.BeginSample("Read Collider Data");
            // Read back data
            vertsBuffer.GetData(verts);
            trisBuffer.GetData(tris);

            // Use Data
            Mesh mesh = new Mesh();
            mesh.name = $"Chunk {chunk.gridPosition} Collider Mesh";
            mesh.SetVertices(verts.ToVectorArray());
            mesh.triangles = tris;

            meshCollider.sharedMesh = mesh;

            Profiler.EndSample();

            Profiler.BeginSample("Dispose Collider Data");
            // Dispose all buffers
            chunkVoxelBuffer.Dispose();
            vertsBuffer.Dispose();
            trisBuffer.Dispose();
            Profiler.EndSample();

            Profiler.EndSample();
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
