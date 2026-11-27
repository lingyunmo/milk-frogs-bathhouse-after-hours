using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AfterHours
{
    internal sealed class AfterHoursRoomSurfaceAudit : MonoBehaviour
    {
        const string Flag = "-afterhours-room-surfaces";
        const string QaCompany = "AfterHours QA";
        const int Width = 800, Height = 450;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly MethodInfo Present = typeof(NightCameraGrade).GetMethod("Present", Private);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), Flag) >= 0)
                new GameObject("Lighting smoke capture").AddComponent<AfterHoursRoomSurfaceAudit>();
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return null;
            var run = Run();
            while (true)
            {
                bool hasNext;
                try { hasNext = run.MoveNext(); }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    Debug.LogError("ROOM SURFACE AUDIT: FAILED");
                    Application.Quit(76);
                    yield break;
                }
                if (!hasNext) break;
                yield return run.Current;
            }
            Debug.Log("ROOM SURFACE AUDIT: PASS");
            Application.Quit(0);
        }

        IEnumerator Run()
        {
            if (Application.companyName != QaCompany)
                throw new InvalidOperationException("Use the isolated AfterHours QA player.");
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Flag);
            if (index + 2 >= args.Length || !Path.IsPathFullyQualified(args[index + 1]) ||
                args[index + 2] is not ("baseline" or "adjusted"))
                throw new ArgumentException("Specify an absolute output directory and baseline or adjusted.");
            string folder = args[index + 1], label = args[index + 2];
            Directory.CreateDirectory(folder);

            var game = FindFirstObjectByType<NightGame>();
            if (!game) throw new InvalidOperationException("The After Hours scene is missing.");
            game.enabled = false;
            Time.timeScale = 1;
            var player = game.Player;
            var world = (NightWorld)typeof(NightGame).GetField("world", Private).GetValue(game);
            var camera = player.Eyes.GetComponent<Camera>();
            var grade = camera.GetComponent<NightCameraGrade>();
            if (!grade || !grade.Ready || Present == null)
                throw new InvalidOperationException("The actual graded camera is unavailable.");
            game.State.torch = true;
            game.State.charge = NightState.FullTorchCharge;
            game.SetScreen(NightScreen.Play);
            player.SetTorchVisible(false);
            player.SetTorch(false);
            var creature = GameObject.Find("奶娃");
            if (creature) creature.SetActive(false);
            yield return new WaitForSecondsRealtime(.50f);

            player.SetTorch(true);
            player.AdvanceTorch(game, .5f);
            Physics.SyncTransforms();
            string poses = Path.Combine(folder, "poses.tsv");
            if (label == "baseline")
            {
                using var file = File.CreateText(poses);
                for (int floor = 0; floor <= NightAtlas.Street; floor++)
                foreach (var area in game.Atlas.Areas(floor))
                {
                    float height = floor == NightAtlas.Street ? 0 : (floor-1)*3.6f;
                    Locate(file, floor, area, height);
                }
                foreach (var door in world.Doors)
                {
                    var side = Vector3.Cross(Vector3.up,door.Inward);
                    var feet = door.Position - door.Inward*1.15f + side*2.1f + Vector3.up*.045f;
                    int floor = Mathf.RoundToInt(door.Position.y/3.6f)+1;
                    Pose(file,$"{floor}-{door.Name}-frame",feet,door.Position+side*.61f+Vector3.up*1.51f);
                }
                Pose(file,"4-后场工具间-desk",new(4.65f,.49f,36.1f),new(5.67f,1.1f,37.6f));
                Pose(file,"4-后巷洗衣店-frame",new(30.95f,.06f,32.36f),new(29.98f,1.55f,31));
                Pose(file,"4-停车棚泵房-frame",new(50.3f,.06f,12.27f),new(48.98f,1.46f,11));
                Pose(file,"4-街尾维修站-frame",new(152.05f,.06f,2.24f),new(153.02f,1.54f,1));
                Pose(file,"2-副楼机修间-frame",new(25.2f,3.64f,11.43f),new(26.14f,5.2f,10));
                Pose(file,"3-副楼留档室-frame",new(29.2f,7.24f,12.63f),new(27.9f,8.75f,14));
                Pose(file,"2-卸货平台-ledge",new(-11.55f,1.90f,25.7f),new(-10.4f,3.05f,24));
                Pose(file,"2-西侧雨棚-ledge",new(-21.24f,1.90f,5f),new(-19.95f,3.18f,3.6f));
                Pose(file,"3-检修平台-ledge",new(10.14f,5.51f,25.4f),new(11.2f,6.86f,24));
            }
            var cameras = File.ReadAllLines(poses);
            if (cameras.Length == 0) throw new InvalidOperationException("No baseline poses");
            foreach (string line in cameras)
            {
                var fields = line.Split('\t');
                Vector3 Read(int k) => new(float.Parse(fields[k],CultureInfo.InvariantCulture),
                    float.Parse(fields[k+1],CultureInfo.InvariantCulture),float.Parse(fields[k+2],CultureInfo.InvariantCulture));
                player.ResetPosition(Read(1),0);
                player.Eyes.LookAt(Read(4));
                Physics.SyncTransforms();
                yield return new WaitForEndOfFrame();
                Capture(camera,grade,Path.Combine(folder,label + "-" + fields[0] + ".png"));
            }
        }

        static void Pose(StreamWriter file, string id, Vector3 feet, Vector3 target) =>
            file.WriteLine(FormattableString.Invariant($"{id}\t{feet.x:R}\t{feet.y:R}\t{feet.z:R}\t{target.x:R}\t{target.y:R}\t{target.z:R}\t{ id }\tauthored\t-"));

        static void Locate(StreamWriter log, int floor, NightAtlas.Area area, float height)
        {
            const int mask = ~(1 << NightPlayer.PlayerLayer | 1 << NightPlayer.ViewModelLayer | 1 << 10);
            var bounds = area.Bounds;
            var best = new float[]{-1000,-1000};
            var lines = new string[2];
            foreach (float u in new[]{.50f,.22f,.78f})
            foreach (float v in new[]{.50f,.24f,.77f})
            {
                var sample = new Vector3(Mathf.Lerp(bounds.xMin,bounds.xMax,u),height+1.22f,
                    Mathf.Lerp(bounds.yMin,bounds.yMax,v));
                if (!Physics.Raycast(sample,Vector3.down,out var ground,2.7f,mask) ||
                    ground.normal.y < .67f || Mathf.Abs(ground.point.y-height) > .86f) continue;
                var feet = ground.point + Vector3.up*.04f;
                if (Physics.CheckCapsule(feet+Vector3.up*.32f,feet+Vector3.up*1.39f,.26f,mask)) continue;
                var eye = feet + Vector3.up*1.60f;
                for (int i=0; i<24; i++)
                foreach (float pitch in new[]{-.04f,-.28f})
                {
                    var direction = Quaternion.Euler(0,i*15+8,0)*new Vector3(0,pitch,1).normalized;
                    if (!Physics.Raycast(eye,direction,out var hit,8.5f,mask) || hit.distance<.65f ||
                        Mathf.Abs(hit.normal.y)>.65f || !bounds.Contains(new Vector2(hit.point.x,hit.point.z)-new Vector2(direction.x,direction.z)*.3f)) continue;
                    var collider = hit.collider;
                    string name = collider.name;
                    var rend = collider.GetComponent<Renderer>() ?? collider.GetComponentInChildren<Renderer>();
                    if (!rend || !rend.sharedMaterial) continue;
                    bool shell = name.Contains("墙") || name.Contains("门") || name.Contains("壁") || name.Contains("梁") || name.Contains("柱");
                    int k = shell ? 0 : 1;
                    float grazing = Vector3.Dot(hit.normal,-direction);
                    float score = -Mathf.Abs(.47f-grazing)*6 - Mathf.Abs(hit.distance-(shell?2.9f:2.25f))*.30f;
                    if (score <= best[k]) continue;
                    best[k]=score;
                    string id = floor+"-"+area.Name+"-"+(shell?"shell":"detail");
                    string pos=FormattableString.Invariant($"{feet.x:R}\t{feet.y:R}\t{feet.z:R}\t{hit.point.x:R}\t{hit.point.y:R}\t{hit.point.z:R}");
                    lines[k]=id+"\t"+pos+"\t"+name+"\t"+rend.sharedMaterial.name+"\t"+grazing.ToString("F2",CultureInfo.InvariantCulture);
                }
            }
            foreach (string line in lines) if (line!=null) log.WriteLine(line);
            if (lines[0]==null && lines[1]==null && area.Name is not ("卸货平台" or "西侧雨棚" or "检修平台"))
                throw new InvalidOperationException("No oblique shot for " + area.Key);
        }

        static void Capture(Camera camera, NightCameraGrade grade, string destination)
        {
            if (!camera.targetTexture) throw new InvalidOperationException("The HDR frame is unavailable.");
            var target = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            var savedActive = RenderTexture.active;
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                Present.Invoke(grade, new object[] { target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply(false);
                File.WriteAllBytes(destination, image.EncodeToPNG());
                LogLevels(destination, image.GetPixels32());
            }
            finally
            {
                RenderTexture.active = savedActive;
                RenderTexture.ReleaseTemporary(target);
                Destroy(image);
            }
        }

        static void LogLevels(string destination, Color32[] pixels)
        {
            var histogram = new int[256];
            long total = 0;
            foreach (Color32 pixel in pixels)
            {
                int luminance = (54 * pixel.r + 183 * pixel.g + 19 * pixel.b) >> 8;
                histogram[luminance]++;
                total += luminance;
            }

            int cumulative = 0, percentile = 0;
            while (percentile < 255 && (cumulative += histogram[percentile]) < pixels.Length * .90)
                percentile++;
            string mean = ((float)total / pixels.Length).ToString("0.00", CultureInfo.InvariantCulture);
            Debug.Log($"LIGHTING LEVEL {Path.GetFileNameWithoutExtension(destination)} mean={mean} p90={percentile}");
        }
    }
}
