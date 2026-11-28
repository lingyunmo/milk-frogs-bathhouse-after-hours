using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightBrineBlast
    {
        enum Member { StreetWall, Wall, Support, Slab, Lintel }
        readonly List<StructuralSection> structure = new();

        System.Collections.IEnumerable CaptureStructure(Transform district)
        {
            var mill = district.Find("旧脱水车间");
            var cement = mill.Find("挡水基座").GetComponent<MeshRenderer>().sharedMaterial;
            var iron = mill.Find("铆接窗梁").GetComponent<MeshRenderer>().sharedMaterial;
            BuildDebrisBank(mill,cement,iron);
            yield return null;
            void Capture(MeshRenderer part, Member member)
            {
                structure.Add(new StructuralSection(part, member, transform, Burn(cement), iron));
            }
            foreach (Transform part in mill)
            {
                if (!part.TryGetComponent<MeshRenderer>(out var renderer)) continue;
                // The far west bay stays standing. At the torn end it exposes a cross section.
                if (renderer.bounds.center.x < 237f) continue;
                switch (part.name)
                {
                    case "预制楼板": Capture(renderer,Member.Slab); break;
                    case "正面窗下墙": case "侧面砖墙": case "后排砖墙": Capture(renderer,Member.Wall); break;
                    case "混凝土窗间柱": Capture(renderer,Member.Support); break;
                    case "铆接窗梁": Capture(renderer,Member.Lintel); break;
                    default: continue;
                }
                yield return null;
            }
            void Street(string name, Rect plan)
            {
                var building = district.Find(name);
                if (!building) return;
                // Choose an unmarked flank, away from door lintels, quest signs and street windows.
                MeshRenderer flank = null;
                foreach (Transform child in building)
                {
                    if (child.name != "实心墙体" || !child.TryGetComponent<MeshRenderer>(out var candidate)) continue;
                    var b = candidate.bounds;
                    if (Mathf.Abs(b.center.x-plan.xMin)>.15f || b.size.y<2.8f || b.size.z<3) continue;
                    if (!flank || b.size.z>flank.bounds.size.z) flank=candidate;
                }
                if (flank) Capture(flank,Member.StreetWall);
            }
            Street("防疫监测哨",BrineInquiry.Room);
            yield return null;
            foreach (var site in NightWorld.StreetRoomAreas)
            {
                Street(site.Name,site.Bounds);
                yield return null;
            }
            foreach (var site in NightWorld.RiverBuildings)
            {
                Street(site.Name,site.Bounds);
                yield return null;
            }
        }

        void DisposeStructure()
        {
            foreach (var part in structure) if (part.Debris) Destroy(part.Debris);
            if(debrisBankMesh) Destroy(debrisBankMesh);
            if(wetConcrete) Destroy(wetConcrete);
        }

        void TickCollapseDust(float previous, float now)
        {
            var eyes = game.Player.Eyes.position;
            foreach (var part in structure)
                if (part.ImpactWidth>0 && previous<part.ImpactTime && now>=part.ImpactTime &&
                    (part.Contact-eyes).sqrMagnitude<10000)
                    ImpactDust(part.Contact,part.ImpactWidth);
        }

        sealed class StructuralSection
        {
            sealed class Piece
            {
                internal int First, Last;
                internal Vector3 Pivot, Offset, Angles;
                internal float Lag, Fall;
                internal bool Detached;
            }

            internal readonly Mesh Debris;
            internal readonly float ImpactWidth, ImpactTime;
            internal readonly Vector3 Contact;
            readonly MeshRenderer intact, broken;
            readonly BoxCollider kerb;
            readonly Vector3 fullSize, fullCenter;
            readonly List<Piece> pieces = new();
            readonly Vector3[] rest, vertices, normals, restNormals;
            readonly Vector4[] tangents, restTangents;
            readonly float delay, finalTime;
            bool active, settled;

            internal StructuralSection(MeshRenderer intact,Member kind,Transform owner,Material concrete,Material iron)
            {
                this.intact = intact;
                var bounds = intact.bounds;
                Vector3 origin=bounds.center, size=bounds.size;
                bool street=kind==Member.StreetWall, floor=kind==Member.Slab;
                bool wall=kind==Member.Wall || street;
                float phase = kind switch
                {
                    Member.Support => .16f, Member.Wall => .51f, Member.Lintel => .90f,
                    Member.Slab => 1.42f+(10.4f-origin.y)*.20f, _ => .38f
                };
                delay = phase+Vector3.Distance(origin,Source)*.012f;
                if (street && intact.TryGetComponent(out kerb))
                {
                    fullSize=kerb.size; fullCenter=kerb.center;
                }
                Vector3 across = !floor && size.x<size.z ? Vector3.forward : Vector3.right;
                Vector3 up = floor ? Vector3.forward : Vector3.up;
                Vector3 normal = Vector3.Cross(up,across);
                float width=Vector3.Dot(size,across), height=Vector3.Dot(size,up);
                float depth=Mathf.Abs(Vector3.Dot(size,normal));
                Vector3 release=street ? Vector3.left : new Vector3(.38f,0,.92f);
                int cols=wall ? Mathf.Clamp(Mathf.CeilToInt(width/1.64f),2,9) : floor ? 2 : kind==Member.Lintel ? 2 : 1;
                int rows=wall ? 3 : floor ? 2 : 1;
                float Hash(int x,int y) => Mathf.Repeat(Mathf.Sin(16.47f*x+37.52f*y+origin.x*.36f+origin.y*.82f+origin.z*.21f)*8587.2f,1);
                Vector3 Corner(int col,int row)
                {
                    float x=col, y=row;
                    if(col>0 && col<cols) x+=(Hash(col,row)-.5f)*.66f;
                    if(row>0 && row<rows) y+=(Hash(row,col+19)-.5f)*.53f;
                    return across*((x/cols-.5f)*width)+up*((y/rows-.5f)*height);
                }
                var points=new List<Vector3>(); var uv=new List<Vector2>();
                var faces=new[]{new List<int>(),new List<int>(),new List<int>()};
                void Quad(int surface,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
                {
                    int first=points.Count;
                    var s=(b-a).normalized;
                    var t=Vector3.Cross(Vector3.Cross(s,c-a).normalized,s);
                    foreach(var point in new[]{a,b,c,d})
                    {
                        points.Add(point);
                        uv.Add(new(Vector3.Dot(point+origin,s)*.74f,Vector3.Dot(point+origin,t)*.74f));
                    }
                    var indices=faces[surface];
                    indices.Add(first); indices.Add(first+1); indices.Add(first+2);
                    indices.Add(first); indices.Add(first+2); indices.Add(first+3);
                }
                void Reinforcement(Vector3 start,Vector3 end)
                {
                    var axis=(end-start).normalized;
                    var side=Vector3.Cross(axis,Vector3.up).normalized*.036f;
                    var top=Vector3.Cross(side,axis);
                    Quad(2,start+side+top,start+side-top,end+side-top,end+side+top);
                    Quad(2,start-side-top,start-side+top,end-side+top,end-side-top);
                    Quad(2,start-side+top,start+side+top,end+side+top,end-side+top);
                    Quad(2,start+side-top,start-side-top,end-side-top,end+side-top);
                }

                void Cap(Vector3 a,Vector3 b,Vector3 c,int surface)
                {
                    var face=faces[surface]; int first=points.Count;
                    foreach(var vertex in new[]{a,b,c})
                    {
                        points.Add(vertex);
                        uv.Add(new(Vector3.Dot(origin+vertex,across)*.77f,Vector3.Dot(origin+vertex,up)*.77f));
                    }
                    face.Add(first);face.Add(first+1);face.Add(first+2);
                }
                void RaggedPanel(Vector3[] outer,Vector3 center,Vector3 half,int seed)
                {
                    var rim=new Vector3[8]; var crown=new Vector3[8];var underside=new Vector3[8];
                    for(int edge=0;edge<4;edge++)
                    {
                        var from=outer[edge];var toward=outer[(edge+1)%4];
                        rim[2*edge]=Vector3.Lerp(from,center,.014f+Hash(seed,edge)*.055f);
                        var chip=Vector3.Lerp(from,toward,.24f+.44f*Hash(edge+6,seed));
                        rim[2*edge+1]=Vector3.MoveTowards(chip,center,.07f+.17f*Hash(edge+11,seed));
                    }
                    for(int i=0;i<rim.Length;i++)
                    {
                        var bite=Vector3.MoveTowards(rim[i],center,.035f+.07f*Hash(i+3,seed));
                        crown[i]=bite+half*(.84f+.16f*Hash(seed,i));
                        underside[i]=Vector3.Lerp(rim[i],center,.03f+.13f*Hash(i+9,seed+4))-half;
                    }
                    for(int i=0;i<rim.Length;i++)
                    {
                        int j=(i+1)%rim.Length;
                        Cap(center+half,crown[j],crown[i],0);
                        Cap(center-half,underside[i],underside[j],0);
                        Quad(1,crown[i],crown[j],underside[j],underside[i]);
                    }
                }

                Vector3 impact=origin; float impactAge=0, duration=0;
                for(int row=0;row<rows;row++)
                for(int col=0;col<cols;col++)
                {
                    Vector3 a=Corner(col,row), b=Corner(col+1,row), c=Corner(col+1,row+1), d=Corner(col,row+1);
                    var half=normal*(depth*.5f);
                    var pivot=(a+b+c+d)*.25f;
                    var chunk=new Piece { First=points.Count,Pivot=pivot };
                    if(floor || wall && !street)
                        RaggedPanel(new[]{a,b,c,d},pivot,half,col+rows*row+27);
                    else
                    {
                        Quad(0,a+half,d+half,c+half,b+half); Quad(0,a-half,b-half,c-half,d-half);
                        Quad(1,a+half,d+half,d-half,a-half); Quad(1,d+half,c+half,c-half,d-half);
                        Quad(1,c+half,b+half,b-half,c-half); Quad(1,b+half,a+half,a-half,b-half);
                    }
                    if(floor && col==0)
                        for(int wire=1;wire<=3;wire++)
                        {
                            Vector3 at=Vector3.Lerp(b,c,wire*.24f);
                            var knee=at+across*(.27f+Hash(wire,row)*.28f);
                            Reinforcement(at-across*.17f,knee);
                            Reinforcement(knee,knee+across*(.17f+.37f*Hash(wire+1,row))+Vector3.down*(.10f+.27f*Hash(row+3,wire)));
                        }
                    chunk.Last=points.Count;
                    float r=Hash(col+13,row+4), signed=2*Hash(row+7,col+11)-1;
                    // The continuous lower course remains a real barrier on playable street flanks.
                    chunk.Detached=!street || row>0 && (row==2 || col%3!=0);
                    chunk.Lag=chunk.Detached ? (.08f+.17f*r+col*.048f) : 0;
                    chunk.Fall=.48f+.18f*r+Mathf.Sqrt(Mathf.Max(0,origin.y+pivot.y))*.21f;
                    if(chunk.Detached)
                    {
                        float slope=Hash(row+47,col+12)*2-1;
                        chunk.Angles=floor ? new Vector3(slope*39,signed*42,(r-.41f)*71) :
                            kind==Member.Lintel ? new(signed*19,slope*64,r*28-9f) :
                            across==Vector3.forward ? new Vector3(signed*22,slope*72,(signed<0?-1:1)*(47+43*r)) :
                            new Vector3((slope<0?-1:1)*(47+43*r),signed*56,slope*27);
                        var rotation=Quaternion.Euler(chunk.Angles);
                        chunk.Offset=release*(street ? .6f+r : floor ? .4f+1.02f*r : .25f+1.30f*r)+
                            across*(signed*(floor?.76f:.49f));
                        float settle=float.MinValue; Vector3 onGround=Vector3.zero;
                        for(int n=chunk.First;n<chunk.Last;n++)
                        {
                            Vector3 point=origin+pivot+chunk.Offset+rotation*(points[n]-pivot);
                            float support=street?.04f:SpoilHeight(point.x,point.z);
                            float contact=support-point.y;
                            if(contact>settle) { settle=contact;onGround=new(point.x,support,point.z); }
                        }
                        chunk.Offset.y=settle;
                        if(impactAge==0 || r>.67f)
                        {
                            impact=onGround;
                            impactAge=chunk.Lag+chunk.Fall;
                        }
                    }
                    pieces.Add(chunk);
                    duration=Mathf.Max(duration,chunk.Lag+chunk.Fall+.26f);
                }
                ImpactWidth = floor ? width : street ? Mathf.Min(4,width*.37f) :
                    kind==Member.Support && origin.y<3 ? 1.2f : 0;
                Contact=impact; ImpactTime=delay+impactAge; finalTime=delay+duration;
                var node=new GameObject("断落的"+intact.name); node.transform.SetParent(owner,false);
                node.transform.position=origin;
                Debris=new Mesh { name=node.name,subMeshCount=3 };
                Debris.MarkDynamic(); Debris.SetVertices(points); Debris.SetUVs(0,uv);
                for(int i=0;i<3;i++) Debris.SetTriangles(faces[i],i);
                Debris.RecalculateNormals(); Debris.RecalculateTangents();
                rest=Debris.vertices; restNormals=Debris.normals; restTangents=Debris.tangents;
                vertices=new Vector3[rest.Length]; normals=new Vector3[rest.Length]; tangents=new Vector4[rest.Length];
                Debris.bounds=new Bounds(Vector3.down*origin.y*.5f,size+new Vector3(12,Mathf.Abs(origin.y)+8,12));
                node.AddComponent<MeshFilter>().sharedMesh=Debris; broken=node.AddComponent<MeshRenderer>();
                broken.sharedMaterials=new[]{intact.sharedMaterial,concrete,iron};
                broken.enabled=false;
            }

            internal void Pose(float time)
            {
                if (!intact || !broken) return;
                if(time<delay) { if(active) Restore(); return; }
                if(settled && time>=finalTime) return;
                if(!active)
                {
                    active=true; intact.enabled=false; broken.enabled=true;
                    if(kerb)
                    {
                        var size=fullSize; size.y*=.32f;
                        kerb.size=size; kerb.center=fullCenter-Vector3.up*((fullSize.y-size.y)*.5f);
                    }
                }
                foreach(var chunk in pieces)
                {
                    float t=Mathf.Clamp01((time-delay-chunk.Lag)/chunk.Fall);
                    float bounce=Mathf.Clamp01((time-delay-chunk.Lag-chunk.Fall)/.24f);
                    float slump=Mathf.Sin(bounce*Mathf.PI)*(1-bounce);
                    var rotation=chunk.Detached ? Quaternion.Euler(chunk.Angles*(t*(.33f+.67f*t)+.028f*slump)) : Quaternion.identity;
                    var offset=new Vector3(chunk.Offset.x*t,chunk.Offset.y*t*t,chunk.Offset.z*t);
                    if(chunk.Detached) offset.y+=.12f*Mathf.Sin(t*Mathf.PI)+slump*.115f;
                    for(int i=chunk.First;i<chunk.Last;i++)
                    {
                        vertices[i]=chunk.Pivot+offset+rotation*(rest[i]-chunk.Pivot);
                        normals[i]=rotation*restNormals[i];
                        Vector3 tangent=rotation*(Vector3)restTangents[i];
                        tangents[i]=new(tangent.x,tangent.y,tangent.z,restTangents[i].w);
                    }
                }
                Debris.vertices=vertices; Debris.normals=normals; Debris.tangents=tangents;
                settled=time>=finalTime;
            }

            internal void Restore()
            {
                if(intact) intact.enabled=true;
                if(broken) broken.enabled=false;
                if(kerb) { kerb.size=fullSize; kerb.center=fullCenter; }
                active=false; settled=false;
            }
        }
    }
}
