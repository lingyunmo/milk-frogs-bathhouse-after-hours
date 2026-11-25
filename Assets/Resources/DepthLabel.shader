
Shader "AfterHours/DepthLabel"
{
    Properties
    {
        _MainTex ("Font atlas", 2D) = "white" {}
        _Cutoff ("Letter cutoff", Range(0,1)) = .32
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest+5" "RenderType"="TransparentCutout" }
        Cull Off

        CGPROGRAM
        #pragma surface Surface Lambert alphatest:_Cutoff
        #pragma target 3.0

        sampler2D _MainTex;
        struct Input
        {
            float2 uv_MainTex;
            fixed4 color : COLOR;
            float facing : VFACE;
        };

        void Surface(Input input, inout SurfaceOutput output)
        {
            output.Albedo = input.color.rgb * .79;
            output.Emission = input.color.rgb * .028;
            output.Normal = float3(0, 0, input.facing > 0 ? 1 : -1);
            output.Alpha = tex2D(_MainTex, input.uv_MainTex).a * input.color.a;
        }
        ENDCG
    }
    Fallback Off
}
