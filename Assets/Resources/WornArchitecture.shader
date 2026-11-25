Shader "AfterHours/WornArchitecture"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 1
        _MetallicGlossMap ("Metal · AO · Smoothness", 2D) = "white" {}
        _GlossMapScale ("Smoothness Scale", Range(0,1)) = 1
        _StainTex ("Wear Detail", 2D) = "gray" {}
        _Wear ("Weather · Damp · Wetness", Vector) = (.35,0,0,0)
        _FloorBounds ("Floor Center XZ · Half Size XZ", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface Surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"

        sampler2D _MainTex, _BumpMap, _MetallicGlossMap, _StainTex;
        half4 _Color;
        float4 _Wear, _FloorBounds;
        half _BumpScale, _GlossMapScale;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        void Surf(Input IN, inout SurfaceOutputStandard O)
        {
            float2 uv = IN.uv_MainTex;
            half4 baseColor = tex2D(_MainTex, uv);
            half4 finish = tex2D(_MetallicGlossMap, uv);

            // A second real scanned texture reads across spans much larger than the tile repeat.
            float2 broadUV = float2(IN.worldPos.x + .71 * IN.worldPos.z, IN.worldPos.z + .83 * IN.worldPos.y);
            broadUV = broadUV * .118 + float2(.16, .43);
            half broad = tex2D(_StainTex, broadUV).r;
            half wear = smoothstep(.07, .32, broad);
            half surface = lerp(1 - .48 * _Wear.x, 1 + .24 * _Wear.x, wear);

            // Damp plaster and glazed tile share the same story height and stain positions.
            float height = frac((IN.worldPos.y + .015) / 3.6) * 3.6;
            half damp = (1 - smoothstep(.08, 1.03, height)) * _Wear.y * (1 - .55 * wear);
            float2 toEdge = _FloorBounds.zw - abs(IN.worldPos.xz - _FloorBounds.xy);
            half edge = 0;
            if (_FloorBounds.z > 0)
                edge = (1 - smoothstep(.05, .58, min(toEdge.x, toEdge.y))) * (.68 + .32 * wear);

            O.Albedo = baseColor.rgb * _Color.rgb * surface * (1 - .36 * damp - .18 * edge);
            O.Normal = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale);
            O.Metallic = min(finish.r, .18);
            O.Occlusion = lerp(.62, 1, finish.g) * (1 - .37 * edge - .24 * damp);
            O.Smoothness = saturate(finish.a * _GlossMapScale + _Wear.z * (damp + .35 * wear));
            O.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
