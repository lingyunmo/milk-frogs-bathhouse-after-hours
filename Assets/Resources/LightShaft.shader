Shader "Hidden/AfterHours/LightShaft"
{
    Properties
    {
        _Color ("Light radiance", Color) = (1,1,1,1)
        _Density ("Scattering density", Float) = .01
        _ShadowTile ("Occlusion atlas rectangle", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        // Draw the exit face; the opaque camera depth can lie inside the volume.
        Blend One One
        Cull Front ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            sampler2D _BathLightDepth;
            half4 _Color;
            float4 _ShadowTile;
            half _Density;
            struct Varyings { float4 position : SV_POSITION; float3 world : TEXCOORD0; float4 screen : TEXCOORD1; };
            Varyings Vert(float4 vertex : POSITION)
            {
                Varyings o;
                o.position = UnityObjectToClipPos(vertex);
                o.world = mul(unity_ObjectToWorld, vertex).xyz;
                o.screen = ComputeScreenPos(o.position);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float3 delta = i.world - _WorldSpaceCameraPos;
                float exitDistance = length(delta);
                float3 viewRay = delta / max(exitDistance,.0001);
                float eyeDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screen)));
                float end = min(exitDistance, eyeDepth * exitDistance / max(i.screen.w,.001));
                float3 eye = mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz;
                float3 ray = mul((float3x3)unity_WorldToObject,viewRay);
                float3 inv = 1 / (sign(ray + 1e-9) * max(abs(ray),.000001));
                float3 nearSide = min((float3(-1,-1,0)-eye)*inv,(float3(1,1,1)-eye)*inv);
                float start = max(0,max(nearSide.x,max(nearSide.y,nearSide.z)));
                if (end <= start) discard;
                const int steps = 8;
                float stride = (end-start)/steps;
                half scatter = 0;
                [unroll] for (int s=0;s<steps;s++)
                {
                    float3 p = eye + ray * (start + (s+.5)*stride);
                    float2 projected = p.xy/max(p.z,.001);
                    float2 uv = saturate(projected*.5+.5)*_ShadowTile.zw + _ShadowTile.xy;
                    half blocker = tex2Dlod(_BathLightDepth,float4(uv,0,0)).r;
                    half visibility = smoothstep(-.035,.015,blocker-p.z);
                    half edge = saturate(1-dot(projected,projected));
                    scatter += visibility * edge * edge * saturate(p.z*9) * pow(saturate(1-p.z),1.7);
                }
                return half4(_Color.rgb * (_Density * scatter * stride),0);
            }
            ENDCG
        }
    }
    Fallback Off
}
