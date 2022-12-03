using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace VoxelTerrain.ECS.Components
{
    [Serializable]
    [GenerateAuthoringComponent]
    public struct ChunkParent : IComponentData
    {
        public int2 gridPosition;
        public Grid grid;
    }
}
