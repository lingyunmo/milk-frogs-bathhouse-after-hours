Shader "AfterHours/Brine Dust Front"
{
    Properties { _Noise ("Billows", 3D) = "" {} _Radius ("Radius", Float) = 1 _Age ("Travel time", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler3D _Noise;
            float _Radius,_Age;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            struct v2f { float4 pos:SV_POSITION; float3 local:TEXCOORD0; float2 uv:TEXCOORD1; float4 screen:TEXCOORD2; float bank:TEXCOORD3; };
            v2f vert(appdata_base v)
            {
                v2f o;
                float2 bearing=v.vertex.xz;
                float angle=atan2(bearing.x,bearing.y);
                float ripple=.8*sin(angle*9.0+.4)+.54*sin(angle*19.-.7)+.34*sin(angle*37.+_Age*.24);
                float width=min(1,_Radius*.14);
                float bank=smoothstep(-.50,.73,.59*sin(angle*5+.2)+.71*sin(angle*11-.8)+.47*cos(angle*23));
                float radius=_Radius+width*(ripple*2.15+v.texcoord.x*(1+bank*.36));
                o.local=float3(bearing.x*radius, v.vertex.y*(.25+bank*1.11+.092*ripple)*width, bearing.y*radius);
                o.pos=UnityObjectToClipPos(float4(o.local,1));
                o.uv=v.texcoord.xy; o.bank=bank;
                o.screen=ComputeScreenPos(o.pos);
                // Soft intersections with the street use the deformed vertex.
                o.screen.z=-UnityObjectToViewPos(float4(o.local,1)).z;
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float drift=_Age*.27;
                float3 pos=float3(i.local.x*.034,i.local.y*.075-drift,i.local.z*.034);
                float4 macro=tex3D(_Noise,pos);
                float4 curl=tex3D(_Noise,pos*2.65+float3(macro.r*.29,drift*.44,macro.g*.38));
                float grain=tex3D(_Noise,pos*5.74+curl.g*.15).r;
                float billow=.41*macro.g+.40*curl.g+.19*grain;
                float crest=1-smoothstep(.66,1.0,i.uv.y+.46*(.48-billow));
                float ground=smoothstep(-.22,.27,i.local.y);
                float width=smoothstep(-8.2,-5.5,i.uv.x)*(1-smoothstep(.25,2.4,i.uv.x));
                float light=saturate(.15+.72*i.uv.y+.51*(billow-.43));
                float3 color=lerp(float3(.022,.021,.023),float3(.23,.183,.147),light*light);
                // Open gaps divide the dusty front into banks sweeping between the buildings.
                float thick=(.35+.65*i.bank)*(.63+.37*smoothstep(.23,.56,curl.g));
                float scene=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                float contact=saturate((scene-i.screen.z)*1.5);
                float opacity=thick*crest*ground*width*contact*(.24+.64*smoothstep(.16,.64,billow));
                opacity*=smoothstep(0,.32,_Age)*(1-smoothstep(148,188,_Radius));
                return float4(color,opacity);
            }
            ENDCG
        }
    }
    FallBack Off
}
