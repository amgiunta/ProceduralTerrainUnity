Shader "Unlit/LitTerrainBasic"
{
    Properties
    {
        //_Color("Color", Color) = (1, 1, 1, 1)
        _MinSnowTemperature("Min Snow Temeperature", Range(0.0, 1.0)) = 0
        _MaxSnowTemperature("Max Snow Temeperature", Range(0.0, 1.0)) = 0
        _MinSnowMoisture("Min Snow Moisture", Range(0.0, 1.0)) = 0
        _MaxSnowMoisture("Max Snow Moisture", Range(0.0, 1.0)) = 0
        _SnowCapHeight("Snow Cap Height", Float) = 0
        _SnowSpread("Snow Spread", Range(0.0, 0.5)) = 0
        _SnowAlbedo("Snow Albedo", 2d) = "white" {}
        [NoScaleOffset] _SnowNormalMap("Snow Normal Map", 2D) = "bump" {}
        _TopAlbedo("Top Albedo", 2D) = "black"{}
        [NoScaleOffset]_TopNormalMap("Top Normal Map", 2D) = "bump" {}
        [NoScaleOffset]_SlopeAlbedo("Slope Albedo", 2D) = "black" {}
        [NoScaleOffset]_SlopeNormalMap("Slope Normal Map", 2D) = "bump" {}
        [NoScaleOffset]_ClimateTexture("Climate Texture", 2D) = "black" {}
        [NoScaleOffset]_ColorTexture("Color Texture", 2D) = "black" {}
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

            float _MinSnowTemperature;
            float _MaxSnowTemperature;
            float _MinSnowMoisture;
            float _MaxSnowMoisture;
            float _SnowSpread;
            float _SnowCapHeight;

            sampler2D _SnowAlbedo;
            float4 _SnowAlbedo_ST;
            sampler2D _SnowNormalMap;
            sampler2D _TopAlbedo;
            sampler2D _TopNormalMap;
            float4 _TopAlbedo_ST;
            sampler2D _SlopeAlbedo;
            sampler2D _SlopeNormalMap;
            sampler2D _ClimateTexture;
            float4 _ClimateTexture_ST;
            sampler2D _ColorTexture;

            float3 viewDir;
            //float4 _Color;

            struct MeshData
            {
                float4 vertex : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float3 normals : NORMAL;
                float4 tangent : TANGENT;
            };

            struct Interpolators
            {
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD5;
                float3 normal : TEXCOORD2;
                float3 tangent : TEXCOORD3;
                float3 bitangent : TEXCOORD4;
            };

            Interpolators vert (MeshData v)
            {
                Interpolators o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv0 = v.uv0;
                o.uv1 = v.uv1;
                o.normal = v.normals;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.tangent = v.tangent.xyz;
                o.bitangent = cross(o.normal, o.tangent);
                o.bitangent *= v.tangent.w * unity_WorldTransformParams.w;
                return o;
            }

            float invLerp(float from, float to, float value) {
                return (value - from) / (to - from);
            }

            float remap(float origFrom, float origTo, float targetFrom, float targetTo, float value) {
                float rel = invLerp(origFrom, origTo, value);
                return lerp(targetFrom, targetTo, rel);
            }

            float GetSnowChance(float temperature, float moisture, float height) {
                float temperatureSpreadDistance = (_MaxSnowTemperature - _MinSnowTemperature) * _SnowSpread;
                float moistureSpreadDistance = (_MaxSnowMoisture - _MinSnowMoisture) * _SnowSpread;
                float heightSpreadDistance = 50 * _SnowSpread;

                float heightPercent = 0;
                if (height < _SnowCapHeight) {
                    heightPercent = clamp(invLerp(_SnowCapHeight - heightSpreadDistance, _SnowCapHeight, height), 0, 1);
                }
                else {
                    heightPercent = 1;
                }

                float temperaturePercent = 0;
                if (temperature < _MinSnowTemperature) {
                    temperaturePercent = clamp(invLerp(_MinSnowTemperature - temperatureSpreadDistance, _MinSnowTemperature, temperature), 0, 1);
                }
                else if (temperature > _MaxSnowTemperature) {
                    temperaturePercent = clamp(invLerp(_MaxSnowTemperature + temperatureSpreadDistance, _MaxSnowTemperature, temperature), 0, 1);
                }
                else {
                    temperaturePercent = 1;
                }

                float moisturePercent = 0;
                if (moisture < _MinSnowMoisture) {
                    moisturePercent = clamp(invLerp(_MinSnowMoisture - moistureSpreadDistance, _MinSnowMoisture, moisture), 0, 1);
                }
                else if (moisture > _MaxSnowMoisture) {
                    moisturePercent = clamp(invLerp(_MaxSnowMoisture + moistureSpreadDistance, _MaxSnowMoisture, moisture), 0, 1);
                }
                else {
                    moisturePercent = 1;
                }

                return max(temperaturePercent * moisturePercent, heightPercent);
            }

            float4 frag(Interpolators i) : SV_Target
            {

                float3 grassColor = tex2D(_ColorTexture, i.uv1).xyz;

                float3x3 tangentToWorld = {
                    i.tangent.x, i.bitangent.x, i.normal.x,
                    i.tangent.y, i.bitangent.y, i.normal.y,
                    i.tangent.z, i.bitangent.z, i.normal.z
                };
                float3 N = normalize(i.normal);
                float3 horizontal = normalize(float3(N.x, 0, N.z));
                float3 L = _WorldSpaceLightPos0.xyz; // Vector pointing from surface to directional light direction.

                //return float4(tex2D(_ClimateTexture, i.uv1).xyz, 1);
                //return float4(tex2D(_ClimateTexture, TRANSFORM_TEX(i.uv1, _ClimateTexture)).xyz, 1);
                //return float4(i.uv1, 0, 1);

                float temperature = tex2D(_ClimateTexture, i.uv1).x;
                float moisture = tex2D(_ClimateTexture, i.uv1).z;
                //return float4(temperature, 0, moisture, 1);

                float3 snowColor = tex2D(_SnowAlbedo, i.uv1).xyz;
                float3 snowNormal = mul(tangentToWorld, UnpackNormal(tex2D(_SnowNormalMap, i.uv1)));

                float snowChance = GetSnowChance(temperature, moisture, i.worldPos.y);

                //return float4(snowNormal, 1);
                //return float4(snowColor, 1);
                //return float4(saturate(dot(snowNormal, L)) * _LightColor0.xyz * snowColor, 1);

                float2 normalUV = frac(TRANSFORM_TEX(i.uv0, _TopAlbedo));                

                float3 topNormal = UnpackNormal(tex2D(_TopNormalMap, normalUV));
                float3 slopeNormal = UnpackNormal(tex2D(_SlopeNormalMap, normalUV));
                float3 groundNormal = mul(tangentToWorld, lerp(topNormal, slopeNormal, saturate(dot(N, horizontal))));

                //return float4(mul(tangentToWorld, surfaceNormal), 1);

                float3 topColor = lerp(tex2D(_TopAlbedo, normalUV) * grassColor, snowColor, snowChance).xyz;
                float3 slopeColor = tex2D(_SlopeAlbedo, normalUV).xyz;
                float3 surfaceColor = lerp(topColor, slopeColor, saturate(dot(groundNormal, horizontal)));

                //float3 surfaceColor = lerp(groundColor, snowColor, snowChance);
                float3 surfaceNormal = lerp(groundNormal, snowNormal, snowChance);

                return float4(saturate(dot(surfaceNormal, L)) * _LightColor0.xyz * surfaceColor, 1);
            }
            ENDCG
        }
    }
}
