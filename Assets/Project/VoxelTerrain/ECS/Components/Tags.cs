using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

namespace VoxelTerrain.ECS.Components
{
    public struct ChunkInitialized : IComponentData { }
    public struct ScatterApplied : IComponentData { }
    public struct FaceCamera : IComponentData { }

    public struct ReadyToSpawn : IComponentData { }

    public struct FrustumCull : IComponentData { }

    public struct RenderInstanced : IComponentData { }
}
