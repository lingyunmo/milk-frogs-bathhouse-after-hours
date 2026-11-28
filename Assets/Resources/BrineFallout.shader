Shader "AfterHours/Brine Fallout"
{
    Properties { _Noise ("Turbulence", 3D) = "" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler3D _Noise;
            struct app { float4 p:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 p:SV_POSITION; float4 c:COLOR; float2 uv:TEXCOORD0; };
            v2f vert(app v) { v2f o; o.p=UnityObjectToClipPos(v.p); o.c=v.color; o.uv=v.uv; return o; }
            float4 frag(v2f i):SV_Target
            {
                float2 uv=i.uv*2-1;
                float4 grain=tex3D(_Noise,float3(uv*.51+.51,i.c.r*.83));
                float fold=tex3D(_Noise,float3(uv*1.06+grain.r*.21,i.c.g)).g;
                float edge=1-length(uv*float2(.9,1.22))+(grain.r-.5)*.43+(fold-.5)*.31;
                float alpha=smoothstep(.02,.85,edge)*(.32+fold*.68)*i.c.a;
                return float4(i.c.rgb*lerp(.51,1.06,grain.g),alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
