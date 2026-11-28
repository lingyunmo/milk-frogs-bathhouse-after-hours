using UnityEngine;

namespace AfterHours
{
    internal static class ReturnWing
    {
        internal static readonly string[] Rooms = { "留根间", "熨印间", "监听间", "气封间", "签收间" };
        internal const float West=214, South=32, North=40, Width=6;
        internal static float X(int n) => West+(n-.5f)*Width;
        internal static Rect Bounds(int n) => Rect.MinMaxRect(X(n)-3,South,X(n)+3,North);
        internal static Vector3 Door(int n) => new(West+(n-1)*Width,0,n%2==0?36.8f:34.8f);
        internal static Vector3 Desk(int n) => new(X(n),.98f,38.45f);
        internal static Vector3 Punch(int i) => Desk(1)+Vector3.right*((i-1)*.72f);
        internal static Vector3 Tap(int i) => new(X(3)+(i-1)*1.20f,1.16f,32.40f);
        internal static Vector3 Bleed => new(X(4)-1.98f,1.16f,32.42f);
        internal static Vector3 Mouth(int i) => new(X(4)+i*1.27f,1.23f,32.45f);
        internal static Vector3 SealPull => Desk(5)+new Vector3(0,.18f,-.31f);
        internal static Vector3 Handset => Desk(5)+Vector3.left*.93f;

        internal static bool CanCross(NightState s,Vector3 from,Vector3 to)
        {
            if (Mathf.Max(from.z,to.z)<South || Mathf.Min(from.z,to.z)>North ||
                Mathf.Max(from.x,to.x)<West-.20f || Mathf.Min(from.x,to.x)>West+Width*4+.20f) return true;
            for(int n=s.circuit.room+1;n<=5;n++)
            {
                var door=Door(n);
                float a=from.x-door.x,b=to.x-door.x;
                if(Mathf.Min(a,b)>.20f || Mathf.Max(a,b)<-.20f) continue;
                float t=Mathf.Abs(b-a)<.0001f?.5f:Mathf.Clamp01(-a/(b-a));
                var crossing=Vector3.Lerp(from,to,t);
                if(Mathf.Abs(crossing.z-door.z)<1.48f && crossing.y<2.75f) return false;
            }
            return true;
        }
    }

    internal sealed partial class NightWorld
    {
        readonly GameObject[] wingShutters=new GameObject[5], brassIndicators=new GameObject[3], witnessHoles=new GameObject[3];
        GameObject dispatchCylinder, sealedWindow, datedReceipt;
        Material lampAmber;

        void DressReturnWing()
        {
            var wing=new GameObject("东堤旧票务所").transform;
            wing.SetParent(root,false);
            const float h=3.22f;
            const float west=ReturnWing.West, south=ReturnWing.South, north=ReturnWing.North;
            float east=west+ReturnWing.Rooms.Length*ReturnWing.Width;
            RegisterInterior(Rect.MinMaxRect(west,south,east,north),0,h);
            void Wall(Vector3 at,Vector3 size,Material finish) => StreetRoomSurface(wing,"承重墙",at,size,finish,1.8f);
            Wall(new((west+east)/2,h/2,south),new(east-west,h,.22f),brick);
            Wall(new((west+east)/2,h/2,north),new(east-west,h,.24f),plaster);
            Wall(new(east,h/2,(south+north)/2),new(.24f,h,north-south),brick);
            StreetRoomSurface(wing,"屋面",new((west+east)/2,h+.07f,36),new(east-west+.4f,.14f,8.44f),ceiling,2.1f,true);
            lampAmber=Material(new Color(.84f,.67f,.38f));
            lampAmber.EnableKeyword("_EMISSION"); lampAmber.SetColor("_EmissionColor",new Color(.95f,.59f,.19f)*1.35f);
            var slip=Material(new Color(.83f,.78f,.65f));
            slip.mainTexture=Resources.Load<Texture2D>("Interface/PreviewMeshes/FaultPaper");

            for(int n=1;n<=5;n++)
            {
                float x=ReturnWing.X(n);
                var door=ReturnWing.Door(n);
                float low=door.z-1.18f, high=door.z+1.18f;
                var finish=n%2==0?glazed:plaster;
                Wall(new(door.x,h/2,(south+low)/2),new(.22f,h,low-south),finish);
                Wall(new(door.x,h/2,(high+north)/2),new(.22f,h,north-high),finish);
                Wall(new(door.x,2.91f,door.z),new(.23f,.62f,2.38f),paintedIron);
                var leaf=Box("钢皮推闸",door+Vector3.up*1.30f,new(.13f,2.6f,2.36f),paintedIron,wing);
                leaf.layer=FloorRoutes.DoorLayer;
                wingShutters[n-1]=leaf;
                for(int j=0;j<5;j++)
                {
                    var rib=Box("压筋",door+new Vector3(-.083f,.35f+j*.44f,0),new(.035f,.035f,2.29f),steel,wing);
                    rib.GetComponent<Collider>().enabled=false;
                    rib.transform.SetParent(leaf.transform,true);
                }
                StreetRoomSurface(wing,"磨损地面",new(x,-.06f,36),new(5.99f,.14f,7.98f),n%2==0?tiles:terrazzo,2.1f,true);
                StreetRoomProp(wing,"metal_office_desk","操作台",ReturnWing.Desk(n)-Vector3.up*.955f,new(2.85f,.89f,1.02f),0);
                WallSign(ReturnWing.Rooms[n-1],new(x,1.98f,39.85f),0,new(1.3f,.29f),.14f);
                StreetRoomLight(wing,new(x+.62f,2.82f,38.61f),new(-.24f,-1,-.24f),
                    n==3?new Color(.55f,.78f,.71f):new Color(.92f,.71f,.48f),2.1f,h);
            }
            WallSign("票据交换",new(west-.15f,2.79f,34.8f),90,new(1.12f,.27f),.14f);
            StreetRoomProp(wing,"painted_wooden_bench","候班座",new(west-2.2f,.015f,38.2f),new(2.05f,.52f,.6f),90);
            StreetRoomLight(wing,new(west-.26f,2.5f,35),new(-.5f,-1,0),new(.77f,.64f,.45f),1.55f,h);
            for(int i=0;i<3;i++)
            {
                var punch=ReturnWing.Punch(i);
                Box("剪票钳座",punch,new(.28f,.07f,.27f),steel,wing);
                Box("剪票压柄",punch+new Vector3(0,.092f,-.02f),new(.055f,.12f,.27f),brass,wing);
                WallSign(ReturnCircuit.Holes[i],punch+new Vector3(0,.10f,.22f),0,new(.30f,.14f),.078f);
            }
            StreetRoomProp(wing,"worn_metal_rack","票根架",new(ReturnWing.X(1)+2.27f,.02f,38),new(1.22f,1.9f,.48f),90);
            StreetRoomProp(wing,"painted_wooden_chair_02","收票员转身留下的椅子",new(ReturnWing.X(1)-1.72f,.02f,37.5f),new(.57f,.85f,.59f),-34);
            StreetRoomProp(wing,"vintage_suitcase","票据箱",new(ReturnWing.X(1)+1.78f,.02f,32.84f),new(.91f,.52f,.45f),11);
            var lightTable=ReturnWing.Desk(2);
            Box("透光架底座",lightTable,new(.96f,.12f,.56f),wood,wing);
            Box("磨砂玻璃",lightTable+Vector3.up*.071f,new(.83f,.016f,.45f),lampAmber,wing);
            Folio(lightTable+new Vector3(.16f,.086f,0),"翻面的复写纸",slip);
            StreetRoomProp(wing,"laundry-basket","收废片的藤筐",new(ReturnWing.X(2)-1.97f,.02f,38.5f),new(.70f,.79f,.66f),8);
            var line=new Vector3(ReturnWing.X(2),2.04f,32.68f);
            Trim(line,steel).Pipe(line-Vector3.right*2.4f,line+Vector3.right*2.4f,.014f);
            for(int i=0;i<6;i++)
            {
                var clip=line+Vector3.right*(i-2.5f)*.62f;
                var sheet=Box("晾晒的背印纸",clip-Vector3.up*.22f,new(.30f,.43f,.008f),slip,wing);
                sheet.GetComponent<Collider>().enabled=false;
                sheet.transform.rotation=Quaternion.Euler(0,(i%3-1)*9,0);
                Box("夹子",clip,new(.04f,.08f,.03f),brass,wing).GetComponent<Collider>().enabled=false;
            }
            StreetRoomProp(wing,"painted_wooden_bench","听线长凳",new(ReturnWing.X(3)+2.37f,.02f,38.12f),new(1.40f,.48f,.46f),90);
            StreetRoomProp(wing,"vintage_radio_transceiver","拾音接收机",ReturnWing.Desk(3)-Vector3.up*.06f,new(.71f,.33f,.45f),180,false);
            for(int i=0;i<3;i++)
            {
                var at=ReturnWing.Tap(i);
                var pipe=Trim(at,i==1?brass:paintedIron);
                pipe.Pipe(at-Vector3.up*1.12f,at,.065f);
                pipe.Pipe(at,at+Vector3.forward*.13f,.088f);
                brassIndicators[i]=Box("拾音灯",at+Vector3.up*.25f,new(.11f,.12f,.07f),lampAmber,wing);
                brassIndicators[i].GetComponent<Collider>().enabled=false;
                WallSign(ReturnCircuit.Pipes[i],at+new Vector3(0,.40f,-.12f),180,new(.28f,.18f),.12f);
            }
            ShowSoundingPipe(-1);
            dispatchCylinder=Shape("气送铜筒",PrimitiveType.Cylinder,ReturnWing.Desk(4)+Vector3.up*.06f,new(.145f,.24f,.145f),brass,new(0,0,90));
            for(int i=0;i<2;i++)
            {
                var at=ReturnWing.Mouth(i);
                var tube=Trim(at,steel);
                tube.Pipe(at+Vector3.down*1.19f,at,.15f);
                tube.Pipe(at,at+Vector3.forward*.17f,.18f);
                WallSign(i==0?"甲":"乙",at+new Vector3(0,.32f,-.10f),180,new(.32f,.21f),.14f);
            }
            // Follow each tube along the ceiling into the receiving room.
            for(int i=0;i<2;i++)
            {
                Vector3 near=ReturnWing.Mouth(i)+new Vector3(0,0,-.12f);
                Vector3 up=new(near.x,2.73f+i*.19f,near.z);
                var pipes=Trim(near,i==0?paintedIron:brass);
                pipes.Pipe(near,up,.065f); pipes.Pipe(up,new(ReturnWing.X(5)+1.8f,up.y,up.z),.065f);
            }
            StreetRoomProp(wing,"tool_cart","气筒保养车",new(ReturnWing.X(4)+2.10f,.02f,38.68f),new(.98f,.88f,.61f),17);
            StreetRoomProp(wing,"painted_wooden_chair_02","对岸留下的座椅",new(ReturnWing.X(5)+1.85f,.02f,33.26f),new(.61f,.87f,.61f),-153);
            StreetRoomProp(wing,"vintage_suitcase","封签行李",new(ReturnWing.X(5)+1.17f,.02f,33.24f),new(.77f,.47f,.34f),-18);
            var bleed=ReturnWing.Bleed;
            Trim(bleed,paintedIron).Pipe(bleed-Vector3.up*1.13f,bleed,.065f);
            Box("泄压阀柄",bleed,new(.32f,.08f,.16f),brass,wing);
            var last=ReturnWing.Desk(5);
            StreetRoomProp(wing,"vintage_radio_transceiver","手摇送话器",ReturnWing.Handset-Vector3.up*.06f,new(.62f,.28f,.34f),180,false);
            sealedWindow=Box("签收窗铅封罩",last+Vector3.up*.16f,new(.78f,.40f,.54f),paintedIron,wing);
            var handle=Box("铅封拉环",ReturnWing.SealPull,new(.18f,.05f,.08f),brass,wing);
            handle.GetComponent<Collider>().enabled=false;
            handle.transform.SetParent(sealedWindow.transform,true);
            datedReceipt=Box("八七年存根",last,new(.66f,.015f,.44f),slip,wing);
            var date=new GameObject("落地联戳记").AddComponent<TextMesh>();
            date.transform.SetParent(root,false);
            date.transform.position=last+new Vector3(0,.012f,.065f);
            date.font=signFont;
            date.fontSize=50;
            date.characterSize=.08f;
            date.anchor=TextAnchor.MiddleCenter;
            date.alignment=TextAlignment.Center;
            date.color=new Color(.21f,.15f,.09f);
            date.GetComponent<Renderer>().sharedMaterial=signInk;
            date.text="落地联　1987.07.17";
            FitLetters(date,.55f,.095f);
            date.transform.rotation=Quaternion.Euler(90,0,0);
            date.transform.SetParent(datedReceipt.transform,true);
            for(int i=0;i<3;i++)
                witnessHoles[i]=Box("穿票孔",last+new Vector3((i-1)*.18f,.012f,-.11f),new(.055f,.008f,.055f),steel,wing);
        }
        internal void ShowSoundingPipe(int index)
        {
            for(int i=0;i<brassIndicators.Length;i++) if(brassIndicators[i]) brassIndicators[i].SetActive(i==index);
        }
        internal void RefreshReturnWing(NightState s)
        {
            for(int n=1;n<=5;n++) wingShutters[n-1].SetActive(s.circuit.room<n);
            dispatchCylinder.SetActive(s.circuit.room==4 && !s.circuit.capsule);
            sealedWindow.SetActive(!s.circuit.coverLifted);
            datedReceipt.SetActive(s.circuit.coverLifted);
            for(int i=0;i<3;i++) witnessHoles[i].SetActive(s.circuit.coverLifted && s.circuit.punch==i);
        }
    }
}
