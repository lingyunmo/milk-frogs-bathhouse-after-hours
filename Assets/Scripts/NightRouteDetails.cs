
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        void DressRoute()
        {
            pickups.Add(("torch", Prop("small_plastic_torch", "更衣凳上的旧手电",
                new Vector3(-2, 4.08f, 20.99f), new Vector3(.095f, .068f, .29f), 79, false)));

            // Cluster furnishings around their use. Leave the passage through each room and the window landings clear.
            var props = new (string asset, string label, Vector3 feet, Vector3 size, float yaw, bool solid)[]
            {
                ("locker-room-tall-lockers", "北墙旧寄存柜", new(-2.95f,3.6f,23.46f), new(2.40f,2.03f,.72f), 180, true),
                ("locker-room-tall-lockers", "第二列寄存柜", new(.26f,3.6f,23.48f), new(2.02f,1.96f,.68f), 180, true),
                ("folded-towel-stack", "长凳上留下的毛巾", new(-2.75f,4.08f,20.90f), new(.38f,.19f,.32f), 24, false),
                ("folded-towel-stack", "未领用的布草", new(-1.27f,4.08f,21.02f), new(.39f,.15f,.29f), -11, false),
                ("laundry-basket", "更衣室换洗筐", new(-3.90f,3.6f,21.64f), new(.76f,.43f,.56f), 36, true),
                ("metal-bucket", "更衣架下水桶", new(-4.14f,3.60f,16.64f), new(.37f,.34f,.38f), 22, true),
                ("industrial_wall_sconce", "更衣室北墙壁灯", new(-4.92f,5.65f,23.77f), new(.17f,.38f,.28f), 180, false),
                ("vintage_suitcase", "长凳旁的寄存行李", new(1.9f, 3.6f, 20.9f), new(1.39f,.60f,.41f), -15, true),
                ("painted_wooden_bench", "门边换鞋长椅", new(2.9f,3.6f,15.83f), new(2.22f,.47f,.59f), 90, true),
                ("worn_metal_rack", "门边拖鞋架", new(-4.33f,3.6f,16.16f), new(1.7f,1.36f,.57f), 12, true),
                ("painted_wooden_chair_02", "褪色木椅", new(-4.91f, 3.6f, 21.37f), new(.59f,.87f,.52f), -47, true),
                ("wall_clock", "停住的更衣室挂钟", new(-3.05f,5.97f,23.875f), new(.39f,.39f,.071f), 0, false),
                ("ceiling_fan", "更衣室吊扇", new(-2.79f,6.23f,20.12f), new(1.75f,.72f,1.75f), 22, false),

                ("painted_wooden_chair_02", "沙发厅扶手椅", new(-17.61f,3.6f,3.48f), new(.64f,.92f,.56f), 184, true),
                ("painted_wooden_chair_02", "沙发厅斜放的木椅", new(-12.18f,3.6f,6.65f), new(.6f,.91f,.56f), 23, true),
                ("WoodenTable_01", "杂志茶几", new(-11.72f,3.6f,4.04f), new(1.17f,.44f,.70f), 5, true),
                ("vintage_suitcase", "折叠间待领行李", new(-13.5f,3.6f,17.1f), new(1.56f,.64f,.49f), 4, true),
                ("painted_wooden_bench", "毛巾领取凳", new(5.35f,3.6f,6.70f), new(1.62f,.48f,.52f), 90, true),
                ("worn_metal_rack", "空布草铁架", new(9.73f,3.6f,1.04f), new(2.5f,1.84f,.48f), 0, true),

                ("painted_wooden_chair_02", "茶桌木椅", new(-8.15f,0,6.18f), new(.59f,.87f,.52f), 164, true),
                ("painted_wooden_chair_02", "茶桌临窗木椅", new(-9.87f,0,7.94f), new(.59f,.87f,.52f), -6, true),
                ("WoodenTable_01", "茶座第二张桌", new(-6.40f,0,5.56f), new(1.44f,.74f,.89f), 7, true),
                ("tea_set_01", "柜台茶杯", new(-6.43f,.74f,5.53f), new(.46f,.20f,.42f), 27, false),
                ("ceiling_fan", "茶座停转的吊扇", new(-9.09f,2.62f,6.65f), new(1.59f,.73f,1.59f), 40, false),
                ("painted_wooden_bench", "检票口靠墙长椅", new(1.63f,0,1.26f), new(1.38f,.49f,.64f), 0, true),
                ("worn_metal_rack", "门厅寄存架", new(4.6f,0,7.87f), new(1.65f,1.73f,.66f), -90, true),
                ("worn_metal_rack", "收费单据铁柜", new(6.35f,0,5.8f), new(1.4f,1.82f,.48f), 90, true),
                ("folded-towel-stack", "散客寄存布草", new(7.51f,ShelfTop(1.69f,3),1.12f), new(.48f,.17f,.33f), -12, false),
                ("painted_wooden_chair_02", "登记台侧椅", new(11.35f,0,5.5f), new(.56f,.89f,.53f), 31, true),
                ("vintage_radio_transceiver", "前台短波机", new(8.23f,.94f,3.51f), new(.59f,.22f,.39f), 180, false),
                ("wall_clock", "门厅挂钟", new(2.65f,2.25f,.13f), new(.48f,.48f,.082f), 180, false),
                ("painted_wooden_bench", "大浴池休息椅", new(-1.99f,0,22.47f), new(2.45f,.49f,.66f), 0, true),
                ("folded-towel-stack", "浴池椅上潮毛巾", new(-2.42f,.49f,22.45f), new(.44f,.15f,.31f), -18, false),
                ("laundry-basket", "洗衣房成堆布草", new(-14.3f,0,21.45f), new(.80f,.53f,.61f), -12, true),
                ("metal-bucket", "淋浴区搪锌水桶", new(14.85f,0,22.64f), new(.44f,.34f,.42f), 48, true),
                ("tool_cart", "布草车", new(-6.27f,0,17.68f), new(1.04f,.96f,.65f), 17, true),

                ("vintage_radio_transceiver", "检修通话机", new(-8.47f,8.08f,18.03f), new(.62f,.22f,.44f), 173, false),
                ("painted_wooden_chair_02", "值班室椅子", new(-9.80f,7.2f,18.9f), new(.6f,.90f,.53f), 8, true),
                ("cardboard_box_01", "弱电间备件纸箱", new(8.84f,7.2f,21.8f), new(.78f,.62f,.54f), -15, true),
                ("wall_clock", "发电机房挂钟", new(-10.3f,-1.03f,23.875f), new(.37f,.37f,.071f), 0, false),
                ("tool_cart", "换下的泵接头", new(3.98f,-3.6f,20.36f), new(1.16f,.98f,.68f), 9, true),
                ("painted_wooden_chair_02", "配电房座椅", new(10.82f,-3.6f,6.63f), new(.59f,.87f,.52f), -27, true),
                ("metal_trash_can", "公交站旁的掉漆垃圾桶", new(152.55f,0,-10.37f), new(.57f,.76f,.55f), 14, true),
                ("painted_wooden_bench", "街边店门口的长凳", new(32,0,-9.51f), new(2.22f,.50f,.64f), 7, true),
                ("kei-van", "路肩停着的旧货车", new(33,0,-23.9f), new(1.99f,1.94f,4.6f), -90, true),
                ("industrial_wall_sconce", "正门旧式玻璃壁灯", new(2.56f,2.18f,-12.97f), new(.17f,.40f,.28f), 0, false),
                ("industrial_wall_sconce", "货门旧式玻璃壁灯", new(-15.33f,2.18f,24.17f), new(.17f,.40f,.28f), 180, false)
            };
            foreach (var (asset, label, at, size, yaw, solid) in props)
            {
                var prop = Prop(asset, label, at, size, yaw, solid);
                if (label == "更衣室北墙壁灯") LightSconce(prop);
            }

            var rubble = new GameObject("路边碎石").transform;
            rubble.SetParent(root, false);
            for (int i = 0; i < 3; i++)
            {
                var rock = Shape("落地的小石子", PrimitiveType.Sphere, new Vector3(23.86f + i * .13f, .024f, -10 + (i % 2) * .08f),
                    new Vector3(.10f, .051f, .077f), plaster, new Vector3(0, i * 23, 0));
                rock.transform.SetParent(rubble, true);
            }
            pickups.Add(("stones", rubble.gameObject));

            var changingSconce = new Vector3(-4.92f, 5.82f, 23.36f);
            Fixture(changingSconce, new Vector3(-2f, 4.12f, 20.99f) - changingSconce,
                new Color(.97f, .77f, .57f), 1.72f, 5.65f, 66);
            WallSign("前厅梯", new Vector3(11.75f, 5.83f, 9.86f), 180, new Vector2(1.08f, .24f), .12f);
            WallSign("干衣布草", new Vector3(-10.7f, 5.78f, 23.87f), 0, new Vector2(1.32f, .33f), .13f);
            WallSign("机房重地", new Vector3(-12.23f, -.96f, 14.12f), 180, new Vector2(1.28f, .29f), .12f);
        }

        GameObject HeldFlashlight()
        {
            var model = Prop("small_plastic_torch", "手持旧手电", Vector3.zero,
                new Vector3(.105f, .080f, .267f), 0, false);
            foreach (var part in model.GetComponentsInChildren<Transform>()) part.gameObject.layer = NightPlayer.ViewModelLayer;
            foreach (var surface in model.GetComponentsInChildren<Renderer>())
            {
                surface.shadowCastingMode = ShadowCastingMode.Off;
                surface.receiveShadows = false;
                surface.lightProbeUsage = LightProbeUsage.Off;
                surface.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            return model;
        }
    }
}
