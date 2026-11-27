using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    // The air of the building: shafts under practical lamps, steam over the baths, dust that only the
    // hand torch reveals, and box-projected reflections so wet tile and enamel pick up the real lamps.
    internal sealed class NightAtmosphere : MonoBehaviour
    {
        internal const int EffectsLayer = 12;
        const int ProbeResolution = 128;
        const int CaptureMask = ~((1 << NightPlayer.PlayerLayer) | (1 << NightPlayer.ViewModelLayer) |
                                  (1 << 10) | (1 << EffectsLayer) | (1 << 5));

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int DensityId = Shader.PropertyToID("_Density");
        static readonly int TorchPositionId = Shader.PropertyToID("_TorchPosition");
        static readonly int TorchDirectionId = Shader.PropertyToID("_TorchDirection");
        static readonly int TorchColorId = Shader.PropertyToID("_TorchColor");

        sealed class Shaft
        {
            internal Light Lamp;
            internal Renderer Cone;
            internal float Density, Shown = -1;
            internal readonly MaterialPropertyBlock Block = new();
        }

        readonly List<Shaft> shafts = new();
        readonly List<(ReflectionProbe Probe, RenderTexture Cube)> probes = new();
        Material shaftMaterial, moteMaterial, steamMaterial;
        Mesh cone;
        Texture2D puff, lampDepth;
        Camera capture;
        Light torch;
        int nextProbe = -1, nextFace;
        bool fastCapture;

        internal static NightAtmosphere Create(Transform root)
        {
            var atmosphere = new GameObject("Atmosphere").AddComponent<NightAtmosphere>();
            atmosphere.transform.SetParent(root, false);
            var shaft = Resources.Load<Shader>("LightShaft");
            var air = Resources.Load<Shader>("AirParticles");
            if (shaft && shaft.isSupported) atmosphere.shaftMaterial = new Material(shaft) { name = "Lamp shaft" };
            if (air && air.isSupported)
            {
                atmosphere.moteMaterial = new Material(air) { name = "Dust motes" };
                atmosphere.moteMaterial.SetColor("_Ambient", new Color(.002f, .0024f, .0024f));
                atmosphere.moteMaterial.SetFloat("_TorchResponse", .13f);
                atmosphere.steamMaterial = new Material(air) { name = "Bath steam" };
                atmosphere.steamMaterial.SetFloat("_Steam", 1);
                atmosphere.steamMaterial.SetColor("_Ambient", new Color(.055f, .062f, .062f));
                atmosphere.steamMaterial.SetFloat("_SourceBlend", (float)BlendMode.One);
                atmosphere.steamMaterial.SetFloat("_TargetBlend", (float)BlendMode.OneMinusSrcAlpha);
                atmosphere.steamMaterial.mainTexture = atmosphere.Puff();
            }
            return atmosphere;
        }

        // ------------------------------------------------------------------ Light shafts

        internal void AddShaft(Light lamp, float density, float reach = .82f)
        {
            if (!shaftMaterial || !lamp || lamp.type != LightType.Spot) return;
            cone ??= Cone();
            var go = new GameObject("Light shaft") { layer = EffectsLayer };
            go.transform.SetParent(lamp.transform, false);
            float length = lamp.range * reach;
            float radius = Mathf.Tan(lamp.spotAngle * .5f * Mathf.Deg2Rad * .66f) * length;
            go.transform.localScale = new Vector3(radius, radius, length);
            go.AddComponent<MeshFilter>().sharedMesh = cone;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = shaftMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            shafts.Add(new Shaft { Lamp = lamp, Cone = renderer, Density = density });
        }

        // Static angular depth for dusty air. The normal dynamic light still shadows moving actors.
        internal void CaptureLampOcclusion()
        {
            if (shafts.Count == 0) return;
            const int tile = 16;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(shafts.Count));
            int rows = Mathf.CeilToInt(shafts.Count / (float)columns);
            int width = columns * tile, height = rows * tile;
            var pixels = new Color32[width * height];
            for (int i = 0; i < shafts.Count; i++)
            {
                var shaft = shafts[i];
                var beam = shaft.Cone.transform;
                int left = (i % columns) * tile, bottom = (i / columns) * tile;
                for (int y = 0; y < tile; y++)
                for (int x = 0; x < tile; x++)
                {
                    var local = new Vector3(2f*x/(tile-1)-1, 2f*y/(tile-1)-1, 1);
                    var ray = beam.TransformVector(local);
                    float length = ray.magnitude;
                    var direction = ray / length;
                    byte depth = 255;
                    if (Physics.Raycast(beam.position + direction * .085f, direction, out var hit,
                            length - .085f, 1, QueryTriggerInteraction.Ignore))
                        depth = (byte)Mathf.Clamp(Mathf.RoundToInt(255*(hit.distance+.085f)/length),0,255);
                    pixels[(bottom+y)*width+left+x] = new Color32(depth,depth,depth,255);
                }
                shaft.Block.SetVector("_ShadowTile", new Vector4((left+.5f)/width,(bottom+.5f)/height,
                    (tile-1f)/width,(tile-1f)/height));
                shaft.Cone.SetPropertyBlock(shaft.Block);
            }
            lampDepth = new Texture2D(width,height,TextureFormat.RGBA32,false,true)
            { name = "Practical light occlusion", wrapMode = TextureWrapMode.Clamp };
            lampDepth.SetPixels32(pixels);
            lampDepth.Apply(false,true);
            shaftMaterial.SetTexture("_BathLightDepth",lampDepth);
        }

        static Mesh Cone()
        {
            const int sides = 24;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int ring = 0; ring <= 1; ++ring)
            for (int side = 0; side <= sides; ++side)
            {
                float angle = side * Mathf.PI * 2 / sides;
                var around = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                float radius = ring == 0 ? .03f : 1;
                vertices.Add(around * radius + Vector3.forward * ring);
                normals.Add(around);
                uvs.Add(new Vector2((float)side / sides, ring));
            }
            for (int side = 0; side < sides; ++side)
            {
                int a = side, b = side + 1, c = side + sides + 1, d = side + sides + 2;
                triangles.AddRange(new[] { a, b, c, b, d, c });
                if (side > 0 && side < sides - 1)
                    triangles.AddRange(new[] { sides + 1, c, d });
            }
            var mesh = new Mesh { name = "Light shaft cone" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(Vector3.forward * .5f, new Vector3(2, 2, 1));
            mesh.UploadMeshData(true);
            return mesh;
        }

        // ------------------------------------------------------------------ Air

        internal void FollowTorch(Transform eyes, Light beam)
        {
            torch = beam;
            if (!moteMaterial || !eyes) return;
            var go = new GameObject("Dust motes") { layer = EffectsLayer };
            go.transform.SetParent(eyes, false);
            go.transform.localPosition = Vector3.forward * 3.2f;
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6, 10);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.005f, .03f);
            main.startSize = new ParticleSystem.MinMaxCurve(.004f, .011f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, 1, 1, .45f), new Color(1, 1, 1, 1));
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            var emission = system.emission;
            emission.rateOverTime = 46;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(6.5f, 3.4f, 6.5f);
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = .045f;
            noise.frequency = .35f;
            noise.scrollSpeed = .08f;
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(Fade());
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = moteMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.minParticleSize = 0;
            system.Play();
        }

        internal void AddSteam(Vector3 center, Vector3 size, Color tint, float rate = 2.4f)
        {
            if (!steamMaterial) return;
            var go = new GameObject("Bath steam") { layer = EffectsLayer };
            go.transform.SetParent(transform, false);
            go.transform.position = center;
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(10, 16);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.02f, .07f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.1f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = tint;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission;
            emission.rateOverTime = rate;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var rise = system.velocityOverLifetime;
            rise.enabled = true;
            rise.space = ParticleSystemSimulationSpace.World;
            rise.x = new ParticleSystem.MinMaxCurve(-.03f, .03f);
            rise.y = new ParticleSystem.MinMaxCurve(.05f, .12f);
            rise.z = new ParticleSystem.MinMaxCurve(-.03f, .03f);
            var fade = system.colorOverLifetime;
            fade.enabled = true;
            fade.color = new ParticleSystem.MinMaxGradient(Fade());
            var grow = system.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .55f, 1, 1.35f));
            var spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-.12f, .12f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = steamMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 3;
            system.Play();
        }

        static Gradient Fade()
        {
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .25f), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
            return gradient;
        }

        Texture2D Puff()
        {
            if (puff) return puff;
            const int size = 128;
            var random = new System.Random(2718);
            var bumps = new Vector4[9];
            for (int i = 0; i < bumps.Length; ++i)
                bumps[i] = new Vector4((float)random.NextDouble() * .9f - .45f, (float)random.NextDouble() * .9f - .45f,
                    .22f + (float)random.NextDouble() * .25f, .4f + (float)random.NextDouble() * .6f);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; ++y)
            for (int x = 0; x < size; ++x)
            {
                float u = (x + .5f) / size * 2 - 1, v = (y + .5f) / size * 2 - 1;
                float density = 0;
                foreach (var bump in bumps)
                {
                    float dx = u - bump.x, dy = v - bump.y;
                    density += bump.w * Mathf.Exp(-(dx * dx + dy * dy) / (bump.z * bump.z));
                }
                float edge = 1 - Mathf.SmoothStep(.55f, 1, Mathf.Sqrt(u * u + v * v));
                byte a = (byte)(Mathf.Clamp01(density * .42f) * edge * 255);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
            puff = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Steam puff",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            puff.SetPixels32(pixels);
            puff.Apply(true, true);
            return puff;
        }

        // ------------------------------------------------------------------ Reflections

        internal void AddProbe(Vector3 center, Vector3 size)
        {
            var go = new GameObject("Room reflection");
            go.transform.SetParent(transform, false);
            go.transform.position = center;
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.boxProjection = true;
            probe.size = size;
            probe.center = Vector3.zero;
            probe.resolution = ProbeResolution;
            probe.hdr = true;
            probe.blendDistance = .45f;
            probe.intensity = 1;
            probe.cullingMask = CaptureMask;
            var cube = new RenderTexture(ProbeResolution, ProbeResolution, 16, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = "Room reflection cube",
                dimension = TextureDimension.Cube,
                useMipMap = true,
                autoGenerateMips = true,
                hideFlags = HideFlags.HideAndDontSave
            };
            cube.Create();
            probe.realtimeTexture = cube;
            probes.Add((probe, cube));
        }

        // Behind the title screen every room is shot quickly, a handful of probes a frame. After the lights
        // change during play, three faces a frame keeps the refresh to a couple of milliseconds.
        internal void CaptureReflections(bool behindTitle)
        {
            if (probes.Count == 0) return;
            fastCapture = behindTitle;
            nextProbe = 0;
            nextFace = 0;
        }

        void Capture(int first, int count, int faces)
        {
            if (!capture)
            {
                capture = new GameObject("Reflection capture").AddComponent<Camera>();
                capture.transform.SetParent(transform, false);
                capture.enabled = false;
                capture.cullingMask = CaptureMask;
                capture.clearFlags = CameraClearFlags.Skybox;
                capture.backgroundColor = RenderSettings.fogColor;
                capture.nearClipPlane = .05f;
                capture.farClipPlane = 48;
                capture.allowHDR = true;
                capture.allowMSAA = false;
                capture.renderingPath = RenderingPath.Forward;
                capture.useOcclusionCulling = false;
            }
            // The torch and the creature are moments, not part of the room. Capture runs synchronously,
            // so hiding them here never shows on screen.
            bool torchWasOn = torch && torch.enabled;
            if (torchWasOn) torch.enabled = false;
            var hidden = new List<Renderer>();
            var creature = GameObject.Find("奶娃");
            if (creature)
                foreach (var renderer in creature.GetComponentsInChildren<Renderer>())
                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                        hidden.Add(renderer);
                    }
            try
            {
                for (int i = first; i < first + count && i < probes.Count; ++i)
                {
                    var (probe, cube) = probes[i];
                    if (!probe) continue;
                    capture.transform.position = probe.transform.position;
                    capture.RenderToCubemap(cube, faces);
                }
            }
            finally
            {
                if (torchWasOn) torch.enabled = true;
                foreach (var renderer in hidden) if (renderer) renderer.enabled = true;
            }
        }

        void LateUpdate()
        {
            if (torch)
            {
                var beam = torch.transform;
                bool lit = torch.enabled && torch.isActiveAndEnabled;
                Shader.SetGlobalVector(TorchPositionId, new Vector4(beam.position.x, beam.position.y, beam.position.z, torch.range));
                Shader.SetGlobalVector(TorchDirectionId, new Vector4(beam.forward.x, beam.forward.y, beam.forward.z,
                    Mathf.Cos(torch.spotAngle * .5f * Mathf.Deg2Rad)));
                Shader.SetGlobalVector(TorchColorId, lit ? (Vector4)(torch.color.linear * torch.intensity) : Vector4.zero);
            }

            foreach (var shaft in shafts)
            {
                if (!shaft.Lamp) continue;
                float level = shaft.Lamp.isActiveAndEnabled ? shaft.Lamp.intensity : 0;
                if (Mathf.Approximately(level, shaft.Shown)) continue;
                shaft.Shown = level;
                shaft.Cone.enabled = level > .001f;
                shaft.Block.SetColor(ColorId, shaft.Lamp.color.linear * level);
                shaft.Block.SetFloat(DensityId, shaft.Density);
                shaft.Cone.SetPropertyBlock(shaft.Block);
            }

            if (nextProbe < 0) return;
            if (fastCapture)
            {
                Capture(nextProbe, 6, 63);
                nextProbe += 6;
                if (nextProbe >= probes.Count) nextProbe = -1;
                return;
            }
            int faces = (7 << nextFace) & 63;
            Capture(nextProbe, 1, faces);
            nextFace += 3;
            if (nextFace >= 6)
            {
                nextFace = 0;
                if (++nextProbe >= probes.Count) nextProbe = -1;
            }
        }

        void OnDestroy()
        {
            foreach (var (_, cube) in probes) if (cube) cube.Release();
            if (puff) Destroy(puff);
            if (lampDepth) Destroy(lampDepth);
        }
    }
}
