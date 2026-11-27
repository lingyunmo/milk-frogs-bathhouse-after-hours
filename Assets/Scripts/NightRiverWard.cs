using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        internal static readonly (string Name, Rect Bounds)[] RiverBuildings =
        {
            ("水文站值班厅", Rect.MinMaxRect(187, 8, 211, 26)),
            ("旧照相馆暗房", Rect.MinMaxRect(240, 5, 259, 22)),
            ("东堤闸井房", Rect.MinMaxRect(307, 12, 328, 29)),
            ("守闸宿舍", Rect.MinMaxRect(282, -10, 298, 3))
        };

        void DressRiverWard()
        {
            var district = new GameObject("石桥东堤").transform;
            district.SetParent(root, false);
            StreetRoomSurface(district, "河堤水泥护面", new(256,-.125f,4), new(168,.20f,79.4f), plaster, 3.4f,true);
            StreetRoomSurface(district, "堤顶旧沥青", new(256,-.084f,-18), new(168,.16f,11), asphalt, 3.1f,true);
            StreetRoomSurface(district, "巡堤步道", new(256,-.083f,3.8f), new(168,.16f,4), terrazzo, 2.4f,true);
            for (int x=180; x<340; x+=24)
            {
                var post = new Vector3(x,0,-11);
                StreetRoomSurface(district,"堤路旧灯柱",post+Vector3.up*1.84f,new(.18f,3.68f,.18f),steel,.72f);
                StreetLampAt(post+new Vector3(0,3.62f,-.24f),1.32f,8.2f);
                StreetRoomSurface(district,"防撞墩",new(x,.29f,-25),new(2.2f,.6f,.62f),plaster,1.15f);
            }
            for (int x=185; x<340; x+=43)
                StreetRoomProp(district,"public-housing-slab","对岸旧宿舍楼",new(x,-.7f,55),new(36,15,9),180,false);
            StreetRoomProp(district,"kei-van","测站面包车",new(185,0,-7),new(1.9f,1.89f,4.6f),-35);
            StreetRoomProp(district,"tool-shed","堤外器材棚",new(225,0,22),new(4.8f,3.3f,4.2f),18);
            StreetRoomProp(district,"painted_wooden_bench","候潮长凳",new(228,0,16.5f),new(2.5f,.55f,.63f),-18);
            StreetRoomProp(district,"kei-van","废弃抢修车",new(269,0,-19),new(1.95f,1.88f,4.62f),63);
            Box("堤外警铃灯杆",new(274,1.55f,-5),new(.14f,3.1f,.14f),steel,district);
            StreetLampAt(new(274,3f,-5),1.1f,5.2f);
            WallSign("东堤水文站 →",new(173,2.45f,-9),0,new(3.4f,.52f),.22f);
            Box("堤口指路柱",new(173,1.15f,-8.91f),new(.14f,2.3f,.14f),steel,district);

            var hall = StreetRoomShell(RiverBuildings[0],192,205,false,brick,terrazzo,208.3f);
            BuildTower(hall);
            var pipework=Trim(new(188,1.5f,7.5f),paintedIron);
            pipework.Pipe(new(187.6f,.24f,7.58f),new(187.6f,3.20f,7.58f),.11f);
            pipework.Pipe(new(187.6f,3.20f,7.58f),new(210.8f,3.20f,7.58f),.10f);
            for(float x=188;x<211;x+=2.65f) pipework.Box(new(x,3.19f,7.68f),new(.10f,.29f,.35f));
            StreetRoomProp(hall,"kei-van","站内巡堤车",new(214,0,6.9f),new(1.84f,1.86f,4.50f),-67);
            StreetRoomLight(hall,new(207.6f,2.69f,9.9f),new(.16f,-.4f,-1),new(.63f,.82f,.91f),2.45f);
            EnamelFace(hall,"水文站搪瓷匾","Surfaces/RiverStationEnamel",new(198.2f,2.16f,7.83f),new(4.95f,1.65f));

            StreetRoomLight(hall,new(198,2.86f,7.16f),new(.02f,-.59f,.76f),new(.91f,.79f,.58f),2.05f);
            Partition(hall,199,16,6.8f);
            StreetRoomProp(hall,"metal_office_desk","测绘器材领用台",new(196,.025f,19.8f),new(2.45f,.83f,.84f),0);
            SurveyCamera(hall,new(196,.855f,19.8f));
            StreetRoomProp(hall,"metal_office_desk","测深艇遥控席",new(201.5f,.025f,21.5f),new(2.10f,.83f,1.45f),0);
            StreetRoomProp(hall,"vintage_radio_transceiver","测深遥测机",new(201.5f,.86f,21.5f),new(.78f,.36f,.48f),180,false);
            WallSign("测深艇",new(201.5f,1.23f,21.32f),0,new(.47f,.12f),.073f);
            StreetRoomProp(hall,"painted_wooden_chair_02","值班椅",new(197.5f,.025f,21.1f),new(.58f,.85f,.63f),20);
            StreetRoomProp(hall,"worn_metal_rack","雨量记录架",new(209,.025f,20),new(3.7f,1.92f,.62f),90);
            StreetRoomProp(hall,"vintage_suitcase","测绘箱",new(204,.025f,24.6f),new(1.05f,.65f,.63f),164);
            StreetRoomProp(hall,"sofa_02","候班长沙发",new(190,.025f,22.2f),new(3.0f,1.0f,1.02f),90);
            WallSign("测绘器材",new(196,1.72f,25.78f),0,new(1.9f,.39f),.17f);
            StreetRoomLight(hall,new(196.2f,2.63f,19.2f),new(.04f,-1,.25f),new(.98f,.76f,.51f),1.65f);
            StreetRoomLight(hall,new(203,2.62f,11),new(.1f,-1,-.3f),new(.56f,.75f,.84f),1.28f);

            var lab = StreetRoomShell(RiverBuildings[1],245,16,true,paint,tiles);
            Partition(lab,246.7f,12.8f,5.1f);
            StreetRoomProp(lab,"kitchen-base-unit-sink","显影水槽",new(249,.025f,13.2f),new(2.45f,.85f,.84f),180);
            StreetRoomProp(lab,"metal_office_desk","旧底片接触印相台",new(254,.025f,18.8f),new(2.63f,.86f,.85f),0);
            for(int i=0;i<3;i++)
            {
                Box("显影药盘",new(248.30f+i*.67f,.906f,13.2f),new(.55f,.055f,.39f),steel,lab);
                Box("药液表面",new(248.30f+i*.67f,.939f,13.2f),new(.50f,.008f,.34f),olive,lab);
            }
            SurveyCamera(lab,new(254,.89f,18.8f));
            StreetRoomProp(lab,"white-kitchen-wall-cabinet","避光药品壁柜",new(255,1.43f,21.65f),new(1.75f,.72f,.45f),0,false);
            WallSign("显影　停显　定影",new(249.1f,1.72f,14.18f),0,new(1.62f,.26f),.12f);
            Box("药槽上挡水板",new(249.1f,1.05f,14.31f),new(2.52f,1.94f,.12f),glazed,lab);
            StreetRoomProp(lab,"worn_metal_rack","避光药柜",new(257.8f,.025f,10.8f),new(2.7f,1.84f,.62f),90);
            StreetRoomProp(lab,"painted_wooden_bench","洗相等候座",new(242,.025f,17.8f),new(2.55f,.55f,.65f),90);
            StreetRoomLight(lab,new(249.2f,2.66f,13.9f),new(.08f,-1,-.16f),new(1,.30f,.16f),2.25f);
            StreetRoomLight(lab,new(254.2f,2.57f,19.1f),new(-.12f,-1,.08f),new(1,.72f,.49f),1.5f);

            var pump = StreetRoomShell(RiverBuildings[2],312,323,false,plaster,tiles,326);
            Partition(pump,320,20,5.8f);
            StreetRoomProp(pump,"portable_generator","闸门卷扬机",new(324,.025f,25.5f),new(2,1.42f,1.43f),162);
            StreetRoomProp(pump,"metal_office_desk","试鸣接线台",new(316,.025f,24.5f),new(1.90f,.84f,.77f),0);
            StreetRoomProp(pump,"vintage_radio_transceiver","堤外扬声器遥控台",new(316,.87f,24.5f),new(.52f,.28f,.36f),180,false);
            StreetRoomProp(pump,"tool_cart","闸工工具车",new(310,.025f,19.6f),new(1.3f,.96f,.69f),16);
            StreetRoomLight(pump,new(316,2.65f,24),new(.18f,-1,.18f),new(.61f,.87f,.89f),1.65f);
            StreetRoomLight(pump,new(323,2.62f,15.8f),new(0,-1,-.25f),new(.94f,.73f,.49f),1.42f);

            var bunk = StreetRoomShell(RiverBuildings[3],286,294,false,brick,planks,294.8f);
            Partition(bunk,292,-3,4.3f);
            StreetRoomProp(bunk,"vintage_day_bed","护堤员行军床",new(284,.025f,.8f),new(2.09f,.86f,.91f),0);
            StreetRoomProp(bunk,"folded-towel-stack","干净的旧棉被",new(284.6f,.84f,.8f),new(.52f,.16f,.48f),0,false);
            StreetRoomProp(bunk,"WoodenTable_01","守闸员饭桌",new(295,.025f,-5),new(1.5f,.82f,.89f),90);
            StreetRoomProp(bunk,"tea_set_01","交班茶壶",new(295,.85f,-5.43f),new(.39f,.20f,.28f),7,false);
            Box("玻璃看片灯台",new(295,.89f,-4.83f),new(.72f,.10f,.47f),steel,bunk);
            var glass=Material(new Color(.65f,.63f,.48f));
            glass.EnableKeyword("_EMISSION");
            glass.SetColor("_EmissionColor",new Color(.45f,.43f,.29f));
            Box("灯台乳白玻璃",new(295,.945f,-4.83f),new(.63f,.013f,.39f),glass,bunk);
            StreetRoomProp(bunk,"painted_wooden_chair_02","守闸员木椅",new(294,.025f,-4.9f),new(.61f,.85f,.64f),-66);
            StreetRoomLight(bunk,new(295,2.58f,-4.7f),new(-.14f,-1,0),new(1,.66f,.42f),1.55f);
            for (int i=0; i<RiverSurvey.Plates.Length; i++) DressSurveyMark(district,i);
        }

        void EnamelFace(Transform parent,string name,string texture,Vector3 at,Vector2 size)
        {
            var face=GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name=name;
            face.transform.SetParent(parent,false);
            face.transform.position=at;
            face.transform.localScale=new Vector3(size.x,size.y,1);
            face.GetComponent<Collider>().enabled=false;
            face.GetComponent<Renderer>().sharedMaterial=Pbr(name,texture,null,null,Color.white,.48f);
        }

        void BuildTower(Transform hall)
        {
            // An elevated tank identifies the station above the flat roofline from the main street.
            var struts=Trim(new Vector3(204,4.6f,23),paintedIron);
            for(int x=-1;x<=1;x+=2)
            for(int z=-1;z<=1;z+=2)
            {
                Vector3 bottom=new(205+x*1.74f,3.32f,22+z*1.74f);
                Vector3 top=new(205+x*1.28f,7.05f,22+z*1.28f);
                struts.Pipe(bottom,top,.094f);
                struts.Pipe(bottom,top+new Vector3(-x*2.56f,0,0),.04f);
                struts.Pipe(bottom,top+new Vector3(0,0,-z*2.56f),.04f);
            }
            var tank=Shape("高架铆接水罐",PrimitiveType.Cylinder,new(205,8.12f,22),
                new(4.7f,1.17f,4.7f),paintedIron,Vector3.zero);
            tank.transform.SetParent(hall,true);
            for(int i=0;i<3;i++)
            {
                var seam=Shape("水罐接缝",PrimitiveType.Cylinder,new(205,7.06f+i*1.04f,22),
                    new(4.76f,.045f,4.76f),steel,Vector3.zero);
                seam.transform.SetParent(hall,true);
            }
            Fixture(new(205,4.5f,18.6f),new(0,.9f,.73f),new(.60f,.83f,.84f),3.4f,9,74,.005f);
        }

        void Partition(Transform building,float x,float z,float length)
        {
            var center=new Vector3(x,1.22f,z);
            StreetRoomSurface(building,"半高隔墙",center,new(.21f,2.44f,length),glazed,1.38f);
            StreetRoomSurface(building,"磨损压顶",center+Vector3.up*1.235f,new(.265f,.055f,length+.12f),steel,.72f);
        }

        void SurveyCamera(Transform building, Vector3 feet)
        {
            Box("皮套测绘相机",feet+Vector3.up*.14f,new(.43f,.28f,.25f),wood,building);
            Box("取景器",feet+new Vector3(-.095f,.31f,-.015f),new(.11f,.06f,.10f),steel,building);
            var lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lens.name = "黄铜镜筒";
            lens.transform.SetParent(building,false);
            lens.transform.SetPositionAndRotation(feet+new Vector3(.045f,.14f,-.15f),Quaternion.Euler(90,0,0));
            lens.transform.localScale=new(.16f,.065f,.16f);
            lens.GetComponent<Renderer>().sharedMaterial=brass;
            lens.GetComponent<Collider>().enabled=false;
        }

        void DressSurveyMark(Transform parent, int index)
        {
            var center=RiverSurvey.Plates[index].Center;
            if(index<3)
            {
                Box("水准点背板",center+Vector3.forward*.105f,new(2.25f,1.02f,.12f),steel,parent);
                EnamelFace(parent,"水尺搪瓷刻度","Surfaces/FloodGauge",center+Vector3.forward*.035f,new(2.25f,1.02f));
            }
            else
            {
                // Copper witness marks sit on the surviving masonry at the old camera station.
                for(int side=-1;side<=1;side+=2)
                    Box("旧址铜钉座",RiverSurvey.Pin(index,side)+Vector3.forward*.14f,new(.30f,.30f,.26f),brass,parent);
            }
            if(index==0)
                foreach (float dx in new[]{-.90f,.90f}) Box("观测标桩",center+new Vector3(dx,-.79f,.13f),new(.12f,1.58f,.13f),steel,parent);
            var ink=Material(index<3 ? new Color(.74f,.72f,.57f) : new Color(.79f,.58f,.39f));
            ink.EnableKeyword("_EMISSION");
            ink.SetColor("_EmissionColor",ink.color*.24f);
            for(int side=-1;side<=1;side+=2)
            {
                var pin=RiverSurvey.Pin(index,side)+Vector3.forward*.035f;
                Box("水准标钉横线",pin,new(.24f,.033f,.018f),ink,parent);
                Box("水准标钉竖线",pin,new(.033f,.24f,.018f),ink,parent);
            }
            WallSign(index<3 ? "洪痕　"+(index+1) : "八七年　旧址",center,0,new(1.13f,.31f),.14f);
            StreetLampAt(center+new Vector3(0,1.21f,-.28f),.7f,4.4f);
        }
    }
}
