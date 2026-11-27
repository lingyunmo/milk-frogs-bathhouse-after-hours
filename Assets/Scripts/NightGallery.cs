using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        // All tiles share one mesh. Glaze rolls into the grout across a millimetre-scale bevel.
        // UVs stay registered to the whole painting, including the angled edges.
        static void CeramicPanel(Transform parent, string label, Vector3 center, float yaw, Vector2 size,
            float[] columns, float[] rows, Material glaze)
        {
            var points = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int row = 1; row < rows.Length; row++)
            for (int col = 1; col < columns.Length; col++)
            {
                float left = (columns[col-1] - .5f) * size.x + .0017f;
                float right = (columns[col] - .5f) * size.x - .0017f;
                float bottom = (rows[row-1] - .5f) * size.y + .0017f;
                float top = (rows[row] - .5f) * size.y - .0017f;
                var a = new Vector3(left, bottom, 0);
                var b = new Vector3(left, top, 0);
                var c = new Vector3(right, top, 0);
                var d = new Vector3(right, bottom, 0);
                var ia = a + new Vector3(.004f,.004f,-.003f);
                var ib = b + new Vector3(.004f,-.004f,-.003f);
                var ic = c + new Vector3(-.004f,-.004f,-.003f);
                var id = d + new Vector3(-.004f,.004f,-.003f);
                Face(ia,ib,ic,id);
                Face(a,b,ib,ia); Face(b,c,ic,ib); Face(c,d,id,ic); Face(d,a,ia,id);
            }
            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = points.Count;
                foreach (var p in new[] { a,b,c,d })
                {
                    points.Add(p);
                    uvs.Add(new Vector2(.5f + p.x / size.x, .5f + p.y / size.y));
                }
                triangles.AddRange(new[] {i,i+1,i+2,i,i+2,i+3});
            }
            var mesh = new Mesh { name = label };
            mesh.SetVertices(points); mesh.SetUVs(0,uvs); mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var panel = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
            panel.transform.SetParent(parent,false);
            panel.transform.SetPositionAndRotation(center,Quaternion.Euler(0,yaw,0));
            panel.GetComponent<MeshFilter>().sharedMesh = mesh;
            panel.GetComponent<MeshRenderer>().sharedMaterial = glaze;
        }

        void BuildStreetElevation(Transform level, int floor, float y)
        {
            const float wall = NightMainRing.Front;
            float sill = floor == 1 ? 1.05f : 1.16f;
            float head = floor == 1 ? 2.67f : 2.65f;
            var surrounds = new DetailMesh(.92f);
            var casements = new DetailMesh(.6f);
            var glazing = AnnexGlazing();
            Material elevation = floor == 1 ? glazed : paint;
            float from = NightMainRing.Footprint.xMin;
            // A solid pier backs the mural. The lit windows flank it and the original street entrance.
            foreach (var (center, width) in new[] { (-16.4f,2.4f),(-5.1f,2.65f),(0f,2.34f),(4.9f,2.66f),(9.55f,2.16f) })
            {
                float a = center-width*.5f, b = center+width*.5f;
                Panel(from,a,0,RingWallHeight);
                bool doorway = floor == 1 && center == 0;
                Panel(a,b,doorway ? RingDoorHeight : head,RingWallHeight);
                if (!doorway)
                {
                    Panel(a,b,0,sill);
                    var dimensions = new Vector3(width,head-sill,.055f);
                    var at = new Vector3(center,y+(head+sill)*.5f,wall);
                    var pane = Box("原钢窗玻璃",at,dimensions,glazing,level);
                    pane.layer = 1; // TransparentFX: solid to the controller, transmits the sky bake.
                    pane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    foreach (float x in new[] { a, center, b })
                        casements.Box(new(x,at.y,wall),new(.051f,dimensions.y,.108f));
                    foreach (float h in new[] { sill, head-.38f, head })
                        casements.Box(new(center,y+h,wall),new(width,.048f,.11f));
                    surrounds.Box(new(center,y+sill-.055f,wall-.05f),new(width+.22f,.11f,.39f));
                    surrounds.Box(new(center,y+head+.065f,wall-.04f),new(width+.18f,.13f,.32f));
                }
                else casements.Box(new(center,y+RingDoorHeight+.03f,wall),new(width+.12f,.06f,.26f));
                from=b;
            }
            Panel(from,NightMainRing.Footprint.xMax,0,RingWallHeight);
            surrounds.Box(new(-4,y+3.07f,wall-.09f),new(32,.105f,.32f));
            surrounds.Place(level,"Street elevation stone lintels",plaster);
            casements.Place(level,"Rebated steel window casements",steel);

            void Panel(float left,float right,float bottom,float top)
            {
                AnnexSurface(level,"浴场临街墙体",new((right+left)*.5f,y+(top+bottom)*.5f,wall),
                    new(right-left,top-bottom,.18f),elevation);
            }
        }

        void FinishRingGallery(Transform level, int index, float height)
        {
            float wall = NightMainRing.Front + .11f;
            var trim = new DetailMesh(.68f);
            var band = new DetailMesh(1.12f);
            if (index == 0)
            {
                foreach (float high in new[] { 2.63f, 2.94f })
                {
                    Vector3 start = new(-19.6f, height + high, wall + .22f);
                    trim.Pipe(start, start + Vector3.right * 30.9f, high < 2.8f ? .084f : .054f);
                    for (float x = -19; x < 11; x += 3)
                    {
                        var joint = new Vector3(x, start.y, start.z);
                        trim.Pipe(joint - Vector3.right*.045f, joint + Vector3.right*.045f, .11f);
                        band.Box(joint - Vector3.forward*.09f, new(.075f,.31f,.20f));
                    }
                }
                DropPipe(trim, band, new(-11.2f, height, wall + .21f), 2.63f);
                Grate(trim, band, new(-11.2f, height,-9.9f), .80f, .34f);
            }
            else
            {
                foreach (var (a,b) in new[] { (-19.8f,-1.25f), (1.25f,11.8f) })
                {
                    float cx = (a+b) * .5f;
                    band.Box(new(cx,height+.48f,wall),new(b-a,.94f,.07f));
                    trim.Box(new(cx,height+.97f,wall+.024f),new(b-a,.043f,.086f));
                    trim.Box(new(cx,height+2.94f,wall+.035f),new(b-a,.085f,.12f));
                }
                foreach (float x in new[] { -18.2f, -14.1f, -7.5f, -2.5f, 2.5f, 8.5f })
                {
                    band.Box(new(x,height+1.52f,wall+.043f),new(.19f,3.03f,.13f));
                    trim.Box(new(x,height+.12f,wall+.065f),new(.27f,.24f,.16f));
                    trim.Box(new(x,height+2.93f,wall+.084f),new(.33f,.15f,.20f));
                    if (index == 1)
                        band.Box(new(x,height+3.075f,-6.35f),new(.17f,.13f,12.5f));
                }
            }
            band.Place(level, "Gallery wall lining", index switch { 0 => plaster, 2 => planks, _ => glazed });
            trim.Place(level, "Gallery mouldings and pipe fittings", index == 2 ? planks : steel);

            if (index == 1)
            {
                var mural = Pbr("鹭塘瓷板", "Surfaces/EgretMural", null, null, Color.white, .59f);
                var backing = Box("Ceramic mural mounting", new(-10.8f,1.83f,wall+.05f), new(3.19f,2.15f,.11f), brass,level);
                backing.GetComponent<Collider>().enabled = false;
                CeramicPanel(level, "鹭塘瓷板", new(-10.8f,1.83f,wall+.116f), 180, new(3.12f,2.08f),
                    // The painted panel seams are uneven; keep the relief on the original joints.
                    new[] { 0f,.079f,.157f,.252f,.337f,.412f,.498f,.578f,.661f,.745f,.832f,.910f,1f },
                    new[] { 0f,.092f,.189f,.309f,.423f,.548f,.638f,.747f,.876f,1f }, mural);
                WallPractical(-13.45f, new Color(1,.76f,.49f), 1.55f);
                WallPractical(2.63f, new Color(.60f,.83f,.84f), .86f);
            }
            else
            {
                var color = index == 0 ? new Color(.63f,.82f,.74f) :
                    index == 2 ? new Color(.96f,.77f,.59f) : new Color(.68f,.79f,.83f);
                WallPractical(-12.1f, color, index == 3 ? .54f : 1.05f);
            }

            void WallPractical(float x, Color color, float power)
            {
                Vector3 at = new(x,height+2.28f,wall+.10f);
                var housing = Prop("industrial_wall_sconce","壁面检修灯",at,new(.23f,.39f,.21f),0,false);
                housing.transform.SetParent(level,true);
                LightSconce(housing,index == 3 ? .42f : .78f);
                Fixture(at+new Vector3(.15f,.16f,.28f),new Vector3(.77f,-.37f,-.12f),
                    color,power,7.7f,105,.009f);
            }
        }
    }
}
