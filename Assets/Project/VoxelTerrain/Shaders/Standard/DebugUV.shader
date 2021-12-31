Shader "Unlit/DebugUV"
{
    Properties
    {
        _UVChannel("UV Channel", Int) = 0
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

            int _UVChannel;

            struct MeshData
            {
                float4 vertex : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float3 normals : NORMAL;
            };

            struct Interpolators
            {
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float4 vertex : SV_POSITION;
            };

            Interpolators vert (MeshData v)
            {
                Interpolators o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv0 = v.uv0;
                o.uv1 = v.uv1;
                return o;
            }

            float4 frag (Interpolators i) : SV_Target
            {
                if (_UVChannel == 0) {
                    return float4(frac(i.uv0), 0, 1);
                }
                else {
                    return float4(frac(i.uv1), 0, 1);
                }
            }
            ENDCG
        }
    }
}
