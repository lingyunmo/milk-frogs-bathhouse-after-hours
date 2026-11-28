using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightPlayer
    {
        Transform projector;
        Material[] projectorFinishes;
        float recoil;
        internal void EquipBrineGun(bool equip)
        {
            if(equip && !projector)
            {
                projector=new GameObject("堤防回流炮").transform;
                projector.SetParent(handsCamera.transform,false);
                var body=new Material(Shader.Find("Standard")){ color=new Color(.12f,.27f,.26f) };
                body.SetFloat("_Glossiness",.58f);
                var collar=new Material(body){color=new Color(.56f,.44f,.25f)};
                var emitter=new Material(body){color=new Color(.58f,.92f,.88f)};
                projectorFinishes=new[]{body,collar,emitter};
                emitter.EnableKeyword("_EMISSION"); emitter.SetColor("_EmissionColor",new Color(.22f,.88f,.76f));
                void Part(string name, PrimitiveType shape, Vector3 at, Vector3 size, Material mat, float pitch=0)
                {
                    var part=GameObject.CreatePrimitive(shape); part.name=name; part.layer=ViewModelLayer;
                    part.transform.SetParent(projector,false); part.transform.localPosition=at;
                    part.transform.localRotation=Quaternion.Euler(pitch,0,0); part.transform.localScale=size;
                    part.GetComponent<Collider>().enabled=false; part.GetComponent<Renderer>().sharedMaterial=mat;
                }
                Part("铸铜握把",PrimitiveType.Cube,new(0,-.065f,-.015f),new(.042f,.155f,.057f),collar,-13);
                Part("陶瓷蓄流腔",PrimitiveType.Capsule,new(0,0,.055f),new(.11f,.115f,.11f),body,90);
                Part("表盘包边",PrimitiveType.Cylinder,new(0,.043f,.018f),new(.067f,.012f,.067f),collar,-22);
                Part("水银表盘",PrimitiveType.Cylinder,new(0,.055f,.013f),new(.050f,.005f,.050f),emitter,-22);
                for(int i=0;i<3;i++)
                    Part("脉冲环",PrimitiveType.Cylinder,new(0,0,.119f+i*.024f),new(.10f-i*.009f,.009f,.10f-i*.009f),collar,90);
                Part("发光喷口",PrimitiveType.Cylinder,new(0,0,.18f),new(.059f,.01f,.059f),emitter,90);
            }
            if(projector) projector.gameObject.SetActive(equip);
            if(heldTorch) heldTorch.gameObject.SetActive(!equip);
            if(equip) { recoil=0; SetTorchVisible(true); PoseGun(0); }
        }

        void PoseGun(float sway)
        {
            if(!projector || !projector.gameObject.activeSelf) return;
            recoil=Mathf.MoveTowards(recoil,0,Time.deltaTime*7);
            projector.localPosition=new Vector3(.155f,-.135f+.005f*sway,.35f-.035f*recoil);
            projector.localRotation=Quaternion.Euler(-5-8*recoil,-7,3+sway);
        }
        internal Vector3 BrineMuzzle()
        {
            recoil=1;
            return projector.TransformPoint(0,0,.185f);
        }
    }
}
