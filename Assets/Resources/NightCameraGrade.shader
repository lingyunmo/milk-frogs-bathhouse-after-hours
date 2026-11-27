Shader "Hidden/AfterHours/NightCameraGrade"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        sampler2D _BloomTex;
        sampler2D _OcclusionTex;
        sampler2D _LensDirt;
        sampler2D _BlurTex;
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        float4 _DepthTexel;  // 1 / depth texture size, set from script

        float4 _Direction;   // blur step in UV
        float4 _Projection;  // P00, P11 of the world camera
        float4 _Bloom;       // threshold, knee, intensity, lens dirt
        float4 _Occlusion;   // radius (m), intensity, final strength, fade distance (m)
        float4 _Grade;       // exposure, contrast, saturation, grain
        float4 _Lens;        // vignette, aberration, fear, shock
        float4 _Menu;        // blur, dim
        float _Scatter;

        static const float3 LumaWeights = float3(.2126, .7152, .0722);

        float Luma(float3 rgb)
        {
            return dot(rgb, LumaWeights);
        }

        float2 AuxiliaryUV(float2 uv)
        {
            #if UNITY_UV_STARTS_AT_TOP
            if (_MainTex_TexelSize.y < 0) uv.y = 1 - uv.y;
            #endif
            return uv;
        }

        float Hash(float2 p)
        {
            float3 q = frac(float3(p.xyx) * .1031);
            q += dot(q, q.yzx + 33.33);
            return frac((q.x + q.y) * q.z);
        }

        // ---------------------------------------------------------------- Ambient occlusion

        float EyeDepth(float2 uv)
        {
            return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
        }

        float3 ViewPosition(float2 uv, float depth)
        {
            return float3((uv * 2 - 1) / _Projection.xy * depth, depth);
        }

        float3 ViewNormal(float2 uv, float3 center)
        {
            float2 tx = float2(_DepthTexel.x, 0);
            float2 ty = float2(0, _DepthTexel.y);
            float3 right = ViewPosition(uv + tx, EyeDepth(uv + tx)) - center;
            float3 left = center - ViewPosition(uv - tx, EyeDepth(uv - tx));
            float3 up = ViewPosition(uv + ty, EyeDepth(uv + ty)) - center;
            float3 down = center - ViewPosition(uv - ty, EyeDepth(uv - ty));
            // Take the flatter neighbour on each axis so silhouettes do not bend the normal.
            float3 dx = abs(right.z) < abs(left.z) ? right : left;
            float3 dy = abs(up.z) < abs(down.z) ? up : down;
            float3 n = cross(dy, dx);
            float len = length(n);
            return len > 1e-8 ? n / len : float3(0, 0, -1);
        }

        float4 FragOcclusion(v2f_img i) : SV_Target
        {
            const int Samples = 12;
            float2 uv = i.uv;
            float depth = EyeDepth(uv);
            if (depth > _Occlusion.w) return 1;
            float3 center = ViewPosition(uv, depth);
            float3 normal = ViewNormal(uv, center);
            float radius = _Occlusion.x;
            float2 reach = min(radius * .5 * _Projection.xy / depth, .11);
            float2 pixel = uv * _ScreenParams.xy;
            float spin = frac(52.9829189 * frac(dot(pixel, float2(.06711056, .00583715)))) * UNITY_TWO_PI;
            float radius2 = radius * radius;
            float sum = 0;
            [unroll]
            for (int s = 0; s < Samples; ++s)
            {
                float t = (s + .5) / Samples;
                float angle = spin + t * 43.98; // seven turns of a spiral
                float2 offset = float2(cos(angle), sin(angle)) * reach * t;
                float2 at = uv + offset;
                float3 v = ViewPosition(at, EyeDepth(at)) - center;
                float vv = dot(v, v);
                float vn = dot(v, normal);
                float falloff = max(radius2 - vv, 0);
                sum += falloff * falloff * falloff * max((vn - .006 * depth) / (vv + .012), 0);
            }
            float occlusion = saturate(1 - sum * _Occlusion.y * 5 / (radius2 * radius2 * radius2 * Samples));
            occlusion = isfinite(occlusion) ? occlusion : 1;
            occlusion = lerp(occlusion, 1, smoothstep(_Occlusion.w * .55, _Occlusion.w, depth));
            return occlusion;
        }

        float4 FragOcclusionBlur(v2f_img i) : SV_Target
        {
            float center = EyeDepth(i.uv);
            float total = 0, weight = 0;
            [unroll]
            for (int k = -4; k <= 4; ++k)
            {
                float2 at = i.uv + _Direction.xy * k;
                float depth = EyeDepth(at);
                float w = exp(-k * k * .11) * saturate(1 - abs(depth - center) / (center * .045 + .04));
                total += tex2D(_MainTex, at).r * w;
                weight += w;
            }
            return total / max(weight, 1e-4);
        }

        float4 FragOcclusionApply(v2f_img i) : SV_Target
        {
            float4 scene = tex2D(_MainTex, i.uv);
            float occlusion = tex2D(_OcclusionTex, AuxiliaryUV(i.uv)).r;
            // Forward lighting cannot separate ambient light, so keep direct pools from going muddy.
            float lit = smoothstep(.10, .95, Luma(scene.rgb));
            scene.rgb *= lerp(1, occlusion, _Occlusion.z * (1 - .62 * lit));
            return scene;
        }

        // ---------------------------------------------------------------- Bloom and blur

        float3 Box13(sampler2D source, float2 uv, float2 texel)
        {
            float3 a = tex2D(source, uv + texel * float2(-1, -1)).rgb;
            float3 b = tex2D(source, uv + texel * float2( 0, -1)).rgb;
            float3 c = tex2D(source, uv + texel * float2( 1, -1)).rgb;
            float3 d = tex2D(source, uv + texel * float2(-.5, -.5)).rgb;
            float3 e = tex2D(source, uv + texel * float2( .5, -.5)).rgb;
            float3 f = tex2D(source, uv + texel * float2(-1,  0)).rgb;
            float3 g = tex2D(source, uv).rgb;
            float3 h = tex2D(source, uv + texel * float2( 1,  0)).rgb;
            float3 j = tex2D(source, uv + texel * float2(-.5,  .5)).rgb;
            float3 k = tex2D(source, uv + texel * float2( .5,  .5)).rgb;
            float3 l = tex2D(source, uv + texel * float2(-1,  1)).rgb;
            float3 m = tex2D(source, uv + texel * float2( 0,  1)).rgb;
            float3 n = tex2D(source, uv + texel * float2( 1,  1)).rgb;
            float3 o = (d + e + j + k) * .125;
            o += (a + b + f + g) * .03125;
            o += (b + c + g + h) * .03125;
            o += (f + g + l + m) * .03125;
            o += (g + h + m + n) * .03125;
            return o;
        }

        float3 Tent(sampler2D source, float2 uv, float2 texel)
        {
            float4 d = texel.xyxy * float4(1, 1, -1, 0);
            float3 s = tex2D(source, uv - d.xy).rgb;
            s += tex2D(source, uv - d.wy).rgb * 2;
            s += tex2D(source, uv - d.zy).rgb;
            s += tex2D(source, uv + d.zw).rgb * 2;
            s += tex2D(source, uv).rgb * 4;
            s += tex2D(source, uv + d.xw).rgb * 2;
            s += tex2D(source, uv + d.zy).rgb;
            s += tex2D(source, uv + d.wy).rgb * 2;
            s += tex2D(source, uv + d.xy).rgb;
            return s * .0625;
        }

        float4 FragPrefilter(v2f_img i) : SV_Target
        {
            float2 t = _MainTex_TexelSize.xy;
            // A luminance-weighted average keeps single hot pixels from flickering into large discs.
            float3 a = tex2D(_MainTex, i.uv + t * float2(-1, -1)).rgb;
            float3 b = tex2D(_MainTex, i.uv + t * float2( 1, -1)).rgb;
            float3 c = tex2D(_MainTex, i.uv + t * float2(-1,  1)).rgb;
            float3 d = tex2D(_MainTex, i.uv + t * float2( 1,  1)).rgb;
            float wa = 1 / (1 + Luma(a)), wb = 1 / (1 + Luma(b)), wc = 1 / (1 + Luma(c)), wd = 1 / (1 + Luma(d));
            float3 color = (a * wa + b * wb + c * wc + d * wd) / (wa + wb + wc + wd);
            color = min(color * _Grade.x, 48);
            float brightness = max(color.r, max(color.g, color.b));
            float soft = clamp(brightness - _Bloom.x + _Bloom.y, 0, 2 * _Bloom.y);
            soft = soft * soft / (4 * _Bloom.y + 1e-5);
            float contribution = max(soft, brightness - _Bloom.x) / max(brightness, 1e-5);
            return float4(color * contribution, 1);
        }

        float4 FragDownsample(v2f_img i) : SV_Target
        {
            return float4(Box13(_MainTex, i.uv, _MainTex_TexelSize.xy), 1);
        }

        float4 FragUpsample(v2f_img i) : SV_Target
        {
            float3 lower = Tent(_MainTex, i.uv, _MainTex_TexelSize.xy);
            float3 level = tex2D(_BloomTex, i.uv).rgb;
            return float4(level + lower * _Scatter, 1);
        }

        // ---------------------------------------------------------------- Tone mapping and grade

        float3 AgxCurve(float3 x)
        {
            float3 x2 = x * x;
            float3 x4 = x2 * x2;
            return 15.5 * x4 * x2 - 40.14 * x4 * x + 31.96 * x4 - 6.868 * x2 * x + .4298 * x2 + .1191 * x - .00232;
        }

        // AgX after Troy Sobotka, with Benjamin Wrensch's polynomial fit.
        float3 AgxEncode(float3 v)
        {
            const float3x3 inset = float3x3(
                .842479062253094, .0423282422610123, .0423756549057051,
                .0784335999999992, .878468636469772, .0784336,
                .0792237451477643, .0791661274605434, .879142973793104);
            const float low = -12.47393, high = 4.026069;
            v = mul(v, inset);
            v = clamp(log2(max(v, 1e-10)), low, high);
            return AgxCurve((v - low) / (high - low));
        }

        float3 AgxDecode(float3 v)
        {
            const float3x3 outset = float3x3(
                1.19687900512017, -.0528968517574562, -.0529716355144438,
                -.0980208811401368, 1.15190312990417, -.0980434501171241,
                -.0990297440797205, -.0989611768448433, 1.15107367264116);
            return pow(max(mul(v, outset), 0), 2.2);
        }

        float4 FragComposite(v2f_img i) : SV_Target
        {
            float2 uv = i.uv;
            float2 aux = AuxiliaryUV(uv);
            float fear = _Lens.z, shock = _Lens.w;

            // Lateral aberration grows toward the frame edge, never in the reading centre.
            float2 fromCenter = uv - .5;
            float edge = dot(fromCenter, fromCenter);
            float2 shift = fromCenter * edge * _Lens.y * 4;
            float3 scene;
            scene.r = tex2D(_MainTex, uv - shift).r;
            scene.g = tex2D(_MainTex, uv).g;
            scene.b = tex2D(_MainTex, uv + shift).b;
            scene = max(scene, 0) * _Grade.x;

            float3 bloom = tex2D(_BloomTex, aux).rgb;
            float dirt = tex2D(_LensDirt, aux).r;
            scene += bloom * (_Bloom.z + dirt * _Bloom.w);

            // Display encoding: filmic shoulder that keeps lamp cores warm instead of clipping to flat yellow.
            float3 display = AgxEncode(scene);

            // A restrained "night print" look: lifted printed blacks, cool shadow, tungsten highlights.
            float tone = Luma(display);
            display = lerp(display * float3(.955, 1.012, 1.03), display * float3(1.04, .995, .935), smoothstep(.18, .78, tone));
            display = (display - .42) * _Grade.y + .42;
            tone = Luma(display);
            display = lerp(tone.xxx, display, _Grade.z * (1 - .42 * fear));
            display = lerp(display, display * float3(1.10, .88, .86) + float3(.012, 0, 0), fear * .55);
            // Printed blacks: the deepest shadow keeps a trace of green-black instead of clipping.
            display = display + float3(.020, .024, .023) * (1 - smoothstep(0, .16, tone));

            // Elliptical lens falloff.
            float2 frame = fromCenter * float2(_ScreenParams.x / _ScreenParams.y * .78, 1);
            float vignette = smoothstep(.95, .22, length(frame) * 1.18);
            display *= lerp(1, vignette, _Lens.x);

            // Shock frame: the screen flashes toward bled-out red as the creature lands.
            display = lerp(display, float3(display.r * 1.5 + .05, display.g * .45, display.b * .42), shock * .8);

            // Silver-halide style grain, strongest in the shadows and mids, plus 8-bit dithering.
            float2 pixel = uv * _ScreenParams.xy;
            float seed = frac(_Time.y * 7.31);
            float grain = Hash(pixel + seed * 917.3) + Hash(pixel * 1.31 + seed * 311.7) - 1;
            display += grain * _Grade.w * (.35 + .65 * (1 - Luma(display)));
            display += (Hash(pixel + 17.13) - .5) / 255;

            return float4(saturate(AgxDecode(saturate(display))), 1);
        }

        float4 FragMenu(v2f_img i) : SV_Target
        {
            float3 sharp = tex2D(_MainTex, i.uv).rgb;
            float3 soft = tex2D(_BlurTex, AuxiliaryUV(i.uv)).rgb;
            float3 color = lerp(sharp, soft, _Menu.x);
            float tone = Luma(color);
            color = lerp(color, lerp(tone.xxx, color, .55), _Menu.y);
            color *= lerp(1, .52, _Menu.y);
            return float4(color, 1);
        }
        ENDCG

        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragOcclusion
            #pragma target 3.0
            ENDCG }

        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragOcclusionBlur
            #pragma target 3.0
            ENDCG }

        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragOcclusionApply
            #pragma target 3.0
            ENDCG }

        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragPrefilter
            #pragma target 3.0
            ENDCG }

        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDownsample
            #pragma target 3.0
            ENDCG }

        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUpsample
            #pragma target 3.0
            ENDCG }

        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragComposite
            #pragma target 3.0
            ENDCG }

        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragMenu
            #pragma target 3.0
            ENDCG }
    }
    Fallback Off
}
