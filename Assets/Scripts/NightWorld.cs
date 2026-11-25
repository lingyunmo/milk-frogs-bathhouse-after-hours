using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed class NightWorld
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
            ApplyFinish();
            foreach(Transform floor in GameObject.Find("浴场").transform)
            {
                var hall=floor.Find("横廊");
                AddCeiling(hall,floor.name.StartsWith("3F"));
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
                    AddLamp(new Vector3(p.x,y+2.85f,p.z),false,.65f,8);
                    AddLamp(new Vector3(p.x,y+2.95f,12),true,1.45f,8);
                }
            }
            AddDoor("地下防火门",new Vector3(14,-3.6f,10),true);
            foreach (int x in new[]{0,25,52,78,102,129,153,164})
            {
                var p=new Vector3(x,0,-14);
                Box("路灯杆",p+Vector3.up*2.2f,new Vector3(.13f,4.4f,.13f),wood);
                Box("路灯灯罩",p+new Vector3(0,4.4f,-.28f),new Vector3(.3f,.16f,.8f),brass);
                AddLamp(p+new Vector3(0,4.25f,-.55f),false,2.4f,15);
            }
            Box("卡座背板",new Vector3(-10,0.55f,5.1f),new Vector3(4,.98f,.2f),wood);
            Box("卡座座面",new Vector3(-10,.37f,4.6f),new Vector3(4,.14f,.86f),wood);
            Box("床单内侧",-Vector3.right*15+new Vector3(0,1.28f,19.5f),new Vector3(3.6f,2.18f,.07f),wood);
            // Guardrails bound the outside balconies. The departure edge stays clear.
            Box("二楼露台护栏",new Vector3(10,4.02f,28.50f),new Vector3(5.4f,.8f,.12f),wood);
            Box("露台左护栏",new Vector3(7.25f,4.02f,26.25f),new Vector3(.12f,.8f,4.4f),wood);
            Box("露台右护栏",new Vector3(12.75f,4.02f,26.25f),new Vector3(.12f,.8f,4.4f),wood);
            // Continuous path around the building and parking pit, filling the whitebox's east apron gaps.
            Box("后巷东段",new Vector3(53,-.11f,13.5f),new Vector3(14,.2f,21),wood);
            Box("停车棚南沿",new Vector3(53,-.11f,.0f),new Vector3(14,.2f,.1f),wood);

            Crate=Box("支援补给箱",Vector3.zero,new Vector3(.92f,.54f,.64f),olive);
            Crate.SetActive(false);
            Pickup("torch",new Vector3(-2,4.13f,21),new Vector3(.29f,.09f,.10f),brass);
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
            Physics.SyncTransforms();
        }

        void ApplyFinish()
        {
            var sun=GameObject.Find("白盒照明").GetComponent<Light>();
            sun.intensity=.25f;
            sun.color=new Color(.63f,.74f,1);
            RenderSettings.ambientLight=new Color(.24f,.25f,.29f);
            RenderSettings.fog=true;
            RenderSettings.fogColor=new Color(.038f,.050f,.066f);
            RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogDensity=.012f;
            Camera.main.clearFlags=CameraClearFlags.SolidColor;
            Camera.main.backgroundColor=RenderSettings.fogColor;
            var finishes=new Dictionary<string,Texture2D>
            {
                {"瓷砖地坪",Resources.Load<Texture2D>("Surfaces/Tiles074")},
                {"室内墙",Resources.Load<Texture2D>("Surfaces/Concrete034")},
                {"柏油",Resources.Load<Texture2D>("Surfaces/Asphalt017")}
            };
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!finishes.TryGetValue(renderer.sharedMaterial.name,out var texture)||!texture) continue;
                var size=renderer.transform.localScale;
                renderer.material.mainTexture=texture;
                renderer.material.mainTextureScale=new Vector2(Mathf.Max(1,size.x/2),Mathf.Max(1,Mathf.Max(size.z,size.y)/2));
            }
        }

        void AddCeiling(Transform slab,bool topFloor)
        {
            if(!slab)return;
            var dimensions=slab.localScale;
            var height=topFloor?3.51f:-.119f;
            var cover=Box("抹灰顶面",slab.position+Vector3.up*height,new Vector3(dimensions.x,.037f,dimensions.z),ceiling);
            if(!topFloor)cover.GetComponent<Collider>().enabled=false;
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
            caption.font=Resources.Load<Font>("Typeface/AfterHours");
            caption.fontSize=42;
            caption.characterSize=.125f;
            caption.color=new Color(.82f,.78f,.66f);
            caption.text=name;
            caption.GetComponent<Renderer>().sharedMaterial=caption.font.material;
        }

        void AddLamp(Vector3 p,bool switched,float intensity,float range)
        {
            var lamp=new GameObject("灯").AddComponent<Light>();
            lamp.transform.SetParent(root);
            lamp.transform.position=p;
            lamp.type=LightType.Point;
            lamp.range=range;
            lamp.color=switched?new Color(1,.84f,.62f):new Color(.66f,.81f,1);
            lamp.intensity=intensity;
            if(switched){ workLights.Add(lamp); lamp.enabled=false; }
        }

        void AddDoor(string name,Vector3 p,bool fire=false)
        {
            var leaf=Box(name+" 门",p+new Vector3(0,1.21f,0),new Vector3(fire?1.91f:1.36f,2.42f,.10f),fire?red:wood);
            leaf.layer=FloorRoutes.DoorLayer;
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
