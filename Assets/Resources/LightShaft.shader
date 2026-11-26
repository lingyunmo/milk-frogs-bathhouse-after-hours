Shader "Hidden/AfterHours/LightShaft"
{
    // A faint additive cone under practical lamps. It reads as light catching steam and dust,
    // fades where it meets geometry and as the camera walks through it.
    Properties
    {
        _Color ("Light (rgb, premultiplied)", Color) = (1, 1, 1, 1)
        _Density ("Density", Float) = .05
    }

    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _Color;
            float _Density;

            struct Attributes
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 world : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float4 screen : TEXCOORD2;
                float along : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.world = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.normal = UnityObjectToWorldNormal(input.normal);
                output.screen = ComputeScreenPos(output.position);
                output.screen.z = -UnityObjectToViewPos(input.vertex).z;
                output.along = input.uv.y;
                return output;
            }

            float Noise(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float n = dot(i, float3(1, 57, 113));
                float4 a = frac(sin(n + float4(0, 1, 57, 58)) * 43758.5453);
                float4 b = frac(sin(n + 113 + float4(0, 1, 57, 58)) * 43758.5453);
                float4 m = lerp(a, b, f.z);
                float2 r = lerp(m.xz, m.yw, f.x);
                return lerp(r.x, r.y, f.y);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 view = normalize(_WorldSpaceCameraPos - input.world);
                float facing = abs(dot(normalize(input.normal), view));
                float body = pow(facing, 2.6) * smoothstep(.05, .45, facing);
                float along = input.along;
                float reach = (1 - along) * (1 - along) * smoothstep(0, .12, along);
                float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(input.screen)));
                float soft = saturate((scene - input.screen.z) / .45);
                float near = saturate((input.screen.z - .35) / 1.4);
                float drift = Noise(input.world * 1.7 + float3(0, -_Time.y * .18, _Time.y * .05));
                float motes = lerp(.62, 1.18, drift);
                float amount = body * reach * soft * near * motes * _Density;
                return float4(_Color.rgb * amount, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
