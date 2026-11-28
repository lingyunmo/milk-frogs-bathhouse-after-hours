using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    internal static class NightGrid
    {
        internal static readonly Vector3 Switch = new(280.7f,1.29f,-33.8f);
        internal const float FailureAt = 8.2f;
        // Conductors follow the old return main. Each junction is visible from the next one.
        internal static readonly Vector3[][] Runs = {
            new[] { BrineRoute.Socket, new(273, .16f,-30.5f), new(273,.16f,-34.25f),
                new(280.7f,.16f,-34.25f),Switch },
            new Vector3[] { Switch,new(280.7f,3.6f,-34.5f),new(269,3.1f,-35.6f),new(258,4.1f,-35.6f),
                new(248,3.65f,-35.6f),new(233,5.8f,-38.1f) },
            new Vector3[] { new(258,4.1f,-35.6f),new(251,4.7f,-19),new(256,3.7f,4.8f),new(266,3.1f,10) },
            new Vector3[] { new(248,3.65f,-35.6f),new(228,5,-23.4f),new(193,5.2f,-21),
                new(158,6.2f,-18),new(98,5.7f,-18),new(49,6.8f,-8),new(6,9,8) }
        };
    }

    internal sealed partial class NightGame
    {
        NightTarget brineInterlock;
        bool guideFeedLook;
        void ConnectBrineFeed()
        {
            var r=State.brine;
            if(r.stage!=BrineStage.Carrying || r.arming || State.detained) return;
            r.arming=true; r.gridTime=0; r.eastSupply=false;
            world.PreloadBrineMedia();
            State.beacon=false; State.rescueTime=-1; circuitAnnouncedUntil=-1;
            hunter.JoinGuard(Guard);
            world.Refresh();
            SpeakPA(0);
            soundscape.Play(BrineRoute.Socket,"switch",.75f);
            SaveBrineCheckpoint();
        }

        void CloseEastSupply()
        {
            var r=State.brine;
            if(!r.arming || r.eastSupply) return;
            r.eastSupply=true; r.gridTime=0;
            guideFeedLook=Preferences.CameraMotion>.01f && Vector3.Dot(Player.Eyes.forward,(NightGrid.Switch-Player.Eyes.position).normalized)>.83f;
            soundscape.Play(NightGrid.Switch,"switch",.95f);
            SpeakPA(1);
            world.Refresh();
            SaveBrineCheckpoint();
        }

        void TickBrineFeed(float dt)
        {
            var r=State.brine;
            TickPA(dt);
            if(guideFeedLook && r.eastSupply)
            {
                if(Mathf.Abs(Input.GetAxisRaw("Mouse X"))+Mathf.Abs(Input.GetAxisRaw("Mouse Y"))>.005f ||
                    (Player.Feet-NightGrid.Switch).sqrMagnitude>11) guideFeedLook=false;
                else if(r.gridTime>.85f)
                {
                    // Follow the overhead cables from the switch only while the operator leaves the mouse still.
                    Vector3 to=NightBrineBlast.Source-Player.Feet; to.y=0;
                    Player.transform.rotation=Quaternion.RotateTowards(Player.transform.rotation,Quaternion.LookRotation(to),dt*51f);
                    float pitch=Mathf.DeltaAngle(0,Player.Eyes.localEulerAngles.x);
                    Player.LookDown(Mathf.MoveTowards(pitch,-7f,dt*22f));
                }
            }
            float before=r.gridTime;
            r.gridTime=Mathf.Min(NightGrid.FailureAt,r.gridTime+dt);
            if(r.eastSupply)
            {
                if(before<.75f && r.gridTime>=.75f) soundscape.Play(NightGrid.Runs[1][2],"switch",.93f);
                if(before<2.2f && r.gridTime>=2.2f) soundscape.Play(NightGrid.Runs[1][3],"truck",.94f);
                if(before<4.1f && r.gridTime>=4.1f) soundscape.Play(NightGrid.Runs[1][4],"signal",.75f);
            }
            world.ShowBrineFeed(r);
            var shelter=new Vector3(273,.04f,-32.1f);
            hunter.GuardStep(shelter,NightBrineBlast.Source,dt,false);
            if(r.eastSupply && r.gridTime>=NightGrid.FailureAt) BeginBrineDefence();
        }
    }

    internal sealed partial class NightWorld
    {
        readonly LineRenderer[,] feeders=new LineRenderer[4,3];
        GameObject gridPulses;
        Material feedInk;
        Transform eastKnife;
        Renderer eastLens;
        Material gridAmber,gridJade;
        Light eastPilot;
        int eastKnifeState=-1;

        void DressBrineFeed()
        {
            var at=NightGrid.Switch;
            var tower=new GameObject("东闸应急转接箱").transform;
            tower.SetParent(root,false);
            var cast=Trim(at,paintedIron);
            cast.Box(at+new Vector3(-.27f,-.65f,-.28f),new(.10f,1.30f,.10f));
            cast.Box(at+new Vector3(.27f,-.65f,-.28f),new(.10f,1.30f,.10f));
            Box("防雨转接箱",at+new Vector3(0,-.03f,-.26f),new(.83f,.89f,.36f),paintedIron,tower);
            cast.Box(at+new Vector3(0,.45f,-.22f),new(.96f,.045f,.51f));
            WallSign("东闸应急供电",at+new Vector3(0,.29f,-.063f),180,new(.76f,.17f),.081f);
            eastKnife=new GameObject("双臂应急转换把").transform;
            eastKnife.SetParent(tower,false); eastKnife.position=at+new Vector3(.065f,-.025f,-.034f);
            var handle=new DetailMesh(.88f);
            foreach(float side in new[]{-.094f,.094f})
            {
                handle.Pipe(eastKnife.position+new Vector3(side,0,0),eastKnife.position+new Vector3(side,-.14f,.105f),.017f);
                cast.Pipe(eastKnife.position+Vector3.right*side-Vector3.right*.026f,
                    eastKnife.position+Vector3.right*side+Vector3.right*.026f,.037f);
            }
            handle.Place(eastKnife,"铰臂",steel);
            var grip=new DetailMesh(.9f);
            grip.Pipe(eastKnife.position+new Vector3(-.127f,-.14f,.105f),eastKnife.position+new Vector3(.127f,-.14f,.105f),.032f);
            grip.Place(eastKnife,"胶木横握把",red);
            foreach(float side in new[]{-.35f,.35f})
                foreach(float high in new[]{.34f,-.39f})
                    cast.Pipe(at+new Vector3(side,high,-.09f),at+new Vector3(side,high,-.066f),.018f);
            for(int rib=0;rib<6;rib++)
                cast.Box(at+new Vector3(.26f,-.16f-rib*.033f,-.065f),new(.15f,.013f,.03f));
            gridAmber=Material(new(.59f,.24f,.045f)); gridJade=Material(new(.12f,.57f,.39f));
            gridAmber.EnableKeyword("_EMISSION"); gridAmber.SetColor("_EmissionColor",new Color(2.4f,.88f,.13f));
            gridJade.EnableKeyword("_EMISSION"); gridJade.SetColor("_EmissionColor",new Color(.18f,1.8f,.95f));
            var lens=Box("回路指示灯",at+new Vector3(-.25f,.10f,-.025f),new(.11f,.13f,.055f),gridAmber,tower);
            eastLens=lens.GetComponent<Renderer>();
            var bezel=Trim(at,brass);
            for(int edge=0;edge<4;edge++)
            {
                var offset=edge<2?new Vector3(edge==0?-.067f:.067f,0,0):new Vector3(0,edge==2?-.075f:.075f,0);
                bezel.Box(lens.transform.position+offset+Vector3.forward*.013f,edge<2?new(.012f,.16f,.03f):new(.144f,.012f,.03f));
            }
            eastPilot=new GameObject("箱盖下的检修灯").AddComponent<Light>();
            eastPilot.transform.SetParent(tower,false); eastPilot.transform.position=at+new Vector3(-.12f,.36f,.39f);
            MountHorn(at+new Vector3(-.42f,1.24f,-.31f),tower);
            eastPilot.range=2.15f; eastPilot.intensity=1.22f; eastPilot.shadows=LightShadows.None;
            gridPulses=new GameObject("旧回水线 · 送电");
            gridPulses.transform.SetParent(root,false);
            feedInk=new Material(Resources.Load<Shader>("BrineConduit"));
            for(int i=0;i<NightGrid.Runs.Length;i++)
            {
                var run=NightGrid.Runs[i];
                for(int n=1;n<run.Length;n++)
                {
                    Trim(run[n],steel).Pipe(run[n-1],run[n],i==0?.039f:.052f);
                    // Ceramic insulator and stanchion at the change of direction.
                    if(i==1 && n>=2 && n<run.Length-1) {
                        var foot=new Vector3(run[n].x,.03f,run[n].z);
                        Trim(foot,paintedIron).Pipe(foot,run[n]-Vector3.up*.16f,.059f);
                        Trim(run[n],tiles).Pipe(run[n]-Vector3.up*.20f,run[n]-Vector3.up*.05f,.103f);
                    }
                }
                for(int j=0;j<3;j++)
                {
                    var strip=new GameObject(j==2?"穿行的电弧":"导管荧光").AddComponent<LineRenderer>();
                    strip.transform.SetParent(gridPulses.transform,false);
                    strip.sharedMaterial=feedInk; strip.useWorldSpace=true; strip.numCornerVertices=3;
                    strip.shadowCastingMode=ShadowCastingMode.Off; strip.receiveShadows=false;
                    strip.startWidth=strip.endWidth=j==0?.25f:j==1?.083f:.14f;
                    feeders[i,j]=strip;
                }
            }
            HideBrineFeed();
        }

        void MountHorn(Vector3 pivot,Transform tower)
        {
            var direction=new Vector3(-.70f,-.16f,.54f).normalized;
            var rot=Quaternion.LookRotation(direction);
            var profile=new[]{new Vector2(0,.069f),new(.15f,.089f),new(.26f,.14f),new(.45f,.28f),
                new(.46f,.29f),new(.485f,.265f),new(.33f,.16f),new(.17f,.068f),new(.15f,.025f)};
            const int count=32;
            var verts=new Vector3[count*profile.Length];
            var uv=new Vector2[verts.Length];
            var triangles=new int[count*(profile.Length-1)*6];
            for(int j=0;j<profile.Length;j++)
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count;
                var radius=profile[j].y*(1+.018f*Mathf.Sin(a*3+.9f));
                verts[j*count+i]=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,profile[j].x);
                uv[j*count+i]=new Vector2((float)i/count*2,profile[j].x*2);
                if(j==profile.Length-1) continue;
                int v=j*count+i, next=j*count+(i+1)%count,k=v*6;
                triangles[k]=v; triangles[k+1]=next; triangles[k+2]=v+count;
                triangles[k+3]=next; triangles[k+4]=next+count; triangles[k+5]=v+count;
            }
            var bell=new Mesh{name="搪瓷扩音喇叭"};
            bell.vertices=verts; bell.uv=uv; bell.triangles=triangles; bell.RecalculateNormals(); bell.RecalculateTangents();
            var horn=new GameObject("东堤紧急广播 · 号筒");
            horn.transform.SetParent(tower,false); horn.transform.SetPositionAndRotation(pivot,rot);
            horn.AddComponent<MeshFilter>().sharedMesh=bell; horn.AddComponent<MeshRenderer>().sharedMaterial=steel;
            Trim(pivot,paintedIron).Pipe(pivot-Vector3.up*1.0f,pivot,.041f);
            Trim(pivot,brass).Pipe(pivot+direction*.13f,pivot+direction*.30f,.049f);
        }

        internal void HideBrineFeed() { if(gridPulses) gridPulses.SetActive(false); }

        internal void ShowBrineFeed(BrineRun r)
        {
            if(!gridPulses) return;
            int state=r.arming ? r.eastSupply ? 2 : 1 : r.Committed ? 3 : 0;
            if(state!=eastKnifeState)
            {
                eastKnifeState=state;
                eastKnife.localRotation=Quaternion.Euler(state>=2?-28:32,0,0);
                eastPilot.enabled=state==1 || state==2;
                eastPilot.color=state==2?new(.47f,.83f,.68f):new(1f,.68f,.32f);
                eastLens.sharedMaterial=state>=2?gridJade:state==1?gridAmber:paintedIron;
                gridPulses.SetActive(r.arming);
            }
            if(!r.arming) return;
            for(int i=0;i<NightGrid.Runs.Length;i++)
            {
                float travel=i==0 && r.eastSupply?100:r.gridTime*(i==3?61:i==0?5:16)-(i==2?22:i==3?33:0);
                if(i>0 && !r.eastSupply) travel=0;
                var color=i==0?new Color(1,.53f,.13f):Color.Lerp(new(.18f,.96f,.81f),new(1,.35f,.095f),Mathf.InverseLerp(3.5f,7,r.gridTime));
                float flutter=.84f+.16f*Mathf.Sin(r.gridTime*(i==0?7:17)+i*2);
                float pulse=Mathf.Repeat((r.eastSupply?r.gridTime:Time.time)*(i==3?65:19),i==3?250:62);
                for(int j=0;j<3;j++)
                {
                    var line=feeders[i,j];
                    var tint=j==2?Color.Lerp(color,Color.white,.64f):color;
                    tint.a=(j==0?.17f:j==1?.85f:1)*flutter;
                    line.startColor=line.endColor=tint;
                    DrawFeed(line,NightGrid.Runs[i],j==2?pulse:0,j==2?Mathf.Min(travel,pulse+2.8f):travel);
                }
            }
        }

        static void DrawFeed(LineRenderer line,Vector3[] path,float from,float to)
        {
            int count=0; float at=0;
            if(to>from)
            {
                line.positionCount=path.Length+1;
                for(int i=1;i<path.Length;i++)
                {
                    float length=Vector3.Distance(path[i-1],path[i]);
                    if(to>at && from<at+length)
                    {
                        var lift=Vector3.up*.055f;
                        if(count==0) line.SetPosition(count++,Vector3.Lerp(path[i-1],path[i],(from-at)/length)+lift);
                        line.SetPosition(count++,Vector3.Lerp(path[i-1],path[i],(to-at)/length)+lift);
                    }
                    at+=length;
                }
            }
            line.positionCount=count; line.enabled=count>=2;
        }
    }
}
