using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        void RefinishFurniture()
        {
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!renderer.enabled || renderer.sharedMaterial == null ||
                    renderer.sharedMaterial.name != "旧家具") continue;
                var obj=renderer.gameObject;
                string group=obj.transform.parent.name;
                var bounds=renderer.bounds;
                switch (obj.name)
                {
                    case "循环泵":
                        DressCirculationPump(bounds);
                        renderer.enabled=false;
                        break;
                    case "电源分配面板":
                    case "上行设备机柜":
                        Prop("utility_box_02",obj.name+" · 搪瓷箱体",
                            new(bounds.center.x,bounds.min.y,bounds.center.z),bounds.size,0,false);
                        renderer.enabled=false;
                        break;
                    case "洗衣房 布帘":
                        FoldedScreen(obj);
                        break;
                    default:
                        if (group=="工坊 检修台")
                        {
                            Surface(obj,obj.name=="上沿"?wood:paintedIron,.86f);
                            if (obj.name=="上沿") BenchApron(bounds,wood);
                        }
                        else Surface(obj,group is "候车亭" or "停车棚" ? steel : paintedIron,1.25f);
                        break;
                }
            }
        }

        void BenchApron(Bounds b, Material timber)
        {
            var binding=Trim(b.center,steel);
            foreach (int face in new[]{-1,1})
            {
                Vector3 front=b.center+Vector3.forward*(face*b.extents.z);
                Vector3 edge=Vector3.right*(b.extents.x-.025f);
                Trim(front,timber).Moulding(front-edge,front+edge,Vector3.forward*face,.073f,.017f);
                binding.Box(front-edge+Vector3.right*.04f,new(.085f,b.size.y+.012f,.018f));
                binding.Box(front+edge-Vector3.right*.04f,new(.085f,b.size.y+.012f,.018f));
            }
        }

        void DressCirculationPump(Bounds bounds)
        {
            Vector3 foot=new(bounds.center.x,bounds.min.y,bounds.center.z);
            var casing=Trim(foot,paintedIron);
            var ironwork=Trim(foot,steel);
            casing.Box(foot+Vector3.up*.09f,new(1.97f,.18f,1.48f));
            foreach (float x in new[]{-.57f,.55f})
                casing.Box(foot+new Vector3(x,.26f,0),new(.16f,.24f,.76f));
            void Drum(string name,float x,float y,float length,float diameter)
            {
                Shape(name,PrimitiveType.Cylinder,foot+new Vector3(x,y,0),
                    new(diameter,length*.5f,diameter),paintedIron,new(0,0,90));
            }
            Drum("泵电机筒",-.32f,.65f,.98f,.69f);
            Drum("离心泵蜗壳",.43f,.63f,.41f,.96f);
            Drum("泵吸入口",.76f,.63f,.35f,.35f);
            for (int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI/8;
                var rim=new Vector3(0,.35f*Mathf.Cos(angle),.35f*Mathf.Sin(angle));
                ironwork.Pipe(foot+new Vector3(-.75f,.65f,0)+rim,foot+new Vector3(.09f,.65f,0)+rim,.018f,8);
            }
            // The volute splits at the service face. The collar, washers and bolts stand
            // proud of the casting; the conduit returns behind it to the equipment plinth.
            Drum("吸口压盖",.932f,.63f,.054f,.46f);
            for (int i=0;i<8;i++)
            {
                float turn=i*Mathf.PI/4;
                Vector3 radial=new(0,Mathf.Cos(turn),Mathf.Sin(turn));
                var lug=foot+new Vector3(.633f,.63f,0)+radial*.385f;
                ironwork.Pipe(lug,lug+Vector3.right*.035f,.032f,6);
                var cap=foot+new Vector3(.957f,.63f,0)+radial*.181f;
                ironwork.Pipe(cap,cap+Vector3.right*.022f,.020f,6);
            }
            casing.Box(foot+new Vector3(-.33f,1.02f,0),new(.36f,.135f,.29f));
            ironwork.Box(foot+new Vector3(-.33f,1.105f,0),new(.39f,.035f,.32f));
            var outlet=foot+new Vector3(.43f,.72f,.01f);
            ironwork.Pipe(outlet,outlet+Vector3.forward*.69f,.13f);
            ironwork.Pipe(outlet+Vector3.forward*.55f,outlet+Vector3.forward*.64f,.205f);
            var elbow=outlet+Vector3.forward*.69f;
            ironwork.Pipe(elbow,elbow+new Vector3(0,-.09f,.07f),.13f);
            ironwork.Pipe(elbow+new Vector3(0,-.09f,.07f),elbow+new Vector3(0,-.66f,.07f),.13f);
            for(int i=0;i<8;i++)
            {
                Vector3 center=outlet+new Vector3(.164f*Mathf.Cos(i*Mathf.PI/4),.164f*Mathf.Sin(i*Mathf.PI/4),.645f);
                ironwork.Pipe(center,center+Vector3.forward*.025f,.025f,6);
            }
        }

        // Keep the original hide collider and interaction. Each side has its own wound surface;
        // the weave uses metres, and the folds stay inside the original screen's thickness.
        void FoldedScreen(GameObject obj)
        {
            Vector3 size=obj.transform.lossyScale;
            int folds=Mathf.Max(4,Mathf.RoundToInt(size.x/.235f)), columns=folds*8;
            var points=new List<Vector3>();
            var uv=new List<Vector2>();
            var indices=new List<int>();
            foreach (int side in new[]{-1,1})
            {
                int start=points.Count;
                for (int x=0;x<=columns;x++)
                for (int y=0;y<=4;y++)
                {
                    float u=(float)x/columns, v=y/4f;
                    float phase=u*folds*2*Mathf.PI;
                    float bend=phase+.28f*(1-v)*Mathf.Sin(phase*.51f);
                    float z=(.32f+.08f*(1-v))*Mathf.Cos(bend)+side*.024f;
                    float hem=(1-v)*.011f*Mathf.Sin(phase+.7f);
                    points.Add(new(u-.5f,v-.5f+hem,z));
                    uv.Add(new(u*size.x/.58f, v*size.y/.58f));
                    if(x==0 || y==0)continue;
                    int c=start+x*5+y, a=c-6,b=c-5,d=c-1;
                    if(side<0)indices.AddRange(new[]{a,b,c,a,c,d});
                    else indices.AddRange(new[]{a,c,b,a,d,c});
                }
            }
            var mesh=new Mesh{name="双面褶布"};
            mesh.SetVertices(points);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;
            obj.GetComponent<Renderer>().sharedMaterial=linen;
            var upper=obj.transform.TransformPoint(new Vector3(0,.5f,0));
            var rod=Trim(upper,steel);
            var end=obj.transform.right*(size.x*.5f+.02f);
            rod.Pipe(upper-end,upper+end,.018f);
        }
    }
}
