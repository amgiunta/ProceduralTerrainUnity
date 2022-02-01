using UnityEngine;
using UnityEngine.Profiling;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Collections;
using Unity.Rendering.HybridV2;
using Unity.Jobs;
using VoxelTerrain.ECS.Components;

namespace VoxelTerrain.ECS.Systems {
    public class GroundScatterSystem<Component, Author> : SystemBase where Component : struct, IGroundScatter where Author : GroundScatterAuthor {
        public static GroundScatterSystem<Component, Author> instance;

        private int updateFrames = 50;
        private int lastExecution = 0;
        private int frame = 0;

        protected BeginPresentationEntityCommandBufferSystem ecbSystem;
        protected World defaultWorld;
        protected EntityManager entityManager;
        protected Biome[] biomes;

        protected Component groundScatterData;
        protected Author groundScatterAuthor;
        protected DistanceCullAuthor distanceCullAuthor;

        protected Entity prefab;

        protected EntityArchetype NewGroundScatterArchetype()
        {
            if (entityManager == null) { return default; }
            return entityManager.CreateArchetype(
                typeof(Component),
                typeof(Translation),
                typeof(Rotation),
                typeof(Scale),
                typeof(LocalToWorld),
                typeof(RenderMesh),
                typeof(RenderBounds),
                typeof(ReadyToSpawn),
                typeof(FrustumCull),
                typeof(DistanceCull),
                typeof(DisableRendering)
            );
        }

        protected override void OnCreate()
        {
            instance = this;
            ecbSystem = World.GetOrCreateSystem<BeginPresentationEntityCommandBufferSystem>();
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;

            prefab = entityManager.CreateEntity(NewGroundScatterArchetype());
            entityManager.AddComponent<Prefab>(prefab);
        }

        protected override void OnStartRunning()
        {
            groundScatterAuthor = TerrainManager.instance.groundScatter.OfType<Author>().FirstOrDefault();
            //Debug.Log(groundScatterAuthor.GetType());
            if (groundScatterAuthor == null) { Enabled = false; return; }
            distanceCullAuthor = groundScatterAuthor.GetComponent<DistanceCullAuthor>();

            DistanceCull distanceCull = new DistanceCull
            {
                distance = distanceCullAuthor.distance
            };

            Debug.Log($"Mesh: {groundScatterAuthor.mesh}");

            RenderMesh meshData = new RenderMesh {
                mesh = groundScatterAuthor.mesh,
                material = groundScatterAuthor.material,
                castShadows = UnityEngine.Rendering.ShadowCastingMode.On,
                receiveShadows = true
            };
            Unity.Mathematics.AABB aabb = new AABB();
            aabb.Center = meshData.mesh.bounds.center;
            aabb.Extents = meshData.mesh.bounds.extents;
            RenderBounds renderBounds = new RenderBounds
            {
                Value = aabb
            };

            entityManager.SetSharedComponentData(prefab, meshData);
            entityManager.SetComponentData(prefab, renderBounds);
            entityManager.SetComponentData(prefab, distanceCull);
            entityManager.SetComponentData(prefab, groundScatterData);

            biomes = new Biome[TerrainManager.instance.biomes.Count];
            for (int i = 0; i < biomes.Length; i++) {
                biomes[i] = TerrainManager.instance.biomes[i];
            }
        }

        protected override void OnUpdate()
        {
            if ((frame - lastExecution > updateFrames || frame == 0) || Dependency.IsCompleted) {
                lastExecution = frame;
                SpawnEntities();
            }
            frame++;
        }

        protected virtual void SpawnEntities() { 
            
        }
    }

    [DisableAutoCreation]
    public class GroundScatterPlotSystem : SystemBase {
        protected BeginInitializationEntityCommandBufferSystem removalSystem;
        protected EndInitializationEntityCommandBufferSystem spawnSystem;
        protected BeginSimulationEntityCommandBufferSystem simSystem;
        protected World defaultWorld;
        protected EntityManager entityManager;

        protected Biome[] biomes;

        protected override void OnCreate()
        {
            removalSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
            spawnSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
            simSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
        }

        protected override void OnStartRunning()
        {
            biomes = new Biome[TerrainManager.instance.biomes.Count];
            int count = 0;
            foreach (Biome biome in TerrainManager.instance.biomes)
            {
                biomes[count] = biome;
                count++;
            }
        }

        protected virtual EntityArchetype GetPointArchetype() {
            return entityManager.CreateArchetype(
                typeof(GroundScatterPoint),
                typeof(GroundScatter)
            );
        }

        protected virtual Entity GetNearestChunk(out ChunkComponent chunkComponent, out Translation chunkTranslation) {
            Camera cam = Camera.main;

            EntityQueryDesc chunkQueryDesc = new EntityQueryDesc();
            chunkQueryDesc.All = new ComponentType[1] {typeof(ChunkComponent) };
            chunkQueryDesc.None = new ComponentType[1] { typeof(ScatterApplied) };
            EntityQuery chunkQuery = this.GetEntityQuery(chunkQueryDesc);

            NativeArray<Entity> chunks = chunkQuery.ToEntityArray(Allocator.Temp);

            float closestDistance = float.MaxValue;
            Entity closestChunkEntity = default;
            ChunkComponent closestChunk = default;
            Translation closestTranslation = default;

            foreach (Entity chunk in chunks) {
                Translation translation = entityManager.GetComponentData<Translation>(chunk);
                float distance = math.distance(translation.Value, cam.transform.position);

                if (distance >= closestDistance) { continue; }

                closestDistance = distance;
                closestTranslation = translation;
                closestChunk = entityManager.GetComponentData<ChunkComponent>(chunk);
                closestChunkEntity = chunk;
            }

            chunkComponent = closestChunk;
            chunkTranslation = closestTranslation;

            chunks.Dispose();

            return closestChunkEntity;
        }

        protected override void OnUpdate()
        {
            uint frame = (uint) UnityEngine.Time.frameCount;
            if (frame == 0) { return; }

            ChunkComponent closestChunk = default;
            Translation closestChunkTranslation = default;
            Entity closestChunkEntity = GetNearestChunk(out closestChunk, out closestChunkTranslation);
            if (closestChunkEntity == default) { return; }

            var secb = spawnSystem.CreateCommandBuffer().AsParallelWriter();
            var recb = removalSystem.CreateCommandBuffer().AsParallelWriter();
            var simecb = simSystem.CreateCommandBuffer().AsParallelWriter();
            ClimateSettings climateSettings = TerrainManager.instance.terrainSettings;
            int terrainSeed = TerrainManager.instance.terrainSettings.seed;
            NativeList<GroundScatter> groundScatters = new NativeList<GroundScatter>(Allocator.TempJob);
            EntityArchetype pointArchetype = GetPointArchetype();
            NativeArray<Biome> nativeBiomes = new NativeArray<Biome>(biomes, Allocator.TempJob);

            foreach (var scatterAuthor in TerrainManager.instance.groundScatter) {
                groundScatters.Add(scatterAuthor.GetComponentData());
            }

            Job.
            WithReadOnly(groundScatters).WithDisposeOnCompletion(groundScatters).
            WithReadOnly(nativeBiomes).WithDisposeOnCompletion(nativeBiomes).
            WithBurst().WithCode(() =>
            {
                for (int i = 0; i < groundScatters.Length; i++)
                {
                    GroundScatter scatter = groundScatters[i];
                    uint processSeed = frame;
                    Unity.Mathematics.Random rng = new Unity.Mathematics.Random(processSeed);

                    for (int n = 0; n < scatter.scatterDensity; n++)
                    {
                        float x = rng.NextFloat(0, closestChunk.grid.chunkSize);
                        processSeed += (uint)n * 1000;
                        rng = new Unity.Mathematics.Random(processSeed);
                        float z = rng.NextFloat(0, closestChunk.grid.chunkSize);

                        float2 climate = TerrainNoise.Climate(x, z, climateSettings, closestChunk.gridPosition, closestChunk.grid.chunkSize, terrainSeed);
                        float climateIdealness = TerrainNoise.ClimateIdealness(new float2(scatter.minTemperature, scatter.minMoisture), new float2(scatter.maxTemperature, scatter.maxMoisture), climate);
                        if (rng.NextFloat(0, 1) > climateIdealness) { return; }

                        float height = TerrainNoise.GetHeightAtPoint(x, z, climate, nativeBiomes, 1, closestChunk.gridPosition, closestChunk.grid.chunkSize, terrainSeed);
                        if (height > scatter.maxHeight) { return; }

                        float3 position = new float3(closestChunkTranslation.Value.x + x, height, closestChunkTranslation.Value.z + z);

                        GroundScatterPoint point = new GroundScatterPoint();
                        point.climate = climate;
                        point.position = position;
                        point.scatterID = i;

                        Entity scatterPoint = simecb.CreateEntity(n * i, pointArchetype);
                        simecb.SetComponent(n * i, scatterPoint, groundScatters[i]);
                        simecb.SetComponent(n * i, scatterPoint, point);
                    }
                }

                recb.AddComponent<ScatterApplied>(0, closestChunkEntity);
            }).Schedule();

            /*
            Entities.
            WithAll<ChunkComponent>().WithNone<ScatterApplied>().
            WithReadOnly(groundScatters).WithDisposeOnCompletion(groundScatters).
            WithReadOnly(nativeBiomes).WithDisposeOnCompletion(nativeBiomes).
            ForEach((Entity e, int entityInQueryIndex, in ChunkComponent chunkComponent) =>
            {
                
            }).ScheduleParallel();
            */

            //spawnSystem.AddJobHandleForProducer(Dependency);
            removalSystem.AddJobHandleForProducer(Dependency);
            simSystem.AddJobHandleForProducer(Dependency);
        }
    }

    public class GenericGroundScatterSystem : SystemBase {
        protected List<GroundScatterLods> prefabs;

        protected EndInitializationEntityCommandBufferSystem ecbSystem;
        protected World defaultWorld;
        protected EntityManager entityManager;
        protected Biome[] biomes;
        protected ClimateSettings climateSettings;
        protected int terrainSeed;

        public class LODComparer : IComparer {
            int IComparer.Compare(object x, object y)
            {
                MeshLODAuthor lodX = (MeshLODAuthor) x;
                MeshLODAuthor lodY = (MeshLODAuthor) y;
                if (lodX.renderOrder < lodY.renderOrder) {
                    return -1;
                }
                if (lodX.renderOrder > lodY.renderOrder)
                {
                    return 1;
                }
                else { return 0; }
            }
        }
        public struct GroundScatterLods {
            public float4 distances;
            public Entity[] prefabs;
            public GroundScatter groundScatter;
        }

        protected override void OnCreate() {
            prefabs = new List<GroundScatterLods>();
            ecbSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;
        }

        protected override void OnStartRunning()
        {
            climateSettings = TerrainManager.instance.terrainSettings;
            terrainSeed = TerrainManager.instance.terrainSettings.seed;
            //prefabs = new List<List<Prefab>>();
            biomes = new Biome[TerrainManager.instance.biomes.Count];
            int count = 0;
            foreach (Biome biome in TerrainManager.instance.biomes) {
                biomes[count] = biome;
                count++;
            }
            
            foreach (GroundScatterAuthor gsa in TerrainManager.instance.groundScatter) {
                AddGroundScatterPrefabs(gsa);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            //prefabs.Clear();
        }

        protected virtual Entity GetNearestChunk(out ChunkComponent chunkComponent, out Translation chunkTranslation)
        {
            Camera cam = Camera.main;

            EntityQueryDesc chunkQueryDesc = new EntityQueryDesc();
            chunkQueryDesc.All = new ComponentType[1] { typeof(ChunkComponent) };
            chunkQueryDesc.None = new ComponentType[1] { typeof(ScatterApplied) };
            EntityQuery chunkQuery = this.GetEntityQuery(chunkQueryDesc);

            NativeArray<Entity> chunks = chunkQuery.ToEntityArray(Allocator.Temp);

            float closestDistance = float.MaxValue;
            Entity closestChunkEntity = default;
            ChunkComponent closestChunk = default;
            Translation closestTranslation = default;

            foreach (Entity chunk in chunks)
            {
                Translation translation = entityManager.GetComponentData<Translation>(chunk);
                float distance = math.distance(translation.Value, cam.transform.position);

                if (distance >= closestDistance) { continue; }

                closestDistance = distance;
                closestTranslation = translation;
                closestChunk = entityManager.GetComponentData<ChunkComponent>(chunk);
                closestChunkEntity = chunk;
            }

            chunkComponent = closestChunk;
            chunkTranslation = closestTranslation;

            chunks.Dispose();

            return closestChunkEntity;
        }
        protected EntityArchetype NewGroundScatterArchetype()
        {
            if (entityManager == null) { return default; }
            return entityManager.CreateArchetype(
                typeof(GroundScatter),
                typeof(Translation),
                typeof(Rotation),
                typeof(Scale),
                typeof(LocalToWorld),
                typeof(RenderMesh),
                typeof(RenderBounds),
                typeof(MeshLODComponent),
                typeof(PerInstanceCullingTag)
            );
        }

        private void AddGroundScatterPrefabs (GroundScatterAuthor author) {
            if (!author) { return; }

            MeshLODAuthor[] meshLODs = author.GetComponents<MeshLODAuthor>();
            List<Entity> lodPrefabs = new List<Entity>();
            EntityArchetype archetype = NewGroundScatterArchetype();
            GroundScatter groundScatter = author.GetComponentData();
            Array.Sort(meshLODs, new LODComparer());
            //Array.Reverse(meshLODs);

            float[] distanceArray = new float[4] { 0, 0, 0, 0 };

            int count = 0;
            foreach (MeshLODAuthor meshLOD in meshLODs) {
                if (count == 4) { break; }

                Entity entity = entityManager.CreateEntity(archetype);
                RenderMesh meshData = new RenderMesh();
                MeshLODComponent lod = new MeshLODComponent();
                meshData.mesh = meshLOD.mesh;
                meshData.material = meshLOD.material;

                RenderBounds bounds = new RenderBounds();
                bounds.Value.Center = meshData.mesh.bounds.center;
                bounds.Value.Extents = meshData.mesh.bounds.extents;

                distanceArray[count] = meshLOD.distance;
                entityManager.AddComponent<Prefab>(entity);
                entityManager.SetSharedComponentData(entity, meshData);
                entityManager.SetComponentData(entity, bounds);
                entityManager.SetComponentData(entity, groundScatter);
                entityManager.SetComponentData(entity, lod);
                lodPrefabs.Add(entity);
                count++;
            }

            prefabs.Add(new GroundScatterLods { distances = new float4(distanceArray[0], distanceArray[1], distanceArray[2], distanceArray[3]), prefabs = lodPrefabs.ToArray(), groundScatter = groundScatter });
        }

        protected override void OnUpdate()
        {
            uint frame = (uint)UnityEngine.Time.frameCount;
            if (frame == 0) { return; }

            Translation closestChunkTranslation = default;
            ChunkComponent closestChunk = default;
            Entity closestChunkEntity = GetNearestChunk(out closestChunk, out closestChunkTranslation);
            if (closestChunkEntity == default) { return; }

            NativeArray<JobHandle> jobs = new NativeArray<JobHandle>(TerrainManager.instance.groundScatter.Count, Allocator.Temp);
            
            EntityArchetype lodGroupArchetype = entityManager.CreateArchetype(
                typeof(MeshLODGroupComponent),
                typeof(ActiveLODGroupMask),
                typeof(LocalToWorld),
                typeof(Translation)
            );

            int count = 0;
            foreach (var scatterPrefab in prefabs)
            {
                var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();
                GroundScatterLods gsl = scatterPrefab;
                GroundScatter groundScatter = scatterPrefab.groundScatter;
                ClimateSettings localClimateSettings = climateSettings;
                int localTerrainSeed = terrainSeed;
                NativeArray<Entity> lodPrefabs = new NativeArray<Entity>(gsl.prefabs, Allocator.TempJob);
                NativeArray<Biome> nativeBiomes = new NativeArray<Biome>(biomes, Allocator.TempJob);
                float4 distances = gsl.distances;

                var spawnJob = Job.WithBurst().
                WithReadOnly(lodPrefabs).WithDisposeOnCompletion(lodPrefabs).
                WithReadOnly(nativeBiomes).WithDisposeOnCompletion(nativeBiomes).
                WithCode(() =>
                {
                    uint processSeed = frame;
                    Unity.Mathematics.Random rng = new Unity.Mathematics.Random(processSeed + (uint) count);

                    for (int i = 0; i < groundScatter.scatterDensity; i++)
                    {
                        float x = math.lerp(0, closestChunk.grid.chunkSize, rng.NextFloat(0, 1));
                        processSeed += (uint) count + (uint) i * 100;
                        rng = new Unity.Mathematics.Random(processSeed);
                        float z = math.lerp(0, closestChunk.grid.chunkSize, rng.NextFloat(0, 1));

                        float2 climate = TerrainNoise.Climate(x, z, localClimateSettings, closestChunk.gridPosition, closestChunk.grid.chunkSize, localTerrainSeed);
                        
                        float climateIdealness = TerrainNoise.ClimateIdealness(new float2(groundScatter.minTemperature, groundScatter.minMoisture), new float2(groundScatter.maxTemperature, groundScatter.maxMoisture), climate, groundScatter.heartiness);
                        if (rng.NextFloat(0, 1) > climateIdealness) { return; }

                        float height = TerrainNoise.GetHeightAtPoint(x, z, climate, nativeBiomes, 1, closestChunk.gridPosition, closestChunk.grid.chunkSize, localTerrainSeed);
                        if (height > groundScatter.maxHeight || height < groundScatter.minHeight) { return; }

                        float3 position = new float3(x, height, z);

                        MeshLODGroupComponent lodGroup = new MeshLODGroupComponent();
                        lodGroup.LODDistances0 = distances;
                        lodGroup.LODDistances1 = distances;
                        lodGroup.LocalReferencePoint = float3.zero;

                        float3 worldPosition = new float3(closestChunkTranslation.Value.x + (position.x * closestChunk.grid.voxelSize), height, closestChunkTranslation.Value.z + (position.z * closestChunk.grid.voxelSize));

                        Entity groupLodEntity = ecb.CreateEntity(i, lodGroupArchetype);
                        ecb.SetComponent(i, groupLodEntity, lodGroup);
                        ecb.SetComponent(i, groupLodEntity, new Translation { Value = worldPosition });


                        for (int n = 0; n < lodPrefabs.Length; n++)
                        {
                            Entity scatterEntity = ecb.Instantiate(i+n, lodPrefabs[n]);
                            int hexLodMask = 0x08;

                            if (n == 0) { hexLodMask = 0x01; }
                            if (n == 1) { hexLodMask = 0x02; }
                            if (n == 2) { hexLodMask = 0x04; }
                            if (n == 3) { hexLodMask = 0x08; }

                            ecb.SetComponent(i + n, scatterEntity, new MeshLODComponent
                            {
                                Group = groupLodEntity,
                                LODMask = hexLodMask
                            });

                            ecb.SetComponent(i + n, scatterEntity, new Translation { Value = worldPosition });
                            ecb.SetComponent(i + n, scatterEntity, new Scale { Value = groundScatter.uniformScale });
                            ecb.RemoveComponent<Prefab>(i + n, scatterEntity);
                        }

                        ecb.AddComponent<ScatterApplied>(i, closestChunkEntity);
                    }
                }).Schedule(Dependency);

                /*
                var spawnJob = Entities.WithAll<ChunkComponent, ChunkInitialized>().WithBurst()
                .WithReadOnly(lodPrefabs).WithDisposeOnCompletion(lodPrefabs)
                .WithReadOnly(nativeBiomes).WithDisposeOnCompletion(nativeBiomes)
                .ForEach((Entity chunkEntity, int entityInQueryIndex, in Translation translation, in ChunkComponent chunkComponent) =>
                {
                   
                }).ScheduleParallel(Dependency);
                */
                jobs[count] = spawnJob;
                count++;
            }

            /*
            int count = 0;
            foreach (var scatterPrefab in prefabs) {
                var ecb = ecbSystem.CreateCommandBuffer().AsParallelWriter();
                GroundScatterLods gsl = scatterPrefab;
                GroundScatter groundScatter = scatterPrefab.groundScatter;
                ClimateSettings localClimateSettings = climateSettings;
                int localTerrainSeed = terrainSeed;
                NativeArray<Entity> lodPrefabs = new NativeArray<Entity>(gsl.prefabs, Allocator.TempJob);
                NativeArray<Biome> nativeBiomes = new NativeArray<Biome>(biomes, Allocator.TempJob);
                float4 distances = gsl.distances;
                
                var spawnJob = Entities.WithAll<ChunkComponent, ChunkInitialized>().WithBurst()
                .WithReadOnly(lodPrefabs).WithDisposeOnCompletion(lodPrefabs)
                .WithReadOnly(nativeBiomes).WithDisposeOnCompletion(nativeBiomes)
                .ForEach((Entity chunkEntity, int entityInQueryIndex, in Translation translation, in ChunkComponent chunkComponent) =>
                {
                    uint processSeed = (uint) math.clamp((localTerrainSeed * chunkComponent.gridPosition.x + chunkComponent.gridPosition.y) + 1, 1, int.MaxValue);
                    Unity.Mathematics.Random rng = new Unity.Mathematics.Random(processSeed);

                    for (int i = 0; i < groundScatter.scatterDensity; i++)
                    {
                        float x = rng.NextFloat(0, chunkComponent.grid.chunkSize);
                        processSeed = math.clamp(processSeed + 1, 1, int.MaxValue);
                        rng = new Unity.Mathematics.Random(processSeed);
                        float z = rng.NextFloat(0, chunkComponent.grid.chunkSize);
                        processSeed = math.clamp(processSeed + 1, 1, int.MaxValue);
                        rng = new Unity.Mathematics.Random(processSeed);

                        float2 climate = TerrainNoise.Climate(x, z, localClimateSettings, chunkComponent.gridPosition, chunkComponent.grid.chunkSize, localTerrainSeed);
                        float climateIdealness = TerrainNoise.ClimateIdealness(new float2(groundScatter.minTemperature, groundScatter.minMoisture), new float2(groundScatter.maxTemperature, groundScatter.maxMoisture), climate);
                        if (rng.NextFloat(0, 1) > climateIdealness) { return; }

                        float height = TerrainNoise.GetHeightAtPoint(x, z, climate, nativeBiomes, 1, chunkComponent.gridPosition, chunkComponent.grid.chunkSize, localTerrainSeed);
                        if (height > groundScatter.maxHeight) { return; }

                        float3 position = new float3(x, height, z);
                        
                        MeshLODGroupComponent lodGroup = new MeshLODGroupComponent();
                        lodGroup.LODDistances0 = distances;
                        lodGroup.LODDistances1 = distances;
                        lodGroup.LocalReferencePoint = float3.zero;

                        float3 worldPosition = new float3(translation.Value.x + (position.x * chunkComponent.grid.voxelSize), height, translation.Value.z + (position.z * chunkComponent.grid.voxelSize));

                        Entity groupLodEntity = ecb.CreateEntity(entityInQueryIndex, lodGroupArchetype);
                        ecb.SetComponent(entityInQueryIndex, groupLodEntity, lodGroup);
                        ecb.SetComponent(entityInQueryIndex, groupLodEntity, new Translation { Value = worldPosition });

                        
                        for (int n = 0; n < lodPrefabs.Length; n++)
                        {
                            Entity scatterEntity = ecb.Instantiate(entityInQueryIndex, lodPrefabs[n]);
                            int hexLodMask = 0x08;

                            if (n == 0) { hexLodMask = 0x01; }
                            if (n == 1) { hexLodMask = 0x02; }
                            if (n == 2) { hexLodMask = 0x04; }
                            if (n == 3) { hexLodMask = 0x08; }

                            ecb.SetComponent(entityInQueryIndex, scatterEntity, new MeshLODComponent
                            {
                                Group = groupLodEntity,
                                LODMask = hexLodMask
                            });

                            ecb.SetComponent(entityInQueryIndex, scatterEntity, new Translation { Value = worldPosition });
                            ecb.SetComponent(entityInQueryIndex, scatterEntity, new Scale { Value = groundScatter.uniformScale });
                            ecb.RemoveComponent<Prefab>(entityInQueryIndex, scatterEntity);
                        }
                        
                        ecb.RemoveComponent<ChunkInitialized>(entityInQueryIndex, chunkEntity);
                    }
                }).ScheduleParallel(Dependency);
                jobs[count] = spawnJob;
                count++;
            }

            */
            Dependency = JobHandle.CombineDependencies(jobs);
            jobs.Dispose();
            ecbSystem.AddJobHandleForProducer(Dependency);
        }
    }
}