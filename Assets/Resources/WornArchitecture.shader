
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
        _HeightMap ("Scanned relief", 2D) = "gray" {}
        _WallDepth ("Wall relief in UV units", Float) = 0
        _GrainNormal ("Scanned Detail Normal", 2D) = "bump" {}
        _Rain ("Outdoor wet asphalt", Float) = 0
        _Wear ("Weather · Damp · Wetness · Grain", Vector) = (.35,0,0,.28)
        _FloorBounds ("Floor Center XZ · Half Size XZ", Vector) = (0,0,0,0)
        _WetSourceA ("Wet Center XZ · Radius XZ", Vector) = (0,0,0,0)
        _WetSourceB ("Wet Center XZ · Radius XZ", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGPROGRAM
        #pragma surface Surf Standard fullforwardshadows addshadow finalcolor:WallLight
        #pragma target 4.0
        #pragma multi_compile_local __ WALL_RELIEF
        #include "UnityStandardUtils.cginc"

        sampler2D _MainTex, _BumpMap, _MetallicGlossMap, _StainTex, _GrainNormal;
        #include "NightIndirect.cginc"
        sampler2D _BathWater, _HeightMap;
        float _WallDepth;
        float4x4 _BathWaterProjection;
        float _BathWaterPlane;
        half _Rain;
        half4 _Color;
        float4 _Wear, _FloorBounds, _WetSourceA, _WetSourceB;
        half _BumpScale, _GlossMapScale;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
            #ifdef WALL_RELIEF
                float3 viewDir;
            #endif
            INTERNAL_DATA
        };

        // A soft, irregular footprint leaves some of the concrete exposed between separate leaks.
        half StandingMoisture(float2 sampleCoordinate, float4 source, half breakup)
        {
            float2 radial = (sampleCoordinate - source.xy) / max(source.zw, .005);
            half edge = dot(radial, radial) + (breakup - .43) * .63;
            return (1 - smoothstep(.54, 1., edge)) * step(.02, source.z);
        }

        void WallLight(Input IN, SurfaceOutputStandard surface, inout fixed4 color)
        {
            #if defined(WALL_RELIEF) && defined(UNITY_PASS_FORWARDADD)
                color.rgb *= surface.Alpha;
            #endif
        }

        void Surf(Input IN, inout SurfaceOutputStandard O)
        {
            float2 uv = IN.uv_MainTex;
            half recess = 0;
            half lightVisibility = 1;
            #ifdef WALL_RELIEF
                float strength = _WallDepth * (1 - smoothstep(5, 12, distance(_WorldSpaceCameraPos, IN.worldPos)));
                float upright = abs(WorldNormalVector(IN, half3(0,0,1)).y);
                float2 dx = ddx(uv), dy = ddy(uv);
                if (strength > .0001 && upright < .55)
                {
                    float3 eye = normalize(IN.viewDir);
                    float2 stride = eye.xy * (strength / max(.32, abs(eye.z))) / 6;
                    float2 sampleUV = uv;
                    float above = 1 - tex2Dgrad(_HeightMap, sampleUV, dx, dy).r;
                    [unroll] for (int i = 1; i <= 6; ++i)
                    {
                        sampleUV -= stride;
                        float below = 1 - tex2Dgrad(_HeightMap, sampleUV, dx, dy).r - i / 6.0;
                        if (below <= 0)
                        {
                            uv = sampleUV + stride * saturate(-below / max(.0001, above - below));
                            break;
                        }
                        above = below;
                    }
                    half h = tex2Dgrad(_HeightMap, uv, dx, dy).r;
                    // Trace from the displaced hit towards this light. Only its own direct
                    // contribution can be blocked by the lip of a brick or a glaze joint.
                    #ifdef UNITY_PASS_FORWARDADD
                        float3 L = normalize(UnityWorldSpaceLightDir(IN.worldPos));
                        float3 t = normalize(WorldNormalVector(IN,half3(1,0,0)));
                        float3 b = normalize(WorldNormalVector(IN,half3(0,1,0)));
                        float incidence = dot(L,normalize(WorldNormalVector(IN,half3(0,0,1))));
                        if (incidence > .04 && incidence < .85)
                        {
                            float2 slope = float2(dot(L,t),dot(L,b)) * strength / max(.12,incidence);
                            half occluder = 0;
                            [unroll] for (int stepIndex=1; stepIndex<=3; stepIndex++)
                            {
                                float rise = stepIndex * .13;
                                float sampled = tex2Dgrad(_HeightMap,uv+slope*rise,dx,dy).r;
                                occluder = max(occluder,sampled-h-rise);
                            }
                            lightVisibility -= .67 * smoothstep(.012,.13,occluder) * (1-smoothstep(.60,.85,incidence));
                        }
                    #endif
                    recess = (1 - smoothstep(.18, .52, h)) * saturate(strength / .008);
                }
            #endif
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
            half facingUp = saturate((WorldNormalVector(IN, half3(0,0,1)).y - .85) * 8);
            // Long depressions in the road collect water between islands of rough aggregate.
            half basin = tex2D(_StainTex, world.xz * float2(.042,.17) + float2(.73,.14)).r;
            half road = smoothstep(.17, .214, basin + (breakup-.5)*.018);
            standing = max(standing, road * _Rain) * facingUp;
            half film = saturate(standing * 1.8 + .25 * _Rain * islands) * facingUp;
            half wet = saturate(standing + .44 * damp + .22 * film);

            half3 dry = baseColor.rgb * _Color.rgb * surface * (1 - .22 * edge);
            dry = lerp(dry, dry * .85 + half3(.031, .027, .014), residue * .58);

            // Mop line and soot: walls darken where buckets and boots touched them and under the ceiling.
            half wall = 1 - saturate(abs(WorldNormalVector(IN, half3(0, 0, 1)).y) * 1.6);
            half skirting = 1 - smoothstep(.04, .2 + .16 * breakup, height);
            half soot = smoothstep(2.86 + .2 * breakup, 3.42, height);
            half grime = wall * saturate(skirting * .62 + soot * .4) * (.55 + .45 * _Wear.x);
            dry = lerp(dry, dry * half3(.56, .53, .47), grime);
            O.Albedo = dry * (1 - .33 * wet - .14 * damp);

            // Finer photographed grain gives grazing light extra relief without repeating the base map.
            half3 coarseNormal = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale * (1 - .51 * standing));
            half3 grainNormal = UnpackScaleNormal(tex2D(_GrainNormal, uv * 4.63 + float2(.19, .37)),
                _Wear.w * (1 - .77 * standing));
            O.Normal = BlendNormals(coarseNormal, grainNormal);
            O.Metallic = min(finish.r, .16);
            O.Occlusion = (1 - .42 * recess) * lerp(.69, 1, finish.g) * (1 - .29 * edge - .15 * damp);
            half matte = finish.a * _GlossMapScale * (1 - .45 * grime) + _Wear.z * (.3 * islands + .5 * damp);
            O.Smoothness = saturate(lerp(matte, .81, standing * .76));
            O.Smoothness = lerp(O.Smoothness, .94, standing * standing);
            half3 n = normalize(WorldNormalVector(IN, O.Normal));
            O.Emission = O.Albedo * NightBounce(world + n * .32) * O.Occlusion;
            if (film > .005 && abs(world.y - _BathWaterPlane) < .065)
            {
                float4 clip = mul(_BathWaterProjection, float4(world, 1));
                float2 reflectionUV = clip.xy / max(clip.w, .001) * .5 + .5;
                #if UNITY_UV_STARTS_AT_TOP
                    reflectionUV.y = 1 - reflectionUV.y;
                #endif
                float2 rim = min(reflectionUV, 1-reflectionUV);
                half edgeFade = saturate(min(rim.x, rim.y)*28);
                half3 view = normalize(_WorldSpaceCameraPos - world);
                half fresnel = .055 + .79 * pow(1-saturate(dot(n,view)), 4);
                half blur = lerp(5.2, .55, standing * standing);
                half3 reflected = tex2Dlod(_BathWater, float4(reflectionUV + n.xz*.009, 0, blur)).rgb;
                half amount = fresnel * film * edgeFade * step(.01,clip.w);
                O.Emission += reflected * amount;
                O.Albedo *= 1-amount;
            }
            O.Alpha = lightVisibility;
        }
        ENDCG
    }
    FallBack "Standard"
}
