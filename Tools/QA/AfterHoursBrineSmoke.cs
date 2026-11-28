// Copy this fixture into the isolated QA player's Assets/Scripts and launch with -brine-qa.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed class AfterHoursBrineSmoke : MonoBehaviour
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        NightGame game;
        List<NightTarget> targets;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Application.companyName=="AfterHours QA" && Environment.GetCommandLineArgs().Contains("-brine-qa"))
                new GameObject("核线验收").AddComponent<AfterHoursBrineSmoke>();
        }

        IEnumerator Start()
        {
            Application.runInBackground=true;
            yield return null;
            game=FindFirstObjectByType<NightGame>(); game.enabled=false;
            targets=(List<NightTarget>)typeof(NightGame).GetField("targets",Flags).GetValue(game);
            var files=new[]{NightSave.FilePath,NightSave.FilePath+".bak"};
            var backups=files.Select(p=>File.Exists(p)?File.ReadAllBytes(p):null).ToArray();
            bool had=PlayerPrefs.HasKey(NightTickets.ProfileKey);
            int mask=NightTickets.Mask, result=0;
            try
            {
                var run=VerifyWithPreview();
                while(true)
                {
                    try { if(!run.MoveNext()) break; }
                    catch(Exception ex) { Debug.LogException(ex); result=83; break; }
                    yield return run.Current;
                }
            }
            finally
            {
                for(int i=0;i<files.Length;i++)
                    if(backups[i]!=null) File.WriteAllBytes(files[i],backups[i]); else File.Delete(files[i]);
                if(had) PlayerPrefs.SetInt(NightTickets.ProfileKey,mask); else PlayerPrefs.DeleteKey(NightTickets.ProfileKey);
                PlayerPrefs.Save();
            }
            if(result==0) Debug.Log("BRINE PASS");
            Application.Quit(result);
        }

        IEnumerator VerifyWithPreview()
        {
            Check();
            var args=Environment.GetCommandLineArgs();
            int at=Array.IndexOf(args,"-brine-preview");
            if(at<0 || at+1>=args.Length) yield break;
            game.NewNight(); game.State.brine.stage=BrineStage.Carrying;
            game.State.torch=true; game.State.charge=200; game.Player.SetTorch(true);
            Call("BeginBrineDefence");
            Aim(new Vector3(271,.07f,-28.2f),new Vector3(256,1.16f,-31.5f));
            for(int i=0;i<225;i++) Call("TickBrine",.05f);
            var ui=(NightInterface)typeof(NightGame).GetField("hud",Flags).GetValue(game);
            typeof(NightInterface).GetField("arrival",Flags).SetValue(ui,Time.unscaledTime-20);
            game.Atlas.Track(game.State,game.Player.Feet);
            var panel=game.GetComponentInChildren<UIDocument>().panelSettings;
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            target.Create();
            var previous=panel.targetTexture; bool clear=panel.clearColor;
            var active=RenderTexture.active;
            try
            {
                panel.targetTexture=target; panel.clearColor=false;
                for(int i=0;i<14;i++) { ui.Tick(); yield return null; }
                yield return new WaitForEndOfFrame();
                var cam=game.Player.Eyes.GetComponent<Camera>();
                cam.Render();
                var hands=game.Player.Eyes.GetComponentsInChildren<Camera>().First(c=>c.name=="First person torch camera");
                Need(hands.enabled && gameObject.scene.IsValid(),"Projector camera is disabled"); hands.Render();
                typeof(NightCameraGrade).GetMethod("Present",Flags).Invoke(cam.GetComponent<NightCameraGrade>(),new object[]{target});
                var utility=typeof(UIDocument).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility",true);
                utility.GetMethod("RenderOffscreenPanels",BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).Invoke(null,null);
                RenderTexture.active=target;
                var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
                File.WriteAllBytes(args[at+1],image.EncodeToJPG(92));
                Destroy(image);
            }
            finally { panel.targetTexture=previous; panel.clearColor=clear; RenderTexture.active=active; target.Release(); Destroy(target); }
        }

        void Call(string method,params object[] args) => typeof(NightGame).GetMethod(method,Flags).Invoke(game,args);
        static void Need(bool condition,string failure) { if(!condition) throw new Exception(failure); }
        NightTarget Target(string label) => targets.Single(t=>t.Label==label);
        void Aim(Vector3 feet,Vector3 at)
        {
            game.Player.ResetPosition(feet,0);
            game.Player.Eyes.LookAt(at);
            Physics.SyncTransforms();
        }
        void Use(NightTarget target,Vector3 feet)
        {
            Need(target.Available==null || target.Available(),target.Label+" disabled");
            Aim(feet,target.Position); Call("FocusTarget",.03f);
            Need(game.Focus==target,target.Label+" reticle instead sees "+game.Focus?.Label);
            target.Use();
        }
        void Enter(params int[] keys)
        {
            foreach(int i in keys)
            {
                var p=game.RepairView;
                Need(p!=null,"Panel closed early");
                p.selected=i; Call("ConfirmRepair");
            }
        }
        void BusReady()
        {
            var s=game.State;
            s.dispatch.accepted=true; Array.Fill(s.dispatch.steps,NightDispatch.Contacts);
            s.dispatch.teeth=NightDispatch.Teeth;
            s.pump=s.survey.printed=s.survey.archive=s.story.ledger=s.story.exchanged=s.story.boardAligned=s.contacted=s.beacon=true;
            s.survey.soundings=3; s.rescueTime=0;
            s.collected.Add("key-uplink"); s.collected.Add("key-uplink-used");
        }
        void Check()
        {
            Need((int)Ticket.Return==5 && NightTickets.Book.Skip(4).Take(3).Select(p=>p.Id)
                .SequenceEqual(new[]{Ticket.CoreCargo,Ticket.Overrun,Ticket.NuclearEscape}),"Ticket order or old bits changed");
            game.NewNight();
            var s=game.State;
            var book=targets.Where(t=>t.Label=="拓下旧线记号").ToArray();
            Need(book.Length==4 && !Target("拨动四位字轮").Available(),"Early vault access");
            for(int i=0;i<4;i++)
            {
                var at=BrineRoute.Marks[i].Point;
                if(i==1)
                {
                    s.survey.camera=true; Call("ToggleFinder");
                    Aim(new Vector3(at.x,.07f,at.z-3.7f),at);
                    game.ExposeSurvey();
                    Need(game.Aligned && !string.IsNullOrEmpty(s.brine.exposures[i]),"Camera failed at "+BrineRoute.Marks[i].Place);
                    Call("ToggleFinder");
                }
                else Use(book[i],new Vector3(at.x,.07f,at.z-1.05f));
            }
            Need(s.brine.rubbings==15 && game.InventoryItems.Any(x=>x.Id==NightItemId.BrineBook),"Missing notebook");
            Need(Physics.Raycast(new Vector3(268,1,9),Vector3.forward,1.4f),"Vault door already open");
            Use(Target("拨动四位字轮"),new Vector3(269.6f,.07f,8.55f));
            Enter(0,0,0,0);
            Need(game.RepairView?.row==0 && s.brine.stage==BrineStage.Quiet,"Wrong code opened door or disclosed first error");
            string edge="井";
            var code=new List<int>();
            var used=new HashSet<int>();
            while(edge!="河")
            {
                int i=Enumerable.Range(0,4).Single(i=>!used.Contains(i) && (BrineRoute.Marks[i].Left==edge || BrineRoute.Marks[i].Right==edge));
                var mark=BrineRoute.Marks[i];
                var word=BrineRoute.Rubbing(i,s.brine.wheel).Split('|')[1].Trim();
                bool forward=mark.Left==edge;
                code.Add(Array.IndexOf(BrineRoute.Symbols,(forward?word[0]:word[1]).ToString()));
                edge=forward?mark.Right:mark.Left; used.Add(i);
            }
            Enter(code.ToArray());
            Physics.SyncTransforms();
            Need(!Physics.Raycast(new Vector3(268,1,9),Vector3.forward,1.4f),"Unlocked door blocks passage");
            Use(Target("提起试验核聚变样芯"),new Vector3(270,.07f,14.25f));
            BusReady();
            Need(s.CanBoard,"Sealed cargo disabled the bus");
            Target("登上救援车").Use();
            Need(NightTickets.Result(s)==Ticket.CoreCargo && game.ScreenState==NightScreen.Escaped,"Carried ending");
            game.NewNight(); s=game.State; s.brine.stage=BrineStage.Carrying; BusReady();
            Use(Target("部署样芯 · 封锁车道"),new Vector3(274.1f,.07f,-30.5f)); Enter(0);
            Need(game.RepairView==null && s.CanBoard && s.brine.stage==BrineStage.Carrying,"Cancel committed the route");
            Use(Target("部署样芯 · 封锁车道"),new Vector3(274.1f,.07f,-30.5f)); Enter(1);
            Need(!s.CanBoard && !s.beacon && !Target("登上救援车").Available() && !game.CanObserveLive,"Bus/observer remained available");
            Use(Target("扳南线回水闸"),new Vector3(268.4f,.07f,-33.62f));
            Need(s.brine.divertedLane==0 && !Target("扳北线回水闸").Available(),"Side sluices did not share their cooldown");
            game.Captured(); Need(!s.detained && s.night==1,"Friendly frog captured player");
            // Leave the post without shooting. The frog cannot clear the event by itself.
            for(int i=0;i<6000 && s.brine.Fighting;i++) Call("TickBrine",.05f);
            Need(s.ended && s.night==1 && NightTickets.Result(s)==Ticket.Overrun && game.ScreenState==NightScreen.Caught,"No immediate loss");
            game.NewNight(); s=game.State; s.brine.stage=BrineStage.Carrying; Call("BeginBrineDefence");
            Aim(new Vector3(276,.07f,-29.1f),BrineRoute.Mouth(1)+Vector3.up);
            for(int i=0;i<92;i++) Call("TickBrine",.05f);
            string snapshot=JsonUtility.ToJson(s.brine);
            Call("SaveProgress"); game.ContinueNight(); s=game.State;
            Need(snapshot==JsonUtility.ToJson(s.brine) && s.brine.echoes.Any(e=>e.lane<3),"Mid-wave reload changed battle");
            Aim(new Vector3(276,.07f,-29.1f),BrineRoute.Mouth(1)+Vector3.up);
            // Even a stationary shooter who always picks an unshielded target must now lose.
            for(int i=0;i<4000 && s.brine.Fighting;i++)
            {
                Call("TickBrine",.05f);
                var target=s.brine.echoes.Where(e=>e.lane<3 && !e.armoured).OrderByDescending(e=>e.feet.x).FirstOrDefault();
                if(target!=null) game.Player.Eyes.LookAt(target.feet+Vector3.up*1.1f);
                Call("FireBrine");
            }
            Need(s.brine.stage==BrineStage.Overrun,"Stationary automatic fire still cleared the encounter");
            Debug.Log($"STATIONARY held fire and lost on wave {s.brine.wave+1}; {s.brine.shutter} gate, {s.brine.breath} health");
            for(int seed=0;seed<6;seed++) FightAndEscape(seed);
        }

        void WalkTo(Vector3 target)
        {
            for(int i=0;i<350;i++)
            {
                var delta=target-game.Player.Feet;delta.y=0;
                if(delta.magnitude<.16f) return;
                game.Player.transform.rotation=Quaternion.LookRotation(delta);
                game.Player.Move(Vector2.up,true,.05f); game.Player.SettleView(.05f);
                var threat=game.State.brine.echoes.OrderBy(e=>(e.feet-game.Player.Feet).sqrMagnitude).FirstOrDefault();
                if(threat!=null && i%7==0)
                {
                    game.Player.Eyes.LookAt(threat.feet+Vector3.up); Call("FireBrine");
                }
                Call("TickBrine",.05f);
                Need(game.State.brine.Live,"Flight ended before reaching "+target+" from "+game.Player.Feet);
            }
            throw new Exception("Physical evacuation path stalled near "+game.Player.Feet);
        }

        void FightAndEscape(int seed)
        {
            game.NewNight(); var s=game.State; s.brine.stage=BrineStage.Carrying; Call("BeginBrineDefence");
            s.brine.tideSeed=seed;
            Aim(new Vector3(270,.07f,-31),BrineRoute.Mouth(1)+Vector3.up);
            // Keep the trigger down through its first lockout, including a save while hot.
            for(int i=0;i<110 && !s.brine.overheated;i++) { Call("TickBrine",.05f); Call("FireBrine"); }
            Need(s.brine.overheated,"Projector never vented under sustained fire");
            float heat=s.brine.heat; Call("SaveProgress");game.ContinueNight();s=game.State;
            Need(s.brine.overheated && Mathf.Abs(heat-s.brine.heat)<.001f,"Reload bypassed the projector lockout");
            Call("FireBrine");Need(s.brine.heat==heat,"Overheated projector bypassed lockout");
            Aim(new Vector3(270,.07f,-31),BrineRoute.Mouth(1)+Vector3.up);
            int turns=0,shots=0,frames=0; float held=0,travel=0;
            var before=game.Player.Feet;
            for(;frames<5000 && s.brine.Fighting;frames++)
            {
                Call("TickBrine",.05f);
                if(!s.brine.Fighting) break;
                int lane=s.brine.ShellLane;
                if(lane>=0)
                {
                    var wheel=Target(lane==0?"扳南线回水闸":"扳北线回水闸");
                    var approach=BrineRoute.Handwheel(lane/2)+Vector3.forward*(lane==0?1f:-1f);
                    var delta=approach-game.Player.Feet;delta.y=0;
                    if(delta.magnitude>.32f)
                    {
                        var walk=game.Player.transform.InverseTransformDirection(delta.normalized);
                        game.Player.Move(new Vector2(walk.x,walk.z),true,.05f);game.Player.SettleView(.05f);
                        held=0;
                    }
                    else if(wheel.Available())
                    {
                        game.Player.Eyes.LookAt(wheel.Position);Physics.SyncTransforms();
                        Call("FocusTarget",0f); Need(game.Focus==wheel,"Valve not reachable on foot");
                        held+=.05f;
                        if(held>=wheel.Hold) { wheel.Use();turns++;held=0; }
                        continue;
                    }
                }
                else held=0;
                travel+=Vector3.Distance(before,game.Player.Feet);before=game.Player.Feet;
                var e=s.brine.echoes.Where(x=>x.lane<3 && !x.armoured)
                    .OrderBy(x=>x.kind==DrifterKind.Runner ? Vector3.Distance(game.Player.Feet,x.feet)/2.8f :
                        Vector3.Distance(BrineRoute.Breach(x.lane),x.feet)/BrineRoute.Speed(x,s.brine.wave)).FirstOrDefault();
                if(e!=null && frames%7==0 && s.brine.heat<.84f)
                {
                    shots++;
                    game.Player.Eyes.LookAt(e.feet + Vector3.up*(shots%9==0 ? 3 : 1.08f)); Call("FireBrine");
                }
            }
            Debug.Log($"SURGE layout {seed}, played {frames*.05f:F1}s, travelled {travel:F1}m, {turns} wheel operations, {shots} aimed pulses; {s.brine.shutter} gate, {s.brine.breath} health; {s.brine.stage}");
            Need(travel>12 && turns>=3,"Response route did not require movement between wheels");
            Need(s.brine.stage==BrineStage.Flight && !s.CanBoard,"Three waves could not be cleared using real shots");
            Need(!Target("拉开逃生栓").Available(),"Exit opened before the ally reached the gate");
            for(int i=0;i<140 && !s.brine.gateReleased;i++) Call("TickBrine",.05f);
            Need(s.brine.gateReleased && s.brine.echoes.Count==2,"Gate opened without the tunnel pursuit");
            Need(s.brine.escapeTime>24 && s.brine.escapeTime<=26,"Escape clock ran before the ally opened the gate");
            var flight=JsonUtility.ToJson(s);
            Call("TickBrine",53f);
            Need(s.brine.missedExit && NightTickets.Result(s)==Ticket.Overrun,"Missed departure not terminal");
            File.WriteAllText(NightSave.FilePath,flight);
            game.ContinueNight(); s=game.State;
            Need(!Physics.CapsuleCast(new Vector3(283,.37f,-31),new Vector3(283,1.54f,-31),.24f,Vector3.right,21,~(1<<NightPlayer.PlayerLayer),QueryTriggerInteraction.Ignore),
                 "An obstacle blocks the walkable evacuation corridor");
            // Run from the checkpoint at the last valve, around the sampling desk and through the opened gate.
            WalkTo(new Vector3(game.Player.Feet.x,0,-32.2f));
            WalkTo(new Vector3(303.7f,0,-32.2f));
            var exit=Target("拉开逃生栓");
            game.Player.Eyes.LookAt(exit.Position);
            Physics.SyncTransforms();Call("FocusTarget",0f);
            Need(game.Focus==exit && s.brine.escapeTime>exit.Hold,"Escape latch was not reached in time");
            for(float heldTime=0;heldTime<exit.Hold;heldTime+=.05f) Call("TickBrine",.05f);
            exit.Use();
            Need(s.escaped && NightTickets.Result(s)==Ticket.NuclearEscape && NightTickets.Stamped(Ticket.NuclearEscape),"Tunnel did not award nuclear ending");
            Need(NightTickets.Stamped(Ticket.CoreCargo) && NightTickets.Stamped(Ticket.Overrun),"Missing saved tickets");
            Need(Resources.Load<Texture2D>("Interface/BrineAfterglow") && Resources.Load<Texture2D>("Interface/Items/BrineCase") &&
                Resources.Load<Texture2D>("BrineDrifter") && Resources.Load<Shader>("BrineSilhouette").isSupported,"Missing encounter art");
        }
    }
}
