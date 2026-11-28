Shader "AfterHours/Brine Fire Cloud"
{
    Properties { _Noise ("Turbulence", 3D) = "" {} _Age ("Seconds", Float) = 0 _Final ("Final flare", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent-40" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Front ZWrite Off ZTest Always Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler3D _Noise;
            float _Age, _Final;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            struct v2f { float4 pos:SV_POSITION; float3 local:TEXCOORD0; float4 screen:TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.local=v.vertex.xyz; o.screen=ComputeScreenPos(o.pos); return o;
            }
            // One lobe of the rolling cap. Adjacent sectors differ in radius, height and shape.
            float lobe(float2 radial,float y,float sector,float growth,float rise)
            {
                float phi=sector*.78539816;
                float variation=sin(phi*3+1.5);
                float2 direction=float2(cos(phi),sin(phi));
                float2 centre=direction*(.055+.182*growth)*(.9+.13*variation);
                float2 lateral=radial-centre;
                return 1-length(float3(lateral/(.065+.093*growth+.016*variation),
                    (y-rise-.047*variation)/(.074+.087*growth)));
            }
            // x = signed mass, y = folded cell detail, z = fine soot, w = height in the column.
            float4 sampleMass(float3 p,float growth,float rise,float clock)
            {
                float y=p.y+.5;
                float2 radial=p.xz-float2(.043,-.021)*y*y;
                float r=length(radial);
                float2 swivel=radial+float2(sin(y*13-clock*.11),cos(y*11+clock*.09))*.013;
                float angle=atan2(swivel.y,swivel.x);
                float sector=angle*1.27323954;
                float lo=floor(sector);
                float cap=max(lobe(swivel,y,lo,growth,rise),lobe(swivel,y,lo+1,growth,rise));
                float dome=1-length(float3(swivel/(.074+.123*growth),(y-rise-.046)/(.078+.106*growth)));
                float fraction=saturate(y/rise);
                float radius=.035+.12*pow(1-fraction,2)+.067*pow(fraction,3);
                float core=min(1-length(swivel)/radius,(rise-y)/.11);
                // Two climbing plumes bend around the central updraft instead of merging into one smooth funnel.
                float2 split=float2(sin(y*8.2+clock*.09),cos(y*7.4+clock*.09))*(.013+.035*sin(fraction*3.14159));
                float braid=min((.045-min(length(radial-split),length(radial+split)))*18,(rise-y)/.10);
                float body=max(max(cap,dome),max(core,braid));
                if (body < -.87) return float4(-1,0,0,y);
                float3 uv=float3(swivel.x,y-clock*.015,swivel.y);
                float4 slow=tex3Dlod(_Noise,float4(uv*1.14,0));
                float4 fine=tex3Dlod(_Noise,float4(uv*2.83+slow.r*.055+float3(0,-clock*.006,.22),0));
                float fold=slow.g*.64+fine.g*.36;
                float shape=body+(slow.r-.50)*.71+(fold-.48)*.95+(fine.r-.5)*.16;
                return float4(shape,fold,fine.r,y);
            }
            float4 frag(v2f i):SV_Target
            {
                float3 ro=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz, dir=normalize(i.local-ro);
                float3 inv=1.0/(dir+1e-6);
                float3 a=(-.5-ro)*inv,b=(.5-ro)*inv,lo=min(a,b),hi=max(a,b);
                float start=max(max(max(lo.x,lo.y),lo.z),0),end=min(min(hi.x,hi.y),hi.z);
                float3 wd=normalize(mul((float3x3)unity_ObjectToWorld,dir));
                float eye=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                end=min(end,eye/max(.001,-mul((float3x3)UNITY_MATRIX_V,wd).z)*length(mul((float3x3)unity_WorldToObject,wd)));
                if(end<=start) discard;
                float growth=1-exp(-_Age*.8), rise=.105+.52*growth, clock=fmod(_Age,1200.);
                float furnace=.21+.79*exp(-_Age*.09)+_Final*.95;
                const int steps=37;
                float stride=(end-start)/steps;
                float distance=start+stride*(.15+.67*frac(dot(floor(i.pos.xy),float2(.75487766,.56984029))));
                float4 result=0;
                [loop] for(int k=0;k<steps;k++,distance+=stride)
                {
                    float3 p=ro+dir*distance;
                    float4 s=sampleMass(p,growth,rise,clock);
                    float density=smoothstep(-.10,.39,s.x);
                    if(density<.006) continue;
                    float exposed=sampleMass(p+float3(.038,.058,-.023),growth,rise,clock).x;
                    float sun=saturate(.17+1.65*(s.x-exposed));
                    float ridge=smoothstep(.29,.74,s.y);
                    float silver=saturate(sun*(.26+.78*ridge)+s.z*.12);
                    float3 soot=lerp(float3(.014,.012,.015),float3(.51,.43,.345),silver*silver);
                    // Incandescence breaks through narrow pockets between the ash lobes. The cap
                    // keeps its dark belly and lit ridges even during the final pulse.
                    float pocket=1-smoothstep(.25,.52,s.y+.17*(s.z-.5));
                    float under=saturate(1.22-1.18*s.w/rise);
                    float ember=pocket*(.16+under*.89)*furnace;
                    float3 emission=lerp(float3(.82,.045,.004),float3(2.8,.69,.055),saturate(ember*.95));
                    float3 light=soot+emission*ember*1.55;
                    float alpha=1-exp(-density*stride*26);
                    result.rgb+=(1-result.a)*alpha*light;
                    result.a+=(1-result.a)*alpha;
                    if(result.a>.986) break;
                }
                return result;
            }
            ENDCG
        }
    }
    FallBack Off
}
