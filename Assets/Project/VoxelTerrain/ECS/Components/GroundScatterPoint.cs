using Unity.Entities;
using Unity.Mathematics;

namespace VoxelTerrain.ECS.Components
{
    public struct GroundScatterPoint : IComponentData
    {
        public int scatterID;
        public float3 position;
        public float2 climate;
    }
}
