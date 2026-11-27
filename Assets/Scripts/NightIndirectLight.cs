using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace AfterHours
{
    // Sample one diffuse bounce from installed fixtures after the architecture and furniture exist.
    // The two linear irradiance fields can then crossfade as the mains recover without another trace.
    internal sealed class NightIndirectLight : MonoBehaviour
    {
        const float Cell = 1.25f;
        const int Columns = 166, Rows = 15, Slices = 67;
        static readonly Vector3 Corner = new(-34, -5, -38);
        Texture3D emergency, mains;
        const int SceneMask = 1 << 0;

        internal void Capture(Transform fixtures)
        {
            if (!SystemInfo.supports3DTextures) return;
            var baseLight = new Color[Columns * Rows * Slices];
            var powerLight = new Color[baseLight.Length];
            foreach (var lamp in fixtures.GetComponentsInChildren<Light>(true))
            {
                if (lamp.type != LightType.Spot || lamp.intensity <= 0) continue;
                var field = lamp.enabled ? baseLight : powerLight;
                var t = lamp.transform;
                Sample(field, lamp, t.forward, .5f);
                float spread = Mathf.Tan(lamp.spotAngle * .22f * Mathf.Deg2Rad);
                Sample(field, lamp, (t.forward + t.right * spread).normalized, .25f);
                Sample(field, lamp, (t.forward - t.right * spread).normalized, .25f);
            }
            AddSkylight(baseLight);
            emergency = Volume(baseLight, "Emergency bounce");
            mains = Volume(powerLight, "Mains bounce");
            Shader.SetGlobalTexture("_NightEmergencyGI", emergency);
            Shader.SetGlobalTexture("_NightMainsGI", mains);
            Shader.SetGlobalVector("_NightGICorner", Corner);
            Shader.SetGlobalVector("_NightGIInvSize", new Vector4(1f/(Cell*Columns), 1f/(Cell*Rows), 1f/(Cell*Slices), 1));
        }

        static void AddSkylight(Color[] field)
        {
            // Three cones estimate access to the overcast sky, including sideways through open bays.
            Vector3 westSky = new Vector3(-.82f,.44f,.36f).normalized;
            Vector3 eastSky = new Vector3(.48f,.36f,-.80f).normalized;
            var sky = new Color(.085f,.133f,.174f);
            var shade = new Color(.010f,.016f,.019f);
            for (int z = 0; z < Slices; z++)
            for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
            {
                Vector3 p = Corner + new Vector3(x+.5f,y+.5f,z+.5f)*Cell;
                int index = x + Columns*(y + Rows*z);
                if (p.y < 0 && (p.x < -25 || p.x > 17 || p.z < -13 || p.z > 25)) continue;
                float open = Physics.Raycast(p,Vector3.up,35,SceneMask,QueryTriggerInteraction.Ignore) ? 0 : .44f;
                if (!Physics.Raycast(p,westSky,45,SceneMask,QueryTriggerInteraction.Ignore)) open += .31f;
                if (!Physics.Raycast(p,eastSky,45,SceneMask,QueryTriggerInteraction.Ignore)) open += .25f;
                field[index] += Color.Lerp(shade,sky,open);
            }
        }

        static void Sample(Color[] field, Light lamp, Vector3 direction, float weight)
        {
            if (!Physics.Raycast(lamp.transform.position + direction * .09f, direction, out var hit, lamp.range,
                    SceneMask, QueryTriggerInteraction.Ignore)) return;
            float incidence = Vector3.Dot(-direction, hit.normal);
            if (incidence < .12f || hit.distance < .2f) return;
            var origin = hit.point + hit.normal * .14f;
            var relative = (origin - Corner) / Cell;
            float radius = Mathf.Clamp(lamp.range * .89f, 3f, 9.5f);
            int cells = Mathf.CeilToInt(radius / Cell);
            var middle = Vector3Int.FloorToInt(relative);
            Color energy = lamp.color.linear * (lamp.intensity * incidence * weight * .70f /
                (1f + hit.distance * hit.distance * .058f));
            for (int z = Mathf.Max(0, middle.z-cells); z <= Mathf.Min(Slices-1, middle.z+cells); z++)
            for (int y = Mathf.Max(0, middle.y-cells); y <= Mathf.Min(Rows-1, middle.y+cells); y++)
            for (int x = Mathf.Max(0, middle.x-cells); x <= Mathf.Min(Columns-1, middle.x+cells); x++)
            {
                Vector3 target = Corner + new Vector3(x+.5f, y+.5f, z+.5f) * Cell;
                Vector3 offset = target - origin;
                float distance = offset.magnitude;
                float cosine = Vector3.Dot(offset, hit.normal) / Mathf.Max(.05f, distance);
                if (cosine <= 0 || distance >= radius || Physics.Linecast(origin, target, SceneMask, QueryTriggerInteraction.Ignore)) continue;
                float fade = 1f - distance / radius;
                field[x + Columns * (y + Rows*z)] += energy * (fade * fade * Mathf.Sqrt(cosine));
            }
        }

        static Texture3D Volume(Color[] light, string label)
        {
            var texture = new Texture3D(Columns, Rows, Slices, GraphicsFormat.R8G8B8A8_UNorm, TextureCreationFlags.None)
            { name = label, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels(light);
            texture.Apply(false, true);
            return texture;
        }

        void OnDestroy()
        {
            Shader.SetGlobalVector("_NightGIInvSize", Vector4.zero);
            if (emergency) Destroy(emergency);
            if (mains) Destroy(mains);
        }
    }
}
