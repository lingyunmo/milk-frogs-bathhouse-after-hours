using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    internal sealed partial class NightGame
    {
        internal void SetPressureFeedback(Vector2 offset, float hearing)
        {
            if (grade) grade.SetPressureOffset(offset * Preferences.CameraMotion);
            soundscape.SetPressureHearing(hearing);
        }
    }

    internal sealed partial class NightBrineBlast
    {
        const float FrontDuration = 12.5f;
        static readonly int Radius = Shader.PropertyToID("_Radius");
        readonly AudioSource[] masonry = new AudioSource[3];
        readonly AudioLowPassFilter[] masonryFilters = new AudioLowPassFilter[3];
        AudioSource pressureSound;
        Mesh frontMesh;
        MeshRenderer frontRenderer;
        Material frontMaterial;
        float frontTime = -1, impactAge = -1, frontDustClock;
        int masonryIndex;
        float receptionAge = -1, receptionStrength;
        internal bool PressureFeedbackActive => receptionAge >= 0;
        bool pressurePassed;
        internal float FrontRadius => frontTime < 0 ? 0 : 2 + 18.5f * frontTime;
        internal bool PressurePassed => pressurePassed;

        void BuildFront()
        {
            cloud.SetTexture("_Field",Resources.Load<Texture3D>("BrineCloudField"));

            var node = new GameObject("沿街抬升的尘浪");
            node.transform.SetParent(transform,false);
            node.transform.position = Source;
            frontMaterial = new Material(Resources.Load<Shader>("BrineDustFront"));
            frontMaterial.SetTexture("_Noise",noise);
            // The upturned lip travels fastest at ankle height. A sloping wake trails it.
            Vector2[] profile = {new(-8.2f,-.18f),new(-5.4f,.94f),new(-3.5f,2.8f),
                new(-1.7f,4.4f),new(-.45f,3.6f),new(.86f,2.08f),new(2.4f,-.18f)};
            const int bearings = 160;
            int columns = profile.Length;
            var vertices = new Vector3[bearings * columns];
            var uv = new Vector2[vertices.Length];
            var indices = new int[bearings * (columns-1) * 6];
            int index = 0;
            for (int i=0; i<bearings; i++)
            {
                float phi = i * (2*Mathf.PI/bearings);
                float dx=Mathf.Sin(phi), dz=Mathf.Cos(phi);
                for(int j=0;j<columns;j++)
                {
                    int a=i*columns+j;
                    vertices[a]=new Vector3(dx,profile[j].y,dz);
                    uv[a]=new Vector2(profile[j].x,Mathf.Max(0,profile[j].y/4.4f));
                    if(j==columns-1) continue;
                    int b=((i+1)%bearings)*columns+j;
                    indices[index++]=a; indices[index++]=a+1; indices[index++]=b;
                    indices[index++]=b; indices[index++]=a+1; indices[index++]=b+1;
                }
            }
            frontMesh = new Mesh { name="尘浪横截面", vertices=vertices, uv=uv, triangles=indices };
            frontMesh.bounds = new Bounds(Vector3.up*3,new Vector3(490,16,490));
            node.AddComponent<MeshFilter>().sharedMesh=frontMesh;
            frontRenderer=node.AddComponent<MeshRenderer>();
            frontRenderer.sharedMaterial=frontMaterial;
            frontRenderer.allowOcclusionWhenDynamic=false;
            frontRenderer.shadowCastingMode=ShadowCastingMode.Off;
            frontRenderer.receiveShadows=false;
            frontRenderer.enabled=false;
            var pressureClip=Resources.Load<AudioClip>("Audio/brine-pressure");
            var masonryClip=Resources.Load<AudioClip>("Audio/brine-masonry");
            pressureSound=Emitter("到达镜头的声浪",pressureClip,0);
            for(int i=0;i<masonry.Length;i++)
            {
                masonry[i]=Emitter("楼板落地 "+(i+1),masonryClip,1);
                masonryFilters[i]=masonry[i].gameObject.AddComponent<AudioLowPassFilter>();
                masonryFilters[i].enabled=false;
            }
        }

        AudioSource Emitter(string label, AudioClip clip,float spatial)
        {
            var source=new GameObject(label).AddComponent<AudioSource>();
            source.transform.SetParent(transform,false);
            source.playOnAwake=false;
            source.dopplerLevel=0;
            source.spatialBlend=spatial;
            source.rolloffMode=AudioRolloffMode.Linear;
            source.minDistance=4;
            source.maxDistance=66;
            source.clip=clip;
            return source;
        }

        void StartFront()
        {
            frontTime=0;
            frontDustClock=0;
            pressurePassed=false;
            frontMaterial.SetFloat(Radius,FrontRadius);
            frontMaterial.SetFloat(Age,0);
            frontRenderer.enabled=true;
        }

        void TickFront(float dt)
        {
            if(frontTime<0 || dt<=0) return;
            frontTime+=dt;
            float radius=FrontRadius;
            frontMaterial.SetFloat(Radius,radius);
            frontMaterial.SetFloat(Age,frontTime);
            var eyes=game.Player.Eyes;
            Vector3 delta=eyes.position-Source;
            float distance=new Vector2(delta.x,delta.z).magnitude;
            if(radius>7 && Mathf.Abs(radius-distance)<42 && (frontDustClock-=dt)<=0)
            {
                frontDustClock=.15f;
                float bearing=Mathf.Atan2(delta.x,delta.z);
                for(int i=0;i<4;i++)
                {
                    float phi=bearing+(Noise01()-.5f)*1.9f;
                    var outwards=new Vector3(Mathf.Sin(phi),0,Mathf.Cos(phi));
                    float size=2.8f+Noise01()*3.7f;
                    var pos=Source+outwards*(radius-3.3f+Noise01()*2.6f)+Vector3.up*(.25f+Noise01()*1.4f);
                    Emit(pos,outwards*(10.3f+Noise01()*5.1f)+Vector3.up*(1.2f+Noise01()*1.85f),
                        new Color(.30f,.26f,.233f,.72f),size,.85f+Noise01()*.7f);
                }
            }
            if(!pressurePassed && radius>=distance)
            {
                pressurePassed=true;
                bool sheltered=world.IsInsideBuilding(game.Player.Feet);
                float far=Mathf.Lerp(1,.38f,Mathf.InverseLerp(40,210,distance));
                pressureSound.volume=(sheltered ? .43f : .88f)*far;
                pressureSound.pitch=sheltered ? .78f : 1;
                pressureSound.ignoreListenerPause=finale>=0;
                pressureSound.Play();
                receptionAge=0;
                receptionStrength=(sheltered ? .39f : 1)*far;
                if(!sheltered && eyes.position.y<8)
                    BrushLens(eyes,Vector3.ProjectOnPlane(delta,Vector3.up).normalized);
            }
            if(frontTime>=FrontDuration)
            {
                frontTime=-1;
                frontRenderer.enabled=false;
            }
        }

        void TickReception(float dt)
        {
            if(receptionAge<0) return;
            receptionAge+=dt;
            if(receptionAge>=.83f)
            {
                ReleasePressure();
                return;
            }
            float t=receptionAge;
            float kick=receptionStrength*Mathf.Exp(-11*t);
            Vector2 offset=new(Mathf.Sin(43*t)*.0041f,Mathf.Sin(64*t+.38f)*.006f);
            float hearing=receptionStrength*(1-Mathf.SmoothStep(0,1,t/.83f));
            foreach(var filter in masonryFilters)
            {
                filter.enabled=true;
                filter.cutoffFrequency=Mathf.Lerp(20000,1300,hearing);
            }
            game.SetPressureFeedback(offset*kick,hearing);
        }

        void ReleasePressure()
        {
            if(receptionAge<0) return;
            receptionAge=-1;
            foreach(var filter in masonryFilters) filter.enabled=false;
            game.SetPressureFeedback(Vector2.zero,0);
        }

        void BrushLens(Transform eyes, Vector3 travel)
        {
            // Particles live in world space: turning or running out of the bank clears the lens.
            for(int i=0;i<29;i++)
            {
                var start=eyes.position+eyes.forward*(.24f+Noise01()*1.85f)
                    +eyes.right*((Noise01()-.5f)*2.6f)+Vector3.up*((Noise01()-.55f)*1.45f);
                bool grit=i%3==0;
                Emit(start,travel*(2.4f+Noise01()*4)+Vector3.up*(.28f+Noise01()),
                    grit ? new Color(.76f,.64f,.48f,.9f) : new Color(.55f,.47f,.38f,.55f),
                    grit ? .02f+Noise01()*.035f : .48f+Noise01()*.58f,.34f+Noise01()*.48f);
            }
        }

        void ImpactDust(Vector3 point,float width)
        {
            if((game.Player.Eyes.position-point).sqrMagnitude>90*90) return;
            int fans=Mathf.Clamp(Mathf.RoundToInt(width*1.2f),5,14);
            for(int i=0;i<fans;i++)
            {
                float angle=(i+Noise01())*(2*Mathf.PI/fans);
                var outwards=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Emit(point+outwards*(Noise01()*Mathf.Min(width*.25f,2))+Vector3.up*.18f,
                    outwards*(1.2f+Noise01()*3)+Vector3.up*(.2f+Noise01()*.74f),
                    new Color(.48f,.42f,.36f,.61f),1.1f+Noise01()*1.4f,1.2f+Noise01()*1.1f);
            }
            if(age<impactAge+.19f) return;
            impactAge=age;
            var source=masonry[masonryIndex++%masonry.Length];
            source.transform.position=point;
            source.volume=Mathf.Lerp(.19f,.41f,Mathf.Clamp01(width/12));
            source.pitch=.83f+Noise01()*.36f;
            source.ignoreListenerPause=finale>=0;
            source.Play();
        }

        void ResetFront()
        {
            ReleasePressure();
            frontTime=impactAge=-1;
            pressurePassed=false;
            frontRenderer.enabled=false;
            pressureSound.Stop();
            foreach(var source in masonry) source.Stop();
        }

        void DisposeFront()
        {
            if(frontMesh) Destroy(frontMesh);
            if(frontMaterial) Destroy(frontMaterial);
        }
    }
}
