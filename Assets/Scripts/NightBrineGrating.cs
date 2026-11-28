using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        internal static readonly Vector3 BackwashDrain=new(266.6f,.044f,-34.25f);
        Transform grateLeaf;
        float lastGrateAngle=-999;

        void DressBackwashDrain(Transform yard)
        {
            var at=BackwashDrain;
            var frame=Trim(at,steel);
            foreach(float edge in new[]{-.54f,.54f})
            {
                frame.Box(at+new Vector3(edge,.012f,0),new(.065f,.056f,1.14f));
                frame.Box(at+new Vector3(0,.012f,edge),new(1.14f,.056f,.065f));
            }
            var pit=Box("检修井暗口",at+Vector3.up*.008f,new(1.04f,.018f,1.04f),Material(new(.012f,.018f,.016f)),yard);
            pit.GetComponent<Collider>().enabled=false;
            grateLeaf=new GameObject("倒灌检查井翻盖").transform;
            grateLeaf.SetParent(yard,false); grateLeaf.position=at+new Vector3(0,.039f,-.51f);
            var ironwork=new DetailMesh(.75f);
            for(int i=0;i<11;i++) ironwork.Box(grateLeaf.position+new Vector3(-.47f+i*.094f,.034f,.51f),new(.043f,.067f,1.01f));
            foreach(float z in new[]{.035f,.52f,.985f}) ironwork.Box(grateLeaf.position+new Vector3(0,.003f,z),new(1.02f,.055f,.05f));
            ironwork.Place(grateLeaf,"带锈的扁钢箅",paintedIron);
        }

        internal void TipBackwashGrate(float time)
        {
            float angle=time<0?0:time<.33f?Mathf.Sin(time*62)*6+7:
                time<.79f?Mathf.Lerp(5,125,Mathf.SmoothStep(0,1,(time-.33f)/.46f)):
                111+14*Mathf.Exp(-7*(time-.79f))*Mathf.Cos((time-.79f)*23);
            if(Mathf.Abs(lastGrateAngle-angle)<.04f) return;
            lastGrateAngle=angle;
            grateLeaf.localRotation=Quaternion.Euler(-angle,0,0);
        }
    }
}
