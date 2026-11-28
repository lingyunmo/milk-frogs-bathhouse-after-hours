using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    internal sealed partial class NightPlayer
    {
        Transform projector, gaugeNeedle;
        Material[] projectorFinishes;
        float recoil, shownHeat;
        int heatBand = -1;

        internal void SetProjectorHeat(float heat, bool venting)
        {
            if (!gaugeNeedle) return;
            shownHeat = Mathf.MoveTowards(shownHeat, Mathf.Clamp01(heat), Time.deltaTime * 4f);
            float chatter = venting ? Mathf.Sin(Time.time * 34f) * 1.8f : 0;
            gaugeNeedle.localRotation = Quaternion.Euler(0, 0, 220 - 260 * shownHeat + chatter);
            int band = venting ? 8 : Mathf.FloorToInt(heat * 7);
            if (band == heatBand) return;
            heatBand = band;
            projectorFinishes[2].SetColor("_EmissionColor", Color.Lerp(new Color(.055f, .041f, .019f), new Color(.94f, .19f, .035f), band / 8f));
        }

        internal void EquipBrineGun(bool equip)
        {
            if (equip && !projector) BuildBrineProjector();
            if (projector) projector.gameObject.SetActive(equip);
            if (heldTorch) heldTorch.gameObject.SetActive(!equip);
            if (equip)
            {
                recoil = 0;
                heatBand = -1;
                SetProjectorHeat(shownHeat, false);
                SetTorchVisible(true);
                PoseGun(0);
            }
        }

        void BuildBrineProjector()
        {
            projector = new GameObject("堤防回流炮").transform;
            projector.SetParent(handsCamera.transform, false);
            var stock = Resources.Load<Texture2D>("Finishes/Service/SteelColor");
            var normal = Resources.Load<Texture2D>("Finishes/Service/SteelNormal");
            Material Finish(string name, Color tint, float metal, float gloss, float relief)
            {
                var m = new Material(Shader.Find("Standard")) { name = name, color = tint, mainTexture = stock };
                m.SetFloat("_Metallic", metal);
                m.SetFloat("_Glossiness", gloss);
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", relief);
                m.EnableKeyword("_NORMALMAP");
                return m;
            }
            var housing = Finish("回流泵 · 炉黑铸铁", new(.64f, .68f, .65f), .22f, .32f, .81f);
            var union = Finish("回流泵 · 旧磷铜", new(.86f, .66f, .41f), .40f, .45f, .53f);
            var pilot = Finish("泄压孔 · 琥珀玻璃", new(.48f, .28f, .13f), .05f, .29f, .1f);
            pilot.EnableKeyword("_EMISSION");
            var iron = Finish("夹具 · 擦亮钢沿", new(.84f, .85f, .78f), .54f, .49f, .44f);
            var rubber = Finish("泄水管 · 老化包胶", new(.19f, .21f, .20f), .015f, .21f, .72f);
            var face = new Material(Shader.Find("Standard")) { name = "87 型热负载表", mainTexture = Resources.Load<Texture2D>("Surfaces/BrineProjectorDial") };
            face.SetFloat("_Glossiness", .12f);
            face.SetTexture("_EmissionMap", face.mainTexture);
            face.SetColor("_EmissionColor", new(.22f, .20f, .16f));
            face.EnableKeyword("_EMISSION");
            var hand = Finish("表针 · 暗朱砂", new(.48f, .12f, .075f), .14f, .30f, .12f);
            projectorFinishes = new[] { housing, union, pilot, iron, rubber, face, hand };

            var bounce = new GameObject("铸件反射补光").AddComponent<Light>();
            bounce.transform.SetParent(projector, false);
            bounce.transform.localPosition = new(-.16f, .12f, -.19f);
            bounce.type = LightType.Point;
            bounce.shadows = LightShadows.None;
            bounce.cullingMask = 1 << ViewModelLayer;
            bounce.renderMode = LightRenderMode.ForcePixel;
            bounce.color = new(.83f, .88f, .89f);
            bounce.range = .88f;
            bounce.intensity = 1.05f;
            var meshes = projector.gameObject.AddComponent<ProjectorMeshOwner>();
            var pieces = new Dictionary<Material, List<CombineInstance>>();
            Mesh StandardMesh(PrimitiveType type)
            {
                var primitive = GameObject.CreatePrimitive(type);
                primitive.SetActive(false);
                var shared = primitive.GetComponent<MeshFilter>().sharedMesh;
                Destroy(primitive);
                return shared;
            }
            var cube = StandardMesh(PrimitiveType.Cube);
            var cylinder = StandardMesh(PrimitiveType.Cylinder);
            var hex = meshes.Keep(Lathe(6, new(-.50f,.90f), new(-.42f,1), new(.42f,1), new(.50f,.90f)));
            var place = Matrix4x4.identity;
            void Add(Material mat, Mesh mesh, Vector3 at, Vector3 size, Quaternion rotation)
            {
                if (!pieces.TryGetValue(mat, out var group)) pieces[mat] = group = new List<CombineInstance>();
                group.Add(new CombineInstance { mesh = mesh, transform = place * Matrix4x4.TRS(at, rotation, size) });
            }
            void Box(Material mat, Vector3 at, Vector3 size, float pitch = 0) => Add(mat, cube, at, size, Quaternion.Euler(pitch, 0, 0));
            void Rod(Material mat, Vector3 a, Vector3 b, float diameter)
            {
                Vector3 d = b - a;
                Add(mat, cylinder, (a + b) * .5f, new(diameter, d.magnitude * .5f, diameter), Quaternion.FromToRotation(Vector3.up, d));
            }
            void Turn(Material mat, Vector3 at, int sides, params Vector2[] rings)
            {
                var mesh = meshes.Keep(Lathe(sides, rings));
                Add(mat, mesh, at, Vector3.one, Quaternion.identity);
            }

            // The deep-drawn case is wider at the clamped diaphragm, then narrows to the hose coupling.
            Turn(housing, Vector3.zero, 36, new(-.063f,.033f), new(-.061f,.042f), new(-.050f,.049f), new(-.031f,.052f),
                new(.046f,.052f), new(.061f,.048f), new(.087f,.046f), new(.10f,.037f));
            Turn(iron, Vector3.zero, 32, new(.043f,.051f), new(.046f,.055f), new(.052f,.055f), new(.055f,.049f));
            Turn(union, Vector3.zero, 8, new(.095f,.037f), new(.10f,.044f), new(.123f,.044f), new(.126f,.037f));
            Turn(housing, Vector3.zero, 32, new(.122f,.032f), new(.132f,.036f), new(.170f,.031f), new(.177f,.027f));
            Turn(iron, Vector3.zero, 28, new(.168f,.031f), new(.171f,.034f), new(.180f,.034f), new(.184f,.029f));
            Turn(rubber, Vector3.zero, 28, new(.181f,.024f), new(.185f,.023f));

            // Ribbed palm grip and a bent protective bow leave real negative space around the trigger.
            Box(housing, new(0,-.045f,-.014f), new(.055f,.044f,.089f));
            Box(rubber, new(0,-.088f,-.036f), new(.042f,.101f,.047f), -14);
            for (int i = 0; i < 6; i++)
                Box(rubber, new(0,-.058f-i*.012f,-.026f-i*.003f), new(.047f,.005f,.052f), -14);
            Rod(iron, new(0,-.047f,.050f), new(0,-.102f,.044f), .008f);
            Rod(iron, new(0,-.102f,.044f), new(0,-.118f,-.020f), .008f);
            Rod(union, new(0,-.050f,.015f), new(0,-.074f,.006f), .008f);
            Box(iron, new(0, -.002f, -.064f), new(.052f,.036f,.005f));
            Box(housing, new(0, -.002f, -.067f), new(.044f,.030f,.003f));
            for (int i = 0; i < 5; i++)
                Box(iron, new(0,-.011f+i*.004f,-.069f), new(.029f,.0012f,.0014f));
            // A black bypass hose loops round the near side. The ferrules catch the overhead lamp.
            var hose = new[] { new Vector3(-.037f,.009f,-.040f), new Vector3(-.062f,-.002f,-.035f),
                new Vector3(-.070f,-.012f,-.017f), new Vector3(-.069f,-.017f,.062f),
                new Vector3(-.060f,-.010f,.090f), new Vector3(-.034f,.003f,.102f) };
            for (int i = 1; i < hose.Length; i++) Rod(i == 1 || i == hose.Length - 1 ? union : rubber, hose[i-1], hose[i], .013f);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2 / 7;
                Add(iron, hex, new(Mathf.Cos(a)*.043f, Mathf.Sin(a)*.043f, -.049f), new(.0065f,.0065f,.009f), Quaternion.identity);
            }

            // The recessed gauge faces the operator. Only its small pilot emits light.
            Rod(union, new(-.012f,.030f,-.018f), new(-.020f,.066f,-.008f), .024f);
            var gauge = new GameObject("斜面机械热负载表").transform;
            gauge.SetParent(projector, false);
            gauge.localPosition = new(-.020f,.063f,-.009f);
            gauge.localRotation = Quaternion.Euler(17,16,0);
            place = Matrix4x4.TRS(gauge.localPosition, gauge.localRotation, Vector3.one);
            Turn(iron, Vector3.zero, 42, new(-.0095f,.041f), new(-.008f,.043f), new(.009f,.043f), new(.014f,.036f));
            var dial = meshes.Keep(DialFace());
            Add(face, dial, new(0,0,-.0100f), new(.0388f,.0388f,1), Quaternion.identity);
            Add(union, hex, new(0,0,-.0118f), new(.0048f,.0048f,.005f), Quaternion.identity);
            place = Matrix4x4.identity;
            Box(iron, new(.037f,.031f,-.028f), new(.013f,.012f,.027f));
            Box(pilot, new(.037f,.038f,-.028f), new(.005f,.002f,.014f));

            foreach (var group in pieces)
            {
                var batch = meshes.Keep(new Mesh { name = group.Key.name });
                batch.CombineMeshes(group.Value.ToArray(), true, true);
                var node = new GameObject(group.Key.name);
                node.layer = ViewModelLayer;
                node.transform.SetParent(projector, false);
                node.AddComponent<MeshFilter>().sharedMesh = batch;
                var renderer = node.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = group.Key;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            gaugeNeedle = new GameObject("机械温度指针").transform;
            gaugeNeedle.SetParent(gauge, false);
            gaugeNeedle.localPosition = new(0,0,-.0109f);
            gaugeNeedle.gameObject.layer = ViewModelLayer;
            var needle = meshes.Keep(new Mesh { name = "冲压窄尖指针" });
            needle.vertices = new[] { new Vector3(-.008f,-.0015f,0), new Vector3(-.008f,.0015f,0),
                new Vector3(.014f,.00125f,0), new Vector3(.031f,0,0), new Vector3(.014f,-.00125f,0) };
            needle.triangles = new[] {0,1,2, 0,2,4, 4,2,3};
            needle.RecalculateNormals(); needle.RecalculateBounds();
            gaugeNeedle.gameObject.AddComponent<MeshFilter>().sharedMesh = needle;
            var needleRenderer = gaugeNeedle.gameObject.AddComponent<MeshRenderer>();
            needleRenderer.sharedMaterial = hand;
            needleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            needleRenderer.receiveShadows = false;
        }

        // x is distance along the pump axis, y the turned radius.
        static Mesh Lathe(int sides, params Vector2[] profile)
        {
            int stride = sides + 1;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            for (int j = 0; j < profile.Length; j++)
            for (int i = 0; i <= sides; i++)
            {
                float u = i / (float)sides, a = u * Mathf.PI * 2;
                v.Add(new(Mathf.Cos(a) * profile[j].y, Mathf.Sin(a) * profile[j].y, profile[j].x));
                uv.Add(new(u, Mathf.InverseLerp(profile[0].x, profile[profile.Length-1].x, profile[j].x)));
                if (j == 0 || i == 0) continue;
                int p = j * stride + i;
                tris.AddRange(new[] {p-stride-1,p-stride,p, p-stride-1,p,p-1});
            }
            for (int end = 0; end < 2; end++)
            {
                int j = end == 0 ? 0 : profile.Length-1;
                int center = v.Count;
                v.Add(new(0,0,profile[j].x)); uv.Add(new(.5f,.5f));
                for (int i = 0; i < sides; i++)
                {
                    int a = j*stride+i, b = a+1;
                    tris.AddRange(end == 0 ? new[] {center,b,a} : new[] {center,a,b});
                }
            }
            var mesh = new Mesh();
            mesh.SetVertices(v); mesh.SetUVs(0,uv); mesh.SetTriangles(tris,0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh DialFace()
        {
            const int segments = 48;
            var v = new Vector3[segments+2]; var uv = new Vector2[v.Length]; var indices = new int[segments*3];
            uv[0] = new(.5f,.5f);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                v[i+1] = new(Mathf.Cos(a),Mathf.Sin(a),0);
                uv[i+1] = new(v[i+1].x*.5f+.5f,v[i+1].y*.5f+.5f);
                if (i == 0) continue;
                indices[i*3-3] = 0; indices[i*3-2] = i+1; indices[i*3-1] = i;
            }
            var mesh = new Mesh { vertices = v, uv = uv, triangles = indices };
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }

        sealed class ProjectorMeshOwner : MonoBehaviour
        {
            readonly List<Mesh> owned = new();
            internal Mesh Keep(Mesh mesh) { owned.Add(mesh); return mesh; }
            void OnDestroy() { foreach (var mesh in owned) if (mesh) Destroy(mesh); }
        }

        void PoseGun(float sway)
        {
            if (!projector || !projector.gameObject.activeSelf) return;
            recoil = Mathf.MoveTowards(recoil, 0, Time.deltaTime * 7);
            projector.localPosition = new Vector3(.182f, -.149f + .003f * sway, .431f - .021f * recoil);
            projector.localRotation = Quaternion.Euler(-3 - 5 * recoil, -15, 2 + .55f * sway);
        }
        internal Vector3 BrineMuzzle()
        {
            recoil = 1;
            return projector.TransformPoint(0,0,.185f);
        }
    }
}
