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
            Surface("堤岸隔离闸","来水暗孔",3);
            Surface("堤岸隔离闸","泄洪廊顶");
            foreach (string shelter in new[]{"后场检修棚","面馆侧巷雨棚","街对面值守点","街尾商廊"})
                Surface("石桥路沿街建筑",shelter+"顶棚");
            var road=district.Find("石桥路沿街建筑");
            Towers(district,"路对面老式住宅",4);
            foreach(string block in new[]{"后巷外住宅楼","菜市后旧住宅楼","街尾远处住宅楼"}) Towers(road,block,1);
            Towers(district.Find("石桥东堤"),"对岸旧宿舍楼",4);

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
            var airborne=fragments.sharedMesh.vertices;
            game.ContinueNight();
            Check(plates.All(surface=>!surface.enabled) && skyline.All(t=>t.Model.localPosition.y<t.Origin.y-1),
                "reloading the deployment did not restore every affected district");
            var fallen=fragments.sharedMesh.vertices;
            Check(fragments.GetComponent<MeshRenderer>().enabled &&
                airborne.Where((p,i)=>(fallen[i]-p).sqrMagnitude>.12f).Any(),
                "flight and settled intake meshes are identical");
            var cloudLight=blast.transform.Find("热云斜照").GetComponent<Light>();
            Check(cloudLight.isActiveAndEnabled && cloudLight.intensity>.55f && RenderSettings.ambientSkyColor.r>sky.r,
                "reloaded ruins have no incident light");
            foreach(var c in colliders)
                Check(c.Shape.enabled==c.Enabled && c.Shape.bounds==c.Bounds,"collision changed on "+c.Shape.name);
            game.NewNight();
            Check(!blast.gameObject.activeSelf && plates.All(surface=>surface.enabled) &&
                skyline.All(t=>t.Model.localPosition==t.Origin),"normal route retained damage");
            Check(!cloudLight.isActiveAndEnabled && RenderSettings.ambientSkyColor==sky && RenderSettings.fogColor==fog &&
                Mathf.Approximately(RenderSettings.fogDensity,fogDensity), "incident light/fog leaked into normal play");
            Debug.Log($"BLAST PASS {plates.Count} original building skins, {skyline.Count} skyline blocks, fracture motion, fixed colliders, resume and restart.");
        }
    }
}
