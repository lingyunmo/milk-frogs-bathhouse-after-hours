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
        readonly List<(Light Lamp, Renderer Diffuser, float Intensity, float Delay)> circuits = new();
        readonly MaterialPropertyBlock tubeResponse = new();
        Material litDiffuser;
        Coroutine powerRestoration;
        NightState restoringState;

        void Lighting()
        {
            var moon = GameObject.Find("白盒照明").GetComponent<Light>();
            moon.transform.rotation = Quaternion.LookRotation(new Vector3(-.58f, -.39f, .71f), Vector3.up);
            moon.intensity = .13f;
            moon.color = new Color(.55f, .68f, .84f);
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = .97f;
            moon.shadowBias = .12f;
            moon.shadowNormalBias = .42f;
            QualitySettings.pixelLightCount = 6;
            QualitySettings.shadowDistance = 42;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientIntensity = 1;
            RenderSettings.ambientSkyColor = new Color(.14f, .165f, .183f);
            RenderSettings.ambientEquatorColor = new Color(.092f, .112f, .122f);
            RenderSettings.ambientGroundColor = new Color(.080f, .071f, .055f);
            RenderSettings.reflectionIntensity = .07f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.027f, .043f, .064f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .012f;

            var camera = Camera.main;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.allowHDR = true;
        }

        Light Fixture(Vector3 position, Vector3 direction, Color colour, float energy, float range, float angle)
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
            lamp.shadows = LightShadows.Soft;
            lamp.shadowStrength = .96f;
            lamp.shadowNearPlane = .06f;
            lamp.shadowBias = .08f;
            lamp.shadowNormalBias = .25f;
            return lamp;
        }

        void AddMainsLight(Vector3 position, Color colour, float power, float range)
        {
            var body = Box("熄灭的荧光灯槽", position + Vector3.up * .12f,
                new Vector3(.85f, .17f, .32f), steel);
            var diffuser = Box("荧光灯磨砂罩", position + Vector3.up * .022f,
                new Vector3(.67f, .034f, .21f), emergencyGlass);
            body.GetComponent<Collider>().enabled = false;
            diffuser.GetComponent<Collider>().enabled = false;
            var lamp = Fixture(position + Vector3.down * .065f, Vector3.down, colour, power, range, 109);
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
            litDiffuser.SetColor("_EmissionColor", TubeGlow);
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
                    tubeResponse.SetColor("_EmissionColor", TubeGlow * rise);
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
                Prop("industrial_wall_sconce", "走廊旧式壁灯", mount + new Vector3(0, -.19f, -.13f),
                    new Vector3(.17f, .38f, .28f), 180, false);
                if (x is -16 or 11)
                {
                    var tint = y < 0 ? new Color(1f, .53f, .33f) : new Color(.91f, .69f, .47f);
                    Fixture(mount + new Vector3(0, -.155f, -.28f), new Vector3(.12f, -1f, -.67f),
                        tint, .63f, 4.75f, 90);
                }
                AddMainsLight(new Vector3(x + 2, y + 3.06f, 12), new Color(.70f, .84f, .82f), 1.08f, 5.55f);
            }

            foreach (float x in new[] { -18f, 14f })
            {
                var position = new Vector3(x, y + 2.42f, x < 0 ? 14.8f : 9.16f);
                Fixture(position, new Vector3(0, -1, x < 0 ? .15f : -.15f),
                    new Color(.96f, .66f, .41f), .72f, 5.5f, 104);
            }
        }
    }
}
