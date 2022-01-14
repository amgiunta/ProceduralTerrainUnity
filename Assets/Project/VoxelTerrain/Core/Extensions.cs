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

        public static Vector3 ToVector3(this float3 f3) {
            return new Vector3(f3.x, f3.y, f3.z);
        }

        public static Vector3[] ToVectorArray(this float3[] f3s) {
            Vector3[] vecs = new Vector3[f3s.Length];
            for (int i = 0; i < f3s.Length; i++) {
                vecs[i] = f3s[i];
            }

            return vecs;
        }

        public static Vector2[] ToVectorArray(this float2[] f2s)
        {
            Vector2[] vecs = new Vector2[f2s.Length];
            for (int i = 0; i < f2s.Length; i++)
            {
                vecs[i] = f2s[i];
            }

            return vecs;
        }
    }
}
