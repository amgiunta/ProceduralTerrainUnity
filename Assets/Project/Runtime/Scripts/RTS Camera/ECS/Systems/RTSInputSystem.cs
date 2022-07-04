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
            
            float2 newTranslation = gameActions.Translation.ReadValue<Vector2>();
            var translationJob = Entities.
            WithBurst().
            ForEach((ref RTSInputTranslation translation, in RTSCameraAttributesComponent attributes) =>
            {
                translation.value = newTranslation;
            }).ScheduleParallel(Dependency);
            dependancies.Add(translationJob);


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
            if (selectTerciary.value)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            /*
            RTSInputPointer pointer = entityManager.GetComponentData<RTSInputPointer>(camEntity);
            float2 pointerScreenPoint = pointer.value;
            float3 pointerWorldPoint = new float3(main.ScreenToWorldPoint(new Vector3(pointerScreenPoint.x, pointerScreenPoint.y, main.nearClipPlane)));

            float3 pointDirection = math.normalize(pointerWorldPoint - new float3(main.transform.position));

            Debug.DrawRay(main.transform.position, pointDirection * 100, Color.red);
            */

            RTSInputPointer pointer = entityManager.GetComponentData<RTSInputPointer>(camEntity);
            RTSInputDelta delta = entityManager.GetComponentData<RTSInputDelta>(camEntity);
            float3 deltaWorldPoint = main.ScreenToWorldPoint(new Vector3(delta.value.x + (main.pixelWidth / 2), delta.value.y + (main.pixelHeight / 2), main.nearClipPlane));

            Debug.Log($"Delta world: {deltaWorldPoint}");

            Debug.DrawLine(main.transform.position, deltaWorldPoint, Color.red);

            Entities.
            WithBurst().
            ForEach((
                ref Rotation rotation, ref Translation translation, in LocalToWorld ltw, in RTSCameraAttributesComponent attributes,
                in RTSInputPointer rTSInputPointer, in RTSInputTranslation rTSInputTranslation,
                in RTSInputSelectSecondary secondary, in RTSInputSelectTerciary terciary
            ) => {
                if (terciary.value) {
                    float3 movement = - new float3(
                        translation.Value.x - deltaWorldPoint.x,
                        0,
                        translation.Value.z - deltaWorldPoint.z
                    );

                    //translation.Value = translation.Value - movement * attributes.movementSpeed * attributes.dampening;
                    translation.Value = math.lerp(translation.Value, translation.Value - movement, deltaTime * attributes.movementSpeed * attributes.dampening);
                }              


            }).ScheduleParallel();

            beginSimulationEntityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
