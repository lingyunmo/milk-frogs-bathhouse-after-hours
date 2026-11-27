// The isolated player takes -dispatch-qa. Add -force-d3d11 -dispatch-preview <jpg> for an offscreen HUD still.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed class AfterHoursDispatchSmoke : MonoBehaviour
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const float Dt = 1f / 60;
        string preview;
        NightGame game;
        NightHunter hunter;
        NightInterface ui;
        FloorRoutes routes;
        List<NightTarget> targets;
        float walkingMetres, dialSeconds, winchSeconds;
        Vector3 lastStop;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.companyName == "AfterHours QA" && Array.IndexOf(Environment.GetCommandLineArgs(), "-dispatch-qa") >= 0)
                new GameObject("夜班调度验收").AddComponent<AfterHoursDispatchSmoke>();
        }

        static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name,Private).GetValue(obj);
        static void Invoke(object obj, string name, params object[] args) => obj.GetType().GetMethod(name,Private).Invoke(obj,args);
        static void Check(bool passed, string failure) { if (!passed) throw new Exception(failure); }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs();
            int capture = Array.IndexOf(args,"-dispatch-preview");
            if (capture >= 0 && capture+1 < args.Length && SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
                preview = args[capture+1];
            yield return null;
            game = FindFirstObjectByType<NightGame>();
            game.enabled = false;
            hunter = Field<NightHunter>(game,"hunter");
            routes = Field<FloorRoutes>(hunter,"routing");
            ui = Field<NightInterface>(game,"hud");
            targets = Field<List<NightTarget>>(game,"targets");
            var files = new[] { NightSave.FilePath, NightSave.FilePath + ".bak" };
            var backup = Array.ConvertAll(files, f => File.Exists(f) ? File.ReadAllBytes(f) : null);
            int result = 0;
            try
            {
                var verify = Verify();
                while (true)
                {
                    bool has;
                    try { has = verify.MoveNext(); }
                    catch (Exception ex) { Debug.LogException(ex); result = 72; break; }
                    if (!has) break;
                    yield return verify.Current;
                }
            }
            finally
            {
                for (int i=0; i<files.Length; i++)
                    if (backup[i] != null) File.WriteAllBytes(files[i],backup[i]); else File.Delete(files[i]);
            }
            // Give the render thread time to detach the offscreen UI and retire the readback before exit.
            for (int frame=0; frame<5; frame++) yield return null;
            if (result == 0) Debug.Log($"DISPATCH PASS route {walkingMetres:F0} m; simulated dial {dialSeconds:F1}s; winch incl rest {winchSeconds:F1}s.");
            Application.Quit(result);
        }

        NightTarget Target(string text) => targets.Find(t=>t.Label==text);
        NightTarget Visit(string text)
        {
            var t = Target(text);
            Check(t != null && (t.Available?.Invoke() ?? true),"Unavailable: " + text);
            int floors=0, clear=0, routed=0;
            string other="";
            const int mask = ~(1 << NightPlayer.PlayerLayer);
            for (int ring=0; ring<3; ring++)
            for (int i=0; i<16; i++)
            {
                float angle = i * Mathf.PI / 8f;
                Vector3 probe = t.Position + new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle)) * (.92f + ring*.28f);
                if (!Physics.Raycast(probe + Vector3.up * .30f,Vector3.down,out var ground,2,mask) || ground.normal.y < .85f ||
                    Mathf.Abs((ground.point.y + 1f) - t.Position.y) > 1.2f) continue;
                floors++;
                Vector3 feet = ground.point + Vector3.up*.055f;
                if (Physics.CheckCapsule(feet + Vector3.up*.3f,feet + Vector3.up*1.43f,.25f,mask)) continue;
                clear++;
                var path = new List<Vector3>();
                if (!routes.Find(lastStop,feet,path,game.CanCrossKeyDoors)) continue;
                routed++;
                game.Player.ResetPosition(feet,0);
                game.Player.Eyes.rotation = Quaternion.LookRotation(t.Position-game.Player.Eyes.position);
                Physics.SyncTransforms();
                Invoke(game,"FocusTarget",0f);
                if (game.Focus != t) { other = game.Focus?.Label ?? "none"; continue; }
                var from = lastStop;
                foreach (Vector3 waypoint in path) { walkingMetres += Vector3.Distance(from,waypoint); from=waypoint; }
                lastStop = feet;
                game.Atlas.Track(game.State,feet);
                return t;
            }
            throw new Exception("No reachable, unobstructed F prompt at " + text + " " + t.Position + $" from {lastStop}, floor {floors}, clear {clear}, route {routed}, prompt {other}");
        }

        IEnumerator Verify()
        {
            game.NewNight();
            lastStop = new Vector3(-5,0,12);
            game.State.torch = true;
            game.State.charge = 300;
            game.Player.SetTorch(true);
            var radio = Visit("接听夜班调度");
            Check(radio.Hold >= 1,"Radio must be held");
            ui.Tick();
            Check(Field<Label>(ui,"task").text == "接听夜班电台", "No actionable radio lead");
            radio.Use();
            Invoke(game,"FocusTarget",0f);
            ShowEstablishedView();
            ui.Tick();
            Check(Field<Label>(ui,"objectiveTag").text.Contains("已接单"), "No radio receipt");
            if (preview != null)
            {
                var shot = CaptureInstrument(SiblingPreview("radio"));
                while (shot.MoveNext()) yield return shot.Current;
            }
            game.SetScreen(NightScreen.Map);
            var root = Field<VisualElement>(ui,"root");
            Submit(root.Q<Button>("switch-orders"));
            Check(root.Query<Button>(className:"order-entry").ToList().Count == 4, "Missing order sheets");
            if (preview != null)
            {
                yield return new WaitForSecondsRealtime(.45f);
                var shot = CaptureInstrument(SiblingPreview("orders"));
                while (shot.MoveNext()) yield return shot.Current;
            }
            Submit(root.Q<Button>("order-Barrier"));
            Check(NightSave.Read().dispatch.tracked == FieldOrder.Barrier, "Tracking selection not saved");
            game.ContinueNight();
            ui.Tick();
            Check(Field<Label>(ui,"task").text.Contains("取配重") &&
                !Field<Label>(ui,"objectiveTag").text.Contains("已接单"), "Continue lost tracked lead or replayed receipt");
            var job = game.State.dispatch;
            var normal = Target("翻起反光接应板");
            var backupBeacon = Target("扳开备用接应灯");
            game.State.night = 4;
            game.State.contacted = true;
            Check(!normal.Available() && !backupBeacon.Available(),"Rescue bypass");
            game.State.contacted = false;
            game.State.night = 1;

            for (int k=0; k<4; k++)
            {
                int i = new[]{0,1,3,2}[k];
                string label = "校准机组 · " + NightDispatch.Stations[i].Name;
                Visit(label).Use();
                ui.Tick();
                var dial = Field<NightDetentionLock>(ui,"synchronizer");
                Check(dial.ContactCount==10 && Mathf.Abs(Time.timeScale-1)<.001f,"Repair paused the world");
                if(k==0)
                {
                    Stroke(dial,true); Stroke(dial,true); Stroke(dial,false);
                    Check(job.steps[i]==1 && NightSave.Read().dispatch.steps[i]==1,"Wrong rollback or save");
                    Vector3 alarm = NightDispatch.Approach(i);
                    var origin = hunter.Position;
                    game.Player.ResetPosition(new(100,0,-20),0);
                    for(int n=0;n<80;n++) hunter.Tick(.05f);
                    Check((Field<Vector3>(hunter,"destination")-alarm).magnitude<2 &&
                        Vector3.Distance(hunter.Position,origin)>1,"Hunter didn't move toward tripped station");
                    game.CloseDispatch(); ui.Tick();
                    game.ContinueNight();
                    job = game.State.dispatch;
                    Visit(label).Use(); ui.Tick();
                    dial = Field<NightDetentionLock>(ui,"synchronizer");
                    Check(dial.Seated==1,"Partial generator lost on continue");
                    for(int n=0;n<4;n++) Stroke(dial,true);
                    ShowEstablishedView();
                    Check(Field<VisualElement>(ui,"objectiveBlock").style.display.value == DisplayStyle.None &&
                        Field<VisualElement>(ui,"dispatchStrip").style.display.value == DisplayStyle.Flex,
                        "Repair must keep the four circuits and clear unrelated directions");
                    ui.Tick();
                    game.Player.SettleView(.1f);
                    if (preview != null)
                    {
                        var shot = CaptureInstrument(preview);
                        while (shot.MoveNext()) yield return shot.Current;
                    }
                }
                while (dial.Seated<10) Stroke(dial,true);
                Check(game.ActiveGenerator<0 && !Target(label).Available(),"Finished generator still active");
                ui.Tick();
            }
            Check(job.AllRunning && !job.RoadReady,"Winch bypass");
            Check(NightFieldOrders.Tracking(game.State)==FieldOrder.Barrier, "Generator completion replaced player's tracking choice");
            Visit("背起绞盘配重").Use();
            Check(game.Player.CarryingLoad,"Carrying speed modifier missing");
            game.Player.Move(Vector2.up,true,.02f);
            Check(!game.Player.Sprinting,"Sprinted with a counterweight");
            game.Player.Move(Vector2.zero,false,.05f);
            Invoke(game,"SetLoadDown");
            Check(!job.carrying && !game.Player.CarryingLoad,"Cannot set load down");
            Visit("背起绞盘配重").Use();
            game.Captured();
            Check(!job.carrying && job.loadFeet==NightDispatch.LoadDepot && job.AllRunning,"Capture destroyed progress");
            game.ContinueNight();
            Check(game.State.detained && game.State.dispatch.AllRunning,"Capture not saved");
            Check(NightFieldOrders.Current(game.State).action.Contains("卡簧锁"), "Dispatch obscured confinement objective");
            game.OpenDetentionLock();
            game.ReleaseDetention();
            var next = game.State.dispatch;
            Visit("背起绞盘配重").Use();
            Visit("挂上配重").Use();
            Check(next.delivered && game.Player.HaulTrained && !game.Player.CarryingLoad,"No haul reward");
            Visit("握住绞盘曲柄").Use();
            for(int n=0;n<600;n++) { game.Wind(true,Dt); winchSeconds+=Dt; }
            Check(game.Player.Exhausted && next.teeth>0 && next.teeth<8,"Winch has no stamina cost");
            int saved = next.teeth;
            game.CloseDispatch();
            game.ContinueNight();
            next = game.State.dispatch;
            Check(next.teeth==saved && next.delivered && game.Player.HaulTrained,"Haul/winch save lost");
            Visit("握住绞盘曲柄").Use();
            bool rest=false;
            for(int n=0;n<6000 && game.Winding;n++)
            {
                if (game.Player.Exhausted) rest=true;
                if (game.Player.Stamina>.90f) rest=false;
                game.Wind(!rest,Dt); winchSeconds+=Dt;
            }
            Check(next.RoadReady && !game.Winding,"Could not raise barrier");
            ui.Tick();
            Check(NightFieldOrders.Tracking(game.State)==FieldOrder.Drain &&
                Field<Label>(ui,"location").text==NightReturnLines.Zones[0], "Completed order didn't advance to drain");
            Check(!game.State.RescueAccess && !normal.Available(),"Flooded road permits escape");
            game.State.pump = true;
            Check(game.State.RescueAccess,"Original pump route no longer drains the road");
            game.State.pump = false;
            for (int line=0;line<3;line++)
            {
                int tap=line*2+((next.leakPattern>>line)&1);
                int before=next.sealedLines;
                Visit(NightReturnLines.Label(tap^1)).Use();
                Check(next.sealedLines==before,"Wrong valve credited");
                Visit(NightReturnLines.Label(tap)).Use();
            }
            Check(game.State.Drained && !game.State.pump && game.State.RescueAccess && NightSave.Read().dispatch.sealedLines==7,"Acoustic drain incomplete");
            Visit("取发电机启动件").Use();
            Visit("安装启动件").Use();
            Visit("对准飞轮刻线").Use();
            foreach(int answer in new[]{1,0,2})
            {
                game.RepairView.selected=answer;
                Invoke(game,"ConfirmRepair");
            }
            Visit("点动发电机").Use();
            Visit("给前台和弱电间送电").Use();
            Visit("隔离受潮线路").Use();
            Visit("转动维修站定向天线").Use();
            for(int n=0;n<380 && game.Reception<.95f;n++) game.Tune(1,Dt);
            Check(game.Reception>=.95f,"Cannot acquire carrier");
            for(int n=0;n<185;n++) if(game.Tuning)game.Tune(0,Dt);
            Check(game.State.contacted && !game.Tuning,"Cannot confirm directional radio");
            Check(NightFieldOrders.Current(game.State).action=="翻起接应板", "No actionable rescue lead");
            game.State.night=4;
            Check(normal.Available() && backupBeacon.Available(),"Beacon not reachable on either route");
            Visit("翻起反光接应板").Use();
            Invoke(game,"TickWorld",28f);
            Visit("登上救援车").Use();
            Check(game.State.escaped && game.ScreenState==NightScreen.Escaped,"Could not board");
        }

        void ShowEstablishedView()
        {
            game.Player.SetTorch(true);
            game.Player.SetTorchVisible(true);
            game.Player.AdvanceTorch(game,.3f);
            typeof(NightInterface).GetField("arrival",Private).SetValue(ui,Time.unscaledTime-20f);
            game.Player.SettleView(.1f);
        }

        static void Submit(Button button)
        {
            Check(button != null && button.enabledInHierarchy, "Missing or disabled button");
            using var ev = NavigationSubmitEvent.GetPooled();
            ev.target = button;
            button.SendEvent(ev);
        }

        string SiblingPreview(string suffix) => Path.Combine(Path.GetDirectoryName(preview),
            Path.GetFileNameWithoutExtension(preview) + "-" + suffix + Path.GetExtension(preview));

        IEnumerator CaptureInstrument(string path)
        {
            const int width=1280, height=720;
            var camera=game.Player.Eyes.GetComponent<Camera>();
            var panel=game.GetComponentInChildren<UIDocument>().panelSettings;
            var previousPanel=panel.targetTexture;
            bool clear=panel.clearColor;
            var active=RenderTexture.active;
            var target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            target.Create();
            panel.targetTexture=target;
            panel.clearColor=false;
            try
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                camera.Render();
                Invoke(camera.GetComponent<NightCameraGrade>(),"Present",target);
                var renderer=typeof(UIDocument).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility",true);
                renderer.GetMethod("RenderOffscreenPanels",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
                RenderTexture.active=target;
                var image=new Texture2D(width,height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,width,height),0,0);
                image.Apply();
                File.WriteAllBytes(path,image.EncodeToJPG(89));
                Destroy(image);
            }
            finally
            {
                panel.clearColor=clear;
                panel.targetTexture=previousPanel;
                RenderTexture.active=active;
                Destroy(target);
            }
        }

        void Stroke(NightDetentionLock dial, bool hit)
        {
            int before = dial.Seated;
            for(int frame=0;frame<600;frame++)
            {
                dial.Tick(Dt); dialSeconds+=Dt;
                if ((Mathf.Abs(dial.NeedlePosition-dial.TargetCenter)<=dial.TargetRadius) != hit) continue;
                dial.Confirm();
                if (dial.Seated!=before) return;
            }
            throw new Exception("Dial does not respond at contact " + before);
        }
    }
}
