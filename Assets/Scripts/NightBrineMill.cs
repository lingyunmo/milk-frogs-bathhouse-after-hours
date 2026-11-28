using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        // Across the retaining wall from the night shift's valve station. Each structural bay
        // is present before the incident, so the ruined silhouette belongs to the same mill.
        void DressBrineMill()
        {
            var mill = new GameObject("旧脱水车间").transform;
            mill.SetParent(root, false);
            void Skin(string label, Vector3 point, Vector3 size, Material skin, float tile)
            {
                var part = Box(label, point, size, skin, mill);
                Surface(part, skin, tile);
                part.GetComponent<Collider>().enabled = false;
            }

            Skin("挡水基座", new(245, -.41f, -43.5f), new(31, .9f, 13.5f), plaster, 2.2f);
            const float bay = 5.1f, storey = 3.45f;
            for (int i = 0; i < 5; i++)
            {
                float x = 235 + i * bay;
                int levels = i is 0 or 4 ? 2 : 3;
                for (int level = 0; level < levels; level++)
                {
                    float foot = level * storey;
                    Skin("预制楼板", new(x, foot + storey, -43.65f), new(bay-.025f,.52f,6.15f), plaster, 1.8f);
                    Skin("正面窗下墙", new(x, foot+ .82f, -40.69f), new(bay-.04f,1.64f,.34f), brick, 1.25f);
                    Skin("后排砖墙", new(x, foot+1.61f, -46.48f), new(bay-.035f,3.22f,.28f), brick, 1.5f);
                    Skin("混凝土窗间柱", new(x-bay*.5f+.29f, foot+2.43f, -40.69f),
                        new(.56f,1.60f,.44f), plaster, 1.24f);
                    Skin("铆接窗梁", new(x, foot+3.14f, -40.60f), new(bay,.23f,.42f), steel,.74f);
                    if (i==0 || i==4 || level==2 && i==3)
                    {
                        float end = x + (i==0 ? -1 : 1)*bay*.5f;
                        Skin("侧面砖墙", new(end,foot+1.61f,-43.61f), new(.30f,3.22f,5.78f), brick,1.5f);
                    }
                }
            }
            void Lamp(Vector3 lens)
            {
                Skin("沿堤检修灯桩", new(lens.x,(lens.y-.12f)*.5f,lens.z+.15f),new(.11f,lens.y-.12f,.11f),paintedIron,.55f);
                Skin("挡雨灯壳",lens+new Vector3(0,.10f,.12f),new(.68f,.28f,.35f),steel,.80f);
                Skin("灯罩格面",lens,new(.52f,.135f,.16f),tiles,1.2f);
            }
            Lamp(new(259.8f,4.4f,-37.33f)); Lamp(new(242.1f,2.9f,-38.0f));
            Skin("沉淀井塔座",new(234.5f,1.65f,-47.8f),new(3.1f,3.3f,2.5f),brick,1.24f);
            Skin("排气竖井",new(234.5f,6.45f,-47.8f),new(1.45f,6.4f,1.30f),brick,.95f);
            Skin("排烟竖井压顶",new(234.5f,9.63f,-47.8f),new(1.66f,.24f,1.52f),steel,1);
        }
    }
}
