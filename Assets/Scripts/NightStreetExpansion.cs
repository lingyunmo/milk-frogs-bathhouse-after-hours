using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        internal static readonly (string Id, Vector3 Position, string Location)[] StreetSupplies =
        {
            ("cell-rear-shelter", new(5.66f, 1.29f, 37.64f), "后场工具间"),
            ("cell-noodle-alley", new(39.72f, .61f, -5.78f), "面馆侧巷"),
            ("cell-arcade", new(137.5f, .58f, -3.32f), "街尾商廊")
        };

        internal static readonly (string Name, Rect Bounds)[] StreetPockets =
        {
            ("后场检修棚", Rect.MinMaxRect(-3.75f, 32, 8.5f, 39.8f)),
            ("面馆侧巷", Rect.MinMaxRect(31.5f, -11.8f, 43.5f, 4)),
            ("转角商铺", Rect.MinMaxRect(61.5f, .15f, 87.6f, 23)),
            ("街对面值守点", Rect.MinMaxRect(108, -35.5f, 125, -23.3f)),
            ("街尾商廊", Rect.MinMaxRect(129, -6.3f, 146, 18.5f))
        };

        void DressStreetDistrict()
        {
            var district = new GameObject("石桥路沿街建筑").transform;
            district.SetParent(root, false);

            var structure = Pbr("室外锈蚀钢板", "Finishes/Service/SteelColor",
                "Finishes/Service/SteelNormal", "Finishes/Service/SteelFinish",
                new Color(.58f, .60f, .54f), .30f, true);
            void Dress(string asset, string label, Vector3 feet, Vector3 size, float yaw = 0, bool solid = true) =>
                Prop(asset, label, feet, size, yaw, solid).transform.SetParent(district, true);

            DressToolRoom(district);
            StreetShelter(district, "后场检修棚", new(-.1f, 0, 34.69f), 5.95f, 3.35f, structure, 1);
            Dress("metal_office_desk", "后场检修台", new(1.10f, 0, 35.25f), new(1.5f, .82f, .7f));
            Dress("painted_wooden_bench", "后场旧凳", new(-1.90f, 0, 35.17f), new(1.56f, .48f, .53f));
            Dress("tool_cart", "货棚侧面检修车", new(8.85f, 0, 35.95f), new(.9f, .91f, .48f), -12);
            WallSign("检修棚", new Vector3(-.10f, 2.64f, 32.94f), 0, new Vector2(1.4f, .31f), .15f);

            Dress("noodle-shop", "石桥路老面馆", new(36.06f, -.01f, -3.36f), new(4.8f, 8.01f, 13.21f), 180);
            StreetShelter(district, "面馆侧巷雨棚", new(40.32f, 0, -5.81f), 3.1f, 3.6f, structure, 1);
            Dress("painted_wooden_bench", "面馆候座木凳", new(39.72f, 0, -5.78f), new(1.36f, .56f, .54f));
            Dress("cardboard_box_01", "面馆回收纸箱", new(39.45f, 0, -8.36f), new(.76f, .64f, .63f), 14);
            Dress("metal_trash_can", "面馆巷口旧垃圾桶", new(42.21f, 0, -4.34f), new(.63f, .87f, .59f), -6);
            WallSign("面馆", new Vector3(36.05f, 2.78f, -10.03f), 0, new Vector2(1.9f, .33f), .17f);

            Dress("corner-shop-block", "转角三层商铺", new(72.8f, -.01f, 10.31f), new(18.2f, 13.4f, 18.55f), 180);
            Dress("kei-van", "商铺后巷的送货车", new(68.10f, 0, 23.14f), new(1.95f, 1.89f, 4.62f), -78);
            Dress("painted_wooden_bench", "转角人行道木凳", new(82.8f, 0, -.12f), new(2.25f, .50f, .63f), -8);
            Dress("metal_trash_can", "转角店垃圾桶", new(83.05f, 0, 18.29f), new(.7f, .89f, .67f), 14);
            StreetLampAt(new Vector3(81.4f, 2.71f, .52f), .67f, 5.4f);

            Dress("mixed-tenant-block", "路对面的商住楼", new(116.68f, -.01f, -30.60f), new(12.28f, 21.0f, 8.50f));
            StreetShelter(district, "街对面值守点", new(117.10f, 0, -25.88f), 5.44f, 3.06f, structure, -1);
            Dress("metal_office_desk", "街对面值守工作台", new(116.30f, 0, -25.75f), new(1.55f, .86f, .72f), 180);
            Dress("painted_wooden_chair_02", "值守凳", new(118.18f, 0, -26.31f), new(.61f, .83f, .56f), 13);
            Dress("metal_trash_can", "对街双层铁皮桶", new(119.0f, 0, -27.27f), new(.64f, .82f, .73f), 20);
            WallSign("夜班值守", new Vector3(117.1f, 2.64f, -24.28f), 180, new Vector2(1.91f, .34f), .16f);

            Dress("mixed-tenant-block", "街尾旧商住楼", new(137.5f, -.01f, 9.11f), new(12.28f, 24.8f, 11.62f), 180);
            StreetShelter(district, "街尾商廊", new(137.65f, 0, -3.13f), 7.50f, 3.39f, structure, 1);
            Dress("painted_wooden_bench", "商廊避雨座椅", new(137.5f, 0, -3.32f), new(2.42f, .53f, .61f));
            Dress("metal_trash_can", "商廊东端污物桶", new(141.92f, 0, -1.92f), new(.72f, .86f, .69f), -11);
            Dress("kei-van", "街尾临时停靠货车", new(146.22f, 0, 2.61f), new(2.0f, 1.99f, 4.53f), 90);
            WallSign("石桥商廊", new Vector3(137.65f, 2.62f, -4.88f), 0, new Vector2(1.96f, .33f), .15f);

            // Silhouettes continue beyond the playable alleys without inserting collision into the shortcuts.
            Dress("public-housing-slab", "后巷外住宅楼", new(37, -.38f, 50.6f), new(49.0f, 18.3f, 11.7f), 180, false);
            Dress("public-housing-slab", "菜市后旧住宅楼", new(104, -.25f, 51.1f), new(53.0f, 21.2f, 12.9f), 180, false);
            Dress("public-housing-slab", "街尾远处住宅楼", new(161, -.25f, 50.8f), new(42.0f, 16.4f, 11.9f), 180, false);

            StreetWindowGlow("noodle-shop", 2, .44f);
            StreetWindowGlow("corner-shop-block", 0, .29f);
            StreetWindowGlow("mixed-tenant-block", 1, .25f);
            foreach (var supply in StreetSupplies)
                Pickup(supply.Id, supply.Position, new Vector3(.12f, .09f, .12f), olive);
        }

        void DressToolRoom(Transform district)
        {
            var feet = new Vector3(5.4f, -.01f, 36.54f);
            var shed = Prop("tool-shed", "后场老木工棚", feet, new Vector3(4.3f, 3.17f, 4.15f), 180, false);
            shed.transform.SetParent(district, true);

            var renderers = shed.GetComponentsInChildren<Renderer>();
            var left = System.Array.Find(renderers, renderer => renderer.name.StartsWith("door_L_mesh", System.StringComparison.Ordinal));
            var right = System.Array.Find(renderers, renderer => renderer.name.StartsWith("door_R_mesh", System.StringComparison.Ordinal));
            if (left && right && (left.bounds.center.z + right.bounds.center.z) * .5f > feet.z)
                shed.transform.Rotate(0, 180, 0);
            foreach (var leaf in new[] { left, right })
            {
                if (!leaf) continue;
                var hinge = leaf.transform.parent;
                if (!hinge || !hinge.name.StartsWith("door_", System.StringComparison.Ordinal))
                {
                    leaf.enabled = false;
                    continue;
                }

                hinge.Rotate(Vector3.up, leaf.bounds.center.x > feet.x ? -94f : 94f, Space.World);
                if (leaf.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
                {
                    var bounds = filter.sharedMesh.bounds;
                    var collider = leaf.gameObject.AddComponent<BoxCollider>();
                    collider.center = bounds.center;
                    collider.size = bounds.size;
                }
            }

            // Support the model's existing raised plinth and its shallow step at the south doorway.
            CollisionBox(shed.transform, "工具间石地台", new Vector3(5.4f, .22f, 36.665f), new Vector3(3.8f, .46f, 3.36f));
            CollisionBox(shed.transform, "工具间门口台阶", new Vector3(5.4f, .132f, 34.73f), new Vector3(1.94f, .284f, .55f));
            CollisionBox(shed.transform, "工具间后墙", new Vector3(5.4f, 1.35f, 38.28f), new Vector3(3.8f, 2.70f, .18f));
            foreach (int side in new[] { -1, 1 })
            {
                CollisionBox(shed.transform, "工具间侧墙", new Vector3(5.4f + side * 1.83f, 1.48f, 36.65f),
                    new Vector3(.18f, 2.96f, 3.38f));
                CollisionBox(shed.transform, "工具间门垛", new Vector3(5.4f + side * 1.36f, 1.48f, 35.02f),
                    new Vector3(1.03f, 2.96f, .19f));
            }

            Prop("metal_office_desk", "工具间备品架", new Vector3(5.66f, .45f, 37.64f),
                new Vector3(1.39f, .79f, .58f), 0).transform.SetParent(district, true);
            StreetLampAt(new Vector3(5.12f, 2.28f, 36.88f), .83f, 3.45f);
        }

        void StreetShelter(Transform district, string name, Vector3 feet, float width, float depth,
            Material steelSheet, int northBack)
        {
            const float height = 2.62f;
            Vector3 roof = feet + Vector3.up * height;
            var canopy = Box(name + "顶棚", roof, new Vector3(width, .13f, depth), steelSheet, district);
            canopy.transform.rotation = Quaternion.Euler(northBack * 3.5f, 0, 0);
            Surface(canopy, steelSheet, 1.75f);
            foreach (int side in new[] { -1, 1 })
            foreach (int end in new[] { -1, 1 })
            {
                Vector3 post = feet + new Vector3(width * .46f * side, height * .49f, depth * .43f * end);
                Surface(Box(name + "支架", post, new Vector3(.11f, height, .11f), steelSheet, district), steelSheet, 1.45f);
            }
            Vector3 back = feet + new Vector3(0, .69f, depth * .43f * northBack);
            Surface(Box(name + "矮挡墙", back, new Vector3(width * .86f, 1.38f, .18f), brick, district), brick, 1.7f);
            StreetLampAt(roof + new Vector3(0, -.21f, depth * .09f * northBack), .90f, 5.15f);
        }

        void StreetLampAt(Vector3 point, float intensity, float range)
        {
            var lamp = Prop("industrial_wall_sconce", "街巷罩灯", point + Vector3.down * .13f,
                new Vector3(.17f, .25f, .21f), 0, false);
            LightSconce(lamp);
            Fixture(point + Vector3.down * .08f, Vector3.down, new Color(.95f, .72f, .46f), intensity, range, 112, .0045f);
        }

        void StreetWindowGlow(string asset, int slot, float strength)
        {
            var surface = propMaterials[asset][slot];
            surface.EnableKeyword("_EMISSION");
            surface.SetTexture("_EmissionMap", surface.mainTexture);
            surface.SetColor("_EmissionColor", new Color(1f, .79f, .55f) * strength);
        }
    }
}
