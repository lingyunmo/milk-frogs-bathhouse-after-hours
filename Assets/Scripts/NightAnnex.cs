using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    internal static class NightAnnexLayout
    {
        internal const float Lower = 3.6f, Upper = 7.2f;
        internal const float RampX = 41, RampStart = 7.5f, RampEnd = 19.5f, LowerRampEntry = 6, UpperRampEntry = 21;
        internal static readonly Rect Bridge = Rect.MinMaxRect(16, 10.2f, 23, 13.8f);
        internal static readonly Rect Footprint = Rect.MinMaxRect(23, 4, 39, 24);
        internal static readonly Rect Ramp = Rect.MinMaxRect(39, 4, 43, 24);
        internal static readonly Vector3 IsolationSwitch = new(28, 4.51f, 5.65f);
        internal static readonly Vector3 WiringPlan = new(28, 8.12f, 22.35f);
        internal static readonly Vector3 RelayConsole = new(36.5f, 8.10f, 22.2f);
        internal static readonly (string Id, Vector3 Position, string Location)[] Supplies =
        {
            ("cell-annex-rest", new(35.6f, 4.507f, 21.8f), "休息间"),
            ("cell-annex-store", new(26.2f, 8.117f, 5.75f), "配件库")
        };

        internal static readonly (int Floor, string Name, Rect Bounds, bool Route, Vector2? Entrance)[] Areas =
        {
            (2, "东连廊", Bridge, true, new Vector2(16, 12)),
            (2, "副楼走廊", Rect.MinMaxRect(23, 10, 39, 14), true, new Vector2(23, 12)),
            (2, "机修间", Rect.MinMaxRect(23, 4, 31, 10), false, new Vector2(27, 10)),
            (2, "器材间", Rect.MinMaxRect(31, 4, 39, 10), false, new Vector2(35, 10)),
            (2, "布草间", Rect.MinMaxRect(23, 14, 31, 24), false, new Vector2(27, 14)),
            (2, "休息间", Rect.MinMaxRect(31, 14, 39, 24), false, new Vector2(35, 14)),
            (2, "折坡通道", Ramp, true, new Vector2(39, LowerRampEntry)),
            (3, "东连廊", Bridge, true, new Vector2(16, 12)),
            (3, "副楼走廊", Rect.MinMaxRect(23, 10, 39, 14), true, new Vector2(23, 12)),
            (3, "配件库", Rect.MinMaxRect(23, 4, 31, 10), false, new Vector2(27, 10)),
            (3, "监测室", Rect.MinMaxRect(31, 4, 39, 10), false, new Vector2(35, 10)),
            (3, "留档室", Rect.MinMaxRect(23, 14, 31, 24), false, new Vector2(27, 14)),
            (3, "转接间", Rect.MinMaxRect(31, 14, 39, 24), false, new Vector2(35, 14)),
            (3, "折坡通道", Ramp, true, new Vector2(39, UpperRampEntry))
        };

        internal static float RampHeight(float z) =>
            Mathf.Lerp(Lower, Upper, Mathf.InverseLerp(RampStart, RampEnd, z));
    }

    internal sealed partial class NightWorld
    {
        const float AnnexCeiling = 3.14f;
        const float AnnexDoorHalf = 1.16f;
        const float AnnexHeadroom = 2.46f;

        void DressAnnex()
        {
            var annex = new GameObject("浴场副楼").transform;
            annex.SetParent(root, false);
            Transform bathhouse = GameObject.Find("浴场").transform;
            var glazing = AnnexGlazing();
            foreach (var (index, height) in new[] { (2, NightAnnexLayout.Lower), (3, NightAnnexLayout.Upper) })
            {
                var storey = new GameObject(index + "F 副楼").transform;
                storey.SetParent(annex, false);
                CutAnnexEntrance(bathhouse, height);
                BuildAnnexBridge(storey, height, glazing);
                BuildAnnexFloor(storey, index, height);
                FurnishAnnex(storey, index, height);
            }
            BuildAnnexRamp(annex);
            DressAnnexLoadingBay(annex);

            foreach (var (id, position, _) in NightAnnexLayout.Supplies)
                Pickup(id, position, new(.12f, .09f, .11f), olive);
        }

        void CutAnnexEntrance(Transform bathhouse, float height)
        {
            foreach (Transform storey in bathhouse)
            {
                Transform hall = storey.Find("横廊");
                if (!hall || Mathf.Abs(hall.position.y + hall.localScale.y * .5f - height) > .025f) continue;
                foreach (Transform candidate in storey)
                {
                    Vector3 center = candidate.position;
                    if (candidate.name != "墙" || Mathf.Abs(center.x - 16) > .025f ||
                        Mathf.Abs(center.z - 12) > .025f) continue;

                    // Disable the original end wall before the architecture pass coats the inside face.
                    candidate.gameObject.SetActive(false);
                    AnnexWall(storey, 16, 10, 14, height, false, brick, 12, top: 3.56f);
                    return;
                }
            }
            throw new InvalidOperationException("The east end wall of the original bathhouse corridor has moved.");
        }

        void BuildAnnexBridge(Transform storey, float height, Material glazing)
        {
            bool upper = height == NightAnnexLayout.Upper;
            Rect bounds = NightAnnexLayout.Bridge;
            AnnexDeck(storey, bounds, height, "东连廊地坪", terrazzo);
            AnnexRoof(storey, bounds, height, "东连廊屋面", steel);
            foreach (float z in new[] { bounds.yMin, bounds.yMax })
            {
                float x = bounds.center.x;
                AnnexSurface(storey, "连廊混凝土窗台", new(x, height + .54f, z),
                    new(bounds.width, 1.08f, .20f), plaster);
                AnnexSurface(storey, "连廊窗上梁", new(x, height + 2.92f, z),
                    new(bounds.width, .44f, .22f), plaster);
                AnnexSurface(storey, "连廊玻璃", new(x, height + 1.89f, z),
                    new(bounds.width, 1.69f, .05f), glazing, collider: false);
                CollisionBox(storey, "连廊窗面碰撞", new(x, height + 1.89f, z), new(bounds.width, 1.69f, .05f));
                foreach (float post in new[] { 16.12f, 17.75f, 19.5f, 21.25f, 22.88f })
                    AnnexSurface(storey, "栓接锈蚀窗框", new(post, height + 1.89f, z),
                        new(.13f, 1.69f, .16f), steel);
            }
            WallSign("副楼", new Vector3(16.37f, height + 2.77f, 12), 90, new(1.18f, .28f), .12f);
            AddMainsLight(new Vector3(19.5f, height + 2.98f, 12), new Color(.77f, .84f, .78f), 1.22f, 5.2f);
            var caged = Prop("industrial_wall_sconce", "连廊应急壁灯", new(21.25f, height + 2.05f, 13.60f),
                new(.20f, .31f, .25f), 180, false);
            caged.transform.SetParent(storey, true);
            LightSconce(caged, upper ? .36f : 1f);
            Fixture(new Vector3(21.25f, height + 2.02f, 13.30f), new Vector3(-.35f, -.44f, -1f),
                new Color(.96f, .76f, .49f), upper ? .78f : 1.65f, upper ? 4.70f : 5.8f,
                upper ? 96 : 108, .005f);
            atmosphere.AddProbe(new Vector3(bounds.center.x, height + 1.55f, bounds.center.y),
                new Vector3(bounds.width, AnnexCeiling, bounds.height));
        }

        void BuildAnnexFloor(Transform storey, int index, float height)
        {
            bool lower = index == 2;
            Rect bounds = NightAnnexLayout.Footprint;
            AnnexDeck(storey, bounds, height, "副楼地坪", lower ? terrazzo : planks);
            AnnexRoof(storey, bounds, height, "副楼抹灰顶", plaster);
            AnnexWall(storey, bounds.yMin, bounds.xMin, bounds.xMax, height, true, brick);
            AnnexWall(storey, bounds.yMax, bounds.xMin, bounds.xMax, height, true, brick);
            AnnexWall(storey, bounds.xMin, bounds.yMin, bounds.yMax, height, false, brick, 12);
            AnnexWall(storey, bounds.xMax, bounds.yMin, bounds.yMax, height, false, brick,
                lower ? NightAnnexLayout.LowerRampEntry : NightAnnexLayout.UpperRampEntry);

            foreach (float line in new[] { 10f, 14f })
            {
                AnnexWall(storey, line, bounds.xMin, 31, height, true, paint, 27);
                AnnexWall(storey, line, 31, bounds.xMax, height, true, paint, 35);
            }
            // Staff doors between adjoining rooms let players break line of sight and loop back to either hall entrance.
            AnnexWall(storey, 31, bounds.yMin, 10, height, false, glazed, 7.5f);
            AnnexWall(storey, 31, 14, bounds.yMax, height, false, glazed, 18);
            foreach (var area in NightAnnexLayout.Areas)
            {
                if (area.Floor != index || area.Route || !area.Entrance.HasValue) continue;
                Vector2 door = area.Entrance.Value;
                LabelRoom(area.Name, new Vector3(door.x, height, door.y), door.y > 12);
                Vector2 center = area.Bounds.center;
                atmosphere.AddProbe(new Vector3(center.x, height + 1.57f, center.y),
                    new Vector3(area.Bounds.width, AnnexCeiling, area.Bounds.height));
            }
            for (int x = 25; x <= 37; x += 6)
                AddMainsLight(new Vector3(x, height + 2.98f, 12), new Color(.73f, .84f, .80f), .94f, 5.5f);
            Fixture(new Vector3(30.65f, height + 2.51f, 13.4f), new Vector3(.17f, -1, -.60f),
                new Color(.63f, .78f, .75f), lower ? .52f : .28f, lower ? 5.0f : 4.15f, 105);
            WallSign("楼梯", new Vector3(38.85f, height + 2.73f,
                lower ? NightAnnexLayout.LowerRampEntry : NightAnnexLayout.UpperRampEntry), 90, new(1.24f, .29f), .13f);
        }

        void BuildAnnexRamp(Transform annex)
        {
            Rect bounds = NightAnnexLayout.Ramp;
            const float x = NightAnnexLayout.RampX, lower = NightAnnexLayout.Lower, upper = NightAnnexLayout.Upper;
            const float start = NightAnnexLayout.RampStart, end = NightAnnexLayout.RampEnd;
            var stairs = new GameObject("副楼折坡通道").transform;
            stairs.SetParent(annex, false);
            Rect bottom = Rect.MinMaxRect(bounds.xMin, bounds.yMin, bounds.xMax, start);
            Rect top = Rect.MinMaxRect(bounds.xMin, end, bounds.xMax, bounds.yMax);
            AnnexDeck(stairs, bottom, lower, "折坡下平台", plaster);
            AnnexDeck(stairs, top, upper, "折坡上平台", plaster);
            AnnexRoof(stairs, bottom, lower, "下平台顶", steel);
            AnnexRoof(stairs, top, upper, "上平台顶", steel);

            var rise = new Vector3(0, upper - lower, end - start);
            Quaternion pitch = Quaternion.LookRotation(rise, Vector3.up);
            Vector3 midpoint = new(x, (lower + upper) * .5f, (start + end) * .5f);
            Vector3 inclinedUp = pitch * Vector3.up;
            float length = rise.magnitude + .06f;
            Sloped("防滑坡面", midpoint - inclinedUp * .11f, new(bounds.width, .22f, length), plaster);
            Sloped("坡廊顶", midpoint + Vector3.up * (AnnexCeiling + .10f), new(bounds.width, .22f, length), steel);
            Sloped("坡廊外护墙", midpoint + new Vector3(bounds.xMax - x, 1.55f, 0),
                new(.20f, 3.33f, length), brick);
            // Steel edges leave a slightly recessed walking lane without adding obstacles to the floor.
            Sloped("靠墙扶手", midpoint + new Vector3(-1.80f, .91f, 0), new(.06f, .064f, length), steel, false);
            Sloped("外墙扶手", midpoint + new Vector3(1.80f, .91f, 0), new(.06f, .064f, length), steel, false);
            AnnexWall(stairs, bounds.xMax, bounds.yMin, start, lower, false, brick);
            AnnexWall(stairs, bounds.xMax, end, bounds.yMax, upper, false, brick);
            AnnexWall(stairs, bounds.yMin, bounds.xMin, bounds.xMax, lower, true, brick);
            AnnexWall(stairs, bounds.yMax, bounds.xMin, bounds.xMax, upper, true, brick);
            foreach (float z in new[] { 6f, 13.5f, 21f })
            {
                float y = NightAnnexLayout.RampHeight(z);
                var lamp = Prop("industrial_wall_sconce", "折坡通道防护灯", new(42.77f, y + 2.2f, z),
                    new(.20f, .34f, .27f), 90, false);
                lamp.transform.SetParent(stairs, true);
                bool upperLanding = z == NightAnnexLayout.UpperRampEntry;
                LightSconce(lamp, upperLanding ? .36f : 1f);
                Fixture(new Vector3(42.49f, y + 2.42f, z), new Vector3(-.52f, -1, .19f),
                    new Color(.92f, .67f, .39f), upperLanding ? .26f : .61f, upperLanding ? 3.4f : 4.37f, 110);
            }

            void Sloped(string label, Vector3 position, Vector3 size, Material material, bool collision = true)
            {
                var piece = Box(label, position, size, material, stairs);
                piece.transform.rotation = pitch;
                piece.GetComponent<Collider>().enabled = collision;
                Surface(piece, material, 1.7f, floor: label == "防滑坡面");
            }
        }

        void FurnishAnnex(Transform storey, int index, float height)
        {
            void Add(string asset, string label, float x, float z, Vector3 size, float yaw = 0,
                float raise = .012f, bool solid = true) =>
                StreetRoomProp(storey, asset, label, new(x, height + raise, z), size, yaw, solid);

            var (wallTint, tableTint) = index == 2
                ? (new Color(.67f, .79f, .80f), new Color(1f, .76f, .44f))
                : (new Color(.61f, .77f, .74f), new Color(.93f, .69f, .40f));

            if (index == 2)
            {
                Add("metal_office_desk", "机修台", 28, 5.54f, new(2.6f, .82f, .82f));
                Add("wall_clock", "机修间停走的钟", 28.3f, 4.1325f, new(.42f, .42f, .095f), 0, 2.06f, false);
                Add("worn_metal_rack", "轴承架", 24.0f, 6.2f, new(2.2f, 1.90f, .60f), 90);
                Add("cardboard_box_01", "轴承架上旧包装", 24.13f, 6.76f, new(.39f, .35f, .46f), 4, .012f + ShelfTop(1.90f, 3), false);
                Add("tool_cart", "留在机修间的旧推车", 29.5f, 8.80f, new(.88f, .93f, .55f), -10);
                Add("portable_generator", "拆下的便携式机组", 33.1f, 5.30f, new(1.24f, .92f, .61f), -15);
                Add("worn_metal_rack", "器材钢架", 36.9f, 4.62f, new(2.45f, 1.91f, .67f));
                Add("cardboard_box_01", "底层滤芯纸箱", 36.56f, 4.60f, new(.50f, .44f, .40f), 9, .012f + ShelfTop(1.91f, 1), false);
                Add("vintage_suitcase", "送修的仪器箱", 36.95f, 4.60f, new(.65f, .34f, .42f), -6, .012f + ShelfTop(1.91f, 2), false);
                Add("metal-bucket", "接残油的桶", 37.83f, 8.3f, new(.41f, .43f, .44f), 9);
                Add("laundry-basket", "布草筐", 24.0f, 16.29f, new(.75f, .80f, .79f), -13);
                Add("folded-towel-stack", "待入库的布巾", 26.25f, 20.80f, new(.60f, .23f, .46f), 8, 1.04f, false);
                Add("washing-machine", "带脱水桶的洗衣滚筒", 29.6f, 23.14f, new(1.02f, 1.17f, .83f), 180);
                BuildAnnexSofaHide(storey, height);
                Add("metal_office_desk", "休息间夜班补给桌", 35.6f, 21.95f, new(1.7f, .85f, .64f), 180);
                Add("tea_set_01", "夜班没收的茶具", 35.03f, 22.05f, new(.36f, .18f, .27f), 24, .863f, false);
                Add("cardboard_box_01", "换下的工作服纸箱", 36.93f, 23.19f, new(.72f, .50f, .60f), -7);
                Add("painted_wooden_chair_02", "未推回去的椅子", 32.64f, 21.01f, new(.55f, .91f, .53f), -19);
                BuildAnnexLinenHide(storey, height);
            }
            else
            {
                Add("worn_metal_rack", "配件间旧钢架", 24.1f, 5.86f, new(2.54f, 1.92f, .64f), 90);
                Add("metal_office_desk", "配件间登记台", 26.2f, 5.78f, new(1.92f, .86f, .65f), 180);
                Add("cardboard_box_01", "待登记的配件", 26.84f, 5.85f, new(.40f, .34f, .33f), 8, .872f, false);
                Add("vintage_suitcase", "换下的现场仪器", 29.7f, 8.76f, new(.70f, .42f, .50f), 7);
                Add("metal_office_desk", "监测台", 34.5f, 5.27f, new(2.78f, .89f, .79f), 180);
                Add("television_02", "老监测显像管", 34.78f, 5.23f, new(.67f, .63f, .48f), 180, .898f, false);
                Add("vintage_radio_transceiver", "离线监听设备", 33.67f, 5.27f, new(.51f, .23f, .34f), 175, .902f, false);
                Add("painted_wooden_chair_02", "监测台旁的椅子", 35.71f, 6.37f, new(.56f, .93f, .55f), 25);
                Add("worn_metal_rack", "留档室的资料钢架", 24.12f, 19.65f, new(3.85f, 2.07f, .64f), 90);
                Add("cardboard_box_01", "值班记录封存箱", 24.11f, 18.98f, new(.44f, .39f, .58f), 90, .012f + ShelfTop(2.07f, 2), false);
                Add("cardboard_box_01", "旧线路备份档案", 24.12f, 20.21f, new(.42f, .38f, .54f), 80, .012f + ShelfTop(2.07f, 2), false);
                Add("worn_metal_rack", "留档室后排货架", 29.55f, 17.21f, new(2.2f, 1.97f, .62f), 90);
                Add("metal_office_desk", "留档室接线图桌", 28, 22.45f, new(2.2f, .87f, .81f), 180);
                Add("painted_wooden_chair_02", "留档室椅子", 26.3f, 21.9f, new(.52f, .87f, .52f), 17);
                Add("metal_office_desk", "转接设备台", 36.5f, 22.46f, new(2.59f, .85f, .74f), 180);
                Add("vintage_radio_transceiver", "副楼上行转接机", 36.28f, 22.44f, new(.64f, .28f, .47f), 180, .86f, false);
                Add("tool_cart", "转接间备用工具车", 32.28f, 15.65f, new(.88f, 1.02f, .57f), -12);
                Add("cardboard_box_01", "转接机废线束", 36.99f, 22.47f, new(.45f, .24f, .34f), -8, .86f, false);
                AnnexPaper(storey);
            }

            // The lit work surfaces are navigation anchors even during the blackout.
            StreetRoomLight(storey, new(28.35f, height + 2.71f, index == 2 ? 5.12f : 22.94f),
                new(-.14f, -1, index == 2 ? .30f : -.30f), tableTint, index == 2 ? 1.60f : .64f, height + AnnexCeiling);
            StreetRoomLight(storey, new(36.88f, height + 2.69f, 22.89f), new(-.29f, -1, -.41f),
                wallTint, index == 2 ? .76f : .37f, height + AnnexCeiling);
            AddMainsLight(new Vector3(34.5f, height + 2.98f, 7.4f), wallTint, .94f, 4.63f);
            AddMainsLight(new Vector3(26.8f, height + 2.98f, 18.7f), wallTint, .91f, 5.32f);
            if (index == 2)
                AddMainsLight(new Vector3(26.8f, height + 2.97f, 7.6f), wallTint, 1.20f, 4.9f);
            else
                AddMainsLight(new Vector3(36.0f, height + 2.97f, 20.9f), tableTint, 1.42f, 5.1f);
        }

        void BuildAnnexLinenHide(Transform storey, float height)
        {
            // The shelf has a low opening on its south face, with the hanging cloth shielding the back.
            AnnexSurface(storey, "布草折叠台面", new(26.3f, height + .91f, 20.93f),
                new(2.53f, .26f, 2.28f), planks);
            foreach (float x in new[] { 25.12f, 27.48f })
                AnnexSurface(storey, "折叠台支撑板", new(x, height + .40f, 20.93f),
                    new(.16f, .80f, 2.28f), planks);
            AnnexSurface(storey, "折叠台背板", new(26.3f, height + .40f, 22),
                new(2.53f, .80f, .14f), planks);
            AnnexSurface(storey, "折叠台垂下的布挡", new(26.98f, height + .60f, 19.79f),
                new(.72f, .36f, .11f), paint);
        }

        void BuildAnnexSofaHide(Transform storey, float height)
        {
            Vector3 center = new(37.9f, height, 19.5f);
            var seat = Prop("sofa_02", "夜班休息间高脚沙发", center, new(2.95f, 1.19f, 1.20f), 90, false, "Raised");
            seat.transform.SetParent(storey, true);
            CollisionBox(storey, "高脚沙发座架", center + Vector3.up * .91f, new(1.18f, .51f, 2.92f));
            foreach (float offset in new[] { -1.32f, 1.32f })
                CollisionBox(storey, "高脚沙发两侧支撑", center + new Vector3(0, .33f, offset),
                    new(1.11f, .66f, .17f));
            CollisionBox(storey, "高脚沙发靠墙挡板", center + new Vector3(.54f, .33f, 0),
                new(.15f, .66f, 2.61f));
        }

        void AnnexPaper(Transform storey)
        {
            Vector3 paper = NightAnnexLayout.WiringPlan - Vector3.up * .032f;
            AnnexSurface(storey, "带编号的接线图", paper, new(.36f, .012f, .29f), ceiling, collider: false);
            for (int line = 0; line < 3; line++)
            {
                float offset = line * .064f - .064f;
                AnnexSurface(storey, "接线图墨线", paper + new Vector3(0, .009f, offset),
                    new(.216f, .004f, .013f), steel, collider: false);
                AnnexSurface(storey, "接线图端子标记", paper + new Vector3(-.118f + line * .075f, .010f, offset),
                    new(.028f, .007f, .023f), brass, collider: false);
            }
        }

        void DressAnnexLoadingBay(Transform annex)
        {
            // The original street paths remain open under both-storey loading rooms.
            foreach (var (x, z) in new[] { (23.4f, 4.6f), (23.4f, 23.4f), (38.6f, 4.6f), (38.6f, 23.4f) })
            {
                AnnexSurface(annex, "连廊承重砖柱", new(x, 1.70f, z), new(.56f, 3.40f, .64f), brick);
                AnnexSurface(annex, "连廊防撞柱脚", new(x, .21f, z), new(.68f, .42f, .71f), plaster);
            }
            StreetRoomProp(annex, "painted_wooden_bench", "装卸雨棚边候座", new(29.45f, 0, 23.05f),
                new(2.17f, .56f, .58f), 4);
            StreetRoomProp(annex, "metal_trash_can", "装卸区灰铁桶", new(23.98f, 0, 6.08f),
                new(.60f, .79f, .63f), -8);
            StreetRoomProp(annex, "tool_cart", "卸货旧平车", new(35.4f, 0, 22.65f),
                new(1.04f, .92f, .68f), -12);
            StreetRoomProp(annex, "cardboard_box_01", "货箱堆", new(32.05f, 0, 4.87f),
                new(.87f, .65f, .66f), 5);
            StreetRoomProp(annex, "cardboard_box_01", "压在货堆上的旧纸箱", new(32.08f, .65f, 4.87f),
                new(.55f, .51f, .46f), -12, false);
            WallSign("装卸间", new Vector3(28.7f, 3.18f, 4.03f), 0, new(2.10f, .34f), .16f);
        }

        Material AnnexGlazing()
        {
            var glass = Material(new Color(.18f, .23f, .22f, .25f));
            glass.name = "连廊旧玻璃";
            glass.SetFloat("_Mode", 3);
            glass.SetFloat("_Glossiness", .84f);
            glass.SetInt("_SrcBlend", (int)BlendMode.One);
            glass.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            glass.SetInt("_ZWrite", 0);
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            glass.renderQueue = (int)RenderQueue.Transparent;
            return glass;
        }

        void AnnexWall(Transform storey, float axis, float start, float end, float height, bool alongX,
            Material material, float? door = null, float top = AnnexCeiling)
        {
            if (!door.HasValue)
            {
                Segment(start, end, 0, top);
                return;
            }
            float left = door.Value - AnnexDoorHalf, right = door.Value + AnnexDoorHalf;
            Segment(start, left, 0, top);
            Segment(right, end, 0, top);
            Segment(left, right, AnnexHeadroom, top);
            Frame(left - .028f, .065f, AnnexHeadroom);
            Frame(right + .028f, .065f, AnnexHeadroom);
            Frame(door.Value, right - left + .12f, .067f, AnnexHeadroom - .012f);

            void Segment(float from, float to, float bottom, float topY)
            {
                if (to - from < .015f) return;
                float center = (from + to) * .5f;
                float y = height + (bottom + topY) * .5f;
                Vector3 position = alongX ? new(center, y, axis) : new(axis, y, center);
                Vector3 size = alongX ? new(to - from, topY - bottom, .18f) : new(.18f, topY - bottom, to - from);
                AnnexSurface(storey, "副楼墙体", position, size, material);
            }

            void Frame(float center, float width, float sizeY, float baseY = 0)
            {
                float y = height + baseY + sizeY * .5f;
                Vector3 position = alongX ? new(center, y, axis) : new(axis, y, center);
                Vector3 size = alongX ? new(width, sizeY, .24f) : new(.24f, sizeY, width);
                AnnexSurface(storey, "搪瓷门框", position, size, steel, collider: false);
            }
        }

        void AnnexDeck(Transform storey, Rect bounds, float height, string name, Material surface) =>
            AnnexSurface(storey, name, new(bounds.center.x, height - .11f, bounds.center.y),
                new(bounds.width, .22f, bounds.height), surface, floor: true);

        void AnnexRoof(Transform storey, Rect bounds, float height, string name, Material surface) =>
            AnnexSurface(storey, name, new(bounds.center.x, height + AnnexCeiling + .10f, bounds.center.y),
                new(bounds.width, .20f, bounds.height), surface);

        void AnnexSurface(Transform storey, string label, Vector3 position, Vector3 size, Material material,
            bool floor = false, bool collider = true)
        {
            var piece = Box(label, position, size, material, storey);
            piece.GetComponent<Collider>().enabled = collider;
            if (material.renderQueue == (int)RenderQueue.Transparent)
                piece.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Surface(piece, material, 1.7f, floor: floor);
        }
    }
}
