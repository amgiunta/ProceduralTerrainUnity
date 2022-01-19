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
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public class EntityCullSystem : SystemBase
    {
        protected EndFixedStepSimulationEntityCommandBufferSystem hideSystem;
        protected BeginFixedStepSimulationEntityCommandBufferSystem showSystem;
        protected World defaultWorld;
        protected EntityManager entityManager;

        protected Camera camera;

        protected override void OnCreate()
        {
            hideSystem = World.GetOrCreateSystem<EndFixedStepSimulationEntityCommandBufferSystem>();
            showSystem = World.GetOrCreateSystem<BeginFixedStepSimulationEntityCommandBufferSystem>();
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
        }

        protected override void OnStartRunning() {
            camera = Camera.main;
        }

        protected override void OnUpdate() {
            var hideBuffer = hideSystem.CreateCommandBuffer().AsParallelWriter();
            var showBuffer = showSystem.CreateCommandBuffer().AsParallelWriter();

            NativeArray<float4> unmanagedPlanes1 = new NativeArray<float4>(6, Allocator.Persistent);
            Unity.Rendering.FrustumPlanes.FromCamera(camera, unmanagedPlanes1);
            float3 camPosition = camera.transform.position;
            NativeArray<float4> unmanagedPlanes2 = new NativeArray<float4>(unmanagedPlanes1, Allocator.Persistent);


            var showHandle = Entities.WithBurst().WithAll<FrustumCull, RenderBounds, DisableRendering>().WithAll<DistanceCull>().WithNone<Prefab>().WithReadOnly(unmanagedPlanes1).WithDisposeOnCompletion(unmanagedPlanes1).ForEach((Entity e, int entityInQueryIndex, in WorldRenderBounds renderBounds, in DistanceCull distanceCull, in Translation translation) =>
            {
                float distance = math.distance(camPosition, translation.Value);
                if (distance > distanceCull.distance) { return; }
                Unity.Rendering.FrustumPlanes.IntersectResult intersectResult = Unity.Rendering.FrustumPlanes.Intersect(unmanagedPlanes1, renderBounds.Value);
                if (intersectResult != Unity.Rendering.FrustumPlanes.IntersectResult.Out && distance < distanceCull.distance)
                {
                    showBuffer.RemoveComponent<DisableRendering>(entityInQueryIndex, e);
                }
            }).ScheduleParallel(Dependency);

            var hideHandle = Entities.WithBurst().WithAll<FrustumCull, RenderBounds, DistanceCull>().WithNone<DisableRendering, Prefab>().WithReadOnly(unmanagedPlanes2).WithDisposeOnCompletion(unmanagedPlanes2).ForEach((Entity e, int entityInQueryIndex, in WorldRenderBounds renderBounds, in DistanceCull distanceCull, in Translation translation) =>
            {
                float distance = math.distance(camPosition, translation.Value);
                if (distance > distanceCull.distance) {
                    hideBuffer.AddComponent<DisableRendering>(entityInQueryIndex, e);
                    return;
                }

                Unity.Rendering.FrustumPlanes.IntersectResult intersectResult = Unity.Rendering.FrustumPlanes.Intersect(unmanagedPlanes2, renderBounds.Value);
                if (intersectResult == Unity.Rendering.FrustumPlanes.IntersectResult.Out) {
                    hideBuffer.AddComponent<DisableRendering>(entityInQueryIndex, e);
                }
            }).ScheduleParallel(Dependency);

            showSystem.AddJobHandleForProducer(showHandle);
            hideSystem.AddJobHandleForProducer(hideHandle);
            Dependency = JobHandle.CombineDependencies(showHandle, hideHandle);            

            /*
            Entities.WithBurst(Unity.Burst.FloatMode.Fast, Unity.Burst.FloatPrecision.Medium).WithEntityQueryOptions(EntityQueryOptions.Default | EntityQueryOptions.IncludeDisabled).WithAll<FrustumCull, RenderBounds, DistanceCull>().WithReadOnly(unmanagedPlanes).WithDisposeOnCompletion(unmanagedPlanes).ForEach((Entity e, int entityInQueryIndex, ref WorldRenderBounds renderBounds, ref DistanceCull distanceCull, ref Translation translation) =>
            {
                float distance = math.distance(camPosition, translation.Value);
                Unity.Rendering.FrustumPlanes.IntersectResult intersectResult = Unity.Rendering.FrustumPlanes.Intersect(unmanagedPlanes, renderBounds.Value);
                if (intersectResult == Unity.Rendering.FrustumPlanes.IntersectResult.Out || distance > distanceCull.distance)
                {
                    hideBuffer.AddComponent<Disabled>(entityInQueryIndex, e);
                }
                else {
                    showBuffer.RemoveComponent<Disabled>(entityInQueryIndex, e);
                }
            }).ScheduleParallel();
            */

        }
    }
}
