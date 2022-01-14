using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Mathematics;
using VoxelTerrain.ECS.Components;

namespace VoxelTerrain
{
    namespace ECS {
        namespace Entities {
            public abstract class EntityBuilder {
                public static EntityArchetype GetArchetype(EntityManager entityManager) { return default; }
                public abstract Entity CreateEntity(EntityManager entityManager);
            }

            public class GroundScatterBuilder : EntityBuilder {
                public IGroundScatter groundScatter;
                public float3 position;
                public quaternion rotation;
                public Mesh mesh;
                public Material material;

                public GroundScatterBuilder(GroundScatterAuthor author) {
                    mesh = author.mesh;
                    material = author.material;
                }

                public GroundScatterBuilder(GroundScatterAuthor author, float3 position)
                {
                    this.position = position;

                    mesh = author.mesh;
                    material = author.material;
                }

                new public static EntityArchetype GetArchetype(EntityManager entityManager)
                {
                    EntityArchetype entityArchetype = entityManager.CreateArchetype(
                        typeof(Translation),
                        typeof(Rotation),
                        typeof(RenderMesh),
                        typeof(RenderBounds),
                        typeof(LocalToWorld),
                        typeof(IGroundScatter),
                        typeof(ReadyToSpawn)
                    );

                    return entityArchetype;
                }

                public Entity CreateEntity(EntityManager entityManager, float3 position, uint rotationSeed = 0) {
                    Unity.Mathematics.Random random = new Unity.Mathematics.Random(rotationSeed);

                    this.position = position;
                    rotation = quaternion.AxisAngle(new float3(0, 1, 0), random.NextFloat(0, 360));
                    return CreateEntity(entityManager);
                }

                public override Entity CreateEntity(EntityManager entityManager)
                {
                    Entity entity = entityManager.CreateEntity(GetArchetype(entityManager));
                    entityManager.AddComponentData(entity, groundScatter);
                    entityManager.AddSharedComponentData(entity, new RenderMesh {
                        mesh = mesh,
                        material = material
                    });
                    entityManager.AddComponentData(entity, new Translation
                    {
                        Value = position
                    });
                    entityManager.AddComponentData(entity, new Rotation
                    {
                        Value = rotation
                    });

                    return entity;
                }
            }            
        }
    }
}
