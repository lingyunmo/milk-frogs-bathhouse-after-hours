using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        void Lighting()
        {
            var sun=GameObject.Find("白盒照明").GetComponent<Light>();
            sun.intensity=.19f;
            sun.color=new Color(.63f,.73f,1);
            sun.shadows=LightShadows.Soft;
            sun.shadowStrength=.44f;
            QualitySettings.pixelLightCount=8;
            QualitySettings.shadowDistance=52;
            RenderSettings.ambientLight=new Color(.29f,.31f,.34f);
            RenderSettings.reflectionIntensity=.22f;
            RenderSettings.fog=true;
            RenderSettings.fogColor=new Color(.030f,.039f,.060f);
            RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogDensity=.0060f;
            Camera.main.clearFlags=CameraClearFlags.SolidColor;
            Camera.main.backgroundColor=RenderSettings.fogColor;
            Camera.main.allowHDR=true;
        }

        Light Fixture(Vector3 position, Vector3 direction, Color colour,float energy,float range,float angle)
        {
            var lamp=new GameObject("已安装灯具光源").AddComponent<Light>();
            lamp.transform.SetParent(root);
            lamp.transform.position=position;
            lamp.transform.rotation=Quaternion.LookRotation(direction,Vector3.forward);
            lamp.type=LightType.Spot;
            lamp.range=range;
            lamp.color=colour;
            lamp.spotAngle=angle;
            lamp.innerSpotAngle=angle*.45f;
            lamp.intensity=energy;
            lamp.shadows=LightShadows.Soft;
            lamp.shadowStrength=.82f;
            lamp.shadowNearPlane=.10f;
            lamp.shadowBias=.09f;
            lamp.shadowNormalBias=.22f;
            return lamp;
        }

        void InteriorLight(Vector3 p,string room)
        {
            bool service=p.y<0;
            bool wet=room is "大浴池" or "淋浴间" or "盥洗室" or "循环泵房" or "洗衣房";
            Color tint = service?new Color(1,.65f,.39f)
                :wet?new Color(.68f,.84f,.82f) : new Color(1,.86f,.65f);
            var hanging=Box("金属灯槽",p+Vector3.up*.11f,new Vector3(.83f,.18f,.34f),steel);
            var diffuser=Box("磨砂灯罩",p+Vector3.up*.015f,new Vector3(.66f,.035f,.22f),emergencyGlass);
            hanging.GetComponent<Collider>().enabled=false;
            diffuser.GetComponent<Collider>().enabled=false;
            Fixture(p+Vector3.down*.085f,new Vector3(.11f,-1,.1f),tint,service?2.25f:2.65f,6.6f,120);
        }

        void CorridorLighting(float y)
        {
            foreach(int x in new[]{-16,-7,2,11})
            {
                var mount=new Vector3(x,y+2.67f,13.82f);
                Prop("industrial_wall_sconce", "走廊旧式壁灯", mount + new Vector3(0,-.19f,-.13f),
                    new Vector3(.17f,.38f,.28f), 180, false);
                Fixture(mount+new Vector3(0,-.15f,-.25f),new Vector3(.13f,-1,-.58f),
                    y<0 ? new Color(1,.55f,.30f) : new Color(.88f,.71f,.46f),2.15f,6.7f,114);
                var overhead=Fixture(new Vector3(x+2,y+3.11f,12),Vector3.down,
                    new Color(.69f,.81f,.82f),2.1f,5.8f,113);
                workLights.Add(overhead);
                overhead.enabled=false;
            }
            foreach(var x in new[]{-18f,14f})
            {
                var position=new Vector3(x,y+2.42f,x<0?14.8f:9.16f);
                Fixture(position,new Vector3(0,-1,x<0?.15f:-.15f),new Color(1,.72f,.47f),1.95f,5.5f,104);
            }
        }
    }
}
