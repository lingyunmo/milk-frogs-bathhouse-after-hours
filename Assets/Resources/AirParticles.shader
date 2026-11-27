Shader "Hidden/AfterHours/AirParticles"
{
    // Two kinds of air. Dust motes (_Steam = 0, additive) stay invisible until the hand torch crosses
    // them. Steam (_Steam = 1, alpha blended) is a soft, depth-faded puff tinted by its room.
    Properties
    {
        _MainTex ("Puff", 2D) = "white" {}
        _Ambient ("Ambient tint", Color) = (.05, .06, .06, 1)
        _TorchResponse ("Torch response", Float) = 1
        _Steam ("Steam", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SourceBlend ("Source blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _TargetBlend ("Target blend", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend [_SourceBlend] [_TargetBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _Ambient;
            float _TorchResponse, _Steam;
            float4 _TorchPosition;   // xyz position, w range
            float4 _TorchDirection;  // xyz forward, w cosine of the outer cone
            float4 _TorchColor;      // rgb colour times intensity, zero when off

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
                float4 screen : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                output.world = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.screen = ComputeScreenPos(output.position);
                output.screen.z = -UnityObjectToViewPos(input.vertex).z;
                return output;
            }

            float3 TorchLight(float3 world)
            {
                float3 offset = world - _TorchPosition.xyz;
                float distance = max(length(offset), 1e-3);
                float cosine = dot(offset / distance, _TorchDirection.xyz);
                float cone = smoothstep(_TorchDirection.w, lerp(_TorchDirection.w, 1, .55), cosine);
                float reach = saturate(1 - distance / max(_TorchPosition.w, .01));
                return _TorchColor.rgb * cone * reach * reach / (1 + distance * distance * .35);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float scene = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(input.screen)));
                float span = lerp(.08, .6, _Steam);
                float soft = saturate((scene - input.screen.z) / span) * saturate((input.screen.z - .25) / .6);
                float3 torch = TorchLight(input.world) * _TorchResponse;
                if (_Steam > .5)
                {
                    // Premultiplied: steam mostly adds the light it scatters, and only slightly veils what is behind.
                    float amount = tex2D(_MainTex, input.uv).a * input.color.a * soft;
                    float3 scatter = input.color.rgb * (_Ambient.rgb + torch * .75);
                    return float4(scatter * amount, amount * .3);
                }
                float2 d = input.uv * 2 - 1;
                float glint = saturate(1 - dot(d, d));
                glint *= glint;
                return float4((torch + _Ambient.rgb) * glint * input.color.a * soft, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
