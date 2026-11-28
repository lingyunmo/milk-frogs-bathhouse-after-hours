using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AfterHours
{
    internal enum NightScreen { Title, Play, Map, Inventory, Pause, Capture, Detention, Caught, Escaped, Observer }

    internal sealed partial class NightGame : MonoBehaviour
    {
        internal const KeyCode InteractKey = KeyCode.F;
        internal const KeyCode TorchKey = KeyCode.E;

        internal NightState State { get; private set; }
        internal NightPlayer Player { get; private set; }
        internal float LandingNoise { get; private set; }
        internal NightScreen ScreenState { get; private set; }
        internal NightPreferences Preferences { get; private set; }
        internal NightAtlas Atlas { get; private set; }
        internal NightTarget Focus => panel == null && !WorkingDispatch ? focus : null;
        internal RepairPanel RepairView => panel;
        internal float Hold => held != null && held.Hold > 0 ? Mathf.Clamp01(holdTime / held.Hold) : 0;
        internal bool InPursuit => !State.brine.Committed && hunter.Pursuing;
        internal bool IsInsideBuilding(Vector3 feet) => world.IsInsideBuilding(feet);

        internal string Message => Time.unscaledTime < circuitAnnouncedUntil ? "触发隐藏任务：？？？　水塔背后响了一声收票铃。" : messageTime > 0 ? message : "";
        internal NightState ResumableSave { get; private set; }
        // The most recent pickup, for the acquisition card. Serial changes with every new pickup.
        internal (string Art, string Text, int Serial) LastPickup { get; private set; }

        readonly List<NightTarget> targets = new();
        readonly List<ThrownStone> stones = new();
        NightWorld world;
        NightAudio soundscape;
        NightHunter hunter;
        NightInterface hud;
        NightCameraGrade grade;
        NightTarget focus, held;
        float holdTime, messageTime, pauseTime, caughtTime, releaseGrace;
        string message = "";
        struct ThrownStone
        {
            internal GameObject Object;
            internal float TTL;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (SceneManager.GetActiveScene().name == "AfterHours_Blockout")
                new GameObject("After Hours").AddComponent<NightGame>();
        }

        void Awake()
        {
            State = new NightState();
            Preferences = new NightPreferences();
            soundscape = new NightAudio(transform);
            Player = new GameObject("玩家").AddComponent<NightPlayer>();
            Player.SetUp(Camera.main);
            Player.ResetPosition(State.spawn, State.yaw);
            world = new NightWorld(this);
            world.Build();
            grade = Player.Eyes.GetComponent<NightCameraGrade>();
            BuildObjectives();
            Physics.SyncTransforms();
            hunter = new NightHunter(this);
            world.Refresh();
            Atlas = new NightAtlas();
            hud = new NightInterface(this);
            ApplyPreferences();
            SetScreen(NightScreen.Title);
            world.CaptureReflections();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-observe-hunter") >= 0) EnterObserver();
        }

        void OnDestroy()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            SyncCursor(false);
            Watch?.Close();
            hud?.Dispose();
            ReleaseBrineMaterials();
            world?.Dispose();
        }

        internal void ApplyPreferences()
        {
            Player.ApplyPreferences(Preferences);
            AudioListener.volume = Preferences.Volume;
        }

        internal void PlayMenuSound() => soundscape.PlayMenuSound();
        internal void PlayMenuTick() => soundscape.PlayMenuTick();
        internal void PlayTitleLightFlicker(float strength = 1f)
        {
            if (ScreenState == NightScreen.Title) soundscape.PlayTitleLightFlicker(strength);
        }

        internal void PlayTitleSpark(float strength = 1f)
        {
            if (ScreenState == NightScreen.Title) soundscape.PlayTitleSpark(strength);
        }

        internal void SetTitleBlackout(bool dark)
        {
            if (ScreenState == NightScreen.Title) soundscape.SetTitleBlackout(dark);
        }

        internal void PlayTitleContact()
        {
            if (ScreenState == NightScreen.Title) soundscape.PlayTitleContact();
        }

        static NightState LoadUnfinished()
        {
            var saved = NightSave.Read();
            return saved != null && !saved.escaped && !saved.ended ? saved : null;
        }

        internal void NewNight() => Begin(null);
        internal void ContinueNight()
        {
            var save = LoadUnfinished();
            if (save != null) Begin(save);
        }

        void Begin(NightState save, bool observation = false)
        {
            LeaveFalloutShot();
            world.SetBrineBlast(false);
            circuitAnnouncedUntil=-1;
            StopPipePlayback();
            ResetPA();
            guideFeedLook=false;
            State = save ?? new NightState { shiftEdition = 1, brine = new BrineRun { wheel = UnityEngine.Random.Range(0,4) } };
            CloseDispatch();
            radioCallIn = 0;
            foreach (var stone in stones) Destroy(stone.Object);
            stones.Clear();
            world.Refresh();
            RefreshLoad();
            hunter.Reset();
            RestoreBrineScene();
            Physics.SyncTransforms();
            if (State.detained) SetDetentionCheckpoint();
            State.spawn = WalkableSavePosition(State.spawn);
            Player.ResetPosition(State.spawn, State.yaw);
            if (save == null) Player.LookDown(8);
            Player.SetTorch(false);
            caughtTime = 0;
            releaseGrace = 0;
            panel = null;
            messageTime = 0;
            LastPickup = default;
            Atlas.Track(State, Player.Feet);
            if (grade) grade.SetFear(0);
            if (observation)
            {
                hunter.Pose(new Vector3(106, 0, -18), -90);
                OpenWatch(true);
                Emit(new Vector3(101, 0, -18), .90f, "land");
                return;
            }
            if (save == null) SaveProgress();
            SetScreen(NightScreen.Play);
            hud.Arrive(save == null);
        }

        void Update()
        {
            if (falloutShotCamera) FrameFalloutShot();
            messageTime -= Time.unscaledDeltaTime;
            if (ScreenState == NightScreen.Detention && Application.isFocused)
                State.dispatch.playSeconds += Time.unscaledDeltaTime;
            if (ScreenState == NightScreen.Capture)
            {
                caughtTime += Time.deltaTime;
                if (caughtTime >= soundscape.CaptureDuration) FinishCapture();
                else
                {
                    hunter.LaughAt(Player.Feet, caughtTime);
                    hunter.FrameCapture(Player.Eyes, caughtTime);
                }
            }
            else if (ScreenState == NightScreen.Observer) TickObserver(Time.deltaTime);
            else if (ScreenState == NightScreen.Play) TickPlay(Time.deltaTime);
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (ScreenState == NightScreen.Detention) CloseDetentionLock();
                else if (hud.CloseSubpage())
                    PlayMenuSound();
                else if (ScreenState is NightScreen.Pause or NightScreen.Map or NightScreen.Inventory)
                    SetScreen(NightScreen.Play);
            }
            else if (ScreenState == NightScreen.Inventory && Input.GetKeyDown(KeyCode.Q) && State.dispatch.carrying)
            {
                if (SetLoadDown()) SetScreen(NightScreen.Play);
            }
            else if (ScreenState == NightScreen.Inventory && Input.GetKeyDown(KeyCode.B)) hud.CloseInventory();
            else if (ScreenState == NightScreen.Map && Input.GetKeyDown(KeyCode.M))
                SetScreen(NightScreen.Play);
            if (grade && ScreenState is NightScreen.Play or NightScreen.Capture)
                grade.SetFear(ScreenState == NightScreen.Capture || InPursuit ? 1 : 0);
            hud.Tick();
        }

        void TickPlay(float dt)
        {
            if (Capturing)
            {
                Player.SetTorchVisible(State.torch);
                Player.AdvanceTorch(this, dt);
                Player.SettleView(dt);
                Atlas.Track(State, Player.Feet);
                FocusTarget(dt);
                return;
            }
            if (!Application.isFocused)
            {
                SetScreen(NightScreen.Pause);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (WorkingDispatch || Framing) CloseDispatch();
                else if (panel != null) panel = null;
                else SetScreen(NightScreen.Pause);
                return;
            }
            if (panel == null && !WorkingDispatch)
            {
                if (Input.GetKeyDown(KeyCode.F8) && CanObserveLive) { EnterObserver(); return; }
                if (Input.GetKeyDown(KeyCode.M))
                {
                    SetScreen(NightScreen.Map);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Tab))
                {
                    hud.OpenPack();
                    return;
                }
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
#if UNITY_EDITOR
                // The Editor uses Escape to release the Game view cursor.
                if ((pauseTime -= dt) <= 0) SetScreen(NightScreen.Pause);
#else
                SyncCursor(true);
                pauseTime = .12f;
#endif
                return;
            }
            if ((pauseTime -= dt) > 0) return;

            if (Input.GetKeyDown(KeyCode.Q) && State.dispatch.carrying && panel==null && !WorkingDispatch) SetLoadDown();
            State.dispatch.playSeconds += dt;
            if (WorkingDispatch)
            {
                Player.AdvanceTorch(this, dt);
                TickDispatchWork(dt);
            }
            else if (panel == null) Player.Tick(this, dt);
            else
            {
                Player.AdvanceTorch(this, dt);
                ReadRepairInput();
            }
            Atlas.Track(State, Player.Feet);
            if (!State.detained)
            {
                releaseGrace = Mathf.Max(0, releaseGrace - dt);
                if (State.brine.arming) TickBrineFeed(dt);
                else if (State.brine.Live) TickBrine(dt);
                else if (!State.circuit.Solved) hunter.Tick(dt);
                if (ScreenState != NightScreen.Play) return;
                TickWorld(dt);
            }
            if (panel != null || WorkingDispatch) return;
            if (State.brine.Live || State.brine.arming)
            {
                if (Input.GetKeyDown(KeyCode.F5)) SaveHere();
                FocusTarget(dt);
                return;
            }
            if (Player.InHideSpot)
            {
                if (Input.GetKeyDown(KeyCode.F5)) SaveHere();
                FocusTarget(dt);
                return;
            }
            if (Input.GetKeyDown(KeyCode.V)) ToggleFinder();
            if (Framing)
            {
                focus = held = null;
                holdTime = 0;
                AimSurvey();
                if (Input.GetKeyDown(InteractKey)) ExposeSurvey();
                return;
            }
            if (!State.detained && Input.GetKeyDown(KeyCode.G)) ThrowStone();
            if (Input.GetKeyDown(KeyCode.F5)) SaveHere();
            FocusTarget(dt);
        }

        internal void SetScreen(NightScreen screen)
        {
            if (ScreenState == NightScreen.Observer && screen != NightScreen.Observer)
            {
                Watch.Close();
                Watch = null;
            }
            if (screen != NightScreen.Play) CloseDispatch();
            LeaveFalloutShot();
            ScreenState = screen;
            bool nuclearEnding = screen is (NightScreen.Escaped or NightScreen.Caught)
                && State.brine.stage is (BrineStage.Clear or BrineStage.Overrun);
            bool playing = screen is NightScreen.Play or NightScreen.Capture;
            Time.timeScale = playing || screen == NightScreen.Observer ? 1 : 0;
            AudioListener.pause = screen is NightScreen.Pause or NightScreen.Map or NightScreen.Inventory
                or NightScreen.Caught or NightScreen.Escaped;
            SyncCursor(Application.isFocused);
            Player.Eyes.GetComponent<Camera>().enabled = screen is not (NightScreen.Title or NightScreen.Observer);
            if (grade) grade.SetMenu(!nuclearEnding && screen is (NightScreen.Pause or NightScreen.Map or NightScreen.Inventory
                or NightScreen.Detention or NightScreen.Caught or NightScreen.Escaped));
            Player.SetTorchVisible(screen == NightScreen.Play && (State.torch || State.brine.Live));
            soundscape.SetScreen(screen);
            if (screen != NightScreen.Capture) soundscape.StopCaptureLaugh();
            if (screen == NightScreen.Play) pauseTime = .12f;
            if (screen == NightScreen.Title)
            {
                world.SetBrineBlast(false);
                world.HideBrineFeed();
                ResetPA();
                Player.SetTorch(false);
                var last = NightSave.Read();
                NightTickets.Record(last);
                ResumableSave = last != null && !last.escaped && !last.ended ? last : null;
            }
            focus = held = null;
            holdTime = 0;
            if (screen is NightScreen.Escaped or NightScreen.Caught) NightTickets.Record(State);
            if (nuclearEnding)
            {
                StartFalloutShot();

            }
            hud.Show(screen);
            if (screen == NightScreen.Escaped && !nuclearEnding)
            {
                string cue = State.story.choice switch
                {
                    WitnessChoice.Broadcast => "signal", WitnessChoice.Seal => "paper", _ => "switch"
                };
                soundscape.Play(Player.Eyes.position, cue, .64f, duringPause:true);
            }
        }

        void OnApplicationFocus(bool focused)
        {
            if (hud == null) return;
            if (!focused && ScreenState == NightScreen.Observer)
            {
                if (!EmptyObservation) SetScreen(NightScreen.Pause);
            }
            else if (!focused && ScreenState == NightScreen.Play && !Capturing)
                SetScreen(NightScreen.Pause);
            else
                SyncCursor(focused);
        }

        void SyncCursor(bool focused)
        {
            bool capture = focused && ScreenState is (NightScreen.Play or NightScreen.Capture);
            var mode = capture ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != mode) Cursor.lockState = mode;
            bool visible = Cursor.lockState != CursorLockMode.Locked;
            if (Cursor.visible != visible) Cursor.visible = visible;
        }

        internal void Captured()
        {
            if ((ScreenState != NightScreen.Play && !(ScreenState == NightScreen.Observer && !EmptyObservation)) || Player.Concealed || State.brine.Committed || State.detained || State.circuit.Solved || releaseGrace > 0) return;
            Player.InterruptHiding();
            panel = null;
            if (State.dispatch.carrying) ReturnLoadOnCapture();
            caughtTime = 0;
            if (State.night == NightState.MaxNights) State.ended = true;
            else
            {
                State.night++;
                State.detained = true;
                SetDetentionCheckpoint();
            }
            SaveProgress();
            SetScreen(NightScreen.Capture);
            if (grade) grade.Shock();
            soundscape.PlayCaptureLaugh(hunter.LaughSource);
        }

        internal void RecoverPlayer() => Player.ResetPosition(WalkableSavePosition(State.spawn),State.yaw);

        // A save made before furniture was added can overlap one of the new colliders.
        static Vector3 WalkableSavePosition(Vector3 position)
        {
            const int scene = ~(1 << NightPlayer.PlayerLayer);
            static bool Clear(Vector3 p) => NightWorld.ContainsPlayable(p) &&
                !Physics.CheckCapsule(p + Vector3.up * .29f, p + Vector3.up * 1.44f, .24f, scene, QueryTriggerInteraction.Ignore);
            if (!NightWorld.ContainsPlayable(position)) return NightState.NewSpawn;
            if (Clear(position) && Physics.Raycast(position + Vector3.up * .08f, Vector3.down, .38f, scene,
                QueryTriggerInteraction.Ignore)) return position;
            for(int ring=1;ring<=4;ring++)
            for(int direction=0;direction<8;direction++)
            {
                float angle=direction*Mathf.PI/4;
                var near=position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(ring*.5f);
                if(!Physics.Raycast(near+Vector3.up*.27f,Vector3.down,out var floor,.67f,scene,QueryTriggerInteraction.Ignore))
                    continue;
                var at=floor.point+Vector3.up*.058f;
                if(Clear(at))return at;
            }
            return NightState.NewSpawn;
        }

        internal void Notify(string text,float time=3.4f)
        {
            message=text;
            messageTime=time;
        }

        internal void Emit(Vector3 point,float volume,string sound)
        {
            if(State.power && Vector3.Distance(point,new Vector3(-10,-3.6f,20))<10 && sound=="step")
                volume*=.58f;
            soundscape.Play(point+Vector3.up*.25f,sound,volume);
            hunter?.HearNoise(point, volume, sound);
        }
        internal void PlayCreatureStep(Vector3 point) => soundscape.Play(point,"hunter",.55f);

        void TickWorld(float dt)
        {
            if (!State.dispatch.orderHeld && State.Drained && !State.dispatch.AllRunning && !State.beacon && Vector3.SqrMagnitude(Player.Feet - NightDispatch.Receiver) < 110f && (radioCallIn -= dt) <= 0)
            {
                soundscape.Play(NightDispatch.Receiver, "signal", .29f);
                radioCallIn = 6.4f;
            }
            SoundReturnLines(dt);
            soundscape.Tick(State.power, InPursuit, dt);
            if(State.dropTime>0)
            {
                State.dropTime=Mathf.Max(0,State.dropTime-dt);
                if(State.dropTime==0)
                {
                    world.Refresh();
                    Emit(NightWorld.DropPoint(State),1,"land");
                    Notify("箱子落地了。");
                    SaveProgress();
                }
            }
            if(State.rescueTime>0)
            {
                State.rescueTime=Mathf.Max(0,State.rescueTime-dt);
                if(State.rescueTime==0)
                {
                    world.Refresh();
                    Emit(new Vector3(161,0,-20),1,"truck");
                    Notify("车刹停了。侧门滑开一条缝。",6);
                    SaveProgress();
                }
            }
            world.AnimateArrival();
            for(int i=stones.Count-1;i>=0;i--)
            {
                var current=stones[i];
                current.TTL-=dt;
                if(current.TTL<=0)
                {
                    Destroy(current.Object);
                    stones.RemoveAt(i);
                }
                else stones[i]=current;
            }
        }

        void ThrowStone()
        {
            if(State.stones<=0){Notify("身上没有石子");return;}
            State.stones--;
            var stone=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            stone.transform.position=Player.Eyes.position+Player.Eyes.forward*.45f;
            stone.transform.localScale=Vector3.one*.10f;
            var body=stone.AddComponent<Rigidbody>();
            body.linearVelocity=(Player.Eyes.forward+Vector3.up*.09f)*12;
            stone.AddComponent<StoneNoise>().Landed=pos=>Emit(pos,.88f,"land");
            stones.Add(new ThrownStone{Object=stone,TTL=5});
        }

        internal NightTarget Target(Vector3 position,string label,float seconds,Action action,Func<bool> available=null,float radius=2.15f)
        {
            var target = new NightTarget{Position=position,Label=label,Hold=seconds,Use=action,Available=available,Radius=radius};
            targets.Add(target);
            return target;
        }

        void FocusTarget(float dt)
        {
            focus=null;
            float best=0;
            var eyes=Player.Eyes;
            if (Player.InHideSpot) focus = Player.HideExit;
            else if(!Player.Vaulting)
            foreach(var target in targets)
            {
                if(State.brine.arming && target != brineInterlock) continue;
                if(State.brine.Live && target != brineExit && target != brineWheels[0] && target != brineWheels[1]) continue;
                if(target.Available!=null&&!target.Available())continue;
                Vector3 offset=target.Position-eyes.position;
                float length=offset.magnitude;
                if(length>target.Radius)continue;
                var aim = offset;
                if (target.AimHeight > 0)
                {
                    float depth = Vector3.Dot(offset, eyes.forward);
                    aim.y += Mathf.Clamp(eyes.forward.y * depth - offset.y, 0, target.AimHeight);
                }
                float alignment=Vector3.Dot(eyes.forward,aim.normalized);
                if(alignment<.79f)continue;
                if(Physics.Raycast(eyes.position,offset.normalized,Mathf.Max(0,length-.26f),~(1 << NightPlayer.PlayerLayer),QueryTriggerInteraction.Ignore))continue;
                float value=alignment*.65f+(1-length/target.Radius)*.35f;
                if(value<=best)continue;
                focus=target;
                best=value;
            }
            if(focus!=held || !Input.GetKey(InteractKey))
            {
                holdTime=0;
                held=null;
            }
            if(focus!=null&&Input.GetKeyDown(InteractKey))held=focus;
            if(held==null)return;
            holdTime+=dt;
            if(holdTime<held.Hold)return;
            var chosen=held;
            held=null;
            chosen.Use();
            holdTime=0;
            focus = null;
        }

        internal void ToggleDoor(NightWorld.Door door)
        {
            if (NightDetention.Locks(door.Name, State)) return;
            if (!TryUnlockDoor(door, out bool saveUnlock)) return;
            if(door.Fire)
            {
                if(State.firstFireDoor)return;
                if(Player.Feet.z<NightWorld.FireDoorPosition.z){Notify("防火门的推杆在另一侧");return;}
                State.firstFireDoor=true;
            }
            else if(!State.openDoors.Remove(door.Name))State.openDoors.Add(door.Name);
            world.Refresh();
            Emit(door.Position,Player.Crouched?.25f:.57f,"tool");
            if (saveUnlock) SaveProgress();
        }

        internal void OpenDoorsForHunter(Vector3 position)
        {
            foreach(var door in world.Doors)
            {
                if(!door.Leaf.activeSelf||Mathf.Abs(position.y-door.Position.y)>.55f||
                    Vector3.Distance(position,door.Position)>1.05f)continue;
                if (NightDetention.Locks(door.Name, State) || KeyDoorLocked(door) ||
                    door.Fire && position.z < door.Position.z) continue;
                if(door.Fire)State.firstFireDoor=true;
                else State.openDoors.Add(door.Name);
                door.Leaf.SetActive(false);
                soundscape.Play(door.Position,"tool",.7f);
            }
        }

        internal void SaveHere()
        {
            if (Player.InHideSpot) { Notify("先从藏身处出来，再保存。"); return; }
            if(Player.Vaulting||!Player.Body.isGrounded||Player.Crouched||
               (hunter.Pursuing && Vector3.Distance(hunter.Position,Player.Feet)<13))
            {
                Notify("站到平稳的地方，等脚步离开再存。");
                return;
            }
            State.spawn=Player.Feet+Vector3.up*.06f;
            State.yaw=Player.transform.eulerAngles.y;
            if(SaveProgress())Notify("已保存");
        }

        bool SaveProgress()
        {
            if (EmptyObservation) return false;
            if (!State.brine.Committed)
            {
                FinishShiftOrder();
                CheckReturnCircuit();
            }
            if (Capturing) return true;
            try{ NightSave.Write(State); return true; }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning(ex.Message);
                Notify("存档写入失败");
                return false;
            }
        }

        internal sealed class StoneNoise : MonoBehaviour
        {
            internal Action<Vector3> Landed;
            void OnCollisionEnter(Collision collision)
            {
                Landed?.Invoke(collision.GetContact(0).point);
                Landed=null;
            }
        }
    }
}
