using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelTerrain
{
    namespace ECS
    {
        namespace Components
        {
            public struct ChunkComponent : IComponentData
            {
                public Grid grid;
                public int2 gridPosition;
            }
        }
    }
}
