using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using VoxelTerrain.ECS.Entities;

namespace VoxelTerrain
{
    public class TestSpawner : MonoBehaviour
    {
        public GroundScatterAuthor groundScatterAuthor;

        private EntityManager entityManager;
        private World defaultWorld;

        private GroundScatterBuilder groundScatterBuilder;

        private List<Entity> groundScatter;

        // Start is called before the first frame update
        void Start()
        {
            defaultWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = defaultWorld.EntityManager;

            groundScatterBuilder = new GroundScatterBuilder(groundScatterAuthor);

            groundScatter = new List<Entity>();

            CreateGroundScatter();
        }

        private void CreateGroundScatter() {
            int rad = 10;
            float2[] positions = new float2[(rad * 2) * (rad * 2)];

            for (int y = -rad; y <= rad; y++) {
                for (int x = -rad; x <= rad; x++) {
                    positions[y * (rad * 2) + x] = new float2(x, y);                    
                }
            }
        }
    }
}
