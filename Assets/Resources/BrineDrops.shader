Shader "AfterHours/BrineDrops"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Vertex { float4 p:POSITION; fixed4 c:COLOR; float2 uv:TEXCOORD0; };
            struct Pixel { float4 p:SV_POSITION; fixed4 c:COLOR; float2 uv:TEXCOORD0; };
            Pixel vert(Vertex v) { Pixel o; o.p=UnityObjectToClipPos(v.p); o.c=v.c; o.uv=v.uv*2-1; return o; }
            fixed4 frag(Pixel i):SV_Target
            {
                float r=dot(i.uv,i.uv); clip(1-r);
                float gleam=1-smoothstep(.06,.39,length(i.uv-float2(-.28,.30)));
                return fixed4(i.c.rgb*(.5+.3*r)+gleam*.85,i.c.a*(1-smoothstep(.58,1,r)));
            }
            ENDCG
        }
    }
}
