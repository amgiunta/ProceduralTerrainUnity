using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace VoxelTerrain.ECS.Components{
    [GenerateAuthoringComponent]
    [Serializable]
    public struct VoxelTerrainAutoDisableTag : IComponentData {}
}
