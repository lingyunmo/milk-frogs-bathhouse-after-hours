Shader "AfterHours/HunterListenRipple"
{
    Properties
    {
        _Progress ("Lifetime", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+6" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend SrcAlpha One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Progress;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(appdata_img vertex)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(vertex.vertex);
                output.uv = vertex.texcoord * 2 - 1;
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                float time = _Progress;
                float front = .19 + .76 * (1 - (1-time)*(1-time));
                float radius = length(input.uv);
                float distanceToFront = radius - front;
                float width = .024 + .015 * time;
                float primaryBand = distanceToFront / width;
                float trailingBand = (distanceToFront + width) / (width * 2);
                float crest = exp2(-2.5 * primaryBand * primaryBand);
                float diffuseTail = exp2(-2 * trailingBand * trailingBand);
                float breath = smoothstep(0,.18,time) * (1-smoothstep(.42,1,time));
                float angle = atan2(input.uv.y, input.uv.x);
                float sectors = .68 + .20 * sin(11 * angle + 4*radius) + .12 * sin(19 * angle - 3*time);
                return fixed4(.51,.67,.66, saturate((.24 * crest + .08 * diffuseTail) * sectors * breath));
            }
            ENDCG
        }
    }
    FallBack Off
}
