using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AfterHours
{
    internal enum NightScreen { Title, Play, Map, Inventory, Pause, Capture, Caught, Escaped }

    internal sealed partial class NightGame : MonoBehaviour
    {
        internal NightState State { get; private set; }
        internal NightPlayer Player { get; private set; }
        internal float LandingNoise { get; private set; }
        internal NightScreen ScreenState { get; private set; }
        internal NightPreferences Preferences { get; private set; }
        internal NightAtlas Atlas { get; private set; }
        internal NightTarget Focus => panel == null ? focus : null;
        internal RepairPanel RepairView => panel;
        internal float Hold => held != null && held.Hold > 0 ? Mathf.Clamp01(holdTime / held.Hold) : 0;
        internal bool InPursuit => hunter.Pursuing;
        internal string Message => messageTime > 0 ? message : "";
        internal NightState ResumableSave { get; private set; }

        readonly List<NightTarget> targets = new();
        readonly List<ThrownStone> stones = new();
        NightWorld world;
        NightAudio soundscape;
        NightHunter hunter;
        NightInterface hud;
        NightTarget focus, held;
        float holdTime, messageTime, pauseTime, caughtTime;
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
            BuildObjectives();
            Physics.SyncTransforms();
            hunter = new NightHunter(this);
            world.Refresh();
            Atlas = new NightAtlas();
            hud = new NightInterface(this);
            ApplyPreferences();
            SetScreen(NightScreen.Title);
        }

        void OnDestroy()
        {
            Time.timeScale = 1;
            AudioListener.pause = false;
            SyncCursor(false);
            hud?.Dispose();
            world?.Dispose();
        }

        internal void ApplyPreferences()
        {
            Player.ApplyPreferences(Preferences);
            AudioListener.volume = Preferences.Volume;
        }

        internal void PlayMenuSound() => soundscape.PlayMenuSound();
        internal void PlayMenuTick() => soundscape.PlayMenuTick();

        static NightState LoadUnfinished()
        {
            var saved = NightSave.Read();
            return saved != null && !saved.escaped ? saved : null;
        }

        internal void NewNight() => Begin(null);
        internal void ContinueNight() => Begin(LoadUnfinished());

        void Begin(NightState save)
        {
            State = save ?? new NightState();
            foreach (var stone in stones) Destroy(stone.Object);
            stones.Clear();
            world.Refresh();
            hunter.Reset();
            Physics.SyncTransforms();
            State.spawn = WalkableSavePosition(State.spawn);
            Player.ResetPosition(State.spawn, State.yaw);
            if (save == null) Player.LookDown(8);
            Player.SetTorch(false);
            caughtTime = 0;
            panel = null;
            messageTime = 0;
            Atlas.Track(State, Player.Feet);
            SetScreen(NightScreen.Play);
        }

        void Update()
        {
            messageTime -= Time.unscaledDeltaTime;
            if (ScreenState == NightScreen.Capture)
            {
                caughtTime += Time.deltaTime;
                if (caughtTime >= soundscape.CaptureDuration)
                {
                    ResumableSave = LoadUnfinished();
                    hunter.EndCapture();
                    SetScreen(NightScreen.Caught);
                }
                else
                {
                    hunter.LaughAt(Player.Feet, caughtTime);
                    hunter.FrameCapture(Player.Eyes, caughtTime);
                }
            }
            else if (ScreenState == NightScreen.Play) TickPlay(Time.deltaTime);
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (hud.CloseSubpage())
                    PlayMenuSound();
                else if (ScreenState is NightScreen.Pause or NightScreen.Map or NightScreen.Inventory)
                    SetScreen(NightScreen.Play);
            }
            else if ((ScreenState == NightScreen.Map && Input.GetKeyDown(KeyCode.M)) ||
                (ScreenState == NightScreen.Inventory && Input.GetKeyDown(KeyCode.B)))
                SetScreen(NightScreen.Play);
            hud.Tick();
        }

        void TickPlay(float dt)
        {
            if (!Application.isFocused)
            {
                SetScreen(NightScreen.Pause);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (panel != null) panel = null;
                else SetScreen(NightScreen.Pause);
                return;
            }
            if (panel == null)
            {
                if (Input.GetKeyDown(KeyCode.M))
                {
                    SetScreen(NightScreen.Map);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Tab))
                {
                    SetScreen(NightScreen.Inventory);
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

            if (panel == null) Player.Tick(this, dt);
            else
            {
                Player.AdvanceTorch(this, dt);
                ReadRepairInput();
            }
            Atlas.Track(State, Player.Feet);
            hunter.Tick(dt);
            if (ScreenState != NightScreen.Play) return;
            TickWorld(dt);
            if (panel != null) return;
            if (Input.GetKeyDown(KeyCode.G)) ThrowStone();
            if (Input.GetKeyDown(KeyCode.F5)) SaveHere();
            FocusTarget(dt);
        }

        internal void SetScreen(NightScreen screen)
        {
            ScreenState = screen;
            bool playing = screen is NightScreen.Play or NightScreen.Capture;
            Time.timeScale = playing ? 1 : 0;
            AudioListener.pause = screen is NightScreen.Pause or NightScreen.Map or NightScreen.Inventory
                or NightScreen.Caught or NightScreen.Escaped;
            SyncCursor(Application.isFocused);
            Player.Eyes.GetComponent<Camera>().enabled = screen != NightScreen.Title;
            Player.SetTorchVisible(screen == NightScreen.Play && State.torch);
            soundscape.SetTitle(screen == NightScreen.Title);
            if (screen is not (NightScreen.Capture or NightScreen.Caught)) soundscape.StopCaptureLaugh();
            if (screen == NightScreen.Play) pauseTime = .12f;
            if (screen == NightScreen.Title)
            {
                Player.SetTorch(false);
                ResumableSave = LoadUnfinished();
            }
            focus = held = null;
            holdTime = 0;
            hud.Show(screen);
        }

        void OnApplicationFocus(bool focused)
        {
            if (hud == null) return;
            if (!focused && ScreenState == NightScreen.Play)
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
            if (ScreenState != NightScreen.Play) return;
            panel = null;
            caughtTime = 0;
            SetScreen(NightScreen.Capture);
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
            hunter?.Hear(point,volume);
        }
        internal void PlayCreatureStep(Vector3 point) => soundscape.Play(point,"hunter",.55f);

        void TickWorld(float dt)
        {
            soundscape.Tick(State.power);
            if(State.dropTime>0)
            {
                State.dropTime=Mathf.Max(0,State.dropTime-dt);
                if(State.dropTime==0)
                {
                    world.Refresh();
                    Emit(NightWorld.DropPoint(State),1,"land");
                    Notify("补给已到");
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
                    Notify("救援车已靠站，从车侧登车。",6);
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

        internal void Target(Vector3 position,string label,float seconds,Action action,Func<bool> available=null,float radius=2.15f)
        {
            targets.Add(new NightTarget{Position=position,Label=label,Hold=seconds,Use=action,Available=available,Radius=radius});
        }

        void FocusTarget(float dt)
        {
            focus=null;
            float best=0;
            var eyes=Player.Eyes;
            if(!Player.Vaulting)
            foreach(var target in targets)
            {
                if(target.Available!=null&&!target.Available())continue;
                Vector3 offset=target.Position-eyes.position;
                float length=offset.magnitude;
                if(length>target.Radius)continue;
                float alignment=Vector3.Dot(eyes.forward,offset.normalized);
                if(alignment<.79f)continue;
                if(Physics.Raycast(eyes.position,offset.normalized,Mathf.Max(0,length-.26f),~(1 << NightPlayer.PlayerLayer),QueryTriggerInteraction.Ignore))continue;
                float value=alignment*.65f+(1-length/target.Radius)*.35f;
                if(value<=best)continue;
                focus=target;
                best=value;
            }
            if(focus!=held || !Input.GetKey(KeyCode.E))
            {
                holdTime=0;
                held=null;
            }
            if(focus!=null&&Input.GetKeyDown(KeyCode.E))held=focus;
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
            if(door.Fire)
            {
                if(State.firstFireDoor)return;
                if(Player.Feet.z<10){Notify("防火门的推杆在另一侧");return;}
                State.firstFireDoor=true;
            }
            else if(!State.openDoors.Remove(door.Name))State.openDoors.Add(door.Name);
            world.Refresh();
            Emit(door.Position,Player.Crouched?.25f:.57f,"tool");
        }

        internal void OpenDoorsForHunter(Vector3 position)
        {
            foreach(var door in world.Doors)
            {
                if(!door.Leaf.activeSelf||Mathf.Abs(position.y-door.Position.y)>.55f||
                    Vector3.Distance(position,door.Position)>1.05f)continue;
                if(door.Fire&&position.z<10)continue;
                if(door.Fire)State.firstFireDoor=true;
                else State.openDoors.Add(door.Name);
                door.Leaf.SetActive(false);
                soundscape.Play(door.Position,"tool",.7f);
            }
        }

        internal void SaveHere()
        {
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
