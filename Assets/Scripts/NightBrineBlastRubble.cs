using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightBrineBlast
    {
        readonly List<Fracture> plates = new();
        readonly List<Subsidence> skyline = new();
        readonly Dictionary<Material,Material> finishes = new();

        internal int FracturedSurfaces => plates.Count;
        internal int SkylineBlocks => skyline.Count;

        System.Collections.IEnumerator CaptureArchitecture(Transform district)
        {
            // These are individual roof skins. Their original colliders continue to carry the
            // walkable floors, gallery and held-open doors, including while the skin is in flight.
            var roofSurfaces = new List<MeshRenderer>();
            void Roof(Transform building, string name)
            {
                if (!building) return;
                foreach (Transform part in building)
                    if (part.name == name && part.TryGetComponent<MeshRenderer>(out var surface) && surface.enabled)
                        roofSurfaces.Add(surface);
            }
            foreach (var site in NightWorld.StreetRoomAreas)
            {
                Roof(district.Find(site.Name),"沿街房屋屋顶");
                yield return null;
            }
            foreach (var site in NightWorld.RiverBuildings)
            {
                Roof(district.Find(site.Name),"沿街房屋屋顶");
                yield return null;
            }
            Roof(district.Find("防疫监测哨"),"沿街房屋屋顶");
            yield return null;
            Roof(district.Find("东堤旧票务所"),"屋面");
            Roof(district.Find("冷样保管间"),"沿街房屋屋顶");
            Roof(district.Find("主楼南翼/员工层南翼"),"南翼顶板");
            Roof(district.Find("浴场副楼/3F 副楼"),"副楼抹灰顶");
            Roof(district,"屋顶");
            var road = district.Find("石桥路沿街建筑");
            if (road)
                foreach (Transform child in road)
                    if (child.name.EndsWith("顶棚",System.StringComparison.Ordinal) && child.TryGetComponent<MeshRenderer>(out var canopy) && canopy.enabled)
                        roofSurfaces.Add(canopy);
            var gate = district.Find("堤岸隔离闸");
            Roof(gate,"来水暗孔");
            Roof(gate,"泄洪廊顶");
            foreach (var surface in roofSurfaces)
            {
                plates.Add(new Fracture(surface, transform, Burn(surface.sharedMaterial)));
                yield return null;
            }
            foreach (var step in CaptureStructure(district)) yield return step;
            AddSkyline(district);
            AddSkyline(road);
            AddSkyline(district.Find("石桥东堤"));
        }

        Material Burn(Material source)
        {
            if (finishes.TryGetValue(source,out var finish)) return finish;
            finish=new Material(source) { name=source.name+" · 火蚀断面" };
            if (finish.HasProperty("_Color"))
                finish.color *= new Color(.60f,.54f,.48f,1);
            finishes.Add(source,finish);
            return finish;
        }

        void AddSkyline(Transform group)
        {
            if (!group) return;
            foreach (Transform t in group)
            {
                if (t.name is not ("路对面老式住宅" or "后巷外住宅楼" or "菜市后旧住宅楼" or "街尾远处住宅楼" or "对岸旧宿舍楼")) continue;
                // Only deform the model fit on these background landmarks, never a collision owner.
                var model=t.Find("Fit model to furniture placement");
                if (model) skyline.Add(new Subsidence(model));
            }
        }

        void PoseArchitecture(float seconds)
        {
            PoseDebrisBank(seconds);
            foreach (var section in structure) section.Pose(seconds);
            foreach (var tower in skyline) tower.Pose(seconds);
            foreach (var plate in plates) plate.Pose(seconds);
        }

        void RestoreArchitecture()
        {
            PoseDebrisBank(0);
            foreach (var section in structure) section.Restore();
            foreach (var tower in skyline) tower.Restore();
            foreach (var plate in plates) plate.Restore();
        }

        sealed class Subsidence
        {
            readonly Transform model;
            readonly Vector3 origin, axis;
            readonly Quaternion standing;
            readonly float delay, depth, turn;
            internal Subsidence(Transform t)
            {
                model=t; origin=t.localPosition; standing=t.localRotation;
                float x=t.parent.position.x;
                delay=.46f+Vector3.Distance(t.parent.position,Source)*.011f;
                depth=2.6f+3.6f*Mathf.Abs(Mathf.Sin(x));
                turn=8+13*Mathf.Abs(Mathf.Cos(x));
                axis=new Vector3(.4f*Mathf.Sin(x),.09f,Mathf.Cos(x*.16f)).normalized;
            }
            internal void Pose(float age)
            {
                if (!model) return;
                float f=Mathf.SmoothStep(0,1,(age-delay)/3.25f);
                model.localPosition=origin+Vector3.down*(depth*f);
                model.localRotation=Quaternion.AngleAxis(turn*f,axis)*standing;
            }
            internal void Restore()
            {
                if (model) model.SetLocalPositionAndRotation(origin,standing);
            }
        }

        sealed class Fracture
        {
            internal readonly Mesh Mesh;
            internal readonly Vector3 Origin;
            internal readonly float Delay;
            readonly MeshRenderer intact, broken;
            readonly Vector3[] rest, verts, normals, originalNormals;
            readonly Vector4[] tangents, originalTangents;
            readonly List<Shard> pieces=new();
            bool settled;
            readonly struct Shard
            {
                internal readonly Vector3 Center, Travel, Angles;
                internal readonly float Lift, Duration;
                internal Shard(Vector3 center,Vector3 travel,Vector3 angles,float lift,float duration)
                {
                    Center=center; Travel=travel; Angles=angles; Lift=lift; Duration=duration;
                }
            }

            internal Fracture(MeshRenderer source,Transform owner,Material surface)
            {
                intact=source;
                Bounds bounds=source.bounds;
                Origin=bounds.center;
                Delay=.19f+Vector3.Distance(Origin,Source)*.012f;
                bool upright=bounds.size.x < bounds.size.y && bounds.size.x < bounds.size.z;
                var u=upright ? Vector3.forward : Vector3.right;
                var v=upright ? Vector3.up : Vector3.forward;
                var outward=Vector3.Cross(v,u);
                float width=Vector3.Dot(bounds.size,u),length=Vector3.Dot(bounds.size,v),thickness=Vector3.Dot(bounds.size,outward);
                int columns=Mathf.Clamp(Mathf.CeilToInt(width/3.8f),2,7);
                float Hash(int x,int y) => Mathf.Repeat(Mathf.Sin(x*61.27f+y*16.17f+Origin.x*.46f+Origin.z*.37f)*4084.7f,1);
                Vector3 Corner(int col,int row)
                {
                    float across=col/(float)columns;
                    if (col>0 && col<columns) across+=(Hash(col,row)-.5f)*.54f/columns;
                    float along=row==0 ? 0 : row==2 ? 1 : (upright ? .48f : .18f)+Hash(col,3)*.13f;
                    return u*((across-.5f)*width)+v*((along-.5f)*length);
                }
                var positions=new List<Vector3>(); var tex=new List<Vector2>(); var triangles=new List<int>();
                void Face(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
                {
                    int first=positions.Count;
                    positions.Add(a); positions.Add(b); positions.Add(c); positions.Add(d);
                    // One metre-scale UV basis also maps rough exposed edges; no stretched full-roof texture.
                    var s=(b-a).normalized; var t=Vector3.Cross(Vector3.Cross(s,c-a).normalized,s);
                    foreach(var point in new[]{a,b,c,d}) tex.Add(new(Vector3.Dot(point,s)*.56f,Vector3.Dot(point,t)*.56f));
                    triangles.Add(first); triangles.Add(first+1); triangles.Add(first+2);
                    triangles.Add(first); triangles.Add(first+2); triangles.Add(first+3);
                }
                for(int row=0; row<2; row++)
                for(int col=0; col<columns; col++)
                {
                    var a=Corner(col,row); var b=Corner(col+1,row); var c=Corner(col+1,row+1); var d=Corner(col,row+1);
                    var normal=outward*thickness*.5f;
                    Face(a+normal,d+normal,c+normal,b+normal);
                    Face(a-normal,b-normal,c-normal,d-normal);
                    Face(a+normal,d+normal,d-normal,a-normal);
                    Face(d+normal,c+normal,c-normal,d-normal);
                    Face(c+normal,b+normal,b-normal,c-normal);
                    Face(b+normal,a+normal,a-normal,b-normal);
                    var centre=(a+b+c+d)*.25f;
                    float variation=Hash(col+4,row+6), sign=Hash(col,row+4)-.5f;
                    bool ejected=upright ? row==1 || col%2==0 : row==0 && (col%3!=1 || columns==2);
                    Vector3 travel, angle;
                    float lift;
                    if (ejected)
                    {
                        var eject=upright ? outward : -v;
                        travel=eject*(1.7f+variation*2.3f)+u*sign*2;
                        angle=new Vector3(17+sign*29,sign*39,sign*23);
                        // Pieces settle on an edge beside their own frontage, outside the route.
                        if(upright) angle=new Vector3(0,sign*68,70+variation*24);
                        float tiltHeight=upright ? width/columns*.49f : Mathf.Max(length*.22f,width/columns)*.24f;
                        travel.y=-Origin.y-centre.y+Mathf.Max(.11f,tiltHeight);
                        lift=1.8f+2.9f*variation;
                    }
                    else
                    {
                        travel=new Vector3(sign*.24f,-.15f-variation*.31f,.08f);
                        angle=new Vector3(sign*3,.5f,sign*2);
                        lift=.12f+variation*.15f;
                    }
                    pieces.Add(new Shard(centre,travel,angle,lift,1.4f+variation*.70f));
                }

                var node=new GameObject("断裂的"+source.name);
                node.transform.SetParent(owner,false);
                node.transform.position=Origin;
                Mesh=new Mesh { name=node.name };
                Mesh.MarkDynamic();
                Mesh.SetVertices(positions); Mesh.SetUVs(0,tex); Mesh.SetTriangles(triangles,0);
                Mesh.RecalculateNormals(); Mesh.RecalculateTangents();
                rest=Mesh.vertices; originalNormals=Mesh.normals; originalTangents=Mesh.tangents;
                verts=new Vector3[rest.Length]; normals=new Vector3[rest.Length]; tangents=new Vector4[rest.Length];
                Mesh.bounds=new Bounds(Vector3.down*Origin.y*.5f,bounds.size+new Vector3(16,24+Mathf.Abs(Origin.y),16));
                node.AddComponent<MeshFilter>().sharedMesh=Mesh;
                broken=node.AddComponent<MeshRenderer>();
                broken.sharedMaterial=surface;
                broken.enabled=false;
            }

            internal void Pose(float time)
            {
                if(!intact || !broken) return;
                if(time < Delay) { Restore(); return; }
                if(settled && time>=Delay+2.1f) return;
                intact.enabled=false; broken.enabled=true;
                for(int i=0;i<pieces.Count;i++)
                {
                    var piece=pieces[i];
                    float t=Mathf.Clamp01((time-Delay)/piece.Duration);
                    var rotation=Quaternion.Euler(piece.Angles*(t*(2-t)));
                    var offset=piece.Travel*(t*(2-t))+Vector3.up*(piece.Lift*(4*t*(1-t)));
                    for(int j=i*24;j<(i+1)*24;j++)
                    {
                        verts[j]=piece.Center+offset+rotation*(rest[j]-piece.Center);
                        normals[j]=rotation*originalNormals[j];
                        Vector3 tangent=rotation*(Vector3)originalTangents[j];
                        tangents[j]=new Vector4(tangent.x,tangent.y,tangent.z,originalTangents[j].w);
                    }
                }
                Mesh.vertices=verts; Mesh.normals=normals; Mesh.tangents=tangents;
                settled=time>=Delay+2.1f;
            }

            internal void Restore()
            {
                if (intact) intact.enabled=true;
                if (broken) broken.enabled=false;
                settled=false;
            }
        }
    }
}
