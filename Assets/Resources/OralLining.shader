Shader "AfterHours/OralLining"
{
    Properties
    {
        _MainTex ("Mucosa", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = .12
        _DepthTint ("Use distance into throat", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        CGPROGRAM
        #pragma surface Surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness, _DepthTint;
        struct Input
        {
            float2 uv_MainTex;
            float2 uv2_MainTex;
            float side : VFACE;
        };
        void Surf(Input IN, inout SurfaceOutputStandard O)
        {
            float depth = smoothstep(.03, .90, IN.uv2_MainTex.x) * _DepthTint;
            O.Albedo = tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb * lerp(.84, .19, depth);
            O.Smoothness = _Glossiness * lerp(1,.35,depth);
            O.Normal = float3(0,0,IN.side > 0 ? 1 : -1);
            O.Alpha = 1;
        }
        ENDCG
    }
}
