Shader "Unlit/UnlitTerrain"
{
    Properties
    {
        _Color("Color", Color) = (1, 1, 1, 1)
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float3 viewDir;
            float4 _Color;

            struct MeshData
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normals : NORMAL;
            };

            struct Interpolators
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD1;
            };

            Interpolators vert (MeshData v)
            {
                Interpolators o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = v.normals;
                return o;
            }

            float4 frag(Interpolators i) : SV_Target
            {
                viewDir = UNITY_MATRIX_IT_MV[2].xyz; // Unity camera forward direction;
                return float4(_Color.xyz, 1 * dot(viewDir, i.normal));
            }
            ENDCG
        }
    }
}
