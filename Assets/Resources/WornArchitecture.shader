
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
        _GrainNormal ("Scanned Detail Normal", 2D) = "bump" {}
        _Wear ("Weather · Damp · Wetness · Grain", Vector) = (.35,0,0,.28)
        _FloorBounds ("Floor Center XZ · Half Size XZ", Vector) = (0,0,0,0)
        _WetSourceA ("Wet Center XZ · Radius XZ", Vector) = (0,0,0,0)
        _WetSourceB ("Wet Center XZ · Radius XZ", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface Surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"

        sampler2D _MainTex, _BumpMap, _MetallicGlossMap, _StainTex, _GrainNormal;
        half4 _Color;
        float4 _Wear, _FloorBounds, _WetSourceA, _WetSourceB;
        half _BumpScale, _GlossMapScale;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        // A soft, irregular footprint leaves some of the concrete exposed between separate leaks.
        half StandingMoisture(float2 sampleCoordinate, float4 source, half breakup)
        {
            float2 radial = (sampleCoordinate - source.xy) / max(source.zw, .005);
            half edge = dot(radial, radial) + (breakup - .43) * .63;
            return (1 - smoothstep(.54, 1., edge)) * step(.02, source.z);
        }

        void Surf(Input IN, inout SurfaceOutputStandard O)
        {
            float2 uv = IN.uv_MainTex;
            half4 baseColor = tex2D(_MainTex, uv);
            half4 finish = tex2D(_MetallicGlossMap, uv);

            // Registered in world units so discolouration crosses separate wall and trim meshes.
            float3 world = IN.worldPos;
            float2 broadUV = float2(world.x + .71 * world.z, world.z + .83 * world.y) * .118;
            half coarse = tex2D(_StainTex, broadUV + float2(.16, .43)).r;
            half islands = smoothstep(.164, .221, coarse);
            half breakup = saturate((tex2D(_StainTex, broadUV * 3.76 + float2(.29, .81)).g - .104) / .067);
            half surface = lerp(1 - .35 * _Wear.x, 1 + .18 * _Wear.x, islands);
            surface *= lerp(.94, 1.08, smoothstep(.17, .75, breakup));

            // Vertical mineral traces bleach as they dry above the darker damp band.
            float height = frac((world.y + .015) / 3.6) * 3.6;
            float2 seepUV = float2((world.x + .91 * world.z) * .76, world.y * .16);
            half streak = smoothstep(.159, .226, tex2D(_StainTex, seepUV + .47).r);
            half dampTop = lerp(.56, 1.92, streak * (.6 + .4 * islands));
            half damp = (1 - smoothstep(.08, dampTop, height)) * _Wear.y;
            half residue = _Wear.y * streak * smoothstep(.52, .89, height / dampTop) *
                (1 - smoothstep(.92, 1.28, height / dampTop));

            float2 toEdge = _FloorBounds.zw - abs(world.xz - _FloorBounds.xy);
            half joins = min(toEdge.x, toEdge.y);
            half edge = (1 - smoothstep(.03, .42, joins)) * step(.01, _FloorBounds.z);
            half standing = max(StandingMoisture(world.xz, _WetSourceA, breakup),
                StandingMoisture(world.xz, _WetSourceB, breakup));
            half wet = saturate(standing + .44 * damp);

            half3 dry = baseColor.rgb * _Color.rgb * surface * (1 - .22 * edge);
            dry = lerp(dry, dry * .85 + half3(.031, .027, .014), residue * .58);
            O.Albedo = dry * (1 - .33 * wet - .14 * damp);

            // Finer photographed grain gives grazing light extra relief without repeating the base map.
            half3 coarseNormal = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale * (1 - .51 * standing));
            half3 grainNormal = UnpackScaleNormal(tex2D(_GrainNormal, uv * 4.63 + float2(.19, .37)),
                _Wear.w * (1 - .77 * standing));
            O.Normal = BlendNormals(coarseNormal, grainNormal);
            O.Metallic = min(finish.r, .16);
            O.Occlusion = lerp(.69, 1, finish.g) * (1 - .29 * edge - .15 * damp);
            half matte = finish.a * _GlossMapScale + _Wear.z * (.3 * islands + .5 * damp);
            O.Smoothness = saturate(lerp(matte, .81, standing * .76));
            O.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
