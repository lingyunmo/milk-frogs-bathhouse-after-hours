using UnityEngine;

namespace AfterHours
{
    internal sealed class NightPlayer : MonoBehaviour
    {
        internal const int PlayerLayer = 8;
        internal CharacterController Body { get; private set; }
        internal Transform Eyes { get; private set; }
        internal bool Crouched { get; private set; }
        internal bool TorchOn => torch.enabled;
        internal bool Vaulting => vaultTime > 0;
        internal Vector3 Feet => transform.position;
        float pitch, vertical, stepTime, vaultTime;
        Vector3 vaultStart, vaultEnd;
        Light torch;

        internal void SetUp(Camera camera)
        {
            gameObject.layer=PlayerLayer;
            Body=gameObject.AddComponent<CharacterController>();
            Body.radius=.25f;
            Body.skinWidth=.04f;
            Body.stepOffset=.31f;
            Eyes=camera.transform;
            Eyes.SetParent(transform,false);
            Eyes.localPosition=Vector3.up*1.60f;
            Eyes.localRotation=Quaternion.identity;
            if (!camera.GetComponent<AudioListener>()) camera.gameObject.AddComponent<AudioListener>();
            camera.nearClipPlane=.035f;
            camera.fieldOfView=72;
            torch=Eyes.gameObject.AddComponent<Light>();
            torch.type=LightType.Spot;
            torch.spotAngle=59;
            torch.innerSpotAngle=32;
            torch.range=23;
            torch.intensity=3;
            torch.shadows=LightShadows.Hard;
            torch.shadowNearPlane=.10f;
            torch.color=new Color(1,.92f,.76f);
            torch.enabled=false;
            ApplyHeight(1.7f);
        }

        internal void Tick(NightGame game,float dt)
        {
            float yaw=Input.GetAxisRaw("Mouse X")*2.15f;
            transform.Rotate(0,yaw,0);
            pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*2.15f,-85,85);
            Eyes.localRotation=Quaternion.Euler(pitch,0,0);

            if(Vaulting)
            {
                vaultTime=Mathf.Min(.42f,vaultTime+dt);
                float u=vaultTime/.42f;
                transform.position=Vector3.Lerp(vaultStart,vaultEnd,u)+Vector3.up*(Mathf.Sin(u*Mathf.PI)*.86f);
                if(u>=1)
                {
                    Body.enabled=true;
                    vaultTime=0;
                    game.Emit(transform.position,game.LandingNoise,"land");
                }
                return;
            }

            bool keepLow=!Crouched || Physics.CheckCapsule(Feet+Vector3.up*.93f,Feet+Vector3.up*1.48f,.23f,1,QueryTriggerInteraction.Ignore);
            if(Input.GetKeyDown(KeyCode.C) && (!Crouched || !keepLow)) Crouched=!Crouched;
            bool sprint=!Crouched && (Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));
            float speed=Crouched?1.65f:sprint?5.0f:3.05f;
            ApplyHeight(Crouched?.72f:1.70f);
            float eye=Crouched?.54f:1.60f;
            Eyes.localPosition=Vector3.Lerp(Eyes.localPosition,new Vector3(0,eye,0),Mathf.Min(1,dt*14));
            Vector3 move=Vector3.ClampMagnitude(transform.right*Input.GetAxisRaw("Horizontal")+transform.forward*Input.GetAxisRaw("Vertical"),1);
            vertical=Body.isGrounded?-2:Mathf.Max(-16,vertical-19*dt);
            Vector3 before=Feet;
            Body.Move((move*speed+Vector3.up*vertical)*dt);
            if(!Body.isGrounded || (Feet-before).sqrMagnitude<.00012f) stepTime=0;
            else if(move.sqrMagnitude>.01f && (stepTime-=dt)<=0)
            {
                game.Emit(Feet,Crouched?.13f:sprint?.57f:.33f,"step");
                stepTime=Crouched?.64f:sprint?.28f:.43f;
            }
            if(Input.GetKeyDown(KeyCode.F)) SetTorch(game.State.charge>0&&!TorchOn);
            if(Input.GetKeyDown(KeyCode.R)) game.ReplaceCell();
            if(TorchOn)
            {
                game.State.charge=Mathf.Max(0,game.State.charge-dt);
                if(game.State.charge<=0)SetTorch(false);
            }
            if(Feet.y<-8) game.RecoverPlayer();
        }

        void ApplyHeight(float height)
        {
            if(Mathf.Abs(Body.height-height)>.01f)
            {
                Body.height=height;
                Body.center=Vector3.up*(height/2f);
            }
        }

        internal void SetTorch(bool value) => torch.enabled=value;

        internal void Vault(Vector3 destination)
        {
            Body.enabled=false;
            vaultStart=Feet;
            vaultEnd=destination;
            vaultTime=.001f;
            vertical=0;
        }

        internal void ResetPosition(Vector3 position, float yaw)
        {
            Body.enabled=false;
            transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            vertical=0;
            pitch=0;
            vaultTime=0;
            Crouched=false;
            ApplyHeight(1.70f);
            Eyes.localPosition=Vector3.up*1.6f;
            Eyes.localRotation=Quaternion.identity;
            Body.enabled=true;
        }
    }
}
