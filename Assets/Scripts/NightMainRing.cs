using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal static class NightMainRing
    {
        internal const float Front = -12.8f;
        internal static readonly Rect Footprint = Rect.MinMaxRect(-20, Front, 12, 0);
        internal static readonly Rect BasementConnector = Rect.MinMaxRect(-24, Front, -20, 14);
        static readonly Rect North = Rect.MinMaxRect(-20, -3.4f, 12, 0);
        static readonly Rect South = Rect.MinMaxRect(-20, Front, 12, -9.2f);
        static readonly Rect West = Rect.MinMaxRect(-20, -9.2f, -16.5f, -3.4f);
        static readonly Rect East = Rect.MinMaxRect(8.5f, -9.2f, 12, -3.4f);
        internal static readonly string[] NorthNames = { "设备环廊北侧", "迎宾环廊北侧", "休养环廊北侧", "员工环廊北侧" };
        internal static readonly string[] SouthNames = { "设备环廊南侧", "迎宾环廊南侧", "休养环廊南侧", "员工环廊南侧" };
        internal static readonly string[] Wings = { "设备层", "迎宾层", "休养层", "员工层" };

        internal readonly struct Room
        {
            internal readonly string Name;
            internal readonly Rect Bounds;
            internal readonly bool Open;
            internal Room(string name, float left, float right, bool open = false)
            {
                Name = name;
                Bounds = Rect.MinMaxRect(left, -9.2f, right, -3.4f);
                Open = open;
            }
        }

        internal static readonly Room[][] Rooms =
        {
            new Room[] { new("阀件库", -16.5f, -8.2f), new("南侧机房", -8.2f, 0), new("耗材间", 0, 8.5f) },
            new Room[] { new("迎宾中庭", -16.5f, 8.5f, true) },
            new Room[] { new("静养室", -16.5f, -8.2f), new("茶歇室", -8.2f, 0), new("理疗室", 0, 8.5f) },
            new Room[] { new("员工餐室", -16.5f, -8.2f), new("员工起居区", -8.2f, 0, true), new("夜班会商室", 0, 8.5f) }
        };

        internal static readonly (string Id, Vector3 Target, string Place)[] Batteries =
        {
            ("cell-ring-lobby", new(-5.7f, .90f, -6.55f), "迎宾中庭"),
            ("cell-ring-therapy", new(5.7f, 4.50f, -6.82f), "理疗室")
        };

        // Old rooms serve as separate entrances to the new south wing. The east bridges stay on the old hall.
        internal static readonly float[][] SouthPassages =
        {
            new[] { 9.65f }, new[] { -17f, 10.5f }, new[] { -13.65f, 3.4f }, new[] { -11.6f, 5.4f }
        };

        internal static IEnumerable<(string Name, Rect Bounds, bool Route, Vector2? Entrance)> Areas(int floor)
        {
            yield return (NorthNames[floor], North, true, null);
            yield return (SouthNames[floor], South, true, null);
            yield return ("环廊西侧", West, true, null);
            yield return ("环廊东侧", East, true, null);
            if (floor == 0) yield return ("地下勤务通道", BasementConnector, true, new Vector2(-20, 12));
            foreach (var room in Rooms[floor])
                yield return (room.Name, room.Bounds, room.Open,
                    room.Open ? null : new Vector2(room.Bounds.center.x, room.Bounds.yMax));
        }
    }

    internal sealed partial class NightWorld
    {
        const float RingWallHeight = 3.45f;
        const float RingDoorHalf = 1.17f;
        const float RingDoorHeight = 2.47f;

        void BuildMainRing()
        {
            var parent = new GameObject("主楼南翼").transform;
            parent.SetParent(root, false);
            Transform bathhouse = GameObject.Find("浴场").transform;
            for (int index = 0; index < NightMainRing.Wings.Length; index++)
            {
                float height = FloorRoutes.LevelHeight(index - 1);
                var level = new GameObject(NightMainRing.Wings[index] + "南翼").transform;
                level.SetParent(parent, false);
                var original = FindOriginalFloor(height);
                foreach (float x in NightMainRing.SouthPassages[index])
                    OpenMainRingPassage(original, level, height, true, 0, x);
                if (index == 0) OpenMainRingPassage(original, level, height, false, -20, 12);
                RingShell(level, index, height);
                FurnishMainRing(level, index, height);
            }

            foreach (var (id, target, _) in NightMainRing.Batteries)
                Pickup(id, target - Vector3.up * .033f, new(.12f, .09f, .11f), olive);

            Transform FindOriginalFloor(float height)
            {
                foreach (Transform floor in bathhouse)
                {
                    var hall = floor.Find("横廊");
                    if (hall && Mathf.Abs(hall.position.y + .10f - height) < .03f) return floor;
                }
                throw new InvalidOperationException($"No original bathhouse floor at {height}.");
            }
        }

        void OpenMainRingPassage(Transform original, Transform level, float height, bool alongX, float axis, float center)
        {
            foreach (var wall in original.GetComponentsInChildren<BoxCollider>())
            {
                if (wall.name != "墙") continue;
                Bounds bounds = wall.bounds;
                float middle = alongX ? bounds.center.z : bounds.center.x;
                float begin = alongX ? bounds.min.x : bounds.min.z;
                float end = alongX ? bounds.max.x : bounds.max.z;
                if (Mathf.Abs(axis - middle) > .045f || bounds.min.y > height + .10f || bounds.max.y < height + 3 ||
                    center - RingDoorHalf <= begin || center + RingDoorHalf >= end)
                    continue;
                wall.gameObject.SetActive(false);
                RingWall(level, alongX, axis, begin, end, height, brick, center);
                return;
            }
            throw new InvalidOperationException($"Cannot open main ring passage at {height}, {axis}, {center}.");
        }

        void RingShell(Transform level, int index, float height)
        {
            Rect bounds = NightMainRing.Footprint;
            Material paving = index is 0 or 3 ? index == 0 ? plaster : planks : terrazzo;
            Material partitions = index switch { 0 => glazed, 1 => paint, 2 => glazed, _ => paint };
            AnnexDeck(level, bounds, height, "南翼连续地坪", paving);
            AnnexRoof(level, bounds, height, "南翼顶板", ceiling);
            RingWall(level, true, NightMainRing.Front, index == 0 ? -24 : bounds.xMin, bounds.xMax,
                height, brick, index == 1 ? 0 : null);
            RingWall(level, false, bounds.xMax, bounds.yMin, 0, height, brick);
            if (index != 0)
                RingWall(level, false, bounds.xMin, bounds.yMin, 0, height, brick);
            else
            {
                Rect service = NightMainRing.BasementConnector;
                AnnexDeck(level, service, height, "地下勤务通道地坪", plaster);
                AnnexRoof(level, service, height, "地下勤务通道顶板", ceiling);
                RingWall(level, false, service.xMin, service.yMin, service.yMax, height, plaster);
                RingWall(level, true, service.yMax, service.xMin, service.xMax, height, plaster);
                foreach (float z in new[] { -8f, 2f, 11.8f })
                    Fixture(new Vector3(-22, height + 2.75f, z), Vector3.down,
                        new Color(.74f, .81f, .77f), .74f, 5.4f, 108, .006f);
            }

            var rooms = NightMainRing.Rooms[index];
            for (int i = 0; i < rooms.Length; i++)
            {
                var room = rooms[i];
                Rect zone = room.Bounds;
                if (!room.Open)
                {
                    float middle = zone.center.x;
                    float southDoor = middle + (index == 0 ? .72f : -.82f);
                    RingWall(level, true, zone.yMin, zone.xMin, zone.xMax, height, partitions, southDoor);
                    RingWall(level, true, zone.yMax, zone.xMin, zone.xMax, height, partitions, middle);
                    LabelRoom(room.Name, new Vector3(middle, height, zone.yMax), true);
                    WallSign(room.Name, new Vector3(middle, height + 2.67f, zone.yMax + .14f),
                        180, new Vector2(1.24f, .22f), .095f);
                    WallSign(room.Name, new Vector3(southDoor, height + 2.67f, zone.yMin - .14f),
                        0, new Vector2(1.24f, .22f), .095f);
                }
                if (i == 0 || !rooms[i - 1].Open || !room.Open)
                {
                    if (i != 0 || !room.Open)
                        RingWall(level, false, zone.xMin, zone.yMin, zone.yMax, height, partitions);
                }
                if (i == rooms.Length - 1 && !room.Open)
                    RingWall(level, false, zone.xMax, zone.yMin, zone.yMax, height, partitions);
                atmosphere.AddProbe(new Vector3(zone.center.x, height + 1.55f, zone.center.y),
                    new Vector3(zone.width, 3.25f, zone.height));
            }

            string wing = NightAtlas.FloorNames[index] + " · " + NightMainRing.Wings[index];
            foreach (float x in index == 0 ? new[] { -22f, 9.65f } :
                index == 1 ? new[] { -17f, 0f, 10.5f } : NightMainRing.SouthPassages[index])
            {
                WallSign(wing, new Vector3(x, height + 2.7f, .17f), 180, new Vector2(1.85f, .25f), .106f);
                WallSign(wing, new Vector3(x, height + 2.7f, -.17f), 0, new Vector2(1.85f, .25f), .106f);
            }
        }

        void RingWall(Transform level, bool alongX, float axis, float start, float end, float height, Material material,
            float? door = null)
        {
            if (door.HasValue)
            {
                float left = door.Value - RingDoorHalf, right = door.Value + RingDoorHalf;
                Section("南翼隔墙", start, left, 0, RingWallHeight, material, .18f);
                Section("南翼隔墙", right, end, 0, RingWallHeight, material, .18f);
                Section("门洞上沿", left, right, RingDoorHeight, RingWallHeight, material, .18f);
                Section("旧钢门楣", left - .055f, right + .055f, RingDoorHeight, RingDoorHeight + .07f, steel, .24f);
            }
            else Section("南翼隔墙", start, end, 0, RingWallHeight, material, .18f);

            void Section(string name, float a, float b, float floor, float top, Material surface, float thickness)
            {
                if (b - a < .01f) return;
                float center = (a + b) * .5f;
                float y = height + (floor + top) * .5f;
                Vector3 position = alongX ? new(center, y, axis) : new(axis, y, center);
                Vector3 size = alongX ? new(b - a, top - floor, thickness) : new(thickness, top - floor, b - a);
                AnnexSurface(level, name, position, size, surface);
            }
        }

        void FurnishMainRing(Transform level, int index, float height)
        {
            void Add(string asset, string name, float x, float z, Vector3 size, float yaw = 0,
                float rise = 0, bool solid = true) =>
                StreetRoomProp(level, asset, name, new Vector3(x, height + rise, z), size, yaw, solid);

            Color lampTint = index switch
            {
                0 => new Color(.68f, .81f, .78f),
                1 => new Color(.96f, .78f, .54f),
                2 => new Color(.70f, .82f, .73f),
                _ => new Color(.91f, .72f, .48f)
            };
            foreach (var (x, z) in new[] { (-18.2f, -6.35f), (10.35f, -6.35f), (-10f, -10.9f), (3.7f, -10.9f), (-10f, -1.7f), (3.7f, -1.7f) })
            {
                Vector3 light = new(x, height + 2.89f, z);
                AddMainsLight(light, lampTint, index == 1 ? .88f : .64f, 5.0f);
            }
            Fixture(new Vector3(10.95f, height + 2.40f, -8.4f), new Vector3(-.7f, -1, -.24f),
                lampTint, .59f, 4.7f, 104, .006f);
            Fixture(new Vector3(-19.38f, height + 2.45f, -4.55f), new Vector3(.64f, -1, .4f),
                lampTint, .54f, 4.55f, 104, .006f);

            foreach (var room in NightMainRing.Rooms[index])
            {
                Rect zone = room.Bounds;
                float x = zone.center.x;
                if (room.Open && index == 1)
                {
                    Add("painted_wooden_bench", "中庭等候长凳", -11, -5.36f, new Vector3(2.85f, .50f, .61f));
                    Add("sofa_02", "中庭会客沙发", -11, -7.93f, new Vector3(2.85f, 1.07f, 1.10f));
                    Add("WoodenTable_01", "中庭等候茶几", -11.4f, -6.56f, new Vector3(1.42f, .46f, .62f));
                    Add("metal_office_desk", "中庭失物登记台", -5.7f, -6.55f, new Vector3(2.02f, .84f, .78f));
                    Add("potted_plant_01", "中庭盆栽", -2.9f, -5.36f, new Vector3(.66f, 1.45f, .65f), 16);
                    Add("potted_plant_01", "中庭盆栽", 4.3f, -7.9f, new Vector3(.62f, 1.29f, .61f), -19);
                    Add("painted_wooden_bench", "中庭靠边长凳", 6.4f, -6.4f, new Vector3(2.32f, .54f, .58f), 90);
                    Add("painted_wooden_chair_02", "中庭靠边木椅", 4.76f, -5.14f,
                        new Vector3(.58f, .87f, .54f), 124);
                    StreetRoomLight(level, new Vector3(-5.75f, height + 2.73f, -5.7f),
                        new Vector3(-.35f, -1, -.2f), lampTint, .72f);
                    continue;
                }

                if (index == 0)
                {
                    Add("worn_metal_rack", "设备南翼金属架", zone.xMin + .77f, -6.6f,
                        new Vector3(2.32f, 1.89f, .60f), 90);
                    Add(room.Name == "南侧机房" ? "portable_generator" : "tool_cart", "南翼停用设备",
                        zone.xMax - 1.35f, -6.85f, new Vector3(1.10f, .97f, .75f), -12);
                    Add("cardboard_box_01", "设备南翼装箱", zone.xMin + 1.45f, -7.95f, new Vector3(.60f, .39f, .50f));
                }
                else if (index == 2)
                {
                    Add(room.Name == "理疗室" ? "vintage_day_bed" : "sofa_02", "休养层软座", zone.xMin + 1.50f,
                        -6.38f, new Vector3(2.10f, .85f, .86f), 90);
                    float tableX = room.Name == "理疗室" ? 5.7f : zone.xMax - 1.50f;
                    const float tableZ = -6.82f;
                    Add("WoodenTable_01", "休养层置物台", tableX, tableZ, new Vector3(1.80f, .84f, .74f), 90);
                    Add("painted_wooden_chair_02", "休养层候诊椅", x + .75f, -7.9f,
                        new Vector3(.51f, .86f, .51f), 8);
                    if (room.Name != "理疗室")
                        Add("tea_set_01", "休养层茶具", tableX, tableZ, new Vector3(.36f, .17f, .26f), -12, .84f, false);
                }
                else
                {
                    Add(room.Open ? "sofa_02" : "WoodenTable_01", room.Open ? "员工旧沙发" : "员工长桌",
                        zone.xMin + 1.58f, -6.28f, new Vector3(2.50f, room.Open ? 1.02f : .81f, .98f), 90);
                    if (room.Open)
                    {
                        Add("WoodenTable_01", "员工电视矮桌", zone.xMax - 1.40f, -7.25f,
                            new Vector3(1.36f, .47f, .61f), -90);
                        Add("television_02", "员工旧电视", zone.xMax - 1.40f, -7.25f,
                            new Vector3(.64f, .50f, .43f), -90, .47f, false);
                        Add("WoodenTable_01", "起居区矮茶几", x - .68f, -6.85f,
                            new Vector3(1.24f, .46f, .61f));
                        Add("tea_set_01", "员工茶具", x - .68f, -6.85f,
                            new Vector3(.34f, .16f, .29f), 15, .46f, false);
                    }
                    else
                        Add("worn_metal_rack", "员工储物架", zone.xMax - 1.40f, -7.25f,
                            new Vector3(2.04f, 1.82f, .61f), -90);
                    Add("painted_wooden_chair_02", "员工木椅", zone.xMin + 2.24f, -8.09f,
                        new Vector3(.55f, .86f, .56f), 15);
                }
                Fixture(new Vector3(x + 1.7f, height + 2.74f, -5.7f),
                    new Vector3(-.36f, -1, -.34f), lampTint, .59f, 4.7f, 112, .004f);
            }
        }
    }
}
