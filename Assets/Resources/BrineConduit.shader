Shader "AfterHours/BrineConduit"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off Cull Off Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Attr {float4 position:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0;};
            struct Pix {float4 pos:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float3 world:TEXCOORD1;};
            Pix vert(Attr v) { Pix o; o.pos=UnityObjectToClipPos(v.position); o.color=v.color;
                o.uv=v.uv; o.world=mul(unity_ObjectToWorld,v.position).xyz; return o; }
            fixed4 frag(Pix i):SV_Target
            {
                float cross=1-abs(i.uv.y*2-1);
                float dist=dot(i.world,float3(.81,.43,.62));
                float sleeve=smoothstep(.06,.13,abs(frac(dist*2.2)-.5));
                float filament=.65+.35*sin(dist*13+sin(dist*3.3)*2);
                return fixed4(i.color.rgb, i.color.a*cross*cross*lerp(.35,1,sleeve)*filament);
            }
            ENDCG
        }
    }
}
