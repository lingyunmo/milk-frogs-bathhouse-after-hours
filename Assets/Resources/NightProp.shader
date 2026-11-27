Shader "AfterHours/Prop"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Relief", Float) = 1
        _MetallicGlossMap ("Metal AO Smooth", 2D) = "white" {}
        _Metallic ("Metal", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = .35
        _GlossMapScale ("Smoothness scale", Float) = 1
        _UseFinish ("Packed finish", Float) = 0
        _UseAO ("Packed occlusion", Float) = 0
        _DetailAlbedoMap ("Detail colour", 2D) = "gray" {}
        _DetailNormalMap ("Detail normal", 2D) = "bump" {}
        _DetailNormalMapScale ("Detail strength", Float) = 0
        _FacadePatina ("Street masonry wear", 2D) = "gray" {}
        _EmissionMap ("Emission", 2D) = "white" {}
        _EmissionColor ("Emission colour", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface Surf Standard fullforwardshadows addshadow
        #pragma target 4.0
        #pragma multi_compile_local __ _DETAIL_MULX2
        #pragma multi_compile_local __ _FACADE_WEATHER
        #include "UnityStandardUtils.cginc"
        #include "NightIndirect.cginc"
        sampler2D _MainTex, _BumpMap, _MetallicGlossMap, _EmissionMap, _DetailAlbedoMap, _DetailNormalMap;
        sampler2D _FacadePatina;
        half4 _Color, _EmissionColor;
        half _Metallic, _Glossiness, _BumpScale, _GlossMapScale, _UseFinish, _UseAO, _DetailNormalMapScale;
        struct Input { float2 uv_MainTex; float2 uv_DetailAlbedoMap; float3 worldPos; };
        void Surf(Input IN, inout SurfaceOutputStandard o)
        {
            half4 finish = tex2D(_MetallicGlossMap, IN.uv_MainTex);
            o.Albedo = _Color.rgb * tex2D(_MainTex, IN.uv_MainTex).rgb;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            #ifdef _DETAIL_MULX2
                o.Albedo *= tex2D(_DetailAlbedoMap, IN.uv_DetailAlbedoMap).rgb * unity_ColorSpaceDouble.rgb;
                o.Normal = BlendNormals(o.Normal, UnpackScaleNormal(tex2D(_DetailNormalMap, IN.uv_DetailAlbedoMap), _DetailNormalMapScale));
            #endif
            o.Occlusion = lerp(1, finish.g, _UseAO);
            o.Metallic = lerp(_Metallic, finish.r, _UseFinish);
            o.Smoothness = lerp(_Glossiness, finish.a * _GlossMapScale, _UseFinish);
            #ifdef _FACADE_WEATHER
                // World-sized photographed cement keeps adjacent atlas islands at the same grain scale.
                float2 facade = float2(IN.worldPos.x + .87 * IN.worldPos.z, IN.worldPos.y);
                half grain = tex2D(_FacadePatina, facade * .92).g;
                half runoff = smoothstep(.15, .23, tex2D(_FacadePatina, facade * float2(.60,.12)).r);
                half chalk = smoothstep(.105, .255, grain);
                half matte = saturate((1-o.Smoothness) * (1-o.Metallic));
                half splash = (1-smoothstep(.15,1.45,IN.worldPos.y)) * (.5+.5*runoff);
                o.Albedo *= lerp(1, lerp(.76,1.12,chalk) * (1-.22*runoff-.25*splash), matte);
                o.Smoothness = saturate(o.Smoothness + matte * (.15*splash + .04*runoff));
            #endif
            o.Emission = o.Albedo * (1 - o.Metallic) * o.Occlusion * NightBounce(IN.worldPos)
                + tex2D(_EmissionMap, IN.uv_MainTex).rgb * _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Standard"
}
