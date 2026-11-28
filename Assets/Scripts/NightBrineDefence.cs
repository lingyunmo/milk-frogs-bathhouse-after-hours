using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightGame
    {
        NightTarget brineExit;
        readonly NightTarget[] brineWheels=new NightTarget[2];
        // The isolation shutters draw the main surge along three parallel spillways. The inland
        // sentries occupy their own pieces of floor and never count against the three waves.
        static readonly Vector3[] InlandPosts = { new(-4,.04f,12), new(30,3.65f,12),
            new(101,.04f,-18), new(160,.04f,-18), new(199,.04f,4), new(243,.04f,3.7f),
            new(-4,-3.56f,12), new(4,3.64f,12), new(-7,7.24f,12), new(31,7.24f,12),
            new(30,.04f,35), new(50,.04f,14), new(70,.04f,-3), new(118,.04f,-29),
            new(154,.04f,5), new(197,.04f,12), new(248,.04f,11), new(289,.04f,-5),
            new(314,.04f,16), new(268,.04f,13), new(330,.04f,-17) };
        readonly Dictionary<BrineEcho,Transform> drifters = new();
        Material drifterSkin, tracerInk;
        readonly MaterialPropertyBlock brineCloth = new();
        void ReleaseBrineMaterials()
        {
            ReleaseBrineDetector();
            if(saltWater) Destroy(saltWater);
            if (drifterSkin) Destroy(drifterSkin);
            if (tracerInk) Destroy(tracerInk);
        }
        LineRenderer bolt;
        float triggerIn, allyIn, contactIn, boltLife;
        static readonly Vector3 Guard = new(275,.04f,-32.1f);
        static readonly Vector3 Escort = new(303,.04f,-32.35f);

        void BeginBrineDefence()
        {
            var run=State.brine;
            if(run.stage!=BrineStage.Carrying || State.detained) return;
            if (State.dispatch.carrying)
            {
                State.dispatch.loadFeet=Player.Feet; State.dispatch.carrying=false; RefreshLoad();
            }
            run.arming=false;
            run.tideSeed=UnityEngine.Random.Range(0,6);
            run.stage=BrineStage.Defence; run.spawnIn=3; run.shutter=run.breath=100;
            for(int i=0;i<InlandPosts.Length;i++)
                if (FindInlandFloor(InlandPosts[i], out var floor))
                    run.echoes.Add(new BrineEcho { feet=floor, home=floor, lane=i+3,life=2,stride=i });
            State.beacon=false; State.rescueTime=-1; circuitAnnouncedUntil=-1;
            State.spawn=new Vector3(276,.065f,-30.4f); State.yaw=270;
            SyncBrineScene(true); world.Refresh();
            ScheduleFalloutPA();
            guideFeedLook=false;
            grade?.Shock();

            SaveProgress();
        }

        void RestoreBrineScene() => SyncBrineScene(false);

        void SyncBrineScene(bool impact)
        {
            ResetBrineDetector();
            world.SetBrineBlast(State.brine.Committed && !State.brine.arming,impact);
            foreach(var visual in drifters.Values) if(visual) Destroy(visual.gameObject);
            drifters.Clear();
            ClearGuardContact();
            if(bolt) bolt.enabled=false;
            triggerIn=contactIn=boltLife=0; allyIn=2;
            Player.EquipBrineGun(State.brine.Live);
            world.BriefBrineWheels(State.brine.Fighting ? State.brine.ShellLane : -1);
            if(State.brine.arming) { world.PreloadBrineMedia(); hunter.JoinGuard(Guard); }
            if(!State.brine.Live) return;
            hunter.JoinGuard(State.brine.gateReleased ? Escort : Guard);
            foreach(var echo in State.brine.echoes) SpawnVisual(echo);
        }

        void SpawnVisual(BrineEcho echo)
        {
            if(!drifterSkin)
            {
                drifterSkin=new Material(Resources.Load<Shader>("BrineSilhouette"));
                drifterSkin.mainTexture=Resources.Load<Texture2D>("BrineDrifter");
            }
            var go=GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name="潮返者";
            go.GetComponent<Collider>().enabled=false;
            var renderer=go.GetComponent<Renderer>();
            renderer.sharedMaterial=drifterSkin;
            DressDrifter(renderer,echo);
            go.transform.SetParent(transform,false);
            float height=echo.kind switch { DrifterKind.Dredger=>2.15f, DrifterKind.Runner=>1.57f, _=>1.83f+.06f*(echo.lane%3) };
            go.transform.localScale=new Vector3(height*(echo.kind==DrifterKind.Dredger?.78f:.66f),height,1);
            drifters.Add(echo,go.transform);
            PoseDrifter(echo);
        }

        void DressDrifter(Renderer renderer,BrineEcho echo)
        {
            brineCloth.SetFloat("_Phase",echo.stride);
            brineCloth.SetFloat("_Crust",echo.armoured?1:0);
            brineCloth.SetFloat("_StepRate",echo.kind==DrifterKind.Runner?9:echo.kind==DrifterKind.Dredger?3.2f:5);
            brineCloth.SetColor("_Coat",echo.kind switch {
                DrifterKind.Runner=>new Color(1,.54f,.32f), DrifterKind.Dredger=>new Color(.56f,.80f,.84f),
                _=>new Color(.73f,.87f,.79f) });
            renderer.SetPropertyBlock(brineCloth);
        }

        void PoseDrifter(BrineEcho echo)
        {
            var skin=drifters[echo];
            float sway=Mathf.Sin(echo.stride);
            skin.position=echo.feet+Vector3.up*(skin.localScale.y*.5f+.032f*Mathf.Abs(sway));
            Vector3 away=skin.position-Player.Eyes.position; away.y=0;
            if(away.sqrMagnitude>.01f)
                skin.rotation=Quaternion.LookRotation(away)*Quaternion.Euler(0,0,3.2f*sway);
            if(echo.fromGrate && echo.emergence<1.42f)
            {
                float up=Mathf.SmoothStep(0,1,Mathf.Clamp01((echo.emergence-.24f)/1.18f));
                skin.position-=Vector3.up*((1-up)*1.43f);
                skin.rotation*=Quaternion.Euler(-32*(1-up),0,-13*(1-up));
            }
        }

        void RemoveEcho(BrineEcho echo)
        {
            if(drifters.Remove(echo,out var visual)) Destroy(visual.gameObject);
            State.brine.echoes.Remove(echo);
        }

        void TickBrine(float dt)
        {
            var r=State.brine;
            TickFalloutPA(dt);
            TickGuardFalls(dt);
            if (TickBrineRadiation(dt)) return;
            triggerIn-=dt; allyIn-=dt; contactIn-=dt; boltLife-=dt;
            if(bolt && boltLife<=0) bolt.enabled=false;
            r.heat=Mathf.Max(0,r.heat-dt*(r.overheated?.38f:.23f));
            if(r.overheated && r.heat<=.22f) r.overheated=false;
            if(Input.GetMouseButton(0)) FireBrine();
            Player.SetProjectorHeat(r.heat,r.overheated);
            if(!r.Fighting)
            {
                var lever=new Vector3(283.1f,.04f,-32.1f);
                var goal=r.gateReleased ? Escort : lever;
                if(!r.gateReleased) hunter.GuardStep(goal,goal+Vector3.right,dt,false,5.1f);
                else TickBrineGuard(dt,true);
                if(!r.gateReleased && (hunter.Position-lever).sqrMagnitude<.25f)
                {
                    r.gateReleased=true;
                    SpeakPA(4);
                    world.Refresh(); soundscape.Play(lever,"truck",.78f);
                    // Older flight saves have no tunnel sentries. New ones spawn once, when the gate opens.
                    foreach(var at in new[]{new Vector3(291,.04f,-32),new Vector3(299,.04f,-30)})
                    {
                        var echo=new BrineEcho { lane=1,kind=DrifterKind.Runner,feet=at,life=2,stride=at.x };
                        r.echoes.Add(echo); SpawnVisual(echo);
                    }
                    Notify("奶娃扯断了闸链。东廊还有东西，跑！",4);
                    SaveBrineCheckpoint();
                }
                if(r.gateReleased)
                {
                    foreach(var echo in r.echoes)
                    {
                        if (echo.lane>=3) { PatrolInland(echo,dt); if(ContactPlayer(echo)) return; continue; }
                        var to=new Vector3(Player.Feet.x,echo.feet.y,Player.Feet.z);
                        if((to-echo.feet).sqrMagnitude<100 && FloorRoutes.CanMove(echo.feet,to))
                            echo.feet=Vector3.MoveTowards(echo.feet,to,dt*1.9f);
                        echo.stride+=dt*6; PoseDrifter(echo);
                        if(ContactPlayer(echo)) return;
                    }
                    if((r.escapeTime-=dt)<=0) EndBrine(false);
                }
                return;
            }
            r.divertCooldown=Mathf.Max(0,r.divertCooldown-dt);
            r.divertTime=Mathf.Max(0,r.divertTime-dt);
            if(r.released<BrineRoute.WaveSize(r.wave) && (r.spawnIn-=dt)<=0)
            {
                var echo=BrineRoute.NextEcho(r);
                r.echoes.Add(echo); SpawnVisual(echo);
                if(echo.fromGrate) soundscape.Play(echo.feet,"guard-fall",.91f);
                r.released++;
                r.spawnIn=BrineRoute.SpawnDelay(r.wave,r.released);
                if(echo.armoured)
                {
                    soundscape.Play(BrineRoute.Handwheel(echo.lane/2),"signal",.88f);
                    Notify(echo.lane==0 ? "南管在响。开南手轮，冲掉它的壳！" : "北管在响。开北手轮，冲掉它的壳！",4);
                }
                soundscape.Play(echo.feet,"pipe-leak",.54f);
            }
            int advancing=0;
            for(int i=r.echoes.Count-1;i>=0;i--)
            {
                var echo=r.echoes[i];
                if(echo.lane>=3)
                {
                    PatrolInland(echo,dt);
                    if(ContactPlayer(echo)) return;
                    continue;
                }
                if(echo.fromGrate && echo.emergence<1.42f)
                {
                    float before=echo.emergence;
                    echo.emergence+=dt;
                    world.TipBackwashGrate(echo.emergence);
                    if(before<.82f && echo.emergence>=.82f)
                    {
                        soundscape.Play(echo.feet,"tool",1); SprayBrine(echo.feet+Vector3.up*.24f,Vector3.forward,12);
                    }
                    PoseDrifter(echo); advancing++; continue;
                }
                Vector3 goal;
                {
                    bool flushing=r.divertTime>0 && echo.lane==r.divertedLane;
                    goal=flushing ? BrineRoute.Mouth(echo.lane) : BrineRoute.Breach(echo.lane);
                    if(flushing && echo.armoured)
                    {
                        echo.armoured=false;
                        DressDrifter(drifters[echo].GetComponent<Renderer>(),echo);
                        soundscape.Play(echo.feet,"brine-hit",.85f);
                    }
                    if(!flushing && echo.kind==DrifterKind.Runner)
                    {
                        Vector3 feet=Player.Feet;
                        Vector3 flat=new(feet.x,echo.feet.y,feet.z);
                        if(Mathf.Abs(feet.y-echo.feet.y)<.8f && (flat-echo.feet).sqrMagnitude<81 &&
                            (flat-BrineRoute.Breach(echo.lane)).sqrMagnitude<100 &&
                            FloorRoutes.CanMove(echo.feet,flat)) goal=flat;
                    }
                }
                echo.feet=Vector3.MoveTowards(echo.feet,goal,dt*BrineRoute.Speed(echo,r.wave));
                echo.stride+=dt*(echo.kind==DrifterKind.Runner?8.5f:4.5f+.36f*(echo.lane%3));
                PoseDrifter(echo);
                if(ContactPlayer(echo)) return;
                float left=(BrineRoute.Breach(echo.lane)-echo.feet).magnitude;
                if(left<.24f)
                {
                    r.shutter=Mathf.Max(0,r.shutter-(echo.kind==DrifterKind.Dredger?38:18)); RemoveEcho(echo);
                    soundscape.Play(BrineRoute.Socket,"land",.95f);
                    if(r.shutter<=0) { EndBrine(false); return; }
                    continue;
                }
                advancing++;
            }
            world.BriefBrineWheels(r.ShellLane);
            if(TickBrineGuard(dt,false)) advancing--;
            if(advancing==0 && r.released==BrineRoute.WaveSize(r.wave))
            {
                if(r.wave==2)
                {
                    r.stage=BrineStage.Flight; r.escapeTime=26;
                    // The inland streets stay occupied while the two tunnel pursuers enter.
                    world.Refresh();
                    Notify("奶娃拍了两下地，转身奔向东闸。",5);
                }
                else
                {
                    r.wave++; r.released=0; r.spawnIn=4.5f;
                    if(r.wave==1) SpeakPA(5);
                    Notify(r.wave==1 ? "两岸都在响。当心贴地跑的红衣。" : "三道暗孔全开了！",4);
                }
                SaveBrineCheckpoint();
            }
        }

        static bool FindInlandFloor(Vector3 anchor,out Vector3 floor)
        {
            int mask=~(1<<NightPlayer.PlayerLayer);
            for(int i=0;i<9;i++)
            {
                float angle=i*Mathf.PI/4;
                var point=i==0?anchor:anchor+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*1.25f;
                if(!Physics.Raycast(point+Vector3.up*.45f,Vector3.down,out var hit,.8f,mask,QueryTriggerInteraction.Ignore)
                    || hit.normal.y<.75f) continue;
                floor=hit.point+Vector3.up*.045f;
                if(!Physics.CheckCapsule(floor+Vector3.up*.34f,floor+Vector3.up*1.45f,.29f,mask,QueryTriggerInteraction.Ignore)) return true;
            }
            floor=default;
            return false;
        }

        void PatrolInland(BrineEcho echo,float dt)
        {
            var delta=Player.Feet-echo.feet;
            if(delta.sqrMagnitude>55*55) return;
            var home=echo.home==Vector3.zero ? InlandPosts[echo.lane-3] : echo.home;
            bool hunting=Mathf.Abs(delta.y)<1.3f && delta.sqrMagnitude<9*9 && (Player.Feet-home).sqrMagnitude<13*13
                && !Physics.Linecast(echo.feet+Vector3.up*1.15f,Player.Eyes.position,~(1<<NightPlayer.PlayerLayer),QueryTriggerInteraction.Ignore);
            var goal=hunting ? Player.Feet : home+Vector3.right*(1.5f*Mathf.Sin(echo.stride*.18f));
            goal.y=home.y;
            var step=Vector3.MoveTowards(echo.feet,goal,dt*(hunting?1.48f:.82f));
            if(FloorRoutes.CanMove(echo.feet,step) && Physics.Raycast(step+Vector3.up*.32f,Vector3.down,.65f,
                ~(1<<NightPlayer.PlayerLayer),QueryTriggerInteraction.Ignore)) echo.feet=step;
            echo.stride+=dt*(hunting?6.4f:4.5f);
            PoseDrifter(echo);
        }

        bool ContactPlayer(BrineEcho echo)
        {
            if(contactIn>0 || (echo.feet-Player.Feet).sqrMagnitude>=1.55f ||
                Physics.Linecast(echo.feet+Vector3.up,Player.Eyes.position,~(1<<NightPlayer.PlayerLayer),QueryTriggerInteraction.Ignore)) return false;
            contactIn=1.25f; State.brine.breath-=25; grade?.Shock();
            soundscape.Play(Player.Feet,"land",.8f);
            if(State.brine.breath>0) return false;
            EndBrine(false);
            return true;
        }

        void SaveBrineCheckpoint()
        {
            if (Player.Body.isGrounded && !Player.Vaulting && !Player.InHideSpot)
            {
                State.spawn=Player.Feet+Vector3.up*.06f;
                State.yaw=Player.transform.eulerAngles.y;
            }
            SaveProgress();
        }

        void FireBrine()
        {
            var run=State.brine;
            if(!run.Live || triggerIn>0 || run.overheated) return;
            triggerIn=.24f;
            run.heat=Mathf.Min(1,run.heat+.16f);
            if(run.heat>=1)
            {
                run.overheated=true;
                soundscape.Play(Player.Feet,"pipe-leak",.82f);
            }
            var eye=Player.Eyes;
            var ray=new Ray(eye.position,eye.forward);
            float reach=Physics.Raycast(ray,out var wall,55,~(1<<NightPlayer.PlayerLayer),QueryTriggerInteraction.Ignore) ? wall.distance : 55;
            BrineEcho hit=null;
            foreach(var echo in State.brine.echoes)
            {
                if(echo.fromGrate && echo.emergence<.85f) continue;
                float t=Vector3.Dot(echo.feet+Vector3.up-eye.position,eye.forward);
                if(t<=0 || t>=reach) continue;
                var p=ray.GetPoint(t)-echo.feet;
                if(p.y<.10f || p.y>1.92f || p.x*p.x+p.z*p.z>.46f*.46f) continue;
                hit=echo; reach=t;
            }
            var start=Player.BrineMuzzle();
            ShowBolt(start,ray.GetPoint(reach));
            soundscape.Play(eye.position+eye.forward*.3f,"brine-shot",.78f);
            if(hit==null) return;
            soundscape.Play(hit.feet,"brine-hit",.66f);
            if(hit.armoured) return;
            if(--hit.life<=0) RemoveEcho(hit);
            else if(hit.lane<3 && hit.kind!=DrifterKind.Runner) hit.feet.x=Mathf.Max(BrineRoute.Mouth(hit.lane).x,hit.feet.x-.46f);
        }

        void ShowBolt(Vector3 start,Vector3 end)
        {
            if(!bolt)
            {
                var arc=new GameObject("回流脉冲");
                arc.transform.SetParent(transform,false);
                bolt=arc.AddComponent<LineRenderer>(); bolt.positionCount=2;
                tracerInk=new Material(Shader.Find("Sprites/Default")); bolt.sharedMaterial=tracerInk;
                bolt.startColor=new Color(.64f,1,.92f); bolt.endColor=new Color(.38f,.85f,1,.4f);
                bolt.startWidth=.045f; bolt.endWidth=.015f;
            }
            bolt.SetPosition(0,start); bolt.SetPosition(1,end); bolt.enabled=true; boltLife=.065f;
        }

        void EndBrine(bool throughHatch)
        {
            var run=State.brine;
            if(!run.Live || throughHatch && (run.stage!=BrineStage.Flight || !run.gateReleased)) return;
            ResetPA();
            run.missedExit=!throughHatch && run.stage==BrineStage.Flight && run.escapeTime<=0;
            run.stage=throughHatch ? BrineStage.Clear : BrineStage.Overrun;
            State.escaped=throughHatch; State.ended=true; State.detained=false;
            RestoreBrineScene();
            world.FlareBrineBlast();
            SaveProgress();
            SetScreen(throughHatch ? NightScreen.Escaped : NightScreen.Caught);
        }
    }
}
