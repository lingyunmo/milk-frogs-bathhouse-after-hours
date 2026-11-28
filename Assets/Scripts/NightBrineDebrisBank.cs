using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightBrineBlast
    {
        GameObject debrisBank;
        Mesh debrisBankMesh;
        Light rake, skirt;
        Material wetConcrete;

        // Settled pieces are supported by the broken infill from the same mill. Use the
        // same height field for the visible spoil and the landing position of every slab.
        static float SpoilHeight(float x,float z)
        {
            float Apron(float cx,float cz,float rx,float rz,float height)
            {
                float dx=(x-cx)/rx, dz=(z-cz)/rz;
                float wedge=Mathf.Max(0,1-Mathf.Sqrt(dx*dx+dz*dz));
                return height*wedge;
            }
            float southern=Apron(246,-43.4f,9.3f,4.8f,2.95f);
            float front=Apron(253.3f,-40.5f,7.8f,4.0f,3.36f);
            float footing=Apron(241.1f,-40.0f,5.9f,3.6f,2.18f);
            float crest=Mathf.Max(southern,Mathf.Max(footing,front));
            return .055f+crest*(.94f+.065f*Mathf.Sin(x*1.13f+z*.61f)+.05f*Mathf.Cos(z*1.61f));
        }

        void BuildDebrisBank(Transform mill,Material cement,Material iron)
        {
            var brick=mill.Find("正面窗下墙").GetComponent<MeshRenderer>().sharedMaterial;
            var vertices=new List<Vector3>(); var uv=new List<Vector2>(); var indices=new[]{new List<int>(),
                new List<int>(),new List<int>(),new List<int>()};
            var origin=new Vector3(247,0,-42);
            void Tri(Vector3 a,Vector3 b,Vector3 c,int skin)
            {
                var norm=Vector3.Cross(b-a,c-a);
                int start=vertices.Count;
                foreach(var p in new[]{a,b,c})
                {
                    vertices.Add(p-origin);
                    var coord=Mathf.Abs(norm.y)>Mathf.Abs(norm.x) && Mathf.Abs(norm.y)>Mathf.Abs(norm.z) ?
                        new Vector2(p.x,p.z):Mathf.Abs(norm.x)>Mathf.Abs(norm.z)?new(p.z,p.y):new Vector2(p.x,p.y);
                    uv.Add(coord*1.48f);
                }
                indices[skin].Add(start); indices[skin].Add(start+1); indices[skin].Add(start+2);
            }
            var rng=new System.Random(35027);
            float Rand(float low,float high)=>Mathf.Lerp(low,high,(float)rng.NextDouble());
            const int columns=24,rows=12;
            var grid=new Vector3[columns+1,rows+1];
            for(int row=0;row<=rows;row++) for(int col=0;col<=columns;col++)
            {
                float x=234.15f+col*1.13f+(col==0 || col==columns?0:Rand(-.22f,.22f));
                float z=-48.2f+row*.98f+(row==0 || row==rows?0:Rand(-.16f,.16f));
                grid[col,row]=new(x,SpoilHeight(x,z),z);
            }
            for(int row=0;row<rows;row++) for(int col=0;col<columns;col++)
            {
                var a=grid[col,row]; var b=grid[col+1,row]; var c=grid[col+1,row+1]; var d=grid[col,row+1];
                if(Mathf.Max(Mathf.Max(a.y,b.y),Mathf.Max(c.y,d.y))<.105f) continue;
                Tri(a,d,b,1); Tri(b,d,c,1);
            }
            // Heavy concrete cores and individual brick bats emerge through the dusty matrix.
            for(int i=0;i<245;i++)
            {
                float x=Rand(236,260), z=Rand(-47,-36.55f), ground=SpoilHeight(x,z);
                if(ground<.20f)continue;
                bool block=i%6==0;
                float wide=block?Rand(.36f,.91f):Rand(.105f,.32f);
                float high=wide*Rand(.46f,1.05f), depth=wide*Rand(.57f,1.18f);
                var turn=Quaternion.Euler(Rand(-19,19),Rand(-180,180),Rand(-19,19));
                var baseAt=new Vector3(x,ground-high*.15f,z);
                var low=new Vector3[6]; var rim=new Vector3[6];
                for(int n=0;n<6;n++)
                {
                    float a=(n+Rand(-.13f,.13f))*Mathf.PI/3;
                    var outside=new Vector3(Mathf.Cos(a)*wide*Rand(.85f,1.06f),0,Mathf.Sin(a)*depth);
                    low[n]=baseAt+turn*(outside-Vector3.up*high*.50f);
                    rim[n]=baseAt+turn*(outside*Rand(.56f,.94f)+Vector3.up*high*Rand(.48f,.85f));
                }
                var top=baseAt+turn*new Vector3(wide*.16f,high,depth*.08f);
                int skin=i%4==1 ? 2:0;
                for(int n=0;n<6;n++)
                {
                    int m=(n+1)%6;
                    Tri(low[n],rim[n],low[m],skin); Tri(low[m],rim[n],rim[m],skin);
                    Tri(top,rim[m],rim[n],i%3==0?1:skin);
                }
            }
            void Rebar(Vector3 a,Vector3 b)
            {
                Vector3 direction=(b-a).normalized;
                var left=Vector3.Cross(direction,Vector3.forward).normalized*.029f;
                var up=Vector3.Cross(direction,left);
                var ring=new[]{left+up,left-up,-left-up,-left+up};
                for(int n=0;n<4;n++)
                {
                    var side=ring[n];var next=ring[(n+1)%4];
                    Tri(a+side,b+side,b+next,3);Tri(a+side,b+next,a+next,3);
                }
            }
            foreach(var anchor in new[]{new Vector2(252.6f,-40.1f),new Vector2(247.3f,-43.4f),new Vector2(242.0f,-40.5f)})
            {
                for(int n=0;n<5;n++)
                {
                    float x=anchor.x+n*.20f,z=anchor.y;
                    var baseAt=new Vector3(x,SpoilHeight(x,z)-.24f,z);
                    var knee=baseAt+new Vector3(n*.02f,.60f+n*.14f,.34f);
                    Rebar(baseAt,knee);
                    Rebar(knee,knee+new Vector3(n*.085f,.16f,n*.09f+.38f));
                }
            }
            debrisBank=new GameObject("倾覆车间 · 混凝土与断砖堆");
            debrisBank.transform.SetParent(transform,false);
            debrisBank.transform.position=origin;
            debrisBankMesh=new Mesh {name="河堤倾倒的破碎骨料",subMeshCount=4};
            debrisBankMesh.SetVertices(vertices); debrisBankMesh.SetUVs(0,uv);
            for(int i=0;i<4;i++)debrisBankMesh.SetTriangles(indices[i],i);
            debrisBankMesh.RecalculateNormals();debrisBankMesh.RecalculateTangents();
            debrisBank.AddComponent<MeshFilter>().sharedMesh=debrisBankMesh;
            wetConcrete=new Material(cement) {name="断面渗水的水泥"};
            wetConcrete.SetFloat("_GlossMapScale",.46f);
            wetConcrete.SetFloat("_BumpScale",1.27f);
            debrisBank.AddComponent<MeshRenderer>().sharedMaterials=new[]{wetConcrete,Burn(cement),brick,iron};
            Light Lamp(string name,Vector3 lens,Vector3 aim,float range,float cone,Color tint)
            {
                var lamp=new GameObject(name).AddComponent<Light>();
                lamp.transform.SetParent(transform,false);
                lamp.transform.SetPositionAndRotation(lens,Quaternion.LookRotation(aim-lens));
                lamp.type=LightType.Spot; lamp.spotAngle=cone; lamp.innerSpotAngle=cone*.25f;
                lamp.range=range; lamp.color=tint; lamp.renderMode=LightRenderMode.ForcePixel;
                lamp.cullingMask=~((1<<NightPlayer.PlayerLayer)|(1<<NightPlayer.ViewModelLayer));
                lamp.enabled=false;
                return lamp;
            }
            // A crosslight rakes the projecting ledges and the wet edges. Both beams point away from the playable street.
            rake=Lamp("堤岸检修泛光",new(259.8f,4.34f,-37.52f),new(250.2f,1.30f,-41.8f),27,100,new(.75f,.85f,.91f));
            skirt=Lamp("沿堤备用灯",new(242.1f,2.89f,-38.16f),new(250.3f,1.65f,-41.3f),22,92,new(.94f,.86f,.74f));
            debrisBank.SetActive(false);
        }

        void PoseDebrisBank(float age)
        {
            if(!debrisBank)return;
            bool on=age>=.56f;
            if(rake.enabled!=on) { rake.enabled=on;skirt.enabled=on; }
            if(on)
            {
                // The emergency relay restrikes twice before settling.
                float pulse=age<1.45f && (age>.81f && age<.90f || age>1.13f && age<1.20f) ? .30f : 1;
                rake.intensity=2.60f*pulse; skirt.intensity=1.45f*pulse;
            }
            if(age<1) { if(debrisBank.activeSelf)debrisBank.SetActive(false); return; }
            if(!debrisBank.activeSelf)debrisBank.SetActive(true);
            float rise=Mathf.SmoothStep(.015f,.54f,(age-1)/.85f)+.46f*Mathf.SmoothStep(0,1,(age-2)/1.15f);
            debrisBank.transform.localScale=new Vector3(1,rise,1);
        }
    }
}
