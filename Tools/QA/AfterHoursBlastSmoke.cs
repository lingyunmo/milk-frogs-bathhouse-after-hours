using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace AfterHours
{
    internal static class AfterHoursBlastSmoke
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Check(bool condition, string detail)
        {
            if (!condition) throw new Exception("Brine blast: " + detail);
        }

        internal static void Verify(NightGame game)
        {
            game.NewNight();
            var district = GameObject.Find("First night runtime").transform;
            Color fog = RenderSettings.fogColor, sky = RenderSettings.ambientSkyColor;
            float fogDensity = RenderSettings.fogDensity;
            var plates = new List<MeshRenderer>();
            var colliders = new List<(Collider Shape, bool Enabled, Bounds Bounds)>();
            var skyline = new List<(Transform Model, Vector3 Origin)>();

            void Surface(string path, string label, int expected = 1)
            {
                var parent = path.Length == 0 ? district : district.Find(path);
                Check(parent, "missing district "+path);
                var found = new List<MeshRenderer>();
                foreach (Transform child in parent)
                    if (child.name == label)
                    {
                        var skin = child.GetComponent<MeshRenderer>();
                        Check(skin && skin.enabled, "intact skin missing at "+path+"/"+label);
                        found.Add(skin);
                        var shape = child.GetComponent<Collider>();
                        Check(shape, "missing original collision shape at "+path+"/"+label);
                        colliders.Add((shape,shape.enabled,shape.bounds));
                    }
                Check(expected == 0 ? found.Count > 0 : found.Count == expected,
                    path+"/"+label+" found "+found.Count+" expected "+expected);
                plates.AddRange(found);
            }

            void Towers(Transform parent, string label, int expected)
            {
                int count = 0;
                foreach (Transform child in parent)
                {
                    if (child.name != label) continue;
                    var model = child.Find("Fit model to furniture placement");
                    Check(model, "missing skyline model "+label);
                    skyline.Add((model,model.localPosition)); count++;
                }
                Check(count == expected, "skyline "+label+" found "+count);
            }

            foreach (var site in NightWorld.StreetRoomAreas) Surface(site.Name,"沿街房屋屋顶");
            foreach (var site in NightWorld.RiverBuildings) Surface(site.Name,"沿街房屋屋顶");
            Surface("","屋顶",0);
            Surface("浴场副楼/3F 副楼","副楼抹灰顶");
            Surface("主楼南翼/员工层南翼","南翼顶板");
            Surface("东堤旧票务所","屋面");
            Surface("冷样保管间","沿街房屋屋顶");
            Surface("防疫监测哨","沿街房屋屋顶");
            Surface("堤岸隔离闸","来水暗孔",3);
            Surface("堤岸隔离闸","泄洪廊顶");
            foreach (string shelter in new[]{"后场检修棚","面馆侧巷雨棚","街对面值守点","街尾商廊"})
                Surface("石桥路沿街建筑",shelter+"顶棚");
            var road=district.Find("石桥路沿街建筑");
            Towers(district,"路对面老式住宅",4);
            foreach(string block in new[]{"后巷外住宅楼","菜市后旧住宅楼","街尾远处住宅楼"}) Towers(road,block,1);
            Towers(district.Find("石桥东堤"),"对岸旧宿舍楼",4);

            var mill=district.Find("旧脱水车间");
            Check(mill,"quayside mill is absent before the incident");
            MeshRenderer At(string label,float height)
            {
                return mill.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name==label)
                    .OrderBy(r=>Mathf.Abs(r.bounds.center.x-245.2f)+Mathf.Abs(r.bounds.center.y-height)).First();
            }
            var pillar=At("混凝土窗间柱",2.43f);
            var crown=At("预制楼板",10.35f);
            var lowDeck=At("预制楼板",3.45f);
            var intactBay=At("排气竖井",6.45f);
            var flankWalls=new List<MeshRenderer>();
            foreach(var site in NightWorld.StreetRoomAreas.Concat(NightWorld.RiverBuildings).Append((Name:"防疫监测哨",Bounds:BrineInquiry.Room)))
                flankWalls.Add(district.Find(site.Name).GetComponentsInChildren<MeshRenderer>().Where(r=>
                    r.name=="实心墙体" && Mathf.Abs(r.bounds.center.x-site.Bounds.xMin)<.15f)
                    .OrderByDescending(r=>r.bounds.size.z).First());
            var flanks=flankWalls.Select(w=>w.GetComponent<BoxCollider>()).ToArray();
            var flankHeights=flanks.Select(w=>w.bounds.size.y).ToArray();
            game.State.brine.stage=BrineStage.Carrying;
            typeof(NightGame).GetMethod("BeginBrineDefence", Private).Invoke(game,null);
            var blast=district.GetComponentInChildren<NightBrineBlast>();
            Check(blast && blast.FracturedSurfaces==plates.Count && blast.SkylineBlocks==skyline.Count,
                $"wrong number of distinct structures, expected {plates.Count} surfaces and {skyline.Count} skyline blocks");
            Check(plates.All(surface=>surface.enabled),"initial intact skin missing before shock arrival");
            foreach (string shader in new[]{"BrineFireCloud","BrineFallout"})
            {
                var materialShader=Resources.Load<Shader>(shader);
                Check(materialShader && materialShader.isSupported,shader+" unsupported by player's graphics API");
            }
            var sample=district.Find("堤岸隔离闸/来水暗孔");
            var fragments=blast.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="断裂的来水暗孔" &&
                Vector3.Distance(f.transform.position,sample.position)<.02f);
            var pose=typeof(NightBrineBlast).GetMethod("PoseArchitecture",Private);
            pose.Invoke(blast,new object[]{.87f});
            Check(!pillar.enabled && crown.enabled && lowDeck.enabled,"the supports and floors give way at the same instant");
            var airborne=fragments.sharedMesh.vertices;
            pose.Invoke(blast,new object[]{2.28f});
            Check(!crown.enabled && lowDeck.enabled,"upper deck does not fall before the ground-floor ceiling");
            var roofChunks=blast.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="断落的预制楼板" &&
                (f.transform.position-crown.bounds.center).sqrMagnitude<.01f);
            float flyingCrown=roofChunks.sharedMesh.vertices.Average(v=>v.y);
            game.ContinueNight();
            Check(plates.All(surface=>!surface.enabled) && skyline.All(t=>t.Model.localPosition.y<t.Origin.y-1),
                "reloading the deployment did not restore every affected district");
            Check(!lowDeck.enabled && intactBay.enabled && flankWalls.All(w=>!w.enabled),
                "the loaded ruins lost a storey, the standing silhouette or a damaged street");
            Check(flanks.Select((w,i)=>w.bounds.size.y<flankHeights[i]*.5f).All(v=>v),
                "invisible full-height street wall remains in a breach");
            Check(roofChunks.sharedMesh.vertices.Average(v=>v.y)<flyingCrown-3f,"upper slab never hit the ground");
            var millDebris=blast.GetComponentsInChildren<MeshFilter>()
                .Where(f=>f.name.StartsWith("断落的") && f.transform.position.z < -36);
            Check(millDebris.Count()>24,"missing compound building collapse");
            foreach(var shell in millDebris)
            {
                Check(!shell.GetComponent<Collider>(), "a falling prop added a gameplay collider");
                foreach(var vertex in shell.sharedMesh.vertices)
                {
                    var p=shell.transform.TransformPoint(vertex);
                    Check(p.z < -35.3f && p.x<280,"falling geometry crossed the handwheel approach or escape gallery");
                }
            }
            var bank=blast.transform.Find("倾覆车间 · 混凝土与断砖堆");
            Check(bank && bank.gameObject.activeSelf && bank.GetComponent<MeshRenderer>().bounds.max.y>2.8f &&
                !bank.GetComponent<Collider>(),"ruins have no visible rubble crest on the protected side of the embankment");
            var fallen=fragments.sharedMesh.vertices;
            Check(fragments.GetComponent<MeshRenderer>().enabled &&
                airborne.Where((p,i)=>(fallen[i]-p).sqrMagnitude>.12f).Any(),
                "flight and settled intake meshes are identical");
            var cloudLight=blast.transform.Find("热云斜照").GetComponent<Light>();
            Check(cloudLight.isActiveAndEnabled && cloudLight.intensity>.30f && cloudLight.intensity<.58f && RenderSettings.ambientSkyColor.r>sky.r,
                "reloaded ruins have no incident light");
            foreach(var c in colliders)
                Check(c.Shape.enabled==c.Enabled && c.Shape.bounds==c.Bounds,"collision changed on "+c.Shape.name);
            game.NewNight();
            Check(!blast.gameObject.activeSelf && plates.All(surface=>surface.enabled) &&
                skyline.All(t=>t.Model.localPosition==t.Origin),"normal route retained damage");
            Check(!bank.gameObject.activeSelf,"new game retained the collapsed masonry bank");
            Check(pillar.enabled && crown.enabled && lowDeck.enabled && flankWalls.All(w=>w.enabled) &&
                flanks.Select((w,i)=>Mathf.Abs(w.bounds.size.y-flankHeights[i])<.001f).All(v=>v),
                "new game retained a broken mill member or a shortened original wall");
            Check(!cloudLight.isActiveAndEnabled && RenderSettings.ambientSkyColor==sky && RenderSettings.fogColor==fog &&
                Mathf.Approximately(RenderSettings.fogDensity,fogDensity), "incident light/fog leaked into normal play");
            Debug.Log($"BLAST PASS {plates.Count} original building skins, {skyline.Count} skyline blocks, support shear, progressive floors, restored flank breaches, clear escape, resume and restart.");
        }
    }
}
