Shader "AfterHours/BrineMonitor"
{
    Properties
    {
        _MainTex ("Record", 2D) = "black" {}
        _Lettering ("Lettering", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Lettering;
            struct ScreenVertex { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            ScreenVertex vert(appdata_full v)
            {
                ScreenVertex o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                o.color = v.color;
                return o;
            }
            fixed4 frag(ScreenVertex i) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, i.uv);
                if (_Lettering > .5)
                {
                    clip(source.a * i.color.a - .32);
                    return fixed4(i.color.rgb * .93, 1);
                }
                float luma = dot(source.rgb, float3(.25, .62, .13));
                float band = smoothstep(.94, 1, frac(i.uv.y - _Time.y * .14));
                float scan = .93 + .07 * sin(i.uv.y * 312 + _Time.y * 4);
                float value = (.065 + sqrt(saturate(luma * 2.9)) * .89 + band * .065) * scan;
                return fixed4(value * float3(.67, .93, .79), 1);
            }
            ENDCG
        }
    }
}
