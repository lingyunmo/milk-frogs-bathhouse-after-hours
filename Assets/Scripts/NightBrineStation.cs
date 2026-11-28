using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        GameObject sampleBox, coldDoor, floodGate;
        readonly TextMesh[] brineMarks=new TextMesh[4];
        int etchedWheel=-1;

        void DressBrineStation()
        {
            for(int i=0;i<brineMarks.Length;i++)
                brineMarks[i]=WallSign(BrineRoute.Rubbing(i,0),BrineRoute.Marks[i].Point,0,new(1.60f,.37f),.14f);
            var room=StreetRoomShell(("冷样保管间",BrineRoute.Room),268,271,false,brick,tiles);
            Box("砌封的旧后门",new(271,1.2f,18),new(1.98f,2.42f,.23f),brick,room);
            coldDoor=Box("保温闸门",new(268,1.17f,10),new(1.96f,2.36f,.16f),paintedIron,room);
            WallSign("旧线　井 → 河",BrineRoute.Dial,0,new(1.20f,.33f),.13f);
            StreetRoomProp(room,"metal_office_desk","取样台",new(270,.025f,15.4f),new(2.4f,.82f,1),0);
            StreetRoomProp(room,"worn_metal_rack","封存架",new(264.4f,.03f,15.6f),new(2.8f,1.85f,.54f),90);
            StreetRoomProp(room,"painted_wooden_chair_02","放着交运簿的木椅",new(268.45f,.03f,15.75f),new(.65f,.90f,.54f),5);
            Box("卷起书角的交运簿",new(268.45f,.58f,15.73f),new(.37f,.045f,.27f),wood,room);
            StreetRoomProp(room,"tool_cart","值守推车",new(272.7f,.025f,13.6f),new(.95f,.84f,.60f),12);
            StreetRoomLight(room,new(269.6f,2.65f,15.9f),new(.05f,-1,-.2f),new(.54f,.86f,.84f),2.5f);
            WallSign("试验核聚变样芯　／　封存",new(270,1.76f,17.81f),0,new(2.18f,.36f),.14f);
            var caseRoot=new GameObject("交运匣与样芯").transform;
            caseRoot.SetParent(room,false); sampleBox=caseRoot.gameObject;
            Box("封存匣",BrineRoute.Sample-Vector3.up*.085f,new(.55f,.16f,.33f),paintedIron,caseRoot);
            Box("带齿铜封",BrineRoute.Sample+Vector3.up*.01f,new(.13f,.034f,.36f),brass,caseRoot);
            var glow=Material(new Color(.36f,.78f,.73f));
            glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor",new Color(.17f,.77f,.63f)*1.6f);
            Box("样芯观察窗",BrineRoute.Sample+new Vector3(-.15f,.009f,0),new(.10f,.035f,.20f),glow,caseRoot);

            var yard=new GameObject("堤岸隔离闸").transform;
            yard.SetParent(root,false);
            StreetRoomSurface(yard,"值守台",new(273,.48f,-30.5f),new(.9f,.96f,.86f),steel,1.1f);
            Box("封样座",BrineRoute.Socket,new(.49f,.10f,.41f),brass,yard);
            WallSign("隔离闸　／　东廊 →",new(274,1.85f,-34.25f),180,new(2.05f,.36f),.15f);
            Box("搪瓷路标柱",new(274,.90f,-34.32f),new(.12f,1.85f,.12f),paintedIron,yard);
            for(int i=0;i<3;i++)
            {
                var at=BrineRoute.Mouth(i);
                Box("来水暗孔",at+new Vector3(-.16f,1.06f,0),new(.24f,2.12f,1.94f),paintedIron,yard);
                var ribs=Trim(at,steel);
                ribs.Pipe(at+new Vector3(0,.06f,-.95f),at+new Vector3(0,2.15f,-.95f),.086f);
                ribs.Pipe(at+new Vector3(0,2.15f,-.95f),at+new Vector3(0,2.15f,.94f),.086f);
            }
            StreetRoomSurface(yard,"湿沥青防滑层",new(261.7f,.001f,-30.8f),new(30,.038f,8.7f),asphalt,2.15f,true);
            for(int lane=0;lane<3;lane++)
            {
                var at=BrineRoute.Mouth(lane);
                var edge=Trim(at,steel);
                for(int j=0;j<8;j++)
                    edge.Box(new(at.x+j*.19f,.047f,at.z),new(.072f,.04f,1.72f));
            }
            StreetRoomProp(yard,"portable_generator","报废的岸电箱",new(245,.025f,-32.6f),new(2.54f,1.65f,1.65f),90);
            StreetRoomProp(yard,"worn_metal_rack","沿堤的旧检修架",new(256,.035f,-35.1f),new(3.9f,1.68f,.55f),0);
            StreetRoomProp(yard,"tool_cart","撤班后留下的推车",new(263,.04f,-26.45f),new(1.1f,.90f,.58f),-16);
            Box("南线搪瓷牌托",new(268.4f,.78f,-35.04f),new(.06f,1.55f,.06f),steel,yard);
            Box("北线搪瓷牌托",new(268.4f,.78f,-26.97f),new(.06f,1.55f,.06f),steel,yard);
            WallSign("南回水",new(268.4f,1.54f,-34.97f),180,new(.76f,.22f),.11f);
            WallSign("北回水",new(268.4f,1.54f,-27.04f),0,new(.76f,.22f),.11f);
            Fixture(new(274.5f,3.4f,-34),new(-1,-.27f,.2f),new(.60f,.85f,.81f),2.2f,22,74,.00010f);
            Fixture(new(248.7f,3.8f,-34.3f),new(1,-.24f,.26f),new(.92f,.51f,.32f),2.1f,18,68,.00012f);
            for(int side=0;side<2;side++)
            {
                var at=BrineRoute.Handwheel(side);
                var metal=Trim(at,paintedIron);
                metal.Pipe(at-Vector3.up*1.04f,at,.095f);
                // Horizontal red handwheel above the riser, visible against the pale kerb.
                Vector3 rim=at+Vector3.up*.035f;
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4,b=a+Mathf.PI/4;
                    Trim(at,red).Pipe(rim+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*.24f,
                        rim+new Vector3(Mathf.Sin(b),0,Mathf.Cos(b))*.24f,.028f);
                }
                metal.Pipe(rim-Vector3.right*.22f,rim+Vector3.right*.22f,.018f);
            }
            var bounds=BrineRoute.Gallery;
            RegisterInterior(bounds,0,2.7f);
            StreetRoomSurface(yard,"泄洪廊顶",new(295,2.8f,-31),new(22.25f,.24f,6.25f),plaster,2.2f);
            foreach(float z in new[]{-34f,-28f})
                StreetRoomSurface(yard,"混凝土侧墙",new(295,1.35f,z),new(22,2.7f,.24f),brick,2.1f);
            StreetRoomSurface(yard,"泄洪廊尽头",new(306,1.35f,-31),new(.25f,2.7f,6),plaster,2.1f);
            floodGate=Box("隔离卷闸",new(284,1.34f,-31),new(.17f,2.68f,6),paintedIron,yard);
            WallSign("泄洪廊",new(283.85f,2.52f,-31),90,new(1.42f,.3f),.16f);
            for(int i=0;i<3;i++)
            {
                float x=286.5f+i*8;
                StreetRoomLight(yard,new(x,2.24f,-32.9f),new(.18f,-1,.28f),new(.45f,.81f,.70f),1.75f,2.66f);
            }
            Box("出口检修门",new(305.6f,1.14f,-31),new(.2f,2.28f,1.64f),paintedIron,yard);
            Box("逃生推栓",new(305.43f,1.16f,-31),new(.22f,.09f,1.16f),brass,yard);
            WallSign("出口",new(305.42f,1.83f,-31),90,new(.62f,.29f),.15f);
        }

        void RefreshBrineStation(BrineRun run)
        {
            coldDoor.SetActive(run.stage==BrineStage.Quiet);
            sampleBox.SetActive(run.stage<=BrineStage.Unsealed);
            floodGate.transform.position=new Vector3(284,run.gateReleased ? 3.96f : 1.34f,-31);
            floodGate.SetActive(run.stage!=BrineStage.Clear);
            if(etchedWheel==run.wheel) return;
            etchedWheel=run.wheel;
            for(int i=0;i<brineMarks.Length;i++)
                brineMarks[i].text=BrineRoute.Rubbing(i,run.wheel);
        }
    }
}
