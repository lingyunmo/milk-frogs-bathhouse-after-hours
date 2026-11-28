using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        Transform monitorDoor;
        NightBrineMonitor monitorFilm;
        GameObject monitorKey, monitorCard, monitorCover, monitorStandby, monitorOnline;
        readonly TextMesh[] monitorLabels = new TextMesh[4];

        void DressBrineInquiry()
        {
            var hut = StreetRoomShell(("防疫监测哨", BrineInquiry.Room), 177, 176.6f, false, paint, terrazzo);
            Box("封死的旧检修孔", new(176.6f, 1.18f, -1.5f), new(1.98f, 2.38f, .24f), paint, hut);
            monitorDoor = Group("防疫门铰", hut, new(176.04f, 0, -8.5f));
            var leaf = Box("重型防疫门", new(177, 1.17f, -8.51f), new(1.91f, 2.34f, .13f), paintedIron, monitorDoor);
            leaf.layer = FloorRoutes.DoorLayer;
            Surface(leaf, paintedIron, .82f);
            Decor("旧门拉手", new(177.76f, 1.17f, -8.64f), new(.18f, .035f, .105f), brass, monitorDoor);
            Decor("铭牌槽", new(177, 1.59f, -8.59f), new(.55f, .17f, .035f), brass, monitorDoor);
            EnamelFace(hut, "生物危害警示搪瓷牌", "Surfaces/BrineIsolationPlaque", new(179.37f, 1.85f, -8.625f), new(1.02f, 1.02f));
            EnamelFace(hut, "电离辐射警示搪瓷牌", "Surfaces/BrineRadiationPlaque", new(180.52f, 1.85f, -8.625f), new(1.02f, 1.02f));
            WallSign("防疫封控   /   电离辐射", new(179.94f, 1.10f, -8.65f), 0, new(2.17f, .29f), .14f);
            StreetRoomLight(hut, new(179.88f, 2.77f, -9.02f), new(0, -.82f, .5f), new(.95f, .66f, .33f), 2.5f);
            StreetRoomProp(hut, "industrial_wall_sconce", "檐下壁灯", new(176.02f, 2.31f, -8.73f), new(.22f, .31f, .19f), 0, false);
            WorkLight("檐灯反照门锁", new(176.39f, 2.16f, -9.42f), new(.92f, .79f, .64f), 2.05f, 3.55f);
            WallSign("借匙：水文站正门 →", new(175.61f, 1.53f, -8.64f), 0, new(.93f, .37f), .10f);
            WallSign("防疫监测哨  ↑", new(173, 1.87f, -9), 0, new(2.36f, .34f), .14f);

            Vector3 key = BrineInquiry.Key;
            var yellow = Material(new Color(.74f, .51f, .16f));
            Decor("水文站的黄漆归匙板", key + new Vector3(0, .04f, .12f), new(.64f, .74f, .055f), yellow, hut);
            WallSign("堤口哨匙", key + new Vector3(0, .24f, .058f), 0, new(.49f, .12f), .07f);
            Decor("挂钩", key + new Vector3(0, .055f, .049f), new(.027f, .035f, .087f), steel, hut);
            var keyring = Group("监测哨实物钥匙", hut, key);
            monitorKey = keyring.gameObject;
            for (int k = 0; k < 12; k++)
            {
                float angle = k * Mathf.PI / 6;
                var ring = Decor("钥匙环", key + new Vector3(Mathf.Cos(angle) * .056f, Mathf.Sin(angle) * .056f, 0),
                    new(.015f, .033f, .016f), brass, keyring);
                ring.transform.rotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
            }
            Decor("钥匙齿杆", key + new Vector3(.014f, -.119f, 0), new(.024f, .16f, .024f), brass, keyring);
            Decor("双齿", key + new Vector3(.035f, -.172f, 0), new(.061f, .036f, .024f), brass, keyring);
            Decor("铜匙黄标签", key + new Vector3(-.059f, -.08f, .016f), new(.06f, .10f, .014f), brass, keyring);

            StreetRoomProp(hut, "metal_office_desk", "巡检终端桌", new(179.50f, .022f, -2.16f), new(2.54f, .82f, .97f), 0);
            StreetRoomProp(hut, "painted_wooden_chair_02", "被推到一旁的值班椅", new(181.13f, .025f, -3.86f), new(.6f, .91f, .61f), -34);
            StreetRoomProp(hut, "worn_metal_rack", "西墙退役设备架", new(175.45f, .023f, -4.71f), new(1.95f, 1.76f, .49f), 90);
            StreetRoomProp(hut, "vintage_radio_transceiver", "拆件录像机", new(175.67f, .77f, -4.68f), new(.82f, .42f, .49f), 90, false);
            StreetRoomProp(hut, "tool_cart", "备用物料车", new(181.41f, .025f, -6.77f), new(1.02f, .85f, .58f), 95);
            StreetRoomLight(hut, new(176.68f, 2.91f, -5.42f), new(-.3f, -.90f, .16f), new(.63f, .79f, .75f), 2.5f);
            StreetRoomLight(hut, new(180.68f, 2.80f, -3.53f), new(-.55f, -.69f, .47f), new(.86f, .78f, .65f), 2.4f);
            WorkLight("检修台的反射补光", new(179.32f, 2.06f, -3.45f), new(.77f, .81f, .77f), 2.15f, 3.75f);
            var pc = BrineInquiry.Computer;
            Decor("台式终端钢壳", pc + new Vector3(0, -.10f, .07f), new(1.04f, .28f, .42f), steel, hut);
            Decor("检修槽蓝边", pc + new Vector3(0, .032f, -.081f), new(.62f, .035f, .19f), olive, hut);
            monitorCover = Decor("烟熏检修盖", pc + new Vector3(0, .055f, -.08f), new(.66f, .028f, .22f), paintedIron, hut);
            monitorCard = Decor("外露回放板", BrineInquiry.Recorder + new Vector3(.058f, 0, 0), new(.24f, .043f, .32f), olive, hut);
            Decor("后面板信号插销", pc + new Vector3(.39f, .013f, -.14f), new(.091f, .06f, .095f), brass, hut);
            var enamel = Trim(pc, paintedIron);
            enamel.Box(new(178.69f,.878f,-2.46f), new(.65f,.062f,.285f));
            var keys = Trim(pc, brass);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 11; col++)
                    keys.Box(new(178.448f + col*.046f, .920f, -2.408f - row*.05f), new(.036f,.022f,.039f));
            keys.Box(new(178.705f,.920f,-2.558f), new(.28f,.022f,.034f));
            for (int i = 0; i < 10; i++)
                enamel.Box(new(179.67f+i*.023f, 1.12f,-2.345f), new(.009f,.012f,.093f));
            WallSign("巡检终端   /   B 路", new(179.58f, 1.004f,-2.54f), 0, new(.48f,.085f), .052f);
            var loom = Trim(pc, paintedIron);
            for (int i = 0; i < 2; i++)
            {
                float x = 180.81f + .087f*i;
                loom.Pipe(new(x, 1.76f, -1.73f), new(x, .79f, -1.84f), .022f);
                loom.Pipe(new(x, .79f,-1.84f), new(179.78f, .79f,-2.03f-i*.044f), .022f);
            }
            var frame = BrineInquiry.Display;
            Decor("巡检大屏铸框", frame + Vector3.forward * .110f, new(3.68f, 1.78f, .19f), paintedIron, hut);
            var glass = Material(new Color(.015f, .025f, .022f));
            glass.SetFloat("_Glossiness", .52f);
            Decor("暗色防眩玻璃", frame, new(3.52f, 1.62f, .036f), glass, hut);
            monitorStandby = Group("失步屏幕", hut, frame).gameObject;
            monitorOnline = Group("巡检回放屏", hut, frame).gameObject;
            ScreenText("无视频输入    B 路失步", monitorStandby.transform, frame + new Vector3(0, .075f, -.03f),
                2.45f, .23f, new Color(.98f, .69f, .34f));
            ScreenText("终端柜旁有检修盖", monitorStandby.transform, frame + new Vector3(0, -.18f, -.03f),
                1.36f, .12f, new Color(.69f, .61f, .43f));
            ScreenText(BrineInquiry.DisplayTitle, monitorOnline.transform, frame + new Vector3(0, .644f, -.037f),
                2.4f, .15f, new Color(.77f, .95f, .79f));
            ScreenText(BrineInquiry.DisplayWarning, monitorOnline.transform, frame + new Vector3(0, -.665f, -.038f),
                2.90f, .155f, new Color(1, .71f, .39f));
            var feeds = new Renderer[4];
            for (int i = 0; i < feeds.Length; i++)
            {
                float x = (i % 2 == 0 ? -.84f : .84f), y = i < 2 ? .27f : -.26f;
                var cell = frame + new Vector3(x, y, -.042f);
                feeds[i] = GameObject.CreatePrimitive(PrimitiveType.Quad).GetComponent<Renderer>();
                feeds[i].name = "留档画面 · " + BrineRoute.Marks[i].Place;
                feeds[i].transform.SetParent(monitorOnline.transform, true);
                feeds[i].transform.position = cell + Vector3.left * .37f;
                feeds[i].transform.localScale = new(.80f, .43f, 1);
                feeds[i].GetComponent<Collider>().enabled = false;
                monitorLabels[i] = ScreenText(BrineRoute.Marks[i].Place, monitorOnline.transform,
                    cell + Vector3.right * .455f, .72f, .132f, new Color(.77f, .95f, .79f));
            }
            monitorFilm = hut.gameObject.AddComponent<NightBrineMonitor>();
            monitorFilm.SetFeeds(feeds, monitorStandby, monitorOnline, signFont);
            var glow = monitorOnline.AddComponent<Light>();
            glow.type = LightType.Point; glow.color = new Color(.39f, .78f, .65f); glow.range = 3.9f;
            glow.intensity = 1.1f; glow.shadows = LightShadows.None;
            monitorOnline.SetActive(false);
        }

        void WorkLight(string label, Vector3 at, Color color, float intensity, float range)
        {
            var lamp = new GameObject(label).AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.position = at;
            lamp.type = LightType.Point;
            lamp.color = color;
            lamp.intensity = intensity;
            lamp.range = range;
            lamp.shadows = LightShadows.None;
            lamp.renderMode = LightRenderMode.ForcePixel;
        }

        static Transform Group(string label, Transform parent, Vector3 position)
        {
            var node = new GameObject(label).transform;
            node.SetParent(parent, false);
            node.position = position;
            return node;
        }

        GameObject Decor(string label, Vector3 at, Vector3 size, Material material, Transform parent)
        {
            var obj = Box(label, at, size, material, parent);
            obj.GetComponent<Collider>().enabled = false;
            return obj;
        }

        TextMesh ScreenText(string words, Transform parent, Vector3 at, float width, float height, Color color)
        {
            var text = new GameObject(words).AddComponent<TextMesh>();
            text.transform.SetParent(parent, false);
            text.transform.position = at;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.font = signFont;
            text.fontSize = 72;
            text.characterSize = .10f;
            text.color = color;
            text.text = words;
            var ink = text.GetComponent<MeshRenderer>();
            ink.sharedMaterial = signInk;
            ink.shadowCastingMode = ShadowCastingMode.Off;
            ink.receiveShadows = false;
            FitLetters(text, width, height);
            return text;
        }

        void RefreshBrineInquiry(BrineRun run)
        {
            bool open = run.inquiry >= BrineInquiryStep.Open;
            monitorDoor.localRotation = Quaternion.Euler(0, open ? -91 : 0, 0);
            monitorKey.SetActive(run.inquiry == BrineInquiryStep.Sealed);
            monitorCover.SetActive(run.inquiry < BrineInquiryStep.Fault || run.inquiry == BrineInquiryStep.Online);
            monitorCard.SetActive(run.inquiry < BrineInquiryStep.Card);
            bool online = run.inquiry == BrineInquiryStep.Online;
            monitorStandby.SetActive(!online);
            monitorOnline.SetActive(online);
            monitorFilm.Power(online, run.wheel);
            for (int i = 0; i < monitorLabels.Length; i++)
                monitorLabels[i].color = (run.rubbings & (1 << i)) == 0 ? new Color(.74f, .90f, .77f) : new Color(1f, .74f, .40f);
        }
    }
}
