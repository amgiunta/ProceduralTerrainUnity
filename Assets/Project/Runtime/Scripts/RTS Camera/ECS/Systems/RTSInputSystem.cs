using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

using UnityEngine;

using RTSCamera.Components;
using RTSCamera.Input;

namespace RTSCamera.Systems
{
    public partial class RTSInputComponentUpdateSystem : SystemBase
    {
        private RTSInput input;
        private RTSInput.GameActions gameActions;

        private BeginInitializationEntityCommandBufferSystem beginInitializationEntityCommandBufferSystem;
        private World defaultWorld;
        private EntityManager entityManager;

        protected override void OnCreate()
        {
            base.OnCreate();
            input = new RTSInput();
            input.Enable();

            gameActions = input.Game;

            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
            beginInitializationEntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            var attributeBuffer = beginInitializationEntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();


            NativeList<JobHandle> dependancies = new NativeList<JobHandle>(0, Allocator.Temp);
            /*
            float2 newTranslation = gameActions.Translation.ReadValue<Vector2>();
            var translationJob = Entities.
            WithBurst().
            ForEach((ref RTSInputTranslation translation, in RTSCameraAttributesComponent attributes) =>
            {
                translation.value = newTranslation;
            }).ScheduleParallel(Dependency);
            dependancies.Add(translationJob);
            */

            float2 newPointer = gameActions.Point.ReadValue<Vector2>();
            var pointerJob = Entities.
            WithBurst().
            ForEach((ref RTSInputPointer pointer, in RTSCameraAttributesComponent attributes) =>
            {
                pointer.value = newPointer;
            }).ScheduleParallel(Dependency);
            dependancies.Add(pointerJob);

            float2 newDelta = gameActions.Delta.ReadValue<Vector2>();
            var deltaJob = Entities.
            WithBurst().
            ForEach((ref RTSInputDelta delta, in RTSCameraAttributesComponent attributes) =>
            {
                delta.value = newDelta;
            }).ScheduleParallel(Dependency);
            dependancies.Add(deltaJob);

            bool newPrimary = gameActions.SelectPrimary.ReadValue<float>() > 0 ? true : false;            
            var primaryJob = Entities.
            WithBurst().
            ForEach((ref RTSInputSelectPrimary primary) => {
                primary.value = newPrimary;
            }).ScheduleParallel(Dependency);
            dependancies.Add(primaryJob);

            bool newSecondary = gameActions.SelectSecondary.ReadValue<float>() > 0 ? true : false;
            var secondaryJob = Entities.
            WithBurst().
            ForEach((ref RTSInputSelectSecondary secondary) => {
                secondary.value = newSecondary;
            }).ScheduleParallel(Dependency);
            dependancies.Add(secondaryJob);

            bool newTerciary = gameActions.SelectTerciary.ReadValue<float>() > 0 ? true : false;
            var terciaryJob = Entities.
            WithBurst().
            ForEach((ref RTSInputSelectTerciary terciary) => {
                terciary.value = newTerciary;
            }).ScheduleParallel(Dependency);
            dependancies.Add(terciaryJob);

            float newZoom = gameActions.Zoom.ReadValue<Vector2>().y;
            var zoomJob = Entities.
            WithBurst().
            ForEach((ref RTSInputZoom zoom) =>
            {
                zoom.value = newZoom;
            }).ScheduleParallel(Dependency);
            dependancies.Add(zoomJob);

            Dependency = JobHandle.CombineDependencies(dependancies);
            beginInitializationEntityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }

    [UpdateAfter(typeof(RTSInputComponentUpdateSystem))]
    public partial class RTSInputReactionSystem : SystemBase {

        private BeginSimulationEntityCommandBufferSystem beginSimulationEntityCommandBufferSystem;
        private World defaultWorld;
        private EntityManager entityManager;

        protected override void OnCreate()
        {
            base.OnCreate();
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;

            beginSimulationEntityCommandBufferSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            base.OnCreate();
            var simEcb = beginSimulationEntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();

            float deltaTime = Time.DeltaTime;
            Entity camEntity = GetSingletonEntity<RTSInputPointer>();
            Camera main = Camera.main;

            RTSInputSelectTerciary selectTerciary = entityManager.GetComponentData<RTSInputSelectTerciary>(camEntity);
            RTSInputSelectSecondary selectSecondary = entityManager.GetComponentData<RTSInputSelectSecondary>(camEntity);
            if (selectTerciary.value || selectSecondary.value)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            RTSInputPointer pointer = entityManager.GetComponentData<RTSInputPointer>(camEntity);
            RTSInputDelta delta = entityManager.GetComponentData<RTSInputDelta>(camEntity);
            float2 deltaValue = new float2(
                (delta.value.x / main.pixelWidth),
                (delta.value.y / main.pixelHeight)
            );
            float3 forward = main.transform.forward;
            float3 pointerWorldPoint = main.ScreenToWorldPoint(new float3(pointer.value.x, pointer.value.y, main.nearClipPlane));
            float3 pointerDirection = math.normalize(pointerWorldPoint - new float3(main.transform.position));
            float3 projectedForward = new float3(
                main.transform.forward.x,
                0,
                main.transform.forward.z
            );
            float3 euler = main.transform.rotation.eulerAngles;

            Entities.
            WithBurst().
            ForEach((
                ref Rotation rotation, ref Translation translation, in LocalToWorld ltw, in RTSCameraAttributesComponent attributes,
                in RTSInputTranslation rTSInputTranslation,
                in RTSInputSelectSecondary secondary, in RTSInputSelectTerciary terciary, in RTSInputZoom zoom
            ) => {
                if (terciary.value)
                {
                    quaternion proj = quaternion.LookRotation(projectedForward, new float3(0, 1, 0));
                    float3 movement = new float3(
                        deltaValue.x,
                        0,
                        deltaValue.y
                    );

                    translation.Value = translation.Value - (math.mul(proj, movement * 2)) * (translation.Value.y / 10);
                }
                else if (secondary.value) {
                    float xAngle = math.radians(math.clamp(euler.x + deltaValue.y * -15, 0, 89));
                    float yAngle = math.radians(euler.y + deltaValue.x * 15);
                    rotation.Value = quaternion.EulerXYZ(xAngle, yAngle, 0);


                }

                float targetHeight = translation.Value.y - zoom.value;

                translation.Value = math.lerp(translation.Value, new float3(translation.Value.x, targetHeight, translation.Value.z), attributes.zoomSensetivity * deltaTime * attributes.dampening);
            }).ScheduleParallel();

            beginSimulationEntityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
