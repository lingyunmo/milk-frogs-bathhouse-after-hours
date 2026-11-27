using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        [Serializable] sealed class PropSurfaces { public PropSurface[] surfaces; }
        [Serializable]
        sealed class PropSurface
        {
            public string name, color, normal, arm;
            public float[] tint;
            public float metal, rough;
        }
        readonly Dictionary<string, Material[]> propMaterials = new();
        const string NightFinish = "Finishes/Night/";
        Material plaster, paint, glazed, brick, tiles, terrazzo, planks, asphalt, steel, emergencyGlass;

        // These are the tray surfaces in the model's original 1.90 m envelope.
        static float ShelfTop(float rackHeight, int tier) => (.42f + .49f * (tier - 1)) * rackHeight / 1.90f;

        static Material Pbr(string name, string color, string normal, string finish, Color tint, float shine,
            bool ao = false, Shader shader = null)
        {
            if (!shader) shader = Resources.Load<Shader>("NightProp");
            var material = shader ? new Material(shader) { color = tint } : Material(tint);
            material.name = name;
            material.mainTexture = Resources.Load<Texture2D>(color);
            var bump = string.IsNullOrEmpty(normal) ? null : Resources.Load<Texture2D>(normal);
            if (bump)
            {
                material.SetTexture("_BumpMap", bump);
                material.SetFloat("_BumpScale", .70f);
                material.EnableKeyword("_NORMALMAP");
            }
            var packed = string.IsNullOrEmpty(finish) ? null : Resources.Load<Texture2D>(finish);
            material.SetFloat("_Glossiness", shine);
            if (packed)
            {
                material.SetTexture("_MetallicGlossMap", packed);
                material.SetFloat("_GlossMapScale", 1);
                material.EnableKeyword("_METALLICGLOSSMAP");
                if (material.HasProperty("_UseFinish"))
                {
                    material.SetFloat("_UseFinish", 1);
                    material.SetFloat("_UseAO", ao ? 1 : 0);
                }
                if (ao && material.HasProperty("_OcclusionMap")) material.SetTexture("_OcclusionMap", packed);
            }
            return material;
        }

        void CreateSurfaces()
        {
            var shader = Resources.Load<Shader>("WornArchitecture");
            var wearTexture = Resources.Load<Texture2D>(NightFinish + "CementColor");
            var grainNormal = Resources.Load<Texture2D>(NightFinish + "CementNormal");

            Material Worn(string name, string color, string normal, string finish, Color tint,
                float bump, float smoothness, Vector3 wear)
            {
                var material = Pbr(name, color, normal, finish, tint, smoothness, true, shader);
                material.SetFloat("_BumpScale", bump);
                material.SetFloat("_GlossMapScale", smoothness);
                material.SetTexture("_StainTex", wearTexture);
                material.SetTexture("_GrainNormal", grainNormal);
                float grain = name == "返潮釉面小砖" ? .15f : .26f;
                material.SetVector("_Wear", new Vector4(wear.x, wear.y, wear.z, grain));
                return material;
            }

            Material Photographed(string name, string source, Color tint, float bump, float smoothness, Vector3 wear)
            {
                string path = NightFinish + source;
                return Worn(name, path + "Color", path + "Normal", path + "Finish", tint, bump, smoothness, wear);
            }

            plaster = Photographed("磨损水泥", "Cement", new Color(.69f, .69f, .65f), .96f, .58f, new(.57f, 0, .03f));
            ceiling = Photographed("水泥顶棚", "Cement", new Color(.66f,.69f,.68f), .58f, .32f, new(.18f,0,0));
            paint = Worn("剥落灰泥", "Finishes/PaintColor", "Finishes/PaintNormal", "Finishes/PaintFinish",
                new Color(.60f, .63f, .58f), .95f, .48f, new(.76f, .88f, .04f));
            glazed = Photographed("返潮釉面小砖", "Ceramic", new Color(.50f, .67f, .62f), 1.12f, .84f, new(.52f, .78f, .20f));
            brick = Worn("外墙砖", "Finishes/BrickColor", "Finishes/BrickNormal", "Finishes/BrickFinish",
                new Color(.63f, .60f, .58f), 1.14f, .46f, new(.31f, .23f, .03f));
            void WallRelief(Material surface, string path, float depth)
            {
                surface.SetTexture("_HeightMap", Resources.Load<Texture2D>(path + "Relief"));
                surface.SetFloat("_WallDepth", depth);
                surface.EnableKeyword("WALL_RELIEF");
            }
            WallRelief(brick, "Finishes/Brick", .024f);
            WallRelief(paint, "Finishes/Paint", .010f);
            WallRelief(plaster, NightFinish + "Cement", .012f);
            WallRelief(glazed, NightFinish + "Ceramic", .016f);
            tiles = Photographed("冲洗区旧地砖", "Ceramic", new Color(.74f, .79f, .74f), 1.10f, .79f, new(.58f, 0, .12f));
            terrazzo = Photographed("更衣区磨石地坪", "Terrazzo", new Color(.69f, .72f, .67f), .94f, .66f, new(.47f, 0, .08f));
            planks = Worn("磨损旧木地板", "Finishes/WoodColor", "Finishes/WoodNormal", "Finishes/WoodFinish",
                new Color(.61f, .53f, .44f), .82f, .51f, new(.65f, 0, 0));
            asphalt = Worn("湿柏油", "Surfaces/Asphalt017", "Finishes/RoadNormal", "Finishes/RoadFinish",
                new Color(.65f, .69f, .72f), .88f, .67f, new(.46f, 0, .18f));
            asphalt.SetFloat("_Rain", 1);
            Material Enamel(string name, Color tint) => Pbr(name, "Finishes/Service/SteelColor",
                "Finishes/Service/SteelNormal", "Finishes/Service/SteelFinish", tint, .46f, true);
            steel = Enamel("旧搪瓷铁件", new(.59f, .66f, .63f));
            red = Enamel("朱漆防火门", new(.72f, .25f, .18f));
            wood = Pbr("老榆木", "Finishes/WoodColor", "Finishes/WoodNormal", "Finishes/WoodFinish",
                new(.66f, .56f, .45f), .40f, true);
            emergencyGlass = Material(new Color(.24f, .27f, .24f));
        }

        // World-sized UVs keep grout and wood grain consistent across wall segments and pieces of floor.
        void Surface(GameObject obj, Material material, float metres, bool keepBelowFloor = false, bool floor = false)
        {
            var filter = obj.GetComponent<MeshFilter>();
            if (!filter) return;
            var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
            mesh.name = "Tiled " + obj.name;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                // A whitebox wall can end on the exact plane of the floor above it.
                if (keepBelowFloor && vertices[i].y > .49f)
                    vertices[i].y -= .022f / obj.transform.localScale.y;
                Vector3 point = obj.transform.TransformPoint(vertices[i]);
                Vector3 direction = obj.transform.TransformDirection(normals[i]);
                uv[i] = Mathf.Abs(direction.y)>.6f ? new Vector2(point.x,point.z)/metres
                    : (Mathf.Abs(direction.x)>.6f ? new Vector2(point.z,point.y) : new Vector2(point.x,point.y))/metres;
            }
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            filter.sharedMesh = mesh;
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            if (floor)
            {
                var bounds = renderer.bounds;
                var properties = new MaterialPropertyBlock();
                properties.SetVector("_FloorBounds", new Vector4(bounds.center.x, bounds.center.z, bounds.extents.x, bounds.extents.z));
                void Pool(string slot, Vector4 patch)
                {
                    properties.SetVector(slot, patch);
                    water.AddPool(new Bounds(new Vector3(patch.x, bounds.max.y, patch.y),
                        new Vector3(patch.z * 2, .04f, patch.w * 2)));
                }
                if (material == asphalt)
                    water.AddPool(new Bounds(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z),
                        new Vector3(bounds.size.x, .04f, bounds.size.z)));
                if (obj.name == "横廊")
                {
                    if (bounds.center.y < -.2f)
                    {
                        Pool("_WetSourceA", new Vector4(-6.80f, 12.80f, 2.24f, .93f));
                        Pool("_WetSourceB", new Vector4(2.10f, 13.03f, 1.96f, .88f));
                    }
                    else if (bounds.center.y < 1f)
                        Pool("_WetSourceA", new Vector4(6.46f, 12.50f, 1.39f, .79f));
                }
                else if (obj.transform.parent && obj.transform.parent.name == "淋浴间")
                {
                    Pool("_WetSourceA", new Vector4(9.03f, 21.64f, 1.92f, 1.24f));
                    Pool("_WetSourceB", new Vector4(13.9f, 19.95f, 1.52f, 2.09f));
                }
                else if (obj.name == "南翼连续地坪" && material != planks)
                {
                    if (bounds.max.y < -1)
                        Pool("_WetSourceA", new Vector4(-11.2f,-9.9f,2.9f,.97f));
                    else if (bounds.max.y < 1)
                        Pool("_WetSourceA", new Vector4(.22f,-11.8f,2.2f,1.36f));
                    else
                        Pool("_WetSourceA", new Vector4(7.9f,-1.3f,1.46f,.72f));
                }
                else if (obj.name == "东连廊地坪")
                    Pool("_WetSourceA", new Vector4(20.7f,13.23f,1.64f,.48f));
                else if (obj.name == "副楼地坪" && material != planks)
                    Pool("_WetSourceA", new Vector4(28.5f,6.6f,1.78f,.86f));
                else if (material == tiles || obj.transform.parent.name is "停车棚泵房" or "街尾维修站")
                {
                    var c = bounds.center;
                    Pool("_WetSourceA", new Vector4(c.x - bounds.extents.x*.52f,c.z + bounds.extents.z*.49f,
                        Mathf.Min(2.4f,bounds.extents.x*.64f), Mathf.Min(1.3f,bounds.extents.z*.46f)));
                }
                renderer.SetPropertyBlock(properties);
            }
        }

        void SkinArchitecture()
        {
            var street = GameObject.Find("街区").transform;
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                GameObject obj = renderer.gameObject;
                var p = obj.transform.position;
                var size = obj.transform.localScale;
                switch (renderer.sharedMaterial.name)
                {
                    case "瓷砖地坪":
                        if (obj.transform.IsChildOf(street))
                        {
                            Surface(obj, plaster, 3.8f);
                            break;
                        }
                        bool lodge = obj.name=="地坪" && (obj.transform.parent.name is "沙发厅" or "宿舍" or "经理室" or "前台室" or "茶座" or "值班室");
                        bool wet = obj.transform.parent.name is "洗衣房" or "淋浴间" or "大浴池" or "盥洗室" or "水疗区";
                        var floor = lodge ? planks : wet ? tiles : p.y < -.2f || p.z < 0 ? plaster : terrazzo;
                        Surface(obj, floor, lodge ? 2.2f : floor == plaster ? 1.62f : 2f, floor: true);
                        break;
                    case "室内墙" when obj.name != "墙":
                        Surface(obj, paint, 1.7f);
                        break;
                    case "室内墙":
                        bool outside = p.z<=.01f||p.z>=23.99f||p.x<=-19.99f||p.x>=15.99f;
                        float top=p.y+size.y*.5f;
                        bool levelCap=Mathf.Abs(top-Mathf.Round(top/3.6f)*3.6f)<.025f;
                        Surface(obj,outside ? brick : paint,outside ? 2.2f : 1.7f,levelCap);
                        if (outside)
                        {
                            bool endWall = p.x<=-19.99f||p.x>=15.99f;
                            var offset = endWall ? Vector3.right*(p.x<0?.087f:-.087f) : Vector3.forward*(p.z<12?.087f:-.087f);
                            var innerSize = new Vector3(endWall?.016f:size.x,size.y,endWall?size.z:.016f);
                            var coat=Box("外墙内表面",p+offset,innerSize,paint);
                            coat.GetComponent<Collider>().enabled=false;
                            Surface(coat,paint,1.7f,levelCap);
                        }
                        float baseline=p.y-size.y*.5f;
                        bool meetsFloor=Mathf.Abs(baseline-Mathf.Round(baseline/3.6f)*3.6f)<.12f;
                        if (!outside && size.y>1 && meetsFloor)
                        {
                            float bandHeight=Mathf.Min(1.27f,size.y-.12f);
                            var band=Box("釉砖墙裙",new Vector3(p.x,baseline+bandHeight*.5f,p.z),
                                new Vector3(size.x+.032f,bandHeight,size.z+.032f),glazed);
                            band.GetComponent<Collider>().enabled=false;
                            Surface(band, glazed, 1.12f);

                            var cap = Box("深釉压条", new Vector3(p.x, baseline + bandHeight + .025f, p.z),
                                new Vector3(size.x + .076f, .052f, size.z + .076f), glazed);
                            cap.GetComponent<Collider>().enabled = false;
                            Surface(cap, glazed, 1.12f);
                        }
                        break;
                    case "柏油":
                        Surface(obj,asphalt,3.6f, floor: true);
                        break;
                    case "楼梯及平台":
                        Surface(obj,plaster,1.45f);
                        break;
                }
            }
        }

        GameObject Prop(string asset, string label, Vector3 feet, Vector3 size, float yaw=0, bool solid=true, string variant="Model")
        {
            string folder="Props/"+asset+"/";
            var prefab=Resources.Load<GameObject>(folder+variant);
            if(!prefab)throw new InvalidOperationException("Missing furniture: "+asset);
            var group=new GameObject(label);
            group.transform.SetParent(root);
            var fit=new GameObject("Fit model to furniture placement").transform;
            fit.SetParent(group.transform,false);
            var mesh=UnityEngine.Object.Instantiate(prefab,fit);
            if (!propMaterials.TryGetValue(asset,out var materials))
            {
                var maps=JsonUtility.FromJson<PropSurfaces>(Resources.Load<TextAsset>(folder+"Surfaces").text);
                materials=new Material[maps.surfaces.Length];
                for(int i=0;i<materials.Length;i++)
                {
                    var m=maps.surfaces[i];
                    Color tint=m.tint?.Length==4 ? new Color(m.tint[0],m.tint[1],m.tint[2],m.tint[3]) : Color.white;
                    bool packed=!string.IsNullOrEmpty(m.arm);
                    materials[i]=Pbr(m.name,folder+m.color,folder+m.normal,folder+m.arm,tint,
                        m.tint==null?.34f:1-m.rough,true);
                    if (!packed) materials[i].SetFloat("_Metallic", m.metal);
                    if (packed && asset is ("noodle-shop" or "corner-shop-block" or "mixed-tenant-block" or "public-housing-slab"))
                    {
                        materials[i].SetTexture("_FacadePatina", Resources.Load<Texture2D>(NightFinish + "CementColor"));
                        materials[i].EnableKeyword("_FACADE_WEATHER");
                    }
                    if (asset is "locker-room-tall-lockers" or "metal_office_desk")
                    {
                        var paintedMetal = materials[i];
                        paintedMetal.SetTexture("_DetailAlbedoMap", Resources.Load<Texture2D>("Finishes/Service/SteelColor"));
                        paintedMetal.SetTexture("_DetailNormalMap", Resources.Load<Texture2D>("Finishes/Service/SteelNormal"));
                        paintedMetal.SetTextureScale("_DetailAlbedoMap", new Vector2(1.18f, 1.34f));
                        paintedMetal.SetFloat("_DetailNormalMapScale", .38f);
                        paintedMetal.SetFloat("_GlossMapScale", .73f);
                        paintedMetal.EnableKeyword("_DETAIL_MULX2");
                    }
                }
                propMaterials.Add(asset,materials);
            }
            var renderers=mesh.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    string name=slots[i]?slots[i].name:"";
                    var mapped=Array.Find(materials,m=>name.StartsWith(m.name,StringComparison.Ordinal));
                    slots[i]=mapped?mapped:materials[0];
                }
                renderer.sharedMaterials=slots;
            }
            Vector3 measured=bounds.size;
            Vector3 scale=new(size.x/measured.x,size.y/measured.y,size.z/measured.z);
            fit.localScale=scale;
            fit.localPosition=-Vector3.Scale(bounds.center,scale)+Vector3.up*size.y*.5f;
            group.transform.SetPositionAndRotation(feet,Quaternion.Euler(0,yaw,0));
            if(solid)
            {
                var collision=group.AddComponent<BoxCollider>();
                collision.size=new Vector3(size.x*.96f,size.y,size.z*.96f);
                collision.center=Vector3.up*size.y*.5f;
            }
            return group;
        }

        void Replace(string old, string asset, string label, Vector3 bottom, Vector3 size, float yaw = 0)
        {
            var placeholder=GameObject.Find(old);
            if(placeholder)placeholder.SetActive(false);
            Prop(asset,label,bottom,size,yaw);
        }

        GameObject Shape(string label, PrimitiveType type, Vector3 position, Vector3 size, Material material, Vector3 angles)
        {
            var obj=GameObject.CreatePrimitive(type);
            obj.name=label;
            obj.transform.SetParent(root);
            obj.transform.SetPositionAndRotation(position,Quaternion.Euler(angles));
            obj.transform.localScale=size;
            obj.GetComponent<Renderer>().sharedMaterial=material;
            obj.GetComponent<Collider>().enabled=false;
            return obj;
        }

        void BathFittings()
        {
            const float width = 6.25f, height = 2.42f;
            var bed = Box("淋浴墙水泥衬底", new(11.86f, height*.5f, 23.88f),
                new(width, height, .044f), plaster);
            bed.GetComponent<Collider>().enabled = false;
            var glaze = Pbr("旧青瓷釉", NightFinish+"CementColor", NightFinish+"CementNormal", null,
                new(.55f,.76f,.69f), .71f);
            glaze.SetFloat("_BumpScale", .19f);
            float[] Joints(int tiles)
            {
                var edges = new float[tiles+1];
                for (int i = 0; i <= tiles; i++) edges[i] = (float)i/tiles;
                return edges;
            }
            CeramicPanel(root, "五联淋浴青瓷墙", new(11.86f, height*.5f, 23.855f), 0,
                new(width,height), Joints(20), Joints(8), glaze);

            for (int i=0;i<5;i++)
            {
                float x=9.6f+i*1.13f;
                var pipe=Shape("镀铬淋浴立管",PrimitiveType.Cylinder,new Vector3(x,1.43f,23.76f),new Vector3(.047f,.84f,.047f),steel,Vector3.zero);
                Shape("淋浴花洒",PrimitiveType.Cylinder,new Vector3(x,2.25f,23.47f),new Vector3(.255f,.034f,.255f),steel,Vector3.zero);
                Shape("出水弯管",PrimitiveType.Cylinder,pipe.transform.position+new Vector3(0,.83f,-.14f),new Vector3(.042f,.16f,.042f),steel,new Vector3(90,0,0));
                Shape("十字水阀",PrimitiveType.Cylinder,new Vector3(x,1.02f,23.66f),new Vector3(.16f,.037f,.16f),brass,new Vector3(90,0,0));
            }
            Prop("bathtub","瓷浴缸 · 深水位",new Vector3(3.2f,0,21.3f),new Vector3(3.1f,.86f,1.4f),0);
            Prop("bathtub","瓷浴缸 · 冷水位",new Vector3(7.0f,0,21.3f),new Vector3(2.45f,.79f,1.35f),0);
            Prop("bathtub","冲洗浴缸",new Vector3(13.6f,0,16.8f),new Vector3(1.85f,.77f,.82f),90);
            Replace("回水槽","bathroom-vanity","带陶瓷盆和龙头的回水槽",new Vector3(1,3.6f,2.0f),new Vector3(2,1.16f,.64f));
            Prop("toilet","卫生间陶瓷坐便器",new Vector3(-17.3f,0,1.51f),new Vector3(.55f,.84f,.77f),0);
            for(int i=0;i<3;i++)
                Prop("washing-machine","布草洗衣机",new Vector3(-14.4f+i*1.02f,0,22.65f),
                    new Vector3(.87f,1.04f,.84f),0);

            Prop("kitchen-base-unit-sink","茶座备餐水槽台",new Vector3(-10.76f,0,1.08f),new Vector3(2.4f,1.08f,.78f));
            Prop("kitchen-base-unit-drawers","备餐抽屉柜",new Vector3(-8.9f,0,1.08f),new Vector3(1.2f,.89f,.78f));
            Prop("white-kitchen-wall-cabinet","茶座墙上餐具柜",new Vector3(-10.55f,1.62f,.30f),new Vector3(2.72f,.64f,.44f));
            Prop("tea_set_01","水槽旁茶壶",new Vector3(-8.83f,.89f,1.12f),new Vector3(.45f,.23f,.38f),0,false);
        }

        void DressRooms()
        {
            // Fit the obstructions to the visible work surfaces.
            Replace("发电机组","portable_generator","带启动面板的发电机",new Vector3(-10,-3.60f,20),new Vector3(3.7f,1.70f,1.72f));
            Replace("求援终端台","metal_office_desk","前台接待桌",new Vector3(9,0,3.5f),new Vector3(2.4f,.94f,.77f));
            Replace("密封圈备品架","worn_metal_rack","毛巾库钢架",new Vector3(8,3.60f,4),new Vector3(2,1.40f,.65f));
            Replace("出生点旁的手电长凳","painted_wooden_bench","更衣长凳",new Vector3(-2,3.60f,21),new Vector3(3,.48f,.75f));
            var sofa = GameObject.Find("沙发厅 长沙发").transform;
            foreach (Transform part in sofa)
            {
                part.GetComponent<Renderer>().enabled = false;
                var center = part.position;
                center.y = part.name == "上沿" ? 4.33f : 3.90f;
                part.position = center;
                if (part.name != "上沿")
                    part.localScale = new Vector3(.18f, .60f, .18f);
            }
            var placeholderBed = GameObject.Find("宿舍 床架");
            if (placeholderBed) placeholderBed.SetActive(false);
            Prop("sofa_02","有布裙的旧沙发",new Vector3(-15,3.60f,5),new Vector3(3.4f,1.20f,1.50f),0,false,"Raised");
            Fixture(new Vector3(-17.5f,5.96f,7.35f),new Vector3(.7f,-.46f,-.68f),new Color(.95f,.73f,.53f),.42f,4.5f,86);
            Prop("vintage_day_bed","铁架宿舍床",new Vector3(-3,7.2f,20),new Vector3(2.04f,1.08f,.92f));
            DressDormDesk();
            Prop("television_02","前台报警终端",new Vector3(9,.94f,3.53f),new Vector3(.49f,.37f,.36f),180,false);
            Prop("television_02","走廊录像监视器",new Vector3(-14.2f,8.04f,2.2f),new Vector3(.51f,.43f,.41f),0,false);
            Prop("television_02","放映柜小电视",new Vector3(-7.5f,4.34f,2.0f),new Vector3(.73f,.56f,.55f),0,false);
            Prop("tea_set_01","没收走的茶具",new Vector3(-9.2f,.70f,7.02f),new Vector3(.65f,.22f,.64f),0,false);
            BathFittings();

            var fixtures=new (string asset,string label,Vector3 at,Vector3 size,float yaw)[]
            {
                ("tool_cart","地下工具车",new(-17.1f,-3.6f,2.0f),new(1.1f,1,.66f),13),
                ("worn_metal_rack","发电机备料架",new(-13.9f,-3.6f,22.8f),new(1.6f,2,.6f),0),
                ("metal_office_desk","集水间登记台",new(12,-3.6f,22.4f),new(1.8f,.84f,.61f),0),
                ("worn_metal_rack","泵房管件架",new(2.9f,-3.6f,22.6f),new(1.9f,2.1f,.68f),0),
                ("worn_metal_rack","备品库零件架",new(-2,-3.6f,4.48f),new(2.15f,2.1f,.62f),0),
                ("cardboard_box_01","备品纸箱",new(-4.8f,-3.6f,2.7f),new(.71f,.62f,.46f),-15),
                ("metal_stool_01","值机用凳",new(10.5f,-3.6f,4.5f),new(.42f,.67f,.45f),0),

                ("painted_wooden_bench","门厅验票等候凳",new(4.5f,0,2),new(2.7f,.56f,.70f),90),
                ("WoodenTable_01","茶座茶台",new(-9.2f,0,7.02f),new(2.7f,.70f,1.15f),0),
                ("metal_stool_01","茶座靠窗圆凳",new(-11.8f,0,7.0f),new(.47f,.52f,.47f),24),
                ("worn_metal_rack","布草领取架",new(-5.5f,0,22.4f),new(2.7f,2.1f,.66f),0),
                ("tool_cart","洗衣推车",new(-10.8f,0,22.25f),new(1.3f,1.15f,.69f),12),
                ("painted_wooden_bench","浴池换鞋凳",new(1.3f,0,16.4f),new(2.8f,.58f,.61f),0),
                ("wooden_broom","卫生间清洁用具",new(-18.1f,0,2.4f),new(.35f,1.38f,.19f),12),
                ("metal_stool_01","淋浴隔间小凳",new(13.7f,0,22.1f),new(.44f,.43f,.42f),6),
                ("metal_stool_01","前台收银员转椅",new(9.2f,0,2),new(.47f,.59f,.43f),-15),

                ("WoodenTable_01","折叠台",new(-12.8f,3.6f,21.5f),new(2.2f,.81f,1.1f),0),
                ("cardboard_box_01","折叠间封箱",new(-7.9f,3.6f,22),new(.68f,.47f,.47f),-20),
                ("painted_wooden_bench","更衣室第二排长凳",new(-4.6f,3.6f,22.72f),new(2.1f,.56f,.53f),0),
                ("vintage_day_bed","靠墙按摩榻",new(12,3.6f,20.2f),new(1.90f,1.04f,.84f),90),
                ("metal_stool_01","按摩室手推凳",new(13.8f,3.6f,20.5f),new(.50f,.60f,.46f),0),
                ("WoodenTable_01","沙发厅靠墙茶几",new(-17.6f,3.6f,2.44f),new(1.54f,.49f,.79f),0),
                ("metal_office_desk","放映室器材桌",new(-7.5f,3.6f,2),new(2.0f,.74f,.72f),0),
                ("painted_wooden_bench","放映坐席",new(-7.4f,3.6f,5.1f),new(1.8f,.52f,.55f),180),
                ("wooden_broom","盥洗室扫把",new(-1.6f,3.6f,1.45f),new(.34f,1.42f,.2f),-24),
                ("cardboard_box_01","毛巾库地面箱",new(10.6f,3.6f,2.15f),new(.62f,.58f,.52f),27),

                ("metal_office_desk","值班员桌",new(-9,7.2f,18),new(2,.88f,.68f),0),
                ("metal_office_desk","监控操作台",new(-14.2f,7.2f,2.2f),new(2.7f,.84f,.78f),0),
                ("worn_metal_rack","档案铁皮架",new(-3.7f,7.2f,1.2f),new(3,2.15f,.63f),0),
                ("cardboard_box_01","过期档案箱",new(.5f,7.2f,3.2f),new(.77f,.52f,.5f),7),
                ("WoodenTable_01","经理长桌",new(7.6f,7.2f,2.25f),new(2.7f,.84f,1.12f),0),
                ("sofa_02","经理室会客沙发",new(10,7.2f,6.5f),new(2.65f,1.05f,1.10f),90),
                ("metal_stool_01","床头小凳",new(-.95f,7.2f,22),new(.44f,.53f,.42f),0),
                ("tool_cart","弱电间检修车",new(7.8f,7.2f,22),new(1,1,.63f),24),
                ("painted_wooden_bench","街口候车长凳",new(101,0,-8.65f),new(2.85f,.60f,.69f),180)
            };
            foreach(var (asset,label,feet,size,yaw) in fixtures) Prop(asset,label,feet,size,yaw);
            Replace("停靠车体","kei-van","熄火的面包车",new Vector3(78,0,-14),new Vector3(1.99f,1.94f,5.3f),90);
            foreach(var renderer in Rescue.GetComponentsInChildren<Renderer>(true))
                if(renderer.name!="车顶警示灯")renderer.enabled=false;
            var van=Prop("kei-van","救援车车身",new Vector3(160,0,-20),new Vector3(2.08f,2.13f,5.0f),90,false);
            van.transform.SetParent(Rescue.transform,true);
            SetDressingDetails();
        }

        void DressDormDesk()
        {
            Vector3 feet = new(1.55f, 7.2f, 23.39f);
            var desk = Prop("metal_office_desk", "宿舍换班桌", feet, new Vector3(2.08f, .87f, .76f), 0, false);
            CollisionBox(desk.transform, "宿舍桌面", feet + Vector3.up * .771f, new Vector3(2.06f, .198f, .75f));
            foreach (float side in new[] { -.72f, .72f })
                CollisionBox(desk.transform, "宿舍桌两侧抽屉",
                    feet + new Vector3(side, .335f, 0), new Vector3(.56f, .67f, .70f));
            CollisionBox(desk.transform, "宿舍桌背板",
                feet + new Vector3(0, .335f, .34f), new Vector3(.88f, .67f, .06f));
        }
    }
}
