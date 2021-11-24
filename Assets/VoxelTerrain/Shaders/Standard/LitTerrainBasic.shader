Shader "Unlit/LitTerrainBasic"
{
    Properties
    {
        //_Color("Color", Color) = (1, 1, 1, 1)
        _TopAlbedo("Top Albedo", 2D) = "black"{}
        [NoScaleOffset]_TopNormalMap("Top Normal Map", 2D) = "bump" {}
        [NoScaleOffset]_SlopeAlbedo("Slope Albedo", 2D) = "black" {}
        [NoScaleOffset]_SlopeNormalMap("Slope Normal Map", 2D) = "bump" {}
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
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _TopAlbedo;
            sampler2D _TopNormalMap;
            float4 _TopAlbedo_ST;
            sampler2D _SlopeAlbedo;
            sampler2D _SlopeNormalMap;

            float3 viewDir;
            //float4 _Color;

            struct MeshData
            {
                float4 vertex : POSITION;
                float2 uv0 : TEXCOORD0;
                float3 normals : NORMAL;
                float4 tangent : TANGENT;
            };

            struct Interpolators
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD1;
                float3 tangent : TEXCOORD2;
                float3 bitangent : TEXCOORD3;
            };

            Interpolators vert (MeshData v)
            {
                Interpolators o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv0;
                o.normal = v.normals;
                o.tangent = v.tangent.xyz;
                o.bitangent = cross(o.normal, o.tangent);
                o.bitangent *= v.tangent.w * unity_WorldTransformParams.w;
                return o;
            }

            float4 frag(Interpolators i) : SV_Target
            {
                float2 normalUV = frac(TRANSFORM_TEX(i.uv, _TopAlbedo));
                float3x3 tangentToWorld = {
                    i.tangent.x, i.bitangent.x, i.normal.x,
                    i.tangent.y, i.bitangent.y, i.normal.y,
                    i.tangent.z, i.bitangent.z, i.normal.z
                };
                float3 N = normalize(i.normal);
                float3 horizontal = normalize(float3(N.x, 0, N.z));
                float3 L = _WorldSpaceLightPos0.xyz; // Vector pointing from surface to directional light direction.

                float3 topNormal = UnpackNormal(tex2D(_TopNormalMap, normalUV));
                float3 slopeNormal = UnpackNormal(tex2D(_SlopeNormalMap, normalUV));
                float3 surfaceNormal = mul(tangentToWorld, lerp(topNormal, slopeNormal, saturate(dot(N, horizontal))));

                //return float4(mul(tangentToWorld, surfaceNormal), 1);

                float3 topColor = tex2D(_TopAlbedo, normalUV).xyz;
                float3 slopeColor = tex2D(_SlopeAlbedo, normalUV).xyz;
                float3 surfaceColor = lerp(topColor, slopeColor, saturate(dot(surfaceNormal, horizontal)));

                //return float4(surfaceColor, 1);

                float3 diffuseLight = saturate(dot(surfaceNormal, L)) * _LightColor0.xyz * surfaceColor;

                return float4(diffuseLight, 1);
            }
            ENDCG
        }
    }
}
