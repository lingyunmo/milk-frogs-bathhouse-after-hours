Shader "AfterHours/Brine Fire Cloud"
{
    Properties
    {
        _Field ("Sculpted cloud", 3D) = "" {}
        _Age ("Seconds", Float) = 0
        _Final ("Final flare", Float) = 0
    }
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

            sampler3D _Field;
            float _Age, _Final;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            struct v2f { float4 pos:SV_POSITION; float3 local:TEXCOORD0; float4 screen:TEXCOORD1; };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.local=v.vertex.xyz; o.screen=ComputeScreenPos(o.pos); return o;
            }

            float4 field(float3 p,float clock)
            {
                // Advect the stored relief in three axes; rolling eddies move around the cap
                // and climb the stem without an animated 3D texture sequence.
                float ring=length(p.xz);
                float phase=clock*.48;
                float skirt=smoothstep(.40,.68,p.y);
                float curl=sin(ring*25-p.y*13+phase);
                p.xz+=float2(sin(p.y*16+phase),cos(p.y*12-phase))*(.004+.005*skirt);
                p.y+=.007*curl*skirt+.0024*sin(37*p.x+29*p.z-phase*1.7);
                p.xz*=1+.026*skirt*cos(ring*25-p.y*13+phase);
                return tex3Dlod(_Field,float4(p.x+.5,p.y,p.z+.5,0));
            }

            float4 frag(v2f i):SV_Target
            {
                float3 ro=mul(unity_WorldToObject,float4(_WorldSpaceCameraPos,1)).xyz, dir=normalize(i.local-ro);
                float growth=1-exp(-_Age*.68);
                float3 extent=float3(.23+.77*growth,.11+.89*growth,.23+.77*growth);
                float3 inv=1.0/(dir+1e-6);
                float3 a=(float3(-.5*extent.x,-.5,-.5*extent.z)-ro)*inv;
                float3 b=(float3(.5*extent.x,extent.y-.5,.5*extent.z)-ro)*inv;
                float3 lo=min(a,b),hi=max(a,b);
                float start=max(max(max(lo.x,lo.y),lo.z),0),end=min(min(hi.x,hi.y),hi.z);
                float3 wd=normalize(mul((float3x3)unity_ObjectToWorld,dir));
                float eye=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.screen)));
                end=min(end,eye/max(.001,-mul((float3x3)UNITY_MATRIX_V,wd).z)*length(mul((float3x3)unity_WorldToObject,wd)));
                if(end<=start) discard;
                float3 scale=1/extent;
                float3 origin=(ro+float3(0,.5,0))*scale, ray=dir*scale;
                float clock=fmod(_Age,1200.), furnace=.59+1.26*exp(-_Age*.091)+_Final*.92;
                float3 result=0;
                float trans=1;
                float t=start, shellStep=min((end-start)/69.,.012/length(ray));
                // Empty volume is traversed coarsely. Back up on the first occupied sample
                // so the thin, high-contrast outer folds are resolved without screen dither.
                bool inside=false;
                [loop] for(int k=0;k<116 && t<end;k++)
                {
                    float3 p=origin+ray*t;
                    float4 s=field(p,clock);
                    if(s.r<.005)
                    {
                        inside=false;
                        t+=2.3*shellStep;
                        continue;
                    }
                    if(!inside)
                    {
                        inside=true;
                        t=max(start,t-1.48*shellStep);
                        p=origin+ray*t;
                        s=field(p,clock);
                    }
                    float stride=min(shellStep,end-t);
                    // Use a deep shadow toe: the game's AgX grade otherwise raises the ash
                    // underside to the same tan as its sunlit shoulders.
                    float sunlight=pow(saturate((s.g-.085)*1.27),2.36);
                    float upper=smoothstep(.52,.79,p.y);
                    float sootInk=lerp(.34,1.07,smoothstep(.23,.75,s.a));
                    float3 soot=lerp(float3(.0032,.0040,.0063),float3(.37,.35,.326),
                                     sunlight*lerp(.43,1,upper))*sootInk;
                    float crack=smoothstep(.205,.56,s.b);
                    float stem=1-smoothstep(.46,.67,p.y);
                    float bellyBand=1-smoothstep(.045,.148,abs(p.y-.557));
                    float glow=(.17*s.b+crack*crack*.97)*furnace*(.25+.75*max(stem,bellyBand));
                    float3 light=soot + float3(2.45,.122,.013)*glow
                                      + float3(.61,.49,.052)*glow*glow;
                    float alpha=1-exp(-s.r*stride*length(ray)*139);
                    result+=trans*alpha*light;
                    trans*=1-alpha;
                    if(trans<.009) break;
                    t+=stride;
                }
                return float4(result,1-trans);
            }
            ENDCG
        }
    }
    FallBack Off
}
