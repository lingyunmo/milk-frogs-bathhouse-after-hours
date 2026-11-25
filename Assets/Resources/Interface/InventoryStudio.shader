
Shader "AfterHours/InventoryStudio"
{
    Properties
    {
        _MainTex ("Color", 2D) = "white" {}
        _Color ("Surface tint", Color) = (1,1,1,1)
        _BumpMap ("Normal", 2D) = "bump" {}
        _PackedMap ("Metal, AO and smoothness", 2D) = "white" {}
        _Metalness ("Metalness", Range(0,1)) = 0
        _Roughness ("Roughness", Range(0,1)) = .5
        _Variation ("Material variation", Range(0,1)) = 0
        _UsesPacked ("Read finish map", Float) = 0
        _UsesNormal ("Read normal map", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="Always" }
            ZWrite On
            Cull Back
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex, _BumpMap, _PackedMap;
            float4 _MainTex_ST;
            float4 _Color;
            float _Metalness, _Roughness, _Variation, _UsesPacked, _UsesNormal;

            struct Attributes
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 local : TEXCOORD2;
                half3 normal : TEXCOORD3;
                half4 tangent : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(v.position);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.local = v.position.xyz;
                o.world = mul(unity_ObjectToWorld, v.position).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.tangent = half4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w * unity_WorldTransformParams.w);
                return o;
            }

            float Grain(float3 p)
            {
                return sin(dot(p, float3(3.7, 13.1, 7.8))) * cos(dot(p, float3(9.6, -5.4, 12.7)));
            }

            float3 SoftBox(float3 n, float3 v, float3 l, float3 tint, float3 albedo,
                float3 reflected, float roughness, float metallic)
            {
                float front = saturate(dot(n, l));
                float wrap = saturate((dot(n, l) + .23) / 1.23);
                float3 h = normalize(l + v);
                float broad = lerp(58, 5, roughness);
                float reflection = pow(saturate(dot(n, h)), broad);
                return tint * (albedo * wrap * lerp(.83, .24, metallic)
                    + reflected * reflection * (.16 + .42 * front));
            }

            fixed4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normal);
                if (_UsesNormal > .5)
                {
                    float3 t = normalize(i.tangent.xyz);
                    float3 b = cross(n, t) * i.tangent.w;
                    float3 bump = UnpackNormal(tex2D(_BumpMap, i.uv));
                    n = normalize(n * bump.z + (t * bump.x + b * bump.y) * .83);
                }

                float3 color = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                float metallic = _Metalness;
                float roughness = _Roughness;
                float occlusion = 1;
                if (_UsesPacked > .5)
                {
                    float4 finish = tex2D(_PackedMap, i.uv);
                    metallic = finish.r;
                    roughness = 1 - finish.a;
                    occlusion = lerp(.61, 1, finish.g);
                }
                if (_Variation > .001)
                {
                    float wear = .34 * Grain(i.local * 1.75) + .39 * Grain(i.local * 8.3) + .27 * Grain(i.local * 29);
                    color *= 1 + _Variation * wear;
                    roughness += .16 * wear * _Variation;
                }
                roughness = clamp(roughness, .23, .93);

                float3 v = normalize(_WorldSpaceCameraPos - i.world);
                float3 reflected = lerp(float3(.09, .09, .09), color * 1.15, metallic);
                float rim = pow(1 - saturate(dot(v, n)), 3.1);
                float3 light = color * (.23 + .14 * saturate(n.y)) * float3(.80, .86, .89);
                light += SoftBox(n, v, normalize(float3(-.70, .83, -.65)), float3(1.43, 1.20, .93),
                    color, reflected, roughness, metallic);
                light += SoftBox(n, v, normalize(float3(.73, .23, -.69)), float3(.36, .51, .61),
                    color, reflected, roughness, metallic);
                light += SoftBox(n, v, normalize(float3(.65, .78, .79)), float3(.52, .72, .77),
                    color, reflected, roughness, metallic);
                light += reflected * rim * lerp(.10, .29, metallic) * float3(.67, .89, 1);
                light *= occlusion;
                return fixed4(saturate(light / (1 + .22 * light)), 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
