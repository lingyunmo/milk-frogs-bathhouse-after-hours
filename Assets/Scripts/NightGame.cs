using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AfterHours
{
    internal sealed partial class NightGame : MonoBehaviour
    {
        internal NightState State { get; private set; }
        internal NightPlayer Player { get; private set; }
        internal float LandingNoise { get; private set; }
        readonly List<NightTarget> targets=new();
        NightWorld world;
        NightAudio audio;
        NightHunter hunter;
        NightTarget focus, held;
        float holdTime, messageTime, pauseTime, caughtTime;
        string message="";
        bool running, menu=true, caught;
        readonly List<ThrownStone> stones=new();
        struct ThrownStone
        {
            internal GameObject Object;
            internal float TTL;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if(SceneManager.GetActiveScene().name=="AfterHours_Blockout")
                new GameObject("After Hours").AddComponent<NightGame>();
        }

        void Awake()
        {
            State=new NightState();
            audio=new NightAudio(transform);
            Player=new GameObject("玩家").AddComponent<NightPlayer>();
            Player.SetUp(Camera.main);
            Player.ResetPosition(State.spawn,State.yaw);
            world=new NightWorld(this);
            world.Build();
            BuildObjectives();
            Physics.SyncTransforms();
            hunter=new NightHunter(this);
            world.Refresh();
            SetupHud();
        }

        void OnDestroy() => world?.Dispose();

        void Begin(NightState save)
        {
            State=save??new NightState();
            foreach(var stone in stones)Destroy(stone.Object);
            stones.Clear();
            world.Refresh();
            hunter.Reset();
            Physics.SyncTransforms();
            State.spawn=WalkableSavePosition(State.spawn);
            Player.ResetPosition(State.spawn,State.yaw);
            Player.SetTorch(false);
            menu=false;
            caught=false;
            caughtTime=0;
            running=true;
            focus=held=null;
            panel=null;
            holdTime=0;
            messageTime=0;
            pauseTime=.2f;
            Cursor.lockState=CursorLockMode.Locked;
            Cursor.visible=false;
            if(save==null)Notify("更衣凳上有手电。下楼去前台看看。",6);
        }

        void Update()
        {
            if(!running||State.escaped)return;
            if(caught)
            {
                if(!menu && (caughtTime+=Time.deltaTime)<1.55f)
                {
                    hunter.LaughAt(Player.Feet,caughtTime);
                    var looking=Quaternion.LookRotation(hunter.Position+Vector3.up*1.10f-Player.Eyes.position);
                    Player.Eyes.rotation=Quaternion.Slerp(Player.Eyes.rotation,looking,Time.deltaTime*11);
                }
                else if(!menu)SetMenu(true);
                return;
            }
            if(Input.GetKeyDown(KeyCode.Escape))
            {
                if(panel!=null)panel=null;
                else SetMenu(!menu);
                return;
            }
            if(menu)return;
            if(Cursor.lockState!=CursorLockMode.Locked)
            {
                SetMenu(true);
                return;
            }
            if((pauseTime-=Time.deltaTime)>0)return;
            float dt=Time.deltaTime;
            messageTime-=dt;
            if(panel==null)Player.Tick(this,dt);
            else ReadRepairInput();
            hunter.Tick(dt);
            if(caught)return;
            TickWorld(dt);
            if(Input.GetKeyDown(KeyCode.G))ThrowStone();
            if(Input.GetKeyDown(KeyCode.F5))SaveHere();
            if(panel==null)FocusTarget(dt);
        }

        void SetMenu(bool visible)
        {
            menu=visible;
            Cursor.lockState=visible?CursorLockMode.None:CursorLockMode.Locked;
            Cursor.visible=visible;
            holdTime=0;
            held=null;
        }

        internal void Captured()
        {
            if(caught)return;
            audio.Play(hunter.Position+Vector3.up*1.2f,"laugh",.9f);
            caught=true;
            panel=null;
            focus=held=null;
            caughtTime=0;
        }

        internal void RecoverPlayer() => Player.ResetPosition(WalkableSavePosition(State.spawn),State.yaw);

        // A save made before furniture was added can overlap one of the new colliders.
        static Vector3 WalkableSavePosition(Vector3 position)
        {
            const int scene = ~(1 << NightPlayer.PlayerLayer);
            static bool Clear(Vector3 p) => !Physics.CheckCapsule(p+Vector3.up*.29f,
                p+Vector3.up*1.44f,.24f,scene,QueryTriggerInteraction.Ignore);
            if(Clear(position))return position;
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

        void Notify(string text,float time=3.4f)
        {
            message=text;
            messageTime=time;
        }

        internal void Emit(Vector3 point,float volume,string sound)
        {
            if(State.power && Vector3.Distance(point,new Vector3(-10,-3.6f,20))<10 && sound=="step")
                volume*=.58f;
            audio.Play(point+Vector3.up*.25f,sound,volume);
            hunter?.Hear(point,volume);
        }
        internal void PlayCreatureStep(Vector3 point) => audio.Play(point,"hunter",.55f);

        void TickWorld(float dt)
        {
            audio.Tick(State.power);
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

        internal void ReplaceCell()
        {
            if(!State.torch||State.cells==0){Notify("没有备用电池");return;}
            if(State.charge>=344){Notify("当前电池电量充足");return;}
            State.charge=360;
            State.cells--;
            Player.SetTorch(false);
            Notify("已换电池。按 F 开手电。");
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
                audio.Play(door.Position,"tool",.7f);
            }
        }

        void SaveHere()
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
