using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

namespace VoxelTerrain
{
    public static class Extensions
    {
        public static Vector2 ToVector2(this float2 f2) {
            return new Vector2(f2.x, f2.y);
        }

        public static Vector2Int ToVector2Int(this int2 i2) {
            return new Vector2Int(i2.x, i2.y);
        }

        public static Vector2 ToVector2(this int2 i2)
        {
            return new Vector2(i2.x, i2.y);
        }
    }
}
