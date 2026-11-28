using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AfterHours
{
    // QA player only. Produces a per-area contact sheet source and a repeatable street transect.
    internal sealed class AfterHoursCoverSurvey : MonoBehaviour
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const int Mask = ~(1 << NightPlayer.PlayerLayer | 1 << NightPlayer.ViewModelLayer | 1 << 10);
        NightGame game;
        string folder;
        static T Field<T>(object obj,string name) => (T)obj.GetType().GetField(name,Private).GetValue(obj);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Application.companyName == "AfterHours QA" && Environment.GetCommandLineArgs().Contains("-cover-survey"))
                new GameObject("Cover survey").AddComponent<AfterHoursCoverSurvey>();
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return null;
            var run = Survey();
            while (true)
            {
                bool more;
                try { more = run.MoveNext(); }
                catch(Exception error) { Debug.LogException(error); Application.Quit(75); yield break; }
                if (!more) break;
                yield return run.Current;
            }
            Debug.Log("COVER SURVEY PASS");
            Application.Quit(0);
        }

        IEnumerator Survey()
        {
            var args = Environment.GetCommandLineArgs();
            folder = args[Array.IndexOf(args,"-cover-survey")+1];
            Directory.CreateDirectory(folder);
            game = FindFirstObjectByType<NightGame>();
            game.enabled = false;
            game.State.torch = true;
            game.State.charge = NightState.FullTorchCharge;
            game.SetScreen(NightScreen.Play);
            game.Player.SetTorch(true);
            var targets = Field<List<NightTarget>>(game,"targets");
            var props = GameObject.Find("First night runtime").GetComponentsInChildren<Transform>()
                .Where(t => t.name == "Fit model to furniture placement").Select(t => t.parent).ToArray();
            var areas = new List<string> { "floor\tname\tflags\tcenterXZ\tprops\tinteractions\tpicture" };
            int index = 0;
            for (int floor = 0; floor <= NightAtlas.Street; floor++)
            foreach (var area in game.Atlas.Areas(floor))
            {
                float y = floor == NightAtlas.Street ? 0 : (floor-1)*3.6f;
                bool Inside(Vector3 point) => point.y >= y-.24f && point.y < y+3.36f &&
                    area.Bounds.Contains(new Vector2(point.x,point.z)) &&
                    game.Atlas.FloorAt(new Vector3(point.x,y+.06f,point.z)) == floor;
                var furniture = props.Where(t=>Inside(t.position)).Select(t=>t.name);
                var uses = targets.Where(t=>Inside(t.Position) && !t.Label.StartsWith("推门 · ")).Select(t=>t.Label).Distinct();
                string file = (++index).ToString("D3") + ".jpg";
                if (TryFrame(area.Bounds,floor,area.Name switch { "卸货平台" or "西侧雨棚" => 1.84f, "检修平台" => 5.45f, _ => y },out var feet,out var aim))
                {
                    game.Player.ResetPosition(feet,0);
                    game.Player.Eyes.LookAt(aim);
                    game.Player.SetTorch(true);
                    game.Player.AdvanceTorch(game,.3f);
                    Physics.SyncTransforms();
                    yield return new WaitForEndOfFrame();
                    Photograph(file);
                }
                else file = "-";
                areas.Add(string.Join("\t",floor,area.Name,area.Stair?"stair":area.Route?"route":"place",
                    area.Bounds.center.ToString("F1"),string.Join("|",furniture),string.Join("|",uses),file));
            }
            File.WriteAllLines(Path.Combine(folder,"areas.tsv"),areas);
            var lines = new List<string>{"route\tx\tclear\tnear-cover"};
            foreach(var (name,start,end,z) in new[]{("石桥路",16,164,-16f),("堤顶公路",174,336,-18f),("巡堤步道",174,336,3.8f)})
                for(int x=start;x<=end;x+=2)
                {
                    var feet = new Vector3(x,.045f,z);
                    lines.Add(string.Join("\t",name,x,Free(feet)?1:0,NearCover(feet)?1:0));
                }
            File.WriteAllLines(Path.Combine(folder,"roads.tsv"),lines);
            if(args.Contains("-verify-cover"))
            {
                foreach(var shot in new (string File, Vector3 Feet, Vector3 Aim)[] {
                    ("darkroom-road", new(250,.05f,-20), new(255,.84f,-14.5f)),
                    ("sandbags", new(180,.05f,-18), new(186,.83f,-14.2f)),
                    ("van", new(208,.05f,-17.8f), new(202.8f,1.05f,-21.6f)),
                    ("screen", new(8.0f,3.65f,16.65f), new(10.0f,4.66f,19.0f)),
                    ("linen", new(28.52f,3.65f,14.65f), new(25.55f,4.6f,17.6f)),
                    ("archive", new(-5.3f,7.25f,8.2f), new(-3.5f,8.06f,3.77f)) })
                {
                    if(!Free(shot.Feet)) throw new Exception("Camera occupied: "+shot.File);
                    game.Player.ResetPosition(shot.Feet,0);
                    game.Player.Eyes.LookAt(shot.Aim);
                    game.Player.AdvanceTorch(game,.3f);
                    Physics.SyncTransforms();
                    yield return new WaitForEndOfFrame();
                    Photograph(shot.File+".jpg");
                }
                VerifyCover();
            }
        }

        static bool Free(Vector3 p) => Physics.Raycast(p+Vector3.up*.23f,Vector3.down,.55f,Mask,QueryTriggerInteraction.Ignore) &&
            !Physics.CheckCapsule(p+Vector3.up*.32f,p+Vector3.up*1.32f,.27f,Mask,QueryTriggerInteraction.Ignore);

        // Three adjacent rays must hit the same obstacle. Thin poles do not count as cover.
        static bool NearCover(Vector3 feet)
        {
            for(int yaw=0; yaw<360; yaw+=15)
            {
                Vector3 fwd=Quaternion.Euler(0,yaw,0)*Vector3.forward, side=Vector3.Cross(Vector3.up,fwd)*.28f;
                var start=feet+Vector3.up*.94f;
                Collider Hit(Vector3 offset) => Physics.Raycast(start+offset,fwd,out var h,6,Mask,QueryTriggerInteraction.Ignore)?h.collider:null;
                var center=Hit(Vector3.zero);
                if(center && Hit(side)==center && Hit(-side)==center) return true;
            }
            return false;
        }

        bool TryFrame(Rect rect,int floor,float y,out Vector3 feet,out Vector3 aim)
        {
            feet=aim=default;
            var center=new Vector3(rect.center.x,y+1.17f,rect.center.y);
            float best=-1;
            foreach(float x in new[]{.18f,.82f,.35f,.65f,.50f})
            foreach(float z in new[]{.18f,.82f,.35f,.65f,.50f})
            {
                var at=new Vector3(Mathf.Lerp(rect.xMin,rect.xMax,x),y+.06f,Mathf.Lerp(rect.yMin,rect.yMax,z));
                if(!Free(at) || (floor == NightAtlas.Street && game.Atlas.FloorAt(at) != floor)) continue;
                var eye=at+Vector3.up*1.55f;
                float depth=Vector3.Distance(eye,center);
                if(Physics.Linecast(eye,center,out var h,Mask,QueryTriggerInteraction.Ignore)) depth=h.distance;
                if(depth<=best) continue;
                feet=at; aim=center; best=depth;
            }
            return best>=0;
        }

        void Photograph(string name)
        {
            var camera=game.Player.Eyes.GetComponent<Camera>();
            var grade=camera.GetComponent<NightCameraGrade>();
            var target=RenderTexture.GetTemporary(480,270,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=RenderTexture.active;
            var pic=new Texture2D(480,270,TextureFormat.RGB24,false);
            try
            {
                camera.Render();
                typeof(NightCameraGrade).GetMethod("Present",Private).Invoke(grade,new object[]{target});
                RenderTexture.active=target;
                pic.ReadPixels(new Rect(0,0,480,270),0,0);pic.Apply();
                File.WriteAllBytes(Path.Combine(folder,name),pic.EncodeToJPG(86));
            }
            finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(target); Destroy(pic); }
        }

        void VerifyCover()
        {
            var checks = new List<string> { "prop\tcovered\texposed-again\twalkable-detour" };
            var hunter = Field<NightHunter>(game, "hunter");
            var visible = typeof(NightHunter).GetMethod("PlayerVisible", Private);
            bool Seen(Vector3 watcher, Vector3 player, bool crouch)
            {
                game.Player.ResetPosition(player, 0);
                if (crouch)
                {
                    game.Player.ToggleCrouch();
                    game.Player.Eyes.localPosition = Vector3.up * .41f;
                }
                Vector3 aim = player - watcher;
                hunter.Pose(watcher, Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg);
                Physics.SyncTransforms();
                return (bool)visible.Invoke(hunter, new object[] { 0f });
            }
            var pieces = GameObject.Find("街区追逐掩体").transform.Cast<Transform>().ToList();
            pieces.AddRange(GameObject.Find("楼内勤务陈设").transform.Cast<Transform>().Where(t =>
                t.name is "密封档案柜" or "待检仪表柜" or "机房隔油工服柜" || t.GetComponentInChildren<MeshCollider>()));
            (bool hidden, bool exposed, bool detour) Probe(Transform piece)
            {
                Bounds bounds = piece.GetComponentInChildren<Renderer>().bounds;
                foreach (var mesh in piece.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(mesh.bounds);
                bool hidden = false, exposed = false, detour = false;
                foreach (int turn in new[] { 0,180,90,270 })
                {
                    Vector3 forward = Quaternion.Euler(0,turn,0)*piece.forward;
                    Vector3 right = Vector3.Cross(Vector3.up,forward);
                    float Reach(Vector3 v) => Mathf.Abs(v.x)*bounds.extents.x + Mathf.Abs(v.z)*bounds.extents.z;
                    float depth = Reach(forward), span = Reach(right);
                    foreach(float offset in new[]{ 0f,.30f,-.30f })
                    {
                        Vector3 basePoint = piece.position + Vector3.up*.06f + right*offset;
                        Vector3 watcher = basePoint - forward*(depth+1.7f);
                        Vector3 shelter = basePoint + forward*(depth+.60f);
                        if (!Free(watcher) || !Free(shelter)) continue;
                        bool crouch = bounds.size.y < 1.54f;
                        Vector3 eye = watcher + Vector3.up*1.26f;
                        Vector3 other = shelter + Vector3.up*(crouch ? .41f : 1.60f);
                        if (!Physics.Linecast(eye, other, out var hit, Mask, QueryTriggerInteraction.Ignore) ||
                            !hit.transform.IsChildOf(piece) || Seen(watcher,shelter,crouch)) continue;
                        hidden = true;
                        foreach(int side in new[]{ 1,-1 })
                        {
                            var step = right*side*(span+1.1f);
                            var around = shelter+step;
                            var flank = watcher+step;
                            if (!Free(around) || !Free(flank) || !Seen(flank,around,crouch)) continue;
                            exposed = true;
                            if (FloorRoutes.CanMove(watcher,flank) && FloorRoutes.CanMove(flank,around) &&
                                FloorRoutes.CanMove(around,shelter)) { detour = true; break; }
                        }
                        if(detour) break;
                    }
                    if(detour) break;
                }
                return (hidden,exposed,detour);
            }
            foreach (var piece in pieces)
            {
                var result = Probe(piece);
                checks.Add(string.Join("\t",piece.name,result.hidden,result.exposed,result.detour));
            }
            var shelter = new List<string> { "floor\troom\tusable-furniture" };
            var furniture = GameObject.Find("First night runtime").GetComponentsInChildren<Transform>()
                .Where(t => t.name == "Fit model to furniture placement").Select(t => t.parent).ToArray();
            for(int floor=0; floor<NightAtlas.Street; ++floor)
            foreach(var area in game.Atlas.Areas(floor))
            {
                if(area.Stair || (area.Route && area.Name is not ("迎宾中庭" or "员工起居区"))) continue;
                var usable = new List<string>();
                foreach(var piece in furniture)
                {
                    var pos = piece.position;
                    if (Mathf.Abs(pos.y-(floor-1)*3.6f) > .22f ||
                        !area.Bounds.Contains(new Vector2(pos.x,pos.z))) continue;
                    var collision = piece.GetComponentsInChildren<Collider>().FirstOrDefault(c => c.enabled && !c.isTrigger);
                    if(!collision || collision.bounds.size.y < .72f ||
                        Mathf.Max(collision.bounds.size.x,collision.bounds.size.z) < .85f) continue;
                    if(Probe(piece).detour) usable.Add(piece.name);
                }
                shelter.Add(string.Join("\t",floor,area.Name,string.Join("|",usable)));
            }
            File.WriteAllLines(Path.Combine(folder,"room-cover.tsv"),shelter);
            File.WriteAllLines(Path.Combine(folder,"cover-checks.tsv"),checks);
            var targets = Field<List<NightTarget>>(game,"targets");
            foreach (string place in new[] { "耗材间", "档案室" })
            {
                var target = targets.Single(t => t.Label == "取电池 · "+place);
                var probe = new Vector3(target.Position.x+.94f,target.Position.y-.89f,target.Position.z);
                if (!Free(probe) || !target.Available()) throw new Exception("无法接近电池："+place);
                game.Player.ResetPosition(probe,0);
                game.Player.Eyes.LookAt(target.Position);
                Physics.SyncTransforms();
                typeof(NightGame).GetMethod("FocusTarget",Private).Invoke(game,new object[]{0f});
                if (game.Focus != target) throw new Exception("无法选中电池："+place);
                int previous = game.State.cells;
                game.Focus.Use();
                if (game.State.cells != previous+1 || target.Available())
                    throw new Exception("电池拾取或去重失败："+place);
            }
            var failed = checks.Skip(1).Where(s => s.Contains("False")).ToArray();
            if(failed.Length > 0) throw new Exception("Cover walkaround: "+string.Join(", ",failed));
        }
    }
}
