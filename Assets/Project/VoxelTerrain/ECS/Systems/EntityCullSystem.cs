using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Jobs;
using VoxelTerrain.ECS.Components;

namespace VoxelTerrain.ECS.Systems
{
    public class MeshCullSystem : SystemBase
    {
        protected EndInitializationEntityCommandBufferSystem endInitializationEntityCommandBufferSystem;
        protected World defaultWorld;
        protected EntityManager entityManager;

        protected Camera camera;

        protected override void OnCreate()
        {
            endInitializationEntityCommandBufferSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
        }

        protected override void OnStartRunning() {
            camera = Camera.main;
        }

        protected override void OnUpdate() {
            var firstPassBuffer = endInitializationEntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();

            NativeArray<float4> unmanagedPlanes = new NativeArray<float4>(6, Allocator.Persistent);
            Unity.Rendering.FrustumPlanes.FromCamera(camera, unmanagedPlanes);

            Entities.WithBurst().WithAll<FrustumCull, RenderBounds>().WithNone<Disabled>().WithReadOnly(unmanagedPlanes).ForEach((Entity e, int entityInQueryIndex, ref WorldRenderBounds renderBounds) =>
            {
                Unity.Rendering.FrustumPlanes.IntersectResult intersectResult = Unity.Rendering.FrustumPlanes.Intersect(unmanagedPlanes, renderBounds.Value);
                if (intersectResult == Unity.Rendering.FrustumPlanes.IntersectResult.Out) { 
                    firstPassBuffer.AddComponent<Disabled>(entityInQueryIndex, e);
                }
            }).ScheduleParallel();

            Entities.WithBurst().WithAll<FrustumCull, RenderBounds, Disabled>().WithReadOnly(unmanagedPlanes).WithDisposeOnCompletion(unmanagedPlanes).ForEach((Entity e, int entityInQueryIndex, ref WorldRenderBounds renderBounds) =>
            {
                Unity.Rendering.FrustumPlanes.IntersectResult intersectResult = Unity.Rendering.FrustumPlanes.Intersect(unmanagedPlanes, renderBounds.Value);
                if (intersectResult != Unity.Rendering.FrustumPlanes.IntersectResult.Out)
                {
                    firstPassBuffer.RemoveComponent<Disabled>(entityInQueryIndex, e);
                }
            }).ScheduleParallel();

            endInitializationEntityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
