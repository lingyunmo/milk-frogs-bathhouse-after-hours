using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        void SetDressingDetails()
        {
            DressArchitecturalDetails();
            Prop("worn_metal_rack","开放式更衣架",new Vector3(2.85f,3.60f,22.45f),new Vector3(1.46f,2.02f,.56f),0);
            Prop("cardboard_box_01","未拆封拖鞋",new Vector3(2.78f,3.6f+ShelfTop(2.02f,1),22.44f),new Vector3(.51f,.40f,.40f),4,false);
            Prop("cardboard_box_01","储藏架布草箱",new Vector3(-5.6f,ShelfTop(2.1f,1),22.5f),new Vector3(.59f,.41f,.43f),-5,false);
            Prop("cardboard_box_01","前台资料箱",new Vector3(7.1f,0,.57f),new Vector3(.57f,.35f,.43f),22);
            Prop("worn_metal_rack","营业记录和毛巾架",new Vector3(7.65f,0,1.12f),new Vector3(2.15f,1.69f,.49f),0);
            Prop("cardboard_box_01","结存账簿箱",new Vector3(7.65f,ShelfTop(1.69f,2),1.12f),new Vector3(.59f,.30f,.43f),2,false);
            Prop("worn_metal_rack","沙发厅杂志架",new Vector3(-12.0f,3.6f,1.18f),new Vector3(1.30f,1.30f,.50f),0);
            Prop("tea_set_01","沙发厅留下的茶壶",new Vector3(-17.64f,4.09f,2.39f),new Vector3(.51f,.17f,.47f),15,false);

            const float front = NightMainRing.Front;
            var canopy=Box("浴场前檐",new Vector3(0,2.89f,front-.98f),new Vector3(6.0f,.23f,2.16f),steel);
            Surface(canopy,plaster,1.5f);
            var entranceTrim=Box("门头石线",new Vector3(0,2.54f,front-.17f),new Vector3(2.45f,.22f,.20f),plaster);
            Surface(entranceTrim,plaster,1.25f);
            EnamelSign("奶娃浴场", 0, new Vector3(0,3.65f,front-.19f), 0, new Vector2(5.18f,.87f));
            WallSign("已打烊",new Vector3(1.50f,1.40f,front-.16f),0,new Vector2(.75f,.31f),.116f);
            WallSign("营业登记",new Vector3(8.95f,2.08f,1.12f),180,new Vector2(1.61f,.35f),.143f);
            WallSign("水疗区",new Vector3(5.63f,2.02f,23.88f),0,new Vector2(1.15f,.30f),.116f);
            WallSign("备餐与茶水",new Vector3(-10.79f,1.45f,.12f),180,new Vector2(1.81f,.27f),.113f);
            WallSign("更衣与拖鞋",new Vector3(-1.02f,6.01f,23.88f),0,new Vector2(1.78f,.31f),.132f);
            WallSign("石桥路　桥杆・站牌 →",new Vector3(100,2.29f,-10.34f),0,new Vector2(2.94f,.37f),.17f);
            foreach (float x in new[]{16f,181f})
            {
                var post=new Vector3(x,1.05f,-13.1f);
                Box("街尾路标柱",post,new(.10f,2.10f,.10f),paintedIron);
                WallSign(x<100 ? "街尾站牌 →" : "← 街尾站牌",post+new Vector3(0,.94f,-.09f),0,new(1.85f,.38f),.16f);
            }
            WallSign("救援接应",new Vector3(160,1.84f,-14.923f),0,new Vector2(.78f,.42f),.101f);
            Prop("metal_trash_can","门外金属废物筒",new Vector3(2.84f,0,-13.45f),new Vector3(.45f,.74f,.45f),-8,true);
            Prop("metal_trash_can","上客站垃圾桶",new Vector3(102.9f,0,-8.4f),new Vector3(.52f,.83f,.52f),17);
            Prop("potted_plant_01","前台盆栽",new Vector3(-1.57f,0,3.3f),new Vector3(.82f,1.22f,.77f),-13);
            foreach(float floor in new[]{3.6f,7.2f})
            foreach(float x in new[]{-16.4f,9.55f})
                Prop("rollershutter_window_01","已降下的外窗卷帘",new Vector3(x,floor+1.93f,-12.945f),
                    new Vector3(2.25f,.77f,.18f),180,false);
            Fixture(new Vector3(.0f,4.36f,front-.88f),new Vector3(0,-1,.65f),new Color(1,.73f,.45f),2.7f,6,104);
            Fixture(new Vector3(-9.7f,2.4f,2.25f),new Vector3(-.23f,-1,-.49f),new Color(.98f,.78f,.55f),1.85f,4,90);
            Fixture(new Vector3(101,2.23f,-9.04f),new Vector3(0,-1,.42f),new Color(.79f,.88f,1),1.9f,6,109);

            foreach (float x in new[] { -11f, 48f, 108f, 168f })
                Prop("public-housing-slab", "路对面老式住宅", new Vector3(x,-.4f,-45.3f),
                    new Vector3(54,15.8f,12.4f), 0, false);
            for (int i = 0; i < 5; i++)
            {
                float x = 28 + i * 32;
                var plinth = Box("路肩石台", new Vector3(x,.22f,-33), new Vector3(25,.44f,1.4f),plaster);
                Surface(plinth, plaster, 2.7f);
            }
        }

        // The four painted fascias occupy separate measured strips of the same original scan.
        void EnamelSign(string name, int strip, Vector3 center, float yaw, Vector2 size)
        {
            var rotation = Quaternion.Euler(0, yaw, 0);
            var mounting = Box(name + "招牌底座", center, new Vector3(size.x+.034f,size.y+.034f,.085f), steel);
            mounting.transform.rotation = rotation;
            mounting.GetComponent<Collider>().enabled = false;
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = name + "旧搪瓷";
            face.transform.SetParent(root, false);
            face.transform.SetPositionAndRotation(center + rotation * Vector3.back * .045f, rotation);
            face.transform.localScale = new Vector3(size.x,size.y,1);
            face.GetComponent<Collider>().enabled = false;
            var enamel = Pbr(name, "Surfaces/ShiqiaoEnamel", null, null, Color.white, .58f);
            float[] boundaries = { 1f, .747f, .492f, .249f, 0f };
            float lower = boundaries[strip+1], upper = boundaries[strip];
            enamel.mainTextureScale = new Vector2(.996f, upper-lower-.004f);
            enamel.mainTextureOffset = new Vector2(.002f, lower+.002f);
            face.GetComponent<Renderer>().sharedMaterial = enamel;
        }

        TextMesh WallSign(string words,Vector3 center,float yaw, Vector2 size,float letters)
        {
            var baseplate=Box("搪瓷牌",center,new Vector3(size.x,size.y,.067f),steel);
            baseplate.GetComponent<Collider>().enabled=false;
            var rotation=Quaternion.Euler(0,yaw,0);
            var panel=Box("搪瓷牌面",center+rotation*Vector3.back*.039f,
                new Vector3(size.x-.037f,size.y-.035f,.008f),wood);
            panel.GetComponent<Collider>().enabled=false;
            var caption=new GameObject(words).AddComponent<TextMesh>();
            caption.transform.SetParent(root);
            caption.transform.SetPositionAndRotation(center+rotation*Vector3.back*.048f,rotation);
            caption.anchor=TextAnchor.MiddleCenter;
            caption.alignment=TextAlignment.Center;
            caption.font=signFont;
            caption.fontSize=52;
            caption.characterSize=letters;
            caption.color=new Color(.80f,.75f,.62f);
            var renderer=caption.GetComponent<Renderer>();
            renderer.sharedMaterial=signInk;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            caption.text=words;
            FitLetters(caption,size.x*.88f,size.y*.68f);
            return caption;
        }

        static void FitLetters(TextMesh caption,float width,float height)
        {
            Vector3 actual=caption.GetComponent<Renderer>().bounds.size;
            float scale=Mathf.Min(width/Mathf.Max(.001f,actual.x),height/Mathf.Max(.001f,actual.y));
            caption.transform.localScale *= scale;
        }
    }
}
