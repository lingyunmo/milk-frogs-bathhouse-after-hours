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
            new(101,.04f,-18), new(160,.04f,-18), new(199,.04f,4), new(243,.04f,3.7f) };
        readonly Dictionary<BrineEcho,Transform> drifters = new();
        Material drifterSkin, tracerInk;
        void ReleaseBrineMaterials()
        {
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
            run.stage=BrineStage.Defence; run.spawnIn=3; run.shutter=run.breath=100;
            for(int i=0;i<InlandPosts.Length;i++)
                run.echoes.Add(new BrineEcho { feet=InlandPosts[i],lane=i+3,life=2,stride=i });
            State.beacon=false; State.rescueTime=-1; circuitAnnouncedUntil=-1;
            State.spawn=new Vector3(276,.065f,-30.4f); State.yaw=270;
            RestoreBrineScene(); world.Refresh();
            soundscape.Play(BrineRoute.Socket,"signal",.95f);
            Notify("警报压过了车喇叭。奶娃背朝你，堵住了闸口。　鼠标左键 · 回流炮",7);
            SaveProgress();
        }

        void RestoreBrineScene()
        {
            foreach(var visual in drifters.Values) if(visual) Destroy(visual.gameObject);
            drifters.Clear();
            if(bolt) bolt.enabled=false;
            triggerIn=contactIn=boltLife=0; allyIn=2;
            Player.EquipBrineGun(State.brine.Live);
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
            var cloth=new MaterialPropertyBlock();
            cloth.SetFloat("_Phase",echo.stride);
            cloth.SetColor("_Coat",(echo.lane%3) switch { 0=>new Color(.73f,.87f,.79f),1=>new Color(.91f,.70f,.48f),_=>new Color(.65f,.73f,.87f) });
            renderer.SetPropertyBlock(cloth);
            go.transform.SetParent(transform,false);
            float height=1.83f+.09f*(echo.lane%3);
            go.transform.localScale=new Vector3(height*2/3f,height,1);
            drifters.Add(echo,go.transform);
            PoseDrifter(echo);
        }

        void PoseDrifter(BrineEcho echo)
        {
            var skin=drifters[echo];
            float sway=Mathf.Sin(echo.stride);
            skin.position=echo.feet+Vector3.up*(skin.localScale.y*.5f+.032f*Mathf.Abs(sway));
            Vector3 away=skin.position-Player.Eyes.position; away.y=0;
            if(away.sqrMagnitude>.01f)
                skin.rotation=Quaternion.LookRotation(away)*Quaternion.Euler(0,0,3.2f*sway);
        }

        void RemoveEcho(BrineEcho echo)
        {
            if(drifters.Remove(echo,out var visual)) Destroy(visual.gameObject);
            State.brine.echoes.Remove(echo);
        }

        void TickBrine(float dt)
        {
            var r=State.brine;
            triggerIn-=dt; allyIn-=dt; contactIn-=dt; boltLife-=dt;
            if(bolt && boltLife<=0) bolt.enabled=false;
            if(Input.GetMouseButton(0) && triggerIn<=0) FireBrine();
            if(!r.Fighting)
            {
                var lever=new Vector3(283.1f,.04f,-32.1f);
                var goal=r.gateReleased ? Escort : lever;
                hunter.GuardStep(goal,goal+Vector3.right,dt,false);
                if(!r.gateReleased && (hunter.Position-lever).sqrMagnitude<.25f)
                {
                    r.gateReleased=true;
                    world.Refresh(); soundscape.Play(lever,"truck",.78f);
                    Notify("奶娃扯断了闸链。跑！",4);
                    SaveBrineCheckpoint();
                }
                if((r.escapeTime-=dt)<=0) EndBrine(false);
                return;
            }
            r.divertCooldown=Mathf.Max(0,r.divertCooldown-dt);
            r.divertTime=Mathf.Max(0,r.divertTime-dt);
            if(r.released<BrineRoute.WaveSize(r.wave) && (r.spawnIn-=dt)<=0)
            {
                int lane=(r.released*2+r.wave)%3;
                var echo=new BrineEcho {lane=lane,feet=BrineRoute.Mouth(lane),stride=r.released*1.73f,
                    life=r.wave==2 && r.released%3==0 ? 3 : r.wave!=0 ? 2 : 1};
                r.echoes.Add(echo); SpawnVisual(echo);
                r.released++;
                r.spawnIn=r.wave==1 && r.released%2==1 ? .38f : 2.1f-r.wave*.24f;
                soundscape.Play(echo.feet,"pipe-leak",.54f);
            }
            BrineEcho nearest=null;
            float remaining=float.PositiveInfinity;
            int advancing=0;
            for(int i=r.echoes.Count-1;i>=0;i--)
            {
                var echo=r.echoes[i];
                bool inland=echo.lane>=3;
                Vector3 goal;
                if(inland)
                {
                    var post=InlandPosts[echo.lane-3];
                    var delta=Player.Feet-echo.feet;
                    bool sense=delta.sqrMagnitude<36 && Mathf.Abs(delta.y)<1.25f &&
                        (Player.Feet-post).sqrMagnitude<64;
                    goal=sense?Player.Feet:post+Vector3.right*(1.45f*Mathf.Sin(echo.stride*.18f));
                    goal.y=post.y;
                    var ray=goal-echo.feet;
                    if(Physics.Raycast(echo.feet+Vector3.up*.85f,ray.normalized,.55f,~(1<<NightPlayer.PlayerLayer),QueryTriggerInteraction.Ignore))
                        goal=echo.feet;
                }
                else goal=r.divertTime>0 && echo.lane==r.divertedLane ? BrineRoute.Mouth(echo.lane) : BrineRoute.Breach(echo.lane);
                echo.feet=Vector3.MoveTowards(echo.feet,goal,dt*(inland?.82f:BrineRoute.Speed(echo,r.wave)));
                echo.stride+=dt*(4.5f+.36f*(echo.lane%3));
                PoseDrifter(echo);
                if((echo.feet-Player.Feet).sqrMagnitude<1.55f && contactIn<=0)
                {
                    contactIn=1.25f; r.breath-=25; grade?.Shock();
                    soundscape.Play(Player.Feet,"land",.8f);
                    if(r.breath<=0) { EndBrine(false); return; }
                }
                if(inland) continue;
                float left=(BrineRoute.Breach(echo.lane)-echo.feet).magnitude;
                if(left<.24f)
                {
                    r.shutter-=20; RemoveEcho(echo);
                    soundscape.Play(BrineRoute.Socket,"land",.95f);
                    if(r.shutter<=0) { EndBrine(false); return; }
                    continue;
                }
                advancing++;
                if(left<remaining) { nearest=echo; remaining=left; }
            }
            // Crossing lanes costs the ally time. It catches stragglers, but cannot hold all three mouths alone.
            var postAt=nearest!=null && remaining<7 ? BrineRoute.Breach(nearest.lane)-Vector3.right*1.8f : Guard;
            bool strike=nearest!=null && (nearest.feet-hunter.Position).sqrMagnitude<3.2f && allyIn<=0;
            hunter.GuardStep(postAt,nearest?.feet ?? BrineRoute.Mouth(1),dt,strike);
            if(strike)
            {
                allyIn=4.8f; soundscape.Play(nearest.feet,"land",.88f);
                if(--nearest.life<=0) { RemoveEcho(nearest); advancing--; }
            }
            if(advancing==0 && r.released==BrineRoute.WaveSize(r.wave))
            {
                if(r.wave==2)
                {
                    r.stage=BrineStage.Flight; r.escapeTime=52;
                    foreach(var stray in r.echoes.ToArray()) RemoveEcho(stray);
                    world.Refresh();
                    Notify("奶娃拍了两下地，转身奔向东闸。",5);
                }
                else
                {
                    r.wave++; r.released=0; r.spawnIn=4.5f;
                    Notify(r.wave==1 ? "两边的暗孔同时响了。扳动岸边回水闸，能顶回一线。" : "最后一道闸裂开了。穿厚衣的还站着。",4);
                }
                SaveBrineCheckpoint();
            }
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
            triggerIn=.23f;
            var eye=Player.Eyes;
            var ray=new Ray(eye.position,eye.forward);
            float reach=Physics.Raycast(ray,out var wall,55,~(1<<NightPlayer.PlayerLayer),QueryTriggerInteraction.Ignore) ? wall.distance : 55;
            BrineEcho hit=null;
            foreach(var echo in State.brine.echoes)
            {
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
            if(--hit.life<=0) RemoveEcho(hit);
            else if(hit.lane<3) hit.feet.x=Mathf.Max(BrineRoute.Mouth(hit.lane).x,hit.feet.x-.46f);
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
            run.missedExit=!throughHatch && run.stage==BrineStage.Flight;
            run.stage=throughHatch ? BrineStage.Clear : BrineStage.Overrun;
            State.escaped=throughHatch; State.ended=true; State.detained=false;
            RestoreBrineScene();
            soundscape.Play(Player.Feet,"brine-hit",1,true);
            SaveProgress();
            SetScreen(throughHatch ? NightScreen.Escaped : NightScreen.Caught);
        }
    }
}
