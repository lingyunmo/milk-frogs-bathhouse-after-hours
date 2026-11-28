using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AfterHours
{
    // Run in the isolated QA player with -hide-spot-qa <stills directory>.
    internal sealed class AfterHoursHideSpotSmoke : MonoBehaviour
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const int SceneMask = ~(1 << NightPlayer.PlayerLayer | 1 << NightPlayer.ViewModelLayer | 1 << 10);
        NightGame game;
        NightHunter hunter;
        string folder;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.companyName == "AfterHours QA" && Environment.GetCommandLineArgs().Contains("-hide-spot-qa"))
                new GameObject("Hide spot QA").AddComponent<AfterHoursHideSpotSmoke>();
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return null;
            var run = CheckAll();
            while(true)
            {
                try { if (!run.MoveNext()) break; }
                catch(Exception error) { Debug.LogException(error); Application.Quit(75); yield break; }
                yield return run.Current;
            }
            Debug.Log("HIDE SPOT PASS");
            Application.Quit(0);
        }

        IEnumerator CheckAll()
        {
            var args = Environment.GetCommandLineArgs();
            folder = args[Array.IndexOf(args,"-hide-spot-qa")+1];
            Directory.CreateDirectory(folder);
            game = FindFirstObjectByType<NightGame>();
            game.enabled = false;
            game.NewNight();
            hunter = (NightHunter)typeof(NightGame).GetField("hunter",Private).GetValue(game);
            var targets = (List<NightTarget>)typeof(NightGame).GetField("targets",Private).GetValue(game);
            var player = game.Player;
            var cases = new (string Label,Vector3 Start,Vector3 Outside,string File)[] {
                ("钻进沙发底",new(-15,3.66f,6.75f),new(-15,3.66f,8.2f),"sofa"),
                ("钻进高脚沙发底",new(36.44f,3.66f,19.5f),new(34.7f,3.66f,19.5f),"annex"),
                ("掀开布帘钻入台下",new(26.22f,3.66f,18.75f),new(26.22f,3.66f,17.6f),"linen"),
                ("钻进宿舍床底",new(-3,7.26f,18.54f),new(-3,7.26f,17.1f),"bed"),
                ("钻进检修台底",new(-14,-3.54f,6.68f),new(-14,-3.54f,8.05f),"workbench"),
                ("挤进晾衣架",new(-10.33f,.06f,18.46f),new(-8.87f,.06f,18.46f),"laundry"),
                ("钻进行军床底",new(284,.09f,-.69f),new(284,.09f,-2.26f),"bunk"),
                ("躲进寄存柜",new(1.76f,3.66f,21.91f),new(1.76f,3.66f,19.75f),"locker")
            };
            foreach (var item in cases)
            {
                var target = targets.Single(t => t.Label == item.Label);
                player.ResetPosition(item.Start,0);
                var toward = player.Feet-item.Outside;
                hunter.Pose(item.Outside,Mathf.Atan2(toward.x,toward.z)*Mathf.Rad2Deg);
                player.Eyes.LookAt(target.Position);
                Physics.SyncTransforms();
                Focus();
                if(game.Focus != target)
                {
                    var ray=target.Position-player.Eyes.position;
                    var blocks=Physics.RaycastAll(player.Eyes.position,ray.normalized,ray.magnitude,
                        SceneMask,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance)
                        .Select(h=>h.collider.name+"@"+h.distance.ToString("F2"));
                    throw new Exception(item.Label+"入口无提示，available="+target.Available()+
                        "，focused="+game.Focus?.Label+"，hits="+string.Join(", ",blocks));
                }
                player.Eyes.LookAt(new Vector3(target.Position.x,player.Eyes.position.y,target.Position.z));
                Focus();
                Require(game.Focus == target,item.Label+"站着平视家具时没有交互");
                Require(Sees(),item.Label+"缺少藏前可见对照");
                player.SetTorch(true);
                game.Focus.Use();
                Require(player.InHideSpot && !player.Concealed && !player.TorchOn,item.Label+"没有进入动画");
                for(int i=0;i<25;i++) player.AdvanceHiding(.04f,0,0);
                Physics.SyncTransforms();
                Focus();
                Require(player.Concealed && player.HideExit == game.Focus && game.Focus != null,item.Label+"无法离开");
                Require(!Sees(),item.Label+"被视觉识别");
                game.Captured();
                Require(game.ScreenState == NightScreen.Play && !game.State.detained,item.Label+"被隔家具抓走");
                // Sample the actual opening from eye level, including ankle-height sightlines under upholstery.
                int lines=0;
                for (float h=.11f;h<1.85f;h+=.08f)
                    if (!Physics.Linecast(player.Eyes.position,item.Outside+Vector3.up*h,SceneMask,QueryTriggerInteraction.Ignore)) lines++;
                Require(lines>0,item.Label+"玩家看不到外侧");
                for (int tick=0;tick<40;tick++) hunter.Tick(.05f);
                Require(!game.InPursuit && game.ScreenState==NightScreen.Play,item.Label+"藏匿期间追击仍在继续");
                hunter.Pose(item.Outside,Mathf.Atan2(toward.x,toward.z)*Mathf.Rad2Deg);
                game.SetScreen(NightScreen.Pause);
                game.SetScreen(NightScreen.Play);
                Require(player.Concealed && !player.TorchOn,item.Label+"暂停破坏藏身");
                yield return new WaitForSecondsRealtime(.45f);
                yield return new WaitForEndOfFrame();
                Photograph(item.File+".jpg");
                var feet = player.Feet;
                player.AdvanceHiding(.02f,500,-100);
                Require((player.Feet-feet).sqrMagnitude<.0001f,item.Label+"观察时位置漂移");
                Focus();
                Require(game.Focus == player.HideExit && game.Focus != null,item.Label+"回头后无退出提示");
                game.Focus.Use();
                for(int i=0;i<25 && player.InHideSpot;i++) player.AdvanceHiding(.04f,0,0);
                Physics.SyncTransforms();
                Require(!player.InHideSpot && player.Body.enabled && !player.Crouched,item.Label+"未恢复行走");
                Require(FloorRoutes.CanMove(player.Feet,item.Start),item.Label+"出口堵塞 "+player.Feet);
                Require(Sees(),item.Label+"离开后未恢复视觉");
                Debug.Log("HIDE CHECK "+item.Label+" see-through samples="+lines);
                if (item.File == "locker")
                {
                    player.ResetPosition(item.Start,0);
                    player.Eyes.LookAt(target.Position);
                    Physics.SyncTransforms();
                    Focus();
                    game.Focus.Use();
                    player.AdvanceHiding(.12f,0,0);
                    game.Captured();
                    Require(!player.InHideSpot && player.Body.enabled && game.ScreenState==NightScreen.Capture,
                        "进柜途中被抓时仍占用藏点");
                    game.NewNight();
                    Require(!player.InHideSpot && !player.Concealed && player.Body.enabled,"新一局残留藏身状态");
                }
            }
        }

        bool Sees() => (bool)typeof(NightHunter).GetMethod("PlayerVisible",Private).Invoke(hunter,new object[]{0f});
        void Focus() => typeof(NightGame).GetMethod("FocusTarget",Private).Invoke(game,new object[]{0f});
        static void Require(bool check,string message) { if (!check) throw new Exception(message); }

        void Photograph(string name)
        {
            var camera=game.Player.Eyes.GetComponent<Camera>();
            var grade=camera.GetComponent<NightCameraGrade>();
            var image=RenderTexture.GetTemporary(800,450,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var last=RenderTexture.active;
            var shot=new Texture2D(800,450,TextureFormat.RGB24,false);
            try
            {
                camera.Render();
                typeof(NightCameraGrade).GetMethod("Present",Private).Invoke(grade,new object[]{image});
                RenderTexture.active=image;
                shot.ReadPixels(new Rect(0,0,800,450),0,0);shot.Apply();
                File.WriteAllBytes(Path.Combine(folder,name),shot.EncodeToJPG(91));
            }
            finally { RenderTexture.active=last; RenderTexture.ReleaseTemporary(image); Destroy(shot); }
        }
    }
}
