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
        readonly List<(Light Lamp, Renderer Diffuser, float Intensity, float Delay)> circuits = new();
        readonly MaterialPropertyBlock tubeResponse = new();
        Material litDiffuser, practicalBulb;
        Coroutine powerRestoration;
        static Texture2D practicalProfile;
        NightState restoringState;
        NightAtmosphere atmosphere;
        bool? capturedPower;

        void Lighting()
        {
            var moon = GameObject.Find("白盒照明").GetComponent<Light>();
            moon.transform.rotation = Quaternion.LookRotation(new Vector3(-.58f, -.39f, .71f), Vector3.up);
            moon.intensity = .20f;
            moon.color = new Color(.63f, .73f, .85f);
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = .97f;
            moon.shadowBias = .12f;
            moon.shadowNormalBias = .42f;
            QualitySettings.pixelLightCount = 8;
            QualitySettings.shadowDistance = 42;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientIntensity = 1;
            RenderSettings.ambientSkyColor = new Color(.175f, .188f, .204f);
            RenderSettings.ambientEquatorColor = new Color(.120f, .139f, .150f);
            RenderSettings.ambientGroundColor = new Color(.109f, .101f, .089f);
            // Rooms carry their own box-projected reflections. The sky fallback only reaches the street.
            RenderSettings.reflectionIntensity = .07f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.027f, .043f, .064f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .012f;

            var camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
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
            return lamp;
        }

        void LightSconce(GameObject sconce)
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

        void AddMainsLight(Vector3 position, Color colour, float power, float range)
        {
            power *= 1.35f;
            var body = Box("熄灭的荧光灯槽", position + Vector3.up * .12f,
                new Vector3(.85f, .17f, .32f), steel);
            var diffuser = Box("荧光灯磨砂罩", position + Vector3.up * .022f,
                new Vector3(.67f, .034f, .21f), emergencyGlass);
            body.GetComponent<Collider>().enabled = false;
            diffuser.GetComponent<Collider>().enabled = false;
            var lamp = Fixture(position + Vector3.down * .065f, Vector3.down, colour, power, range, 109, .006f);
            lamp.enabled = false;
            float distanceFromBreaker = Mathf.Abs(position.x - 8) + Mathf.Abs(position.z - 3.38f);
            float floor = Mathf.Clamp(Mathf.Floor((position.y + 3.60f) / 3.60f), 0, 3);
            float delay = .045f + floor * .13f + distanceFromBreaker * .0045f;
            circuits.Add((lamp, diffuser.GetComponent<Renderer>(), power, delay));
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
                foreach (var entry in circuits)
                {
                    float rise = Mathf.SmoothStep(0, 1, Mathf.Clamp01((elapsed - entry.Delay) / TubeRiseSeconds));
                    entry.Lamp.enabled = rise > 0;
                    entry.Lamp.intensity = entry.Intensity * rise;
                    entry.Diffuser.sharedMaterial = rise > 0 ? LitDiffuser() : emergencyGlass;
                    tubeResponse.SetColor("_EmissionColor", TubeGlow * TubeBloom * rise);
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
                entry.Lamp.intensity = entry.Intensity;
                entry.Diffuser.sharedMaterial = power ? LitDiffuser() : emergencyGlass;
                entry.Diffuser.SetPropertyBlock(null);
            }

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
            if (restored)
            {
                var tint = basement ? new Color(.87f, .78f, .63f) : new Color(.72f, .86f, .80f);
                AddMainsLight(position, tint, 1.20f, 6.15f);
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
            Fixture(support, new Vector3(-.4f, -1f, .42f), colour, basement ? .44f : .39f, 4.75f, 84);
        }

        void CorridorLighting(float y)
        {
            foreach (int x in new[] { -16, -7, 2, 11 })
            {
                var mount = new Vector3(x, y + 2.67f, 13.82f);
                var sconce = Prop("industrial_wall_sconce", "走廊旧式壁灯", mount + new Vector3(0, -.19f, -.13f),
                    new Vector3(.17f, .38f, .28f), 180, false);
                if (x is -16 or 11 || y < 0 && x == -7)
                {
                    LightSconce(sconce);
                    // The weak basement wash marks the next bay without carrying far down the hall.
                    bool guide = x == -7;
                    var tint = y < 0 ? new Color(.98f, .62f, .39f) : new Color(.91f, .72f, .52f);
                    Fixture(mount + new Vector3(0, -.155f, -.28f), new Vector3(.20f, -1f, -.58f),
                        tint, guide ? .38f : .76f, guide ? 3.6f : 5.0f, 99);
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
