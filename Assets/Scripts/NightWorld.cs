using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        internal sealed class Door
        {
            internal string Name;
            internal GameObject Leaf;
            internal Vector3 Position;
            internal bool Fire;
        }

        internal readonly List<Door> Doors=new();
        internal GameObject Crate, Rescue;
        readonly List<(string id,GameObject model)> pickups=new();
        readonly List<Light> workLights = new();
        readonly NightGame game;
        readonly Material wood, brass, olive, red, ceiling;
        readonly Transform root;
        Font signFont;
        Material signInk;
        internal NightWorld(NightGame game)
        {
            this.game=game;
            root=new GameObject("First night runtime").transform;
            wood=Material(new Color(.29f,.22f,.16f));
            brass=Material(new Color(.55f,.36f,.12f));
            olive=Material(new Color(.30f,.43f,.28f));
            red=Material(new Color(.45f,.10f,.07f));
            ceiling=Material(new Color(.32f,.34f,.34f));
            ceiling.mainTexture=Resources.Load<Texture2D>("Surfaces/Concrete034");
            ceiling.mainTextureScale=new Vector2(4,4);
            signFont=Resources.Load<Font>("Typeface/AfterHours");
            signInk=new Material(Resources.Load<Shader>("DepthLabel"));
            RefreshSignAtlas(signFont);
            Font.textureRebuilt+=RefreshSignAtlas;
        }

        static Material Material(Color color) => new(Shader.Find("Standard")) { color=color };
        GameObject Box(string name,Vector3 p,Vector3 scale,Material surface,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name=name;
            go.transform.SetParent(parent?parent:root);
            go.transform.position=p;
            go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=surface;
            return go;
        }

        internal void Build()
        {
            Lighting();
            CreateSurfaces();
            SkinArchitecture();
            foreach(Transform floor in GameObject.Find("浴场").transform)
            {
                AddStairRamps(floor.Find("西北勤务梯段"),-18,14,1);
                AddStairRamps(floor.Find("东南前厅梯段"),14,10,-1);
                var hall=floor.Find("横廊");
                AddCeiling(hall,floor.name.StartsWith("3F"));
                if(hall)CorridorLighting(hall.position.y+.10f);
                foreach(Transform room in floor)
                {
                    Transform slab=room.Find("地坪");
                    if (!slab)continue;
                    var p=slab.position;
                    AddCeiling(slab,floor.name.StartsWith("3F"));
                    float y=p.y+.1f;
                    var entry=new Vector3(p.x,y,p.z>12?14:10);
                    if (!new HashSet<string>{"门厅","茶座","大浴池","更衣室","沙发厅"}.Contains(room.name))
                        AddDoor(room.name,entry);
                    LabelRoom(room.name,entry,p.z>12);
                    InteriorLight(new Vector3(p.x,y+3.16f,p.z),room.name);
                }
            }
            AddDoor("地下防火门",new Vector3(14,-3.6f,10),true);
            foreach (int x in new[]{0,25,52,78,102,129,153,164})
            {
                var p=new Vector3(x,0,-14);
                Box("路灯杆",p+Vector3.up*2.2f,new Vector3(.13f,4.4f,.13f),wood);
                Box("路灯灯罩",p+new Vector3(0,4.4f,-.28f),new Vector3(.3f,.16f,.8f),brass);
                Fixture(p+new Vector3(0,4.19f,-.55f),new Vector3(0,-1,-.33f),
                    new Color(.96f,.81f,.62f),2.35f,14,115);
            }
            Box("卡座背板",new Vector3(-10,0.55f,5.1f),new Vector3(4,.98f,.2f),wood);
            Box("卡座座面",new Vector3(-10,.37f,4.6f),new Vector3(4,.14f,.86f),wood);
            Box("床单内侧",-Vector3.right*15+new Vector3(0,1.28f,19.5f),new Vector3(3.6f,2.18f,.07f),wood);
            // Guardrails bound the outside balconies. The departure edge stays clear.
            Box("二楼露台护栏",new Vector3(10,4.02f,28.50f),new Vector3(5.4f,.8f,.12f),wood);
            Box("露台左护栏",new Vector3(7.25f,4.02f,26.25f),new Vector3(.12f,.8f,4.4f),wood);
            Box("露台右护栏",new Vector3(12.75f,4.02f,26.25f),new Vector3(.12f,.8f,4.4f),wood);
            Crate=Box("支援补给箱",Vector3.zero,new Vector3(.92f,.54f,.64f),olive);
            Crate.SetActive(false);
            Pickup("receipt",new Vector3(9,1.00f,3.74f),new Vector3(.26f,.014f,.22f),brass);
            Pickup("motor",new Vector3(-14,-2.42f,4.12f),new Vector3(.19f,.13f,.18f),brass);
            Pickup("seal",new Vector3(8,4.80f,4.45f),new Vector3(.13f,.10f,.12f),brass);
            Pickup("cell-reception",new Vector3(10,.98f,4),new Vector3(.12f,.09f,.11f),olive);
            Pickup("cell-storage",new Vector3(-2,-2.54f,5),new Vector3(.12f,.10f,.11f),olive);
            Pickup("cell-staff",new Vector3(-9,8.15f,18),new Vector3(.12f,.09f,.11f),olive);
            Pickup("cell-parking",new Vector3(49,.051f,4.8f),new Vector3(.12f,.09f,.11f),olive);
            Rescue=new GameObject("救援车");
            Rescue.transform.SetParent(root);
            Box("车身",new Vector3(160,-.45f,-20)+Vector3.up*1.25f,new Vector3(5,1.48f,2.1f),olive,Rescue.transform);
            Box("车厢",new Vector3(159.7f,1.78f,-20),new Vector3(3.1f,.7f,2.05f),wood,Rescue.transform);
            foreach(float x in new[]{158.5f,161.5f})
            foreach(float z in new[]{-19f,-21f})
                Box("轮胎",new Vector3(x,.32f,z),new Vector3(.72f,.64f,.23f),wood,Rescue.transform);
            Box("车顶警示灯",new Vector3(161.0f,2.22f,-20),new Vector3(.82f,.19f,.3f),red,Rescue.transform);
            Rescue.SetActive(false);
            DressRooms();
            BoundTheStreet();
            DressRoute();
            game.Player.AttachHeldTorch(HeldFlashlight());
            Physics.SyncTransforms();
        }

        void AddStairRamps(Transform stair,float x,float entry,float direction)
        {
            if(!stair)return;
            var platform=stair.Find("近廊平台");
            if(!stair.Find("转向平台"))return;
            foreach (Transform step in stair)
                if(step.name=="上行"||step.name=="折返")
                    step.GetComponent<Collider>().enabled=false;
            float y=platform.position.y+.10f;
            // Use one smooth collision surface per flight, while the original treads provide the silhouette.
            foreach(bool returning in new[]{false,true})
            {
                float center=x+(returning?.93f:-.93f);
                var a=new Vector3(center,y+(returning?3.6f:0),entry+direction*1.94f);
                var b=new Vector3(center,y+1.8f,entry+direction*6.48f);
                var rotation=Quaternion.FromToRotation(Vector3.forward,(b-a).normalized);
                var o=Box("梯段碰撞面",(a+b)*.5f-rotation*Vector3.up*.055f,new Vector3(1.84f,.11f,Vector3.Distance(a,b)),wood);
                o.transform.rotation=rotation;
                o.GetComponent<Renderer>().enabled=false;
            }
        }

        void AddCeiling(Transform slab,bool topFloor)
        {
            if(!slab)return;
            var dimensions=slab.localScale;
            var width=new Vector3(dimensions.x,.042f,dimensions.z);
            var cover=Box("抹灰顶面",slab.position-Vector3.up*.129f,width,ceiling);
            cover.GetComponent<Collider>().enabled=false;
            Surface(cover,ceiling,2.5f);
            if(topFloor)
            {
                var roof=Box("屋顶",slab.position+Vector3.up*3.51f,width,ceiling);
                Surface(roof,ceiling,2.5f);
            }
        }

        void LabelRoom(string name,Vector3 entry,bool north)
        {
            float side=north?-1:1;
            Vector3 center=entry+new Vector3(0,2.66f,side*.095f);
            var sign=Box("门牌",center,new Vector3(1.04f,.18f,.035f),wood);
            sign.GetComponent<Collider>().enabled=false;
            var caption=new GameObject(name).AddComponent<TextMesh>();
            caption.transform.SetParent(root);
            caption.transform.position=center+Vector3.forward*side*.022f;
            caption.transform.rotation=Quaternion.Euler(0,north?0:180,0);
            caption.anchor=TextAnchor.MiddleCenter;
            caption.alignment=TextAlignment.Center;
            caption.font=signFont;
            caption.fontSize=42;
            caption.characterSize=.095f;
            caption.color=new Color(.82f,.78f,.66f);
            caption.text=name;
            var renderer=caption.GetComponent<Renderer>();
            renderer.sharedMaterial=signInk;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            FitLetters(caption,.85f,.117f);
        }

        void RefreshSignAtlas(Font font)
        {
            if(font==signFont)signInk.mainTexture=font.material.mainTexture;
        }

        internal void Dispose()
        {
            Font.textureRebuilt-=RefreshSignAtlas;
        }

        void AddDoor(string name,Vector3 p,bool fire=false)
        {
            var leaf=Box(name+" 门",p+new Vector3(0,1.21f,0),new Vector3(fire?1.91f:1.36f,2.42f,.10f),fire?red:wood);
            Surface(leaf,fire?red:planks,1.25f);
            leaf.layer=FloorRoutes.DoorLayer;
            foreach(float direction in new[]{-1f,1f})
            {
                var handle=Box("门把",p+new Vector3(.44f,1.10f,.073f*direction),new Vector3(.135f,.055f,.092f),brass);
                handle.transform.SetParent(leaf.transform,true);
                handle.GetComponent<Collider>().enabled=false;
            }
            var door=new Door{Name=name,Leaf=leaf,Position=p,Fire=fire};
            Doors.Add(door);
            game.Target(p+Vector3.up*1.08f,fire?"推开地下防火门":"推门 · "+name,0,()=>game.ToggleDoor(door));
        }

        void Pickup(string id,Vector3 point,Vector3 scale, Material mat)
        {
            var o=Box(id,point,scale,mat);
            o.GetComponent<Collider>().enabled=false;
            pickups.Add((id,o));
        }

        internal void AnimateArrival()
        {
            NightState s=game.State;
            bool descend=!s.dropTaken&&s.dropTime>=0&&s.dropTime<5;
            Crate.SetActive(descend);
            if(descend)Crate.transform.position=DropPoint(s)+Vector3.up*(s.dropTime*1.25f-.22f);
            bool arriving=s.rescueTime>=0&&s.rescueTime<7;
            Rescue.SetActive(arriving);
            if(arriving)
            {
                Rescue.transform.position=Vector3.right*(s.rescueTime*3.0f);
                foreach(var part in Rescue.GetComponentsInChildren<Collider>())part.enabled=s.rescueTime==0;
            }
        }

        internal void Refresh()
        {
            NightState s=game.State;
            foreach (var door in Doors)
            {
                bool open=door.Fire?s.firstFireDoor:s.openDoors.Contains(door.Name);
                door.Leaf.SetActive(!open);
            }
            foreach (var lamp in workLights)lamp.enabled=s.power;
            foreach(var (id,model) in pickups)model.SetActive(id=="torch"?!s.torch:!s.collected.Contains(id));
            AnimateArrival();
        }

        internal static Vector3 DropPoint(NightState s) => s.rearDrop?new(-9,.5f,30):new(56,.5f,5.5f);
    }
}
