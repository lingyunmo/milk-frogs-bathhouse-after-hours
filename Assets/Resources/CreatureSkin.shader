Shader "AfterHours/CreatureSkin"
{
    // The creature's painted hide with a tiled pore normal and moisture pooling in the darker creases,
    // so the hand torch draws a wet, uneven highlight across it.
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Pores ("Pore normal", 2D) = "bump" {}
        _PoreScale ("Pore strength", Float) = .55
        _PoreTiling ("Pore tiling", Float) = 7
        _Glossiness ("Smoothness", Range(0, 1)) = .36
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface Surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"

        sampler2D _MainTex, _Pores;
        half4 _Color;
        half _PoreScale, _PoreTiling, _Glossiness;

        struct Input
        {
            float2 uv_MainTex;
        };

        void Surf(Input IN, inout SurfaceOutputStandard o)
        {
            half4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Normal = UnpackScaleNormal(tex2D(_Pores, IN.uv_MainTex * _PoreTiling), _PoreScale);
            half crease = 1 - saturate(dot(albedo.rgb, half3(.3, .5, .2)) * 1.6);
            o.Albedo = albedo.rgb;
            o.Metallic = 0;
            o.Smoothness = saturate(_Glossiness + crease * .18);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
