Shader "AfterHours/OvercastNight"
{
    Properties
    {
        _MainTex ("Sky panorama", 2D) = "gray" {}
        _NightHorizon ("Distant fog", Color) = (0,0,0,1)
        _CloudWeight ("Cloud light", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            half4 _NightHorizon;
            half _CloudWeight;
            struct SkyVertex { float4 pos : SV_POSITION; float3 direction : TEXCOORD0; };
            SkyVertex Vert(float4 vertex : POSITION)
            {
                SkyVertex o;
                o.pos = UnityObjectToClipPos(vertex);
                o.direction = vertex.xyz;
                return o;
            }
            half4 Frag(SkyVertex i) : SV_Target
            {
                float3 ray = normalize(i.direction);
                float2 uv = float2(frac(.68 - atan2(ray.z, ray.x) / UNITY_TWO_PI), .5 + asin(clamp(ray.y,-1,1)) / UNITY_PI);
                half3 sky = tex2D(_MainTex, uv).rgb;
                half density = dot(sky, half3(.22,.71,.07));
                half overcast = density / (density + .75);
                half3 cloud = lerp(half3(.003,.005,.009), half3(.053,.084,.114), overcast * overcast);
                half horizon = exp(-abs(ray.y) * 9) * (.4 + .6 * saturate(dot(ray.xz, float2(.89,-.45))));
                return half4(lerp(_NightHorizon.rgb, cloud + half3(.025,.016,.009)*horizon, _CloudWeight), 1);
            }
            ENDCG
        }
    }
}
