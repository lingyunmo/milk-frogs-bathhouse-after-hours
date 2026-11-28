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
        readonly Dictionary<string,Vector3> stops = new();

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
            bool hadTickets=PlayerPrefs.HasKey(NightTickets.ProfileKey);
            int originalTickets=NightTickets.Mask;
            PlayerPrefs.DeleteKey(NightTickets.ProfileKey);
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
                if(hadTickets) PlayerPrefs.SetInt(NightTickets.ProfileKey,originalTickets);
                else PlayerPrefs.DeleteKey(NightTickets.ProfileKey);
                PlayerPrefs.Save();
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
            var candidates=targets.FindAll(t=>t.Label==text);
            Check(candidates.Count>0,"Missing interaction: "+text);
            int floors=0, clear=0, routed=0;
            string other="";
            const int mask = ~(1 << NightPlayer.PlayerLayer);
            foreach (var t in candidates)
            for (int ring=0; ring<4; ring++)
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
                if (text == "拿起手电" && !routes.Find(feet,new Vector3(-5,.055f,12),path,game.CanCrossKeyDoors)) continue;
                if (!routes.Find(lastStop,feet,path,game.CanCrossKeyDoors)) continue;
                routed++;
                game.Player.ResetPosition(feet,0);
                game.Player.Eyes.rotation = Quaternion.LookRotation(t.Position-game.Player.Eyes.position);
                Physics.SyncTransforms();
                Invoke(game,"FocusTarget",0f);
                if (game.Focus != t) { other = game.Focus?.Label ?? "none"; continue; }
                var from = lastStop;
                foreach (Vector3 waypoint in path) { walkingMetres += Vector3.Distance(from,waypoint); from=waypoint; }
                stops[text] = feet;
                lastStop = feet;
                game.Atlas.Track(game.State,feet);
                return t;
            }
            throw new Exception("No reachable, unobstructed F prompt at " + text + " " + candidates[0].Position + $" from {lastStop}, floor {floors}, clear {clear}, route {routed}, prompt {other}");
        }

        IEnumerator Verify()
        {
            game.NewNight();
            lastStop = NightState.NewSpawn;
            Visit("拿起手电").Use();
            game.State.charge = 300;
            game.Player.SetTorch(true);
            var root=Field<VisualElement>(ui,"root");
            game.SetScreen(NightScreen.Pause);
            Check(root.Q<Button>("inspect-pinned-print")==null,"Paused menu leaked undiscovered photograph");
            Submit(root.Q<Button>("options"));
            Submit(root.Q<Button>("settings-1"));
            Check(root.Q<Button>("practical-glow").parent.style.display.value==DisplayStyle.Flex,"Video controls unavailable");
            if(preview!=null)
            {
                yield return new WaitForSecondsRealtime(.5f);
                var shot=CaptureInstrument(SiblingPreview("settings"));
                while(shot.MoveNext()) yield return shot.Current;
            }
            Submit(root.Q<Button>("settings-2"));
            Check(root.Q<Button>("practical-glow").parent.style.display.value==DisplayStyle.None,"Settings pages overlap");
            Check(ui.CloseSubpage(),"Options did not close");
            game.SetScreen(NightScreen.Play);
            var loosePrint = Visit("取前台招领照");
            if(preview!=null)
            {
                ShowEstablishedView(); ui.Tick();
                var shot=CaptureInstrument(SiblingPreview("counter"));
                while(shot.MoveNext()) yield return shot.Current;
            }
            loosePrint.Use();
            Check(!Target("取前台招领照").Available() && NightSave.Read().collected.Contains(NightGame.LostPhotoId),"Lost-property print not collected or saved");
            game.SetScreen(NightScreen.Pause);
            if(preview!=null)
            {
                yield return new WaitForSecondsRealtime(.5f);
                var shot=CaptureInstrument(SiblingPreview("pause"));
                while(shot.MoveNext()) yield return shot.Current;
            }
            Submit(root.Q<Button>("inspect-pinned-print"));
            Check(root.Q("evidence-front")!=null && root.Q("evidence-transcript")==null,"Print or hidden handwriting missing");
            if(preview!=null)
            {
                yield return new WaitForSecondsRealtime(.5f);
                var shot=CaptureInstrument(SiblingPreview("desk"));
                while(shot.MoveNext()) yield return shot.Current;
            }
            Submit(root.Q<Button>("turn-print"));
            Check(root.Q<Label>("evidence-transcript").text.Contains("鞋还在") && root.Q<Button>("compare-record")==null,"Missing back or undiscovered note exposed");
            Submit(root.Q<Button>("turn-print"));
            Check(ui.CloseSubpage() && game.ScreenState==NightScreen.Pause,"Photo did not return to pause");
            game.SetScreen(NightScreen.Map);
            Submit(root.Q<Button>("floor-0"));
            Submit(root.Q<Button>("map-locate"));
            Check(root.Q<Button>("floor-"+game.Atlas.Floor).ClassListContains("floor-tab--selected") &&
                root.Q<NightMapView>().ZoomLevel>1,"Map could not locate player from another floor");
            if(preview!=null)
            {
                yield return new WaitForSecondsRealtime(.5f);
                var shot=CaptureInstrument(SiblingPreview("plan"),1024,768);
                while(shot.MoveNext()) yield return shot.Current;
            }
            yield return null;
            var located = root.Q<NightMapView>();
            var playerPoint = (Vector2)typeof(NightMapView).GetMethod("Point",Private).Invoke(located,new object[]{game.Player.Feet.x,game.Player.Feet.z});
            Check(located.contentRect.width>1 && Vector2.Distance(playerPoint,located.contentRect.center)<3,"Located map arrow not on paper: "+playerPoint+" "+located.contentRect);
            Submit(root.Q<Button>("map-reset"));
            game.SetScreen(NightScreen.Play);
            string Offer(FieldOrder o) => "接取"+NightFieldOrders.Name(o);
            foreach(var station in NightShiftTrail.Stations) Check(!Target(Offer(station.Order)).Available(),"Opening offer: "+station.Order);
            Visit("拿起毛巾库钥匙").Use();
            Check(!Target(Offer(FieldOrder.Drain)).Available(),"Unopened door offers job");
            Visit("用毛巾库钥匙开门").Use();
            Visit(Offer(FieldOrder.Drain)).Use();
            game.SetScreen(NightScreen.Map);
            Submit(root.Q<Button>("switch-orders"));
            Check(root.Query<Button>(className:"order-entry").ToList().Count==1,"Undiscovered jobs in docket");
            game.ContinueNight();
            Check(game.WorkingOn(FieldOrder.Drain),"Lost job on continue");
            var job=game.State.dispatch;
            var normal=Target("翻起反光接应板");
            var backupBeacon=Target("扳开备用接应灯");
            // Exercise the original powered-pump solution from the same legitimate checkpoint.
            var beforeDrain=NightSave.Read();
            Visit("取回水接头密封圈").Use();
            Visit("取发电机启动件").Use();
            Visit("安装启动件").Use();
            Visit("对准飞轮刻线").Use();
            foreach(int answer in new[]{1,0,2}) { game.RepairView.selected=answer; Invoke(game,"ConfirmRepair"); }
            Visit("点动发电机").Use();
            Visit("给前台和弱电间送电").Use();
            Visit("关掉水槽进水阀").Use();
            Visit("换密封圈，拧紧回水接头").Use();
            Visit("排空后场积水").Use();
            Check(game.State.Drained && !job.orderHeld && Target(Offer(FieldOrder.Barrier)).Available(),"Pump route deadlock");
            for(int i=0;i<3;i++)
                Visit(NightReturnLines.Label(i*2+((job.leakPattern>>i)&1))).Use();
            Check(job.sealedLines==7 && game.State.pump,"Pump-first prevented valve inspection");
            // Old sessions can have both later keys already. Do not lose physical work or a player's torch.
            var legacy=NightSave.Read();
            legacy.shiftEdition=0; legacy.story=new ShiftEvidence();
            legacy.collected.Add("key-staff"); legacy.collected.Add("key-staff-used");
            NightSave.Write(legacy); game.ContinueNight();
            Check(game.State.pump && game.State.torch && !game.State.collected.Contains("key-staff"),"Legacy rekey lost progress");
            NightSave.Write(beforeDrain); game.ContinueNight();
            job=game.State.dispatch;
            lastStop=game.Player.Feet;
            for(int line=0;line<3;line++)
            {
                int tap=line*2+((job.leakPattern>>line)&1),before=job.sealedLines;
                Visit(NightReturnLines.Label(tap^1)).Use();
                Check(job.sealedLines==before,"Wrong valve credited");
                Visit(NightReturnLines.Label(tap)).Use();
            }
            Check(!job.orderHeld && !Target("插回毛巾库钥匙").Available(),"Exchange unlocked after one job");
            Visit(Offer(FieldOrder.Lights)).Use();
            Target(Offer(FieldOrder.Barrier)).Use();
            Check(game.WorkingOn(FieldOrder.Lights),"Direct offer replaced a held order");
            Check(!Target(Offer(FieldOrder.Barrier)).Available() && targets.Exists(t=>t.Label=="当前不可接取" && t.Position==NightShiftTrail.Stations[2].At && t.Available()),"No busy prompt");
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
            Check(!job.orderHeld && Target("插回毛巾库钥匙").Available(),"Missing exchange");
            var exchange=Visit("插回毛巾库钥匙");
            if(preview!=null)
            {
                ShowEstablishedView(); ui.Tick();
                var shot=CaptureInstrument(SiblingPreview("exchange"));
                while(shot.MoveNext()) yield return shot.Current;
            }
            exchange.Use();
            Visit("拿起值班室钥匙").Use();
            Visit("用值班室钥匙开门").Use();
            Visit(Offer(FieldOrder.Ledger)).Use();
            Check(!Target("核对交班簿").Available(),"Receipt not needed");
            Visit("翻阅收洗联 · 七月十七").Use();
            Visit("核对交班簿").Use();
            game.RepairView.selected=0; Invoke(game,"ConfirmRepair");
            Check(!game.State.story.ledger,"Wrong wash count accepted");
            game.RepairView.selected=1; Invoke(game,"ConfirmRepair");
            Visit("拿起弱电间钥匙").Use();
            Visit("用弱电间钥匙开门").Use();
            Visit(Offer(FieldOrder.Barrier)).Use();
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
            Check(!normal.Available(),"Rescue without archive");
            Visit(Offer(FieldOrder.Radio)).Use();
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
            Check(!normal.Available(),"Phone alone bypasses survey");
            Visit(Offer(FieldOrder.Survey)).Use();
            Visit("领测绘相机").Use();
            if (preview != null)
            {
                game.Player.ResetPosition(new Vector3(190,.07f,-4),0);
                ShowEstablishedView();
                game.Player.Eyes.rotation=Quaternion.LookRotation(new Vector3(200,2.7f,15)-game.Player.Eyes.position);
                game.Atlas.Track(game.State,game.Player.Feet); Invoke(game,"FocusTarget",0f); ui.Tick();
                var building=CaptureInstrument(SiblingPreview("station"));
                while(building.MoveNext()) yield return building.Current;
            }
            Visit("接管测深艇").Use();
            game.SteerLaunch(0,false,true,Dt);
            Check(game.State.survey.soundings==0 && !game.State.RescueAccess,"Sounded from the jetty");
            float helmSeconds=0;
            for(int frame=0;frame<24000 && game.Piloting;frame++)
            {
                var film=game.State.survey;
                int leg=film.soundings;
                float m=film.boatMetres, aim=SoundingReach.Centre(m+1,leg);
                for(int k=0;k<3;k++)
                {
                    var bar=SoundingReach.Shoal(k,leg);
                    if(m>=bar.y-9 && m<bar.y+4) aim=SoundingReach.Centre(m+1,leg)-Mathf.Sign(bar.x-SoundingReach.Centre(bar.y,leg))*3.4f;
                }
                if(m>SoundingReach.Length-10) aim=SoundingReach.Buoy(leg);
                game.SteerLaunch(Mathf.Clamp((aim-film.boatOffset)*.95f,-1,1),true,true,Dt);
                helmSeconds+=Dt;
                if (leg==0 && film.soundings==1)
                {
                    game.CloseDispatch(); game.ContinueNight();
                    Check(game.State.survey.soundings==1,"Lost measured reach at checkpoint");
                    Visit("接管测深艇").Use();
                }
                if(preview!=null && frame==1400)
                {
                    ShowEstablishedView(); ui.Tick();
                    var radar=CaptureInstrument(SiblingPreview("launch"));
                    while(radar.MoveNext()) yield return radar.Current;
                }
            }
            Check(!game.Piloting && game.State.survey.soundings==3 && !game.State.RescueAccess,$"Launch stopped on reach {game.State.survey.soundings} at {game.State.survey.boatMetres:F1} m, across {game.State.survey.boatOffset:F1}");
            Debug.Log($"SOUNDING PASS three reaches under helm in {helmSeconds:F1} s.");
            for (int i = 0; i < 3; i++)
            {
                Photograph(i);
                if (i==1 && preview!=null)
                {
                    ShowEstablishedView();
                    game.Player.Eyes.rotation=Quaternion.LookRotation(RiverSurvey.Plates[i].Center-game.Player.Eyes.position);
                    Invoke(game,"ToggleFinder"); game.AimSurvey();
                    game.Atlas.Track(game.State,game.Player.Feet); ui.Tick();
                    yield return new WaitForSecondsRealtime(.20f); ui.Tick();
                    var finder=CaptureInstrument(SiblingPreview("finder"));
                    while(finder.MoveNext()) yield return finder.Current;
                    Invoke(game,"ToggleFinder");
                }
            }
            Check(game.State.survey.CanPrint && !normal.Available(),"Unprocessed film accepted");
            Visit("冲洗水尺底片").Use();
            game.Develop(1,true);
            Check(game.State.survey.sheets == 0,"Blank sheet fixed");
            game.Develop(5.5f,true);
            game.CloseDispatch();
            game.ContinueNight();
            Check(game.State.survey.sheets==1 && game.State.survey.prints[0].Length>512,"Lost sheet or real picture after loading");
            Visit("冲洗水尺底片").Use();
            game.Develop(4.8f,true);
            game.Develop(4.4f,true);
            Check(game.State.survey.printed && !normal.Available(), "No usable field report");
            Visit("查看水尺照片").Use(); ui.Tick();
            Check(game.AlbumOpen,"Contact sheet missing");
            game.CloseDispatch();

            Visit(Offer(FieldOrder.Archive)).Use();
            float beforeArchiveMetres = walkingMetres;
            for(int i=3;i<6;i++) Photograph(i);
            Visit("拼接旧底片").Use();
            game.AlignStrips(-1,0,true);
            Check(game.Splicing && !game.State.survey.spliced,"Misaligned strips accepted");
            while(game.State.survey.leftStrip!=0) game.AlignStrips(-1,-Math.Sign(game.State.survey.leftStrip),false);
            while(game.State.survey.rightStrip!=0) game.AlignStrips(1,-Math.Sign(game.State.survey.rightStrip),false);
            game.AlignStrips(1,0,true);
            Check(!game.Splicing && game.State.survey.spliced,"Cannot align prints");
            Visit("印出夜班合影").Use(); ShowEstablishedView(); ui.Tick();
            Check(game.AlbumIsArchive && game.State.survey.archive,"Could not restore archive");
            if(preview != null)
            {
                yield return new WaitForSecondsRealtime(.35f); ui.Tick();
                var archive = CaptureInstrument(SiblingPreview("archive"));
                while(archive.MoveNext()) yield return archive.Current;
            }
            game.CloseDispatch();
            Visit("接管分洪闸台").Use();
            for (int i=0;i<3;i++)
            {
                if ((game.State.survey.head & (1<<i))!=0) game.OperateWeirs(0,true,Dt);
                game.OperateWeirs(1,false,Dt);
            }
            game.OperateWeirs(0,false,4f);
            Check(game.State.survey.balanced && !game.Sluicing, "Pressure system couldn't be balanced");
            Visit("试鸣堤外警铃").Use();
            Check(!Target("试鸣堤外警铃").Available() && NightSave.Read().survey.bellReadyAt > game.State.dispatch.playSeconds,"No persistent horn cooldown");
            Debug.Log($"SURVEY PASS test travel incl. pump branch and reloads {beforeArchiveMetres:F0} m; archive and weirs added {walkingMetres-beforeArchiveMetres:F0} m.");
            Check(!normal.Available() && game.State.RoadSurveyComplete,"Departed without identifying the stop");
            Visit("校正接应板朝向").Use();
            game.RepairView.selected=1; Invoke(game,"ConfirmRepair");
            Check(!normal.Available(),"Wrong station accepted");
            game.RepairView.selected=0; Invoke(game,"ConfirmRepair");
            Check(NightFieldOrders.Current(game.State).action=="翻起接应板", "No actionable rescue lead");
            game.State.night=4;
            Check(normal.Available() && backupBeacon.Available(),"Beacon not reachable on either route");
            Check(NightShiftTrail.Ending(game.State).title=="末班乘客","Missing baseline ending");
            Visit("翻阅浴票第一号").Use();
            Visit("翻阅关门试音记录").Use();
            Visit("翻阅借衣簿 · 末页").Use();
            game.SetScreen(NightScreen.Inventory);
            Submit(root.Q<Button>("inventory-tab-notes"));
            Submit(root.Q<Button>("inventory-item-Print"));
            Check(root.Q("evidence-front").style.backgroundImage.value.texture.name=="FloodWatch","Darkroom and bag show different prints");
            Submit(root.Q<Button>("turn-print"));
            Check(root.Q<Label>("evidence-transcript").text.Contains("朝水塔"), "Board alignment clue absent");
            Submit(root.Q<Button>("compare-record"));
            Check(root.Q<Label>("evidence-transcript").text.Contains("袁素琴"),"Cannot compare photographic clues");
            if(preview!=null)
            {
                yield return new WaitForSecondsRealtime(.5f);
                var shot=CaptureInstrument(SiblingPreview("files"),1024,768);
                while(shot.MoveNext()) yield return shot.Current;
            }
            ui.CloseInventory();
            Check(game.ScreenState==NightScreen.Play,"Standard inventory closed into a pause menu");
            Debug.Log("DESK PASS physical pickup, save, two-sided evidence, comparison, pause, tabs and map location.");
            Check(NightShiftTrail.Ending(game.State).title=="未寄出的名字","Missing name");
            var undecided=NightSave.Read();
            Visit("封存原片 · 交给早班").Use();
            Check(NightSave.Read().story.choice==WitnessChoice.Seal && !Target("播出原名 · 袁素琴").Available(),"Disposition lost");
            NightSave.Write(undecided); game.ContinueNight(); lastStop=game.Player.Feet;
            Visit("播出原名 · 袁素琴").Use();
            Check(NightShiftTrail.Ending(game.State).title=="有人应名" && !Target("封存原片 · 交给早班").Available(),"Missing broadcast ending");
            game.State.dispatch.playSeconds=900;
            backupBeacon.Use();
            Check(game.State.rescueTime==NightEvents.BackupArrivalTime,"Backup beacon clock gate");
            Visit("翻起反光接应板").Use();
            Check(game.State.rescueTime==NightEvents.BackupArrivalTime,"Testing second line redelayed the arriving bus");
            Check(!Target("登上救援车").Available() && game.State.rescueTime==NightEvents.BackupArrivalTime,"Signal replay restarted clock");
            Invoke(game,"TickWorld",28f);
            var preBoard=NightSave.Read();
            Visit("登上救援车").Use();
            if(preview!=null)
            {
                yield return new WaitForSecondsRealtime(.65f);
                var shot=CaptureInstrument(SiblingPreview("ending"));
                while(shot.MoveNext()) yield return shot.Current;
            }
            Check(game.State.escaped && game.ScreenState==NightScreen.Escaped && NightTickets.Stamped(Ticket.Broadcast),"Ordinary ending was replaced");
            game.SetScreen(NightScreen.Title);
            Submit(root.Q<Button>("ticket-tab"));
            var secretRow=root.Q<VisualElement>("ticket-5");
            var secretLabels=secretRow.Query<Label>().ToList();
            Check(secretLabels.Count==2 && secretLabels.TrueForAll(l=>l.text=="？？？"),"Sealed ticket leaked its identity");
            Check(root.Q<VisualElement>("ticket-2").Query<Label>().ToList().Exists(l=>l.text=="已验"),"Missing gallery stamp");
            if(preview!=null)
            {
                yield return new WaitForSecondsRealtime(.55f);
                var shot=CaptureInstrument(SiblingPreview("wallet"));
                while(shot.MoveNext()) yield return shot.Current;
            }
            Check(ui.CloseSubpage() && root.Q<Button>("new-night")!=null,"Cannot close gallery");
            game.NewNight();
            Check(NightTickets.Stamped(Ticket.Broadcast),"New game erased ticket");
            Check(!Target("打左孔").Available(),"New game entered hidden room");
            NightSave.Write(preBoard); game.ContinueNight(); lastStop=game.Player.Feet;
            foreach(var stop in new[]{"读夜班故障单","取回水接头密封圈","关掉水槽进水阀","换密封圈，拧紧回水接头","排空后场积水",
                "查看外线接线图","接通外线旁路"})
            {
                Visit(stop).Use();
                if(stop=="接通外线旁路") Answers(1,2,0);
            }
            Visit("检查标签，接好浴场上行线").Use(); Answers(1,0);
            Visit("打开路由器，确认链路灯").Use();
            Visit("发送街名与站牌编号").Use();
            Visit("取备用保险管").Use();
            Visit("装上备用保险管").Use();
            Visit("调节转接机").Use(); Answers(1,2);
            Check(game.State.circuit.Has(CallPath.Aerial|CallPath.FrontDesk|CallPath.Relay) && !Target("转动维修站定向天线").Available(),"Lost or repeatable contact route");
            Visit("拆开值班补给箱").Use();
            Visit("按下货门试铃").Use();
            Visit("请求一次补给").Use();
            Invoke(game,"TickWorld",22f);
            Check(!Target("打左孔").Available() && game.State.circuit.room==0,"Opened without collecting delivery");
            Visit("打开补给箱").Use();
            Check(game.State.circuit.room==1 && game.Message.Contains("隐藏任务"),"Missing final-service trigger");
            for(int i=1;i<=5;i++)
            {
                var door=ReturnWing.Door(i);
                bool solid=Physics.Raycast(door+Vector3.up+Vector3.left*.45f,Vector3.right,.9f,1<<FloorRoutes.DoorLayer);
                Check(solid==(i>1) && game.CanCrossKeyDoors(door+Vector3.left,door+Vector3.right)==(i==1),"Incorrect gate physics "+i);
            }
            // Both drain methods and all three independent radio contacts must have been made in this run.
            var eligibility=NightSave.Read();
            foreach(CallPath path in new[]{CallPath.Aerial,CallPath.FrontDesk,CallPath.Relay})
            {
                eligibility.circuit.calls^=path;
                Check(!ReturnCircuit.CompleteService(eligibility),"Missing call accepted: "+path);
                eligibility.circuit.calls|=path;
            }
            eligibility.pump=false; Check(!ReturnCircuit.CompleteService(eligibility),"No pump credited"); eligibility.pump=true;
            eligibility.dispatch.sealedLines=3; Check(!ReturnCircuit.CompleteService(eligibility),"Unrepaired circuit accepted");
            Visit("打右孔").Use();
            Check(game.State.circuit.room==2 && !Target("接听三根传声管").Available(),"Skipped second workroom");
            Visit("把票压上透光架").Use(); Answers(2);
            Check(game.RepairView!=null && game.State.circuit.room==2,"Unmirrored hole credited");
            Answers(0);
            var handset=Visit("接听三根传声管");
            if(preview!=null)
            {
                ShowEstablishedView(); ui.Tick();
                var shot=CaptureInstrument(SiblingPreview("listening"));
                while(shot.MoveNext()) yield return shot.Current;
            }
            handset.Use();
            yield return new WaitForSecondsRealtime(.75f);
            Visit("叩井管").Use();
            Visit("叩岸管").Use();
            Check(game.State.circuit.matched==0 && game.State.circuit.room==3,"Wrong signal accepted");
            Visit("叩井管").Use();
            game.ContinueNight(); lastStop=game.Player.Feet;
            Check(game.State.circuit.matched==1 && game.State.circuit.punch==2,"Lost physical clue on reload");
            Visit("叩桥管").Use(); Visit("叩岸管").Use();
            Check(!Target("投递甲路").Available(),"Capsule bypass");
            Visit("取走装票的气筒").Use(); Visit("松开泄压阀").Use();
            Visit("投递乙路").Use(); Check(game.State.circuit.room==4,"Misdirected capsule accepted");
            Visit("投递甲路").Use();
            Check(game.State.circuit.room==5 && !NightTickets.Stamped(Ticket.Return) && !Target("拿起回线听筒").Available(),"Ending granted before evidence");
            Visit("掀开签收窗铅封").Use();
            Visit("重叠两联票根").Use(); Answers(0);
            Check(!game.State.circuit.compared,"Wrong witness mark accepted");
            Answers(2); Visit("拿起回线听筒").Use(); Answers(0,0,1);
            Check(game.State.circuit.Solved && !NightTickets.Stamped(Ticket.Return),"Signed reply awarded ticket before boarding");
            game.ContinueNight(); lastStop=game.Player.Feet;
            Visit("登上救援车").Use();
            Check(game.State.escaped && NightTickets.Stamped(Ticket.Return),"Hidden ticket lost on boarding");
            game.SetScreen(NightScreen.Title); Submit(root.Q<Button>("ticket-tab"));
            Check(root.Q<VisualElement>("ticket-5").Query<Label>().ToList().Exists(l=>l.text==NightTickets.TrueTitle),"Unlocked title missing");
            // Replay the same boarding interaction using the three regular evidence dispositions.
            foreach(var ticket in new[]{Ticket.Passenger,Ticket.Name,Ticket.Deposit})
            {
                var story=JsonUtility.FromJson<NightState>(JsonUtility.ToJson(preBoard));
                story.story.choice=ticket==Ticket.Deposit?WitnessChoice.Seal:WitnessChoice.Undecided;
                if(ticket==Ticket.Passenger) story.collected.Remove(NightItemId.Names.ToString());
                NightSave.Write(story); game.ContinueNight(); lastStop=game.Player.Feet;
                Visit("登上救援车").Use(); Check(NightTickets.Stamped(ticket),"Missing ordinary ticket "+ticket);
            }
            game.NewNight(); game.State.night=NightState.MaxNights; game.Captured(); Invoke(game,"FinishCapture");
            Check(NightTickets.Stamped(Ticket.LastNight) && NightTickets.Mask==63,"Incomplete gallery after six terminal outcomes");
            Debug.Log("RETURN PASS independent service receipts, five gated rooms, saved echo and persistent tickets.");

            void Answers(params int[] values)
            {
                foreach(int value in values) { game.RepairView.selected=value; Invoke(game,"ConfirmRepair"); }
            }
        }

        void Photograph(int i)
        {
            var center=RiverSurvey.Plates[i].Center;
            var spot=new Vector3(center.x,.07f,center.z-6f);
            var path=new List<Vector3>();
            Check(routes.Find(lastStop,spot,path,game.CanCrossKeyDoors),"No route to camera position "+i);
            var last=lastStop;
            foreach(var node in path) { walkingMetres+=Vector3.Distance(last,node);last=node; }
            stops["photo"+i] = spot;
            lastStop=spot;
            game.Player.ResetPosition(spot,180);
            if (!game.Framing) Invoke(game,"ToggleFinder");
            game.Player.Eyes.rotation=Quaternion.LookRotation(Vector3.back);
            game.ExposeSurvey();
            Check(!RiverSurvey.Recorded(game.State,i),"Shot through back of finder");
            game.Player.Eyes.rotation=Quaternion.LookRotation(center-game.Player.Eyes.position);
            game.Player.SetTorch(true);
            game.Player.SetTorchVisible(true);
            game.Player.AdvanceTorch(game,.3f);
            Physics.SyncTransforms();
            game.AimSurvey();
            Check(game.Subject==i && game.Aligned,"Cannot frame both pins at "+i+" from "+spot+" detected "+game.Subject+" pin "+game.Pins[0]);
            game.ExposeSurvey();
            Check(RiverSurvey.Recorded(game.State,i), "Missing exposure "+i);
            Invoke(game,"ToggleFinder");
        }

        void ShowEstablishedView()
        {
            game.Player.SetTorch(true);
            game.Player.SetTorchVisible(true);
            game.Player.AdvanceTorch(game,.3f);
            typeof(NightInterface).GetField("arrival",Private).SetValue(ui,Time.unscaledTime-20f);
            typeof(NightInterface).GetField("pickupUntil",Private).SetValue(ui,-1f);
            typeof(NightGame).GetField("messageTime",Private).SetValue(game,0f);
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

        IEnumerator CaptureInstrument(string path, int width=1280, int height=720)
        {
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
