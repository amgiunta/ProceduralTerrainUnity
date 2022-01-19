using Unity.Entities;
using Unity.Mathematics;

namespace VoxelTerrain.ECS.Components
{
    public struct DistanceCull : IComponentData
    {
        public float distance;
    }
}
