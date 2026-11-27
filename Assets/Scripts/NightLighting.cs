using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        const float TubeRiseSeconds = .25f;
        static readonly Color TubeGlow = new(.43f, .53f, .48f);
        const float TubeBloom = 3.1f;
        readonly List<(Light Lamp, Renderer Diffuser, float Intensity, float Delay, float Glow)> circuits = new();
        readonly MaterialPropertyBlock tubeResponse = new();
        Material litDiffuser, practicalBulb, nightSky;
        Coroutine powerRestoration;
        static Texture2D practicalProfile;
        NightState restoringState;
        NightAtmosphere atmosphere;
        bool? capturedPower;

        // Shorter pools of practical light still mark working equipment when the street goes dark.
        static readonly (float Ambient, float Moon, float Lamps, float Bounce, Color Fog, float Cloud)[] nights =
        {
            (1f,  .20f, 1f,   1f,   new(.098f, .131f, .161f), 1f),
            (.86f,.16f, .90f, .77f, new(.059f, .079f, .105f), .36f),
            (.72f,.12f, .77f, .53f, new(.027f, .043f, .064f), 0f),
            (.50f,.075f,.63f, .32f, new(.018f, .028f, .041f), 0f),
            (.29f,.032f,.46f, .14f, new(.009f, .014f, .021f), 0f)
        };
        readonly List<(Light Lamp, float Energy)> practicals = new();
        Light moon;
        int litNight;
        float lampScale = 1;

        void SetNightLighting(int night)
        {
            night = Mathf.Clamp(night, 1, nights.Length);
            if (litNight == night) return;
            var look = nights[night - 1];
            litNight = night;
            lampScale = look.Lamps;
            moon.intensity = look.Moon;
            RenderSettings.ambientSkyColor = new Color(.175f, .188f, .204f) * look.Ambient;
            RenderSettings.ambientEquatorColor = new Color(.120f, .139f, .150f) * look.Ambient;
            RenderSettings.ambientGroundColor = new Color(.109f, .101f, .089f) * look.Ambient;
            RenderSettings.reflectionIntensity = .07f * look.Ambient;
            RenderSettings.fogColor = look.Fog;
            RenderSettings.fogDensity = night < 3 ? .0135f : .012f;
            game.Player.Eyes.GetComponent<Camera>().backgroundColor = look.Fog;
            if (nightSky)
            {
                nightSky.SetColor("_NightHorizon", look.Fog);
                nightSky.SetFloat("_CloudWeight", look.Cloud);
            }
            foreach (var (lamp, energy) in practicals) lamp.intensity = energy * lampScale;
            Shader.SetGlobalFloat("_NightBounceScale", look.Bounce);
            game.Player.SetNightOptics(night);
            if (capturedPower.HasValue) atmosphere?.CaptureReflections(false);
        }

        void Lighting()
        {
            moon = GameObject.Find("白盒照明").GetComponent<Light>();
            moon.transform.rotation = Quaternion.LookRotation(new Vector3(-.58f, -.39f, .71f), Vector3.up);
            moon.color = new Color(.63f, .73f, .85f);
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = .97f;
            moon.shadowBias = .07f;
            moon.shadowNormalBias = .42f;
            QualitySettings.pixelLightCount = 8;
            QualitySettings.shadowDistance = 42;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientIntensity = 1;
            // Rooms carry their own box-projected reflections. The sky fallback only reaches the street.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            var camera = Camera.main;
            var skyShader = Resources.Load<Shader>("NightSky");
            var clouds = Resources.Load<Texture2D>("Surfaces/OvercastSky");
            if (skyShader && clouds)
            {
                nightSky = new Material(skyShader) { mainTexture = clouds, name = "Stonebridge after midnight" };
                RenderSettings.skybox = nightSky;
            }
            camera.clearFlags = RenderSettings.skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            water = camera.gameObject.AddComponent<NightWetReflections>();
            camera.backgroundColor = RenderSettings.fogColor;
            SetNightLighting(1);
            camera.allowHDR = true;
            camera.gameObject.AddComponent<NightCameraGrade>();
        }

        Light Fixture(Vector3 position, Vector3 direction, Color colour, float energy, float range, float angle,
            float haze = .011f)
        {
            var lamp = new GameObject("已安装灯具光源").AddComponent<Light>();
            lamp.transform.SetParent(root);
            lamp.transform.position = position;
            lamp.transform.rotation = Quaternion.LookRotation(direction, Vector3.forward);
            lamp.type = LightType.Spot;
            lamp.range = range;
            lamp.color = colour;
            lamp.spotAngle = angle;
            lamp.innerSpotAngle = angle * .52f;
            lamp.intensity = energy;
            lamp.cookie = PracticalProfile();
            lamp.shadows = LightShadows.Soft;
            lamp.shadowStrength = .96f;
            lamp.shadowNearPlane = .06f;
            lamp.shadowBias = .08f;
            lamp.shadowNormalBias = .25f;
            atmosphere?.AddShaft(lamp, haze);
            practicals.Add((lamp, energy));
            return lamp;
        }

        void LightSconce(GameObject sconce, float glowScale = 1)
        {
            foreach (var surface in sconce.GetComponentsInChildren<Renderer>())
            {
                var materials = surface.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; ++i)
                {
                    var texture = materials[i]?.mainTexture;
                    if (!texture || texture.name != "industrial_wall_sconce_bulb_diff_2k") continue;
                    if (!practicalBulb)
                    {
                        practicalBulb = new Material(materials[i]) { name = "Powered amber sconce diffuser" };
                        practicalBulb.EnableKeyword("_EMISSION");
                        practicalBulb.SetColor("_EmissionColor", new Color(.69f, .32f, .095f) * 5.2f);
                    }
                    materials[i] = practicalBulb;
                    if (glowScale < 1)
                    {
                        var dimmed = new MaterialPropertyBlock();
                        dimmed.SetColor("_EmissionColor", practicalBulb.GetColor("_EmissionColor") * glowScale);
                        surface.SetPropertyBlock(dimmed, i);
                    }
                    changed = true;
                }
                if (changed) surface.sharedMaterials = materials;
            }
        }

        static Texture2D PracticalProfile()
        {
            if (practicalProfile) return practicalProfile;

            const int size = 96;
            practicalProfile = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Wide diffuser lamp profile",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; ++y)
            for (int x = 0; x < size; ++x)
            {
                var p = new Vector2((x + .5f) / size * 2 - 1, (y + .5f) / size * 2 - 1);
                float radius = new Vector2(p.x * .98f, p.y * 1.12f).magnitude;
                float edge = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.30f, 1, radius));
                float lamp = edge * Mathf.Lerp(1f, .67f, radius);
                byte level = (byte)(Mathf.Clamp01(lamp) * 255);
                pixels[y * size + x] = new Color32(level, level, level, level);
            }
            practicalProfile.SetPixels32(pixels);
            practicalProfile.Apply(false, true);
            return practicalProfile;
        }

        static bool IsThirdFloorFixture(Vector3 position) => position.y >= NightAnnexLayout.Upper + 2f;

        void AddMainsLight(Vector3 position, Color colour, float power, float range)
        {
            bool thirdFloor = IsThirdFloorFixture(position);
            bool annex = thirdFloor && position.x >= NightAnnexLayout.Bridge.xMin;
            // The third-floor circuit has short pools of light around working fixtures.
            var (scale, maxRange, angle, glow) = (thirdFloor, annex) switch
            {
                (true, true) => (.70f, 4.70f, 94f, 1.12f),
                (true, false) => (1.03f, 5.40f, 99f, 1.55f),
                _ => (1.35f, range, 109f, TubeBloom)
            };
            power *= scale;
            range = Mathf.Min(range, maxRange);
            var body = Box("熄灭的荧光灯槽", position + Vector3.up * .12f,
                new Vector3(.85f, .17f, .32f), steel);
            var diffuser = Box("荧光灯磨砂罩", position + Vector3.up * .022f,
                new Vector3(.67f, .034f, .21f), emergencyGlass);
            body.GetComponent<Collider>().enabled = false;
            diffuser.GetComponent<Collider>().enabled = false;
            var lamp = Fixture(position + Vector3.down * .065f, Vector3.down, colour, power, range,
                angle, thirdFloor ? .003f : .006f);
            lamp.enabled = false;
            float distanceFromBreaker = Mathf.Abs(position.x - 8) + Mathf.Abs(position.z - 3.38f);
            float floor = Mathf.Clamp(Mathf.Floor((position.y + 3.60f) / 3.60f), 0, 3);
            float delay = .045f + floor * .13f + distanceFromBreaker * .0045f;
            circuits.Add((lamp, diffuser.GetComponent<Renderer>(), power, delay, glow));
        }

        Material LitDiffuser()
        {
            if (litDiffuser) return litDiffuser;
            litDiffuser = Material(new Color(.51f, .61f, .58f));
            litDiffuser.EnableKeyword("_EMISSION");
            litDiffuser.SetColor("_EmissionColor", TubeGlow * TubeBloom);
            return litDiffuser;
        }

        // Only the breaker interaction calls this. Opening a save applies its existing power immediately.
        internal void StartPowerRestoration()
        {
            restoringState = game.State;
            powerRestoration = game.StartCoroutine(RestorePower());
        }

        IEnumerator RestorePower()
        {
            const float end = .9f;
            for (float elapsed = 0; elapsed < end; elapsed += Time.deltaTime)
            {
                Shader.SetGlobalFloat("_NightCircuit", Mathf.SmoothStep(0, 1, elapsed / end));
                foreach (var entry in circuits)
                {
                    float rise = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - entry.Delay) / TubeRiseSeconds));
                    entry.Lamp.enabled = rise > 0;
                    entry.Lamp.intensity = entry.Intensity * lampScale * rise;
                    entry.Diffuser.sharedMaterial = rise > 0 ? LitDiffuser() : emergencyGlass;
                    tubeResponse.SetColor("_EmissionColor", TubeGlow * lampScale * entry.Glow * rise);
                    entry.Diffuser.SetPropertyBlock(tubeResponse);
                }
                yield return null;
            }
            powerRestoration = null;
            restoringState = null;
            SetPowerLighting(true);
        }

        void SetPowerLighting(bool power)
        {
            if (powerRestoration != null)
            {
                if (power && ReferenceEquals(restoringState, game.State)) return;
                game.StopCoroutine(powerRestoration);
                powerRestoration = null;
                restoringState = null;
            }

            foreach (var entry in circuits)
            {
                entry.Lamp.enabled = power;
                entry.Lamp.intensity = entry.Intensity * lampScale;
                entry.Diffuser.sharedMaterial = power ? LitDiffuser() : emergencyGlass;
                tubeResponse.SetColor("_EmissionColor", TubeGlow * lampScale * entry.Glow);
                entry.Diffuser.SetPropertyBlock(power ? tubeResponse : null);
            }

            Shader.SetGlobalFloat("_NightCircuit", power ? 1f : 0f);
            // Reflections were shot under the other lighting state. Re-shoot them a few faces a frame.
            if (capturedPower.HasValue && capturedPower != power) atmosphere?.CaptureReflections(false);
            if (capturedPower.HasValue) capturedPower = power;
        }

        // Called once everything is in place, while the title screen hides the brief hitch.
        internal void CaptureReflections()
        {
            capturedPower = game.State.power;
            atmosphere?.CaptureReflections(true);
        }

        void InteriorLight(Vector3 position, string room)
        {
            bool basement = position.y < 0;
            bool wet = room is "大浴池" or "淋浴间" or "盥洗室" or "循环泵房" or "洗衣房";
            bool restored = room is "门厅" or "前台室" or "发电机房" or "循环泵房" or "配电间" or "弱电间";
            bool occupied = room is "茶座" or "大浴池" or "盥洗室" or "宿舍" or "更衣室" or "沙发厅" or "淋浴间";
            if (restored || occupied)
            {
                var tint = basement ? new Color(.87f, .78f, .63f) : new Color(.72f, .86f, .80f);
                if (occupied) tint = wet ? new Color(.67f,.85f,.86f) : new Color(.94f,.79f,.59f);
                AddMainsLight(position, tint, restored ? 1.20f : .82f, 6.15f);
            }
            else
            {
                var body = Box("停用的老式荧光灯", position + Vector3.up * .12f,
                    new Vector3(.85f, .17f, .32f), steel);
                var diffuser = Box("旧灯磨砂罩", position + Vector3.up * .022f,
                    new Vector3(.67f, .034f, .21f), emergencyGlass);
                body.GetComponent<Collider>().enabled = false;
                diffuser.GetComponent<Collider>().enabled = false;
            }

            // Torches matter in the bath and service wings. Keep short pools at a few landmarks and
            // fail-safe points instead of covering each room in an independently powered ceiling light.
            if (room is not ("门厅" or "前台室" or "发电机房" or "配电间" or "弱电间" or "洗衣房")) return;
            var support = position + new Vector3(.94f, -.55f, -.96f);
            var colour = basement ? new Color(1f, .54f, .31f) :
                wet ? new Color(.64f, .83f, .84f) : new Color(.93f, .71f, .50f);
            bool thirdFloor = IsThirdFloorFixture(position);
            Fixture(support, new Vector3(-.4f, -1f, .42f), colour, thirdFloor ? .28f : basement ? .44f : .39f,
                thirdFloor ? 4.0f : 4.75f, 84);
        }

        void CorridorLighting(float y)
        {
            bool thirdFloor = y >= NightAnnexLayout.Upper - .1f;
            foreach (int x in new[] { -16, -7, 2, 11 })
            {
                var mount = new Vector3(x, y + 2.67f, 13.82f);
                var sconce = Prop("industrial_wall_sconce", "走廊旧式壁灯", mount + new Vector3(0, -.19f, -.05f),
                    new Vector3(.17f, .38f, .28f), 180, false);
                if (x is -16 or 11 || y < 0 && x == -7)
                {
                    LightSconce(sconce, thirdFloor ? .48f : 1f);
                    // The weak basement wash marks the next bay without carrying far down the hall.
                    bool guide = x == -7;
                    var tint = y < 0 ? new Color(.98f, .62f, .39f) : new Color(.91f, .72f, .52f);
                    Fixture(mount + new Vector3(0, -.155f, -.28f), new Vector3(.20f, -1f, -.58f),
                        tint, thirdFloor ? .46f : guide ? .38f : .76f,
                        guide ? 3.6f : thirdFloor ? 4.25f : 5.0f, thirdFloor ? 90 : 99);
                }
                AddMainsLight(new Vector3(x + 2, y + 3.06f, 12), new Color(.70f, .84f, .82f), 1.08f, 5.55f);
            }

            foreach (float x in new[] { -18f, 14f })
            {
                var position = new Vector3(x, y + 2.42f, x < 0 ? 14.8f : 9.16f);
                Fixture(position, new Vector3(0, -1, x < 0 ? .15f : -.15f),
                    new Color(.94f, .72f, .53f), .90f, 5.5f, 104);
            }
        }
    }
}
