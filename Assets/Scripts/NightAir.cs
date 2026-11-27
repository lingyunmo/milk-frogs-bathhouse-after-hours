using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        Material exitGlow, streetLens;

        // Each room gets a box-projected reflection that matches its walls, and the baths keep steaming.
        void RoomAir(string room, Transform slab)
        {
            Vector3 size = slab.localScale;
            atmosphere.AddProbe(slab.position + Vector3.up * 1.75f, new Vector3(size.x, 3.45f, size.z));
            if (room is "大浴池" or "淋浴间")
                atmosphere.AddSteam(slab.position + Vector3.up * .9f, new Vector3(size.x * .75f, .5f, size.z * .7f),
                    new Color(.80f, .86f, .85f, .11f), room == "大浴池" ? 2.8f : 2.0f);
            else if (room is "盥洗室" or "循环泵房")
                atmosphere.AddSteam(slab.position + Vector3.up * .5f, new Vector3(size.x * .6f, .3f, size.z * .6f),
                    new Color(.76f, .82f, .82f, .09f), 1.1f);
        }

        // A corridor is 36 metres long, so it takes four reflections instead of one stretched box.
        void HallAir(Transform hall)
        {
            Vector3 size = hall.localScale;
            const int bays = 4;
            float bay = size.x / bays;
            for (int i = 0; i < bays; ++i)
            {
                var center = hall.position + new Vector3(-size.x * .5f + bay * (i + .5f), 1.75f, 0);
                atmosphere.AddProbe(center, new Vector3(bay, 3.45f, size.z));
            }
        }

        void StairAir(Transform stair)
        {
            if (!stair) return;
            var renderers = stair.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            atmosphere.AddProbe(bounds.center, new Vector3(bounds.size.x, Mathf.Min(bounds.size.y, 3.6f), bounds.size.z));
        }

        // Battery-backed exit signs stay lit through the blackout and wash the corridor ends in green.
        void ExitSigns()
        {
            if (!exitGlow)
            {
                exitGlow = Material(new Color(.05f, .32f, .16f));
                exitGlow.name = "Exit sign diffuser";
                exitGlow.EnableKeyword("_EMISSION");
                exitGlow.SetColor("_EmissionColor", new Color(.06f, .95f, .38f) * 2.3f);
                exitGlow.SetFloat("_Glossiness", .62f);
            }
            foreach (float floor in new[] { -3.6f, 0f, 3.6f, 7.2f })
            foreach (var (x, facing) in new[] { (-15.55f, 1f), (11.55f, -1f) })
            {
                var center = new Vector3(x, floor + 2.74f, 12);
                var housing = Box("安全出口灯箱", center, new Vector3(.07f, .21f, .56f), steel);
                housing.GetComponent<Collider>().enabled = false;
                var face = Box("安全出口灯面", center, new Vector3(.075f, .17f, .52f), exitGlow);
                face.GetComponent<Collider>().enabled = false;
                face.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                foreach (float rod in new[] { -.2f, .2f })
                {
                    var hanger = Box("灯箱吊杆", center + new Vector3(0, .4f, rod), new Vector3(.012f, .6f, .012f), steel);
                    hanger.GetComponent<Collider>().enabled = false;
                }
                foreach (float side in new[] { -1f, 1f })
                {
                    var words = new GameObject("安全出口").AddComponent<TextMesh>();
                    words.transform.SetParent(root);
                    words.transform.SetPositionAndRotation(center + Vector3.right * (side * .0395f),
                        Quaternion.Euler(0, side > 0 ? -90 : 90, 0));
                    words.anchor = TextAnchor.MiddleCenter;
                    words.alignment = TextAlignment.Center;
                    words.font = signFont;
                    words.fontSize = 52;
                    words.characterSize = .1f;
                    words.color = new Color(.90f, 1f, .93f);
                    words.text = "安全出口";
                    var ink = words.GetComponent<Renderer>();
                    ink.sharedMaterial = signInk;
                    ink.shadowCastingMode = ShadowCastingMode.Off;
                    ink.receiveShadows = false;
                    FitLetters(words, .42f, .11f);
                }
                var spill = new GameObject("安全出口绿光").AddComponent<Light>();
                spill.transform.SetParent(root);
                spill.transform.position = center + new Vector3(facing * .25f, -.22f, 0);
                spill.type = LightType.Point;
                spill.color = new Color(.30f, 1f, .56f);
                bool thirdFloor = floor == NightAnnexLayout.Upper;
                spill.range = thirdFloor ? 2.65f : 3.3f;
                spill.intensity = thirdFloor ? .27f : .42f;
                practicals.Add((spill, spill.intensity));
                spill.shadows = LightShadows.None;
            }
        }

        Material StreetLens()
        {
            if (streetLens) return streetLens;
            streetLens = Material(new Color(.95f, .88f, .74f));
            streetLens.name = "Street lamp lens";
            streetLens.EnableKeyword("_EMISSION");
            streetLens.SetColor("_EmissionColor", new Color(1f, .80f, .56f) * 3.4f);
            return streetLens;
        }
    }
}
