using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        void DressPursuitRoutes()
        {
            var street = new GameObject("街区追逐掩体").transform;
            street.SetParent(root, false);
            var rooms = new GameObject("楼内勤务陈设").transform;
            rooms.SetParent(root, false);

            // Keep the centre of each road open. Alternate the sheltered shoulder so a
            // runner has to cross open ground to reach the next break in the sightline.
            void Street(string asset, string name, float x, float z, Vector3 size, float yaw)
            {
                var prop = Prop(asset, name, new(x, 0, z), size, yaw, solid: asset != "sandbag-wall-straight");
                prop.transform.SetParent(street, true);
            }
            void Sacks(string name, float x, float z, float yaw) =>
                Street("sandbag-wall-straight", name, x, z, new(2.15f, 1.17f, .67f), yaw);
            void Bin(string name, float x, float z, float yaw) =>
                Street("dumpster", name, x, z, new(1.43f, 1.27f, 1.07f), yaw);
            void Van(string name, float x, float z, float yaw) =>
                Street("kei-van", name, x, z, new(1.94f, 1.88f, 4.56f), yaw);
            void Cabinet(string name, float x, float z, float yaw) =>
                Street("utility_box_02", name, x, z, new(1.39f, 1.15f, .62f), yaw);

            Bin("面馆泔水回收车", 37, -12.8f, 12);
            Van("送冰车", 54, -19.8f, 83);
            Bin("商铺街口旧斗车", 67, -12.3f, -18);
            Cabinet("车棚路肩电箱", 91, -19.5f, -6);
            Sacks("站台积水挡袋", 107, -12.8f, 14);
            Van("值守点巡线车", 122, -19.8f, 104);
            Bin("商廊收运车", 151, -12.9f, -16);

            Sacks("堤口第一道挡水袋", 185, -14.6f, 13);
            Sacks("堤口挡水袋折角", 186.38f, -13.49f, 67);
            Van("巡闸班车", 203, -21.6f, 82);
            Cabinet("水文站路边接线柜", 222, -14.5f, -9);
            Sacks("石阶旁护堤袋", 239, -21.3f, -17);
            Sacks("石阶旁第二垛", 240.84f, -21.90f, -18);
            Bin("暗房废料斗", 255, -14.5f, 5);
            Sacks("宿舍门前防汛袋", 284, -14.5f, -16);
            Van("堤岸值班面包车", 302, -21.4f, 96);
            Sacks("闸井备料", 319, -14.4f, 18);
            Cabinet("堤尾断电箱", 332, -21.3f, -7);

            Sacks("步道口旧防汛垛", 181, 1.0f, 18);
            Bin("测站换班回收车", 227, 6.9f, -10);
            Cabinet("堤边水位接线柜", 273, 1.3f, 21);
            Sacks("值班员卸下的沙袋", 306, .9f, -17);
            Sacks("泵站外备用防汛袋", 321, 7.1f, 8);
            Cabinet("前院门边配电箱", 16.4f, -7.2f, -8);
            Bin("西通道清运车", -26.0f, 7.2f, 12);
            Bin("后巷收衣车旁的回收斗", 17.6f, 31.3f, -17);

            void Inside(string asset, string name, Vector3 feet, Vector3 size, float yaw = 0, bool solid = true)
            {
                var prop = Prop(asset, name, feet, size, yaw, solid && asset != "torn-hospital-privacy-screen");
                prop.transform.SetParent(rooms, true);
            }
            void Curtain(string name, Vector3 feet, float yaw) =>
                Inside("torn-hospital-privacy-screen", name, feet, new(1.74f, 1.76f, .31f), yaw);

            // The two issue counters carry ordinary single-use battery pickups.
            Inside("kitchen-base-unit-drawers", "耗材发放柜", new(2.6f,-3.6f,-5.45f), new(1.32f,.88f,.67f), 78);
            Inside("metal_toolbox", "退换的保险丝盒", new(2.6f,-2.72f,-5.13f), new(.36f,.19f,.23f), 81, false);
            Inside("locker-room-tall-lockers", "机房隔油工服柜", new(-5.9f,-3.6f,-6.04f), new(1.51f,1.79f,.53f), -13);
            Inside("metal-bucket", "阀件库接漏桶", new(-10.23f,-3.6f,-5.4f), new(.39f,.41f,.40f), 7);
            Inside("tool_cart", "地下环廊待送零件车", new(-14.6f,-3.6f,-11.94f), new(1.05f,.92f,.63f), 5);
            Inside("vintage_suitcase", "迎宾廊落下的行李", new(-8.0f,0,-.60f), new(.87f,.58f,.34f), -11);
            Inside("laundry-basket", "休养层回收布筐", new(.3f,3.6f,-12.12f), new(.72f,.77f,.64f));
            Inside("tool_cart", "员工走廊晚班餐车", new(6.5f,7.2f,-.64f), new(1.04f,.92f,.62f), 2);
            Inside("bathroom-vanity", "卫生间搪瓷洗手台", new(-19.43f,0,7.3f), new(1.10f,.89f,.68f), 90);
            Curtain("卫生间维修隔挡", new(-17.74f,0,5.50f), -14);
            // Each gallery has a different silhouette on the return trip. Long furniture
            // sits parallel to the edge, away from the stairs, bridges and room thresholds.
            Inside("metal_toolbox", "落在送件车边的扳手盒", new(-13.78f,-3.6f,-11.96f), new(.40f,.21f,.25f), 7);
            Inside("utility_box_02", "西环廊检修箱", new(-19.47f,-3.6f,-5.60f), new(1.26f,1.14f,.57f), 90);
            Inside("painted_wooden_bench", "迎宾西廊换鞋凳", new(-19.44f,0,-7.16f), new(1.87f,.48f,.54f), 90);
            Inside("vintage_suitcase", "检票口待领的小皮箱", new(3.79f,0,2.71f), new(.65f,.43f,.31f), 84);
            Inside("laundry-basket", "换鞋凳旁的脚巾筐", new(-19.38f,0,-8.56f), new(.53f,.58f,.51f), -6);
            Inside("vintage_day_bed", "休养东廊候诊榻", new(11.30f,3.6f,-6.43f), new(1.98f,.91f,.80f), 90);
            Inside("locker-room-tall-lockers", "员工西廊更鞋柜", new(-19.44f,7.2f,-5.93f), new(1.57f,1.77f,.52f), 90);
            Inside("laundry-basket", "副楼待送洗衣筐", new(30.4f,3.6f,13.44f), new(.73f,.76f,.64f), -9);
            Inside("painted_wooden_bench", "副楼交班等候凳", new(30.57f,7.2f,10.61f), new(1.91f,.49f,.56f), 6);
            Inside("tea_set_01", "员工餐桌收班茶具", new(-14.9f,8.01f,-6.31f), new(.54f,.20f,.38f), 16, false);
            Inside("vintage_radio_transceiver", "会商室桌边的听线机", new(1.57f,8.01f,-6.43f), new(.53f,.24f,.32f), 89, false);
            Inside("painted_wooden_chair_02", "放映后排留下的座椅", new(-8.65f,3.6f,6.48f), new(.53f,.87f,.55f), -15);
            Curtain("按摩榻前的旧布屏", new(9.55f,3.6f,19.3f), -31);
            Curtain("静养室的隔帘", new(-12.35f,3.6f,-5.99f), 82);
            Curtain("布草间晾干的屏风", new(26.0f,3.6f,17.10f), -22);
            Inside("locker-room-tall-lockers", "待检仪表柜", new(34.1f,3.6f,7.18f), new(1.42f,1.72f,.53f), 14);
            Inside("kitchen-base-unit-drawers", "档案室清点柜", new(-5.82f,7.2f,5.36f), new(1.54f,.87f,.67f), 83);
            Inside("cardboard_box_01", "等着复核的底册", new(-5.82f,8.07f,5.72f), new(.39f,.28f,.34f), 8, false);
            Inside("locker-room-tall-lockers", "密封档案柜", new(-2.65f,7.2f,4.37f), new(1.65f,1.81f,.59f), -12);
            Inside("painted_wooden_chair_02", "监控席拉开的椅子", new(-13.3f,7.2f,3.60f), new(.53f,.88f,.56f), -31);
            Inside("vintage_suitcase", "经理桌边的出差箱", new(5.50f,7.2f,2.56f), new(.77f,.49f,.32f), -23);
            Inside("painted_wooden_chair_02", "会商室另一侧的听席", new(3.15f,7.2f,-6.02f), new(.52f,.87f,.54f), 92);
        }
    }
}
