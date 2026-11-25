
Shader "Hidden/AfterHours/NightCameraGrade"
{
    Properties
    {
        _MainTex ("Camera color", 2D) = "black" {}
        [HideInInspector] _PracticalGlow ("Practical glow", 2D) = "black" {}
        [HideInInspector] _BlurDirection ("Blur direction", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        sampler2D _PracticalGlow;
        float4 _MainTex_TexelSize;
        float4 _BlurDirection;

        float Luma(float3 rgb)
        {
            return dot(rgb, float3(.2126, .7152, .0722));
        }

        float4 GradeFrame(v2f_img input) : SV_Target
        {
            float3 scene = max(tex2D(_MainTex, input.uv).rgb, 0);
            float2 glowUV = input.uv;
            #if UNITY_UV_STARTS_AT_TOP
            if (_MainTex_TexelSize.y < 0) glowUV.y = 1 - glowUV.y;
            #endif
            scene += max(tex2D(_PracticalGlow, glowUV).rgb, 0) * .16;
            float light = Luma(scene);
            // Raise only existing shadow detail. Absolute black stays black.
            float toe = light + .024 * sqrt(light) + .045 * sqrt(light) * exp2(-10 * light);
            const float shoulderStart = .57;
            float highlights = max(0, toe - shoulderStart);
            float mapped = min(toe, shoulderStart) + highlights / (1 + highlights / (1 - shoulderStart));
            float3 grade = scene * (mapped / max(light, .0001));
            float neutral = lerp(.78, .99, smoothstep(.005, .24, light));
            grade = lerp(mapped.xxx, grade, neutral);
            // A subtle edge falloff leaves the whole center clear for the flashlight and interactions.
            float2 frame = (input.uv - .5) * 2;
            grade *= 1 - .105 * smoothstep(.53, 1.89, dot(frame, frame));
            return float4(saturate(grade), 1);
        }

        float4 ExtractHighlights(v2f_img input) : SV_Target
        {
            float2 offset = abs(_MainTex_TexelSize.xy) * 1.25;
            float3 scene = (tex2D(_MainTex, input.uv + offset).rgb +
                tex2D(_MainTex, input.uv - offset).rgb +
                tex2D(_MainTex, input.uv + float2(offset.x, -offset.y)).rgb +
                tex2D(_MainTex, input.uv + float2(-offset.x, offset.y)).rgb) * .25;
            float level = Luma(scene);
            float gain = smoothstep(.42, .91, level);
            return float4(min(scene, 4) * gain, 1);
        }

        float4 Blur(v2f_img input) : SV_Target
        {
            float2 delta = _BlurDirection.xy;
            half3 bloom = tex2D(_MainTex, input.uv).rgb * .227027;
            bloom += (tex2D(_MainTex, input.uv + delta * 1.384615).rgb +
                tex2D(_MainTex, input.uv - delta * 1.384615).rgb) * .316216;
            bloom += (tex2D(_MainTex, input.uv + delta * 3.230769).rgb +
                tex2D(_MainTex, input.uv - delta * 3.230769).rgb) * .070270;
            return float4(bloom, 1);
        }
        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment GradeFrame
            #pragma target 3.0
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment ExtractHighlights
            #pragma target 3.0
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Blur
            #pragma target 3.0
            ENDCG
        }
    }
    Fallback Off
}
