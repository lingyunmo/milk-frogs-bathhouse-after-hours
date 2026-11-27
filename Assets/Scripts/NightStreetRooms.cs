using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        internal static readonly (string Name, Rect Bounds)[] StreetRoomAreas =
        {
            ("后巷洗衣店", Rect.MinMaxRect(24, 31, 39, 40)),
            ("停车棚泵房", Rect.MinMaxRect(45, 11, 56, 19)),
            ("街尾维修站", Rect.MinMaxRect(150, 1, 165, 11))
        };

        internal static readonly (string Id, Vector3 Position, string Location)[] StreetRoomSupplies =
        {
            ("cell-street-laundry", new(34.6f, .90f, 38.66f), "后巷洗衣店"),
            ("cell-street-pump", new(53.52f, .91f, 15.98f), "停车棚泵房"),
            ("cell-street-workshop", new(160, .91f, 9.86f), "街尾维修站")
        };

        void DressStreetRooms()
        {
            var laundry = StreetRoomShell(StreetRoomAreas[0], 29, 37, true, plaster, tiles);
            StreetRoomProp(laundry, "rollershutter_window_01", "洗衣店卷帘窗", new(35.46f, .95f, 30.87f),
                new(3.48f, 1.39f, .17f), 0, false);
            StreetRoomProp(laundry, "washing-machine", "洗衣滚筒", new(26.2f, .02f, 38.46f),
                new(1.17f, 1.23f, 1.10f), 180);
            StreetRoomProp(laundry, "washing-machine", "待修洗衣滚筒", new(27.63f, .02f, 38.44f),
                new(1.15f, 1.23f, 1.10f), 180);
            StreetRoomProp(laundry, "laundry-basket", "洗衣店布草篮", new(28.72f, .02f, 38.84f),
                new(.71f, .82f, .74f), 13);
            StreetRoomProp(laundry, "metal_office_desk", "洗衣店收付工作台", new(34.6f, .02f, 38.66f),
                new(2.20f, .82f, .75f), 180);
            StreetRoomProp(laundry, "folded-towel-stack", "未收取的旧床单", new(34.01f, .84f, 38.66f),
                new(.48f, .19f, .44f), 8, false);
            StreetRoomProp(laundry, "tool_cart", "洗衣店布草车", new(25.48f, .02f, 34.82f),
                new(.96f, .88f, .59f), 12);
            StreetRoomSurface(laundry, "洗衣店换洗隔断", new(32.34f, 1.06f, 34.81f),
                new(.19f, 2.11f, 3.16f), glazed, 1.05f);
            StreetRoomLight(laundry, new(26.38f, 2.6f, 37.32f), new(.18f, -1, -.11f),
                new(.66f, .79f, .87f), 1.42f);
            StreetRoomLight(laundry, new(35.71f, 2.60f, 38.07f), new(-.28f, -1, -.56f),
                new(.96f, .77f, .51f), .72f);

            var pump = StreetRoomShell(StreetRoomAreas[1], 48, 53, false, paint, plaster);
            StreetRoomProp(pump, "metal_office_desk", "泵房检修工作台", new(53.52f, .02f, 15.98f),
                new(1.66f, .83f, .67f), 180);
            StreetRoomProp(pump, "portable_generator", "断开的排水电机", new(54.38f, .02f, 12.47f),
                new(1.29f, 1.1f, .88f), -17);
            StreetRoomProp(pump, "metal-bucket", "接水铁桶", new(53.13f, .02f, 12.92f),
                new(.43f, .48f, .46f), 0);
            StreetRoomProp(pump, "worn_metal_rack", "泵房管件架", new(55.47f, .02f, 17.06f),
                new(1.70f, 1.8f, .54f), 90);
            StreetRoomProp(pump, "cardboard_box_01", "拆下的过滤芯", new(53.93f, .85f, 16.10f),
                new(.37f, .25f, .31f), 18, false);
            DressPumpCrawlspace(pump);
            StreetRoomLight(pump, new(47.56f, 2.59f, 13.73f), new(.19f, -1, .29f),
                new(1f, .65f, .35f), 1.2f);
            StreetRoomLight(pump, new(52.85f, 2.64f, 16.11f), new(.2f, -1, -.08f),
                new(.79f, .85f, .67f), .78f);

            var workshop = StreetRoomShell(StreetRoomAreas[2], 154, 8, true, brick, terrazzo);
            StreetRoomProp(workshop, "metal_office_desk", "维修站零件台", new(160, .02f, 9.86f),
                new(2.4f, .83f, .70f), 180);
            StreetRoomProp(workshop, "vintage_radio_transceiver", "停用的出勤无线电", new(160.66f, .85f, 9.86f),
                new(.57f, .22f, .38f), 180, false);
            StreetRoomProp(workshop, "worn_metal_rack", "维修站螺栓架", new(162.49f, .02f, 10.58f),
                new(2.8f, 1.83f, .54f), 180);
            StreetRoomProp(workshop, "painted_wooden_bench", "维修站等候凳", new(151.37f, .02f, 8.71f),
                new(2.2f, .56f, .55f), 90);
            StreetRoomProp(workshop, "tool_cart", "未推回的维修车", new(160.34f, .02f, 3.79f),
                new(1.23f, 1.04f, .68f), -17);
            StreetRoomSurface(workshop, "维修站工具屏", new(156.98f, 1.01f, 5.97f),
                new(3.33f, 2.01f, .18f), paint, 1.55f);
            StreetRoomProp(workshop, "locker-room-tall-lockers", "屏后值班工具柜", new(157.35f, .02f, 6.44f),
                new(2.08f, 1.94f, .58f), 0);
            StreetRoomProp(workshop, "rollershutter_window_01", "维修站卷帘窗", new(161.3f, .96f, .87f),
                new(2.87f, 1.27f, .19f), 0, false);
            StreetRoomLight(workshop, new(153.37f, 2.60f, 3.88f), new(.29f, -1, .09f),
                new(.79f, .91f, .72f), 1.27f);
            StreetRoomLight(workshop, new(159.87f, 2.60f, 9.6f), new(.15f, -1, .02f),
                new(1f, .77f, .47f), .90f);

            foreach (var (id, position, _) in StreetRoomSupplies)
                Pickup(id, position, new(.12f, .09f, .12f), olive);
        }

        Transform StreetRoomShell((string Name, Rect Bounds) area, float frontDoor, float backDoor, bool eastExit,
            Material walls, Material floor)
        {
            const float height = 3.16f;
            const float doorHeight = 2.36f;
            const float doorHalfWidth = .98f;
            var (name, bounds) = area;
            var building = new GameObject(name).transform;
            building.SetParent(root, false);

            StreetRoomSurface(building, "连续室内地坪",
                new(bounds.center.x, -.09f, bounds.center.y), new(bounds.width, .22f, bounds.height), floor, 1.55f, true);
            StreetRoomSurface(building, "沿街房屋屋顶",
                new(bounds.center.x, height + .04f, bounds.center.y),
                new(bounds.width + .54f, .25f, bounds.height + .54f), plaster, 1.8f);
            StreetRoomSurface(building, "入口遮雨檐",
                new(frontDoor, doorHeight + .14f, bounds.yMin - .43f), new(2.88f, .16f, 1.04f), steel, 1.12f);
            WallSign(name, new(frontDoor, 2.80f, bounds.yMin - .14f), 0, new(2.16f, .35f), .17f);

            void Segment(float start, float end, float fixedAxis, bool alongX, float bottom, float top)
            {
                float center = (start + end) * .5f;
                float altitude = (bottom + top) * .5f;
                Vector3 position = alongX ? new(center, altitude, fixedAxis) : new(fixedAxis, altitude, center);
                Vector3 size = alongX ? new(end - start, top - bottom, .20f) : new(.20f, top - bottom, end - start);
                StreetRoomSurface(building, "实心墙体", position, size, walls, 1.65f);
            }

            void Side(float start, float end, float fixedAxis, bool alongX, float? opening = null)
            {
                if (!opening.HasValue)
                {
                    Segment(start, end, fixedAxis, alongX, 0, height);
                    return;
                }
                float left = opening.Value - doorHalfWidth;
                float right = opening.Value + doorHalfWidth;
                Segment(start, left, fixedAxis, alongX, 0, height);
                Segment(right, end, fixedAxis, alongX, 0, height);
                Segment(left, right, fixedAxis, alongX, doorHeight, height);
            }

            Side(bounds.xMin, bounds.xMax, bounds.yMin, true, frontDoor);
            Side(bounds.xMin, bounds.xMax, bounds.yMax, true, eastExit ? null : backDoor);
            Side(bounds.yMin, bounds.yMax, bounds.xMin, false);
            Side(bounds.yMin, bounds.yMax, bounds.xMax, false, eastExit ? backDoor : null);
            return building;
        }

        void DressPumpCrawlspace(Transform pump)
        {
            StreetRoomSurface(pump, "泵房水管检修台面", new(46.69f, .98f, 17.39f),
                new(2.68f, .36f, 2.47f), plaster, 1.30f);
            StreetRoomSurface(pump, "检修台左侧支撑", new(45.51f, .42f, 17.39f),
                new(.22f, .84f, 2.45f), plaster, 1.30f);
            StreetRoomSurface(pump, "检修台右侧支撑", new(47.88f, .42f, 17.39f),
                new(.22f, .84f, 2.45f), plaster, 1.30f);
            StreetRoomSurface(pump, "检修台内挡墙", new(46.69f, .42f, 18.48f),
                new(2.39f, .84f, .19f), plaster, 1.30f);
            StreetRoomProp(pump, "tool_cart", "遮挡检修台的拆件推车", new(47.52f, .02f, 15.83f),
                new(.83f, 1.0f, .58f), 0);
        }

        void StreetRoomSurface(Transform building, string label, Vector3 center, Vector3 size, Material material,
            float metres, bool floor = false)
        {
            Surface(Box(label, center, size, material, building), material, metres, floor: floor);
        }

        void StreetRoomProp(Transform building, string asset, string label, Vector3 at, Vector3 size,
            float yaw, bool solid = true)
        {
            Prop(asset, label, at, size, yaw, solid).transform.SetParent(building, true);
        }

        void StreetRoomLight(Transform building, Vector3 point, Vector3 direction, Color colour, float intensity)
        {
            bool thirdFloor = IsThirdFloorFixture(point);
            var lamp = Prop("industrial_wall_sconce", "旧式防护罩灯", point + Vector3.down * .045f,
                new(.20f, .31f, .25f), 0, false);
            lamp.transform.SetParent(building, true);
            LightSconce(lamp, thirdFloor ? .36f : 1f);
            Fixture(point + Vector3.down * .20f, direction, colour, intensity,
                thirdFloor ? 4.25f : 5.5f, thirdFloor ? 96 : 108, .0045f);
        }
    }
}
