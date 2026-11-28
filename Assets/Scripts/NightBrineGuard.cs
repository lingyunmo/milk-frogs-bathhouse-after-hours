using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightGame
    {
        BrineEcho guardPrey;
        float guardSwingTime=-1, guardStepDistance;
        internal int GuardHits { get; private set; }
        internal bool CaughtDrainAttacker { get; private set; }
        ParticleSystem guardSpray;
        Material saltWater;
        sealed class FallingCoat
        {
            internal Transform skin;
            internal Vector3 start,away;
            internal Quaternion rotation;
            internal float age;
        }
        readonly List<FallingCoat> downed=new();

        void ClearGuardContact()
        {
            guardPrey=null; guardSwingTime=-1; guardStepDistance=0; GuardHits=0; CaughtDrainAttacker=false;
            foreach(var coat in downed) if(coat.skin) Destroy(coat.skin.gameObject);
            downed.Clear();
            if(guardSpray) guardSpray.Clear();
        }

        BrineEcho Intercept(bool tunnel)
        {
            var r=State.brine;
            float best=float.PositiveInfinity;
            BrineEcho target=null;
            foreach(var echo in r.echoes)
            {
                if(echo.lane>=3 || echo.armoured) continue;
                var distance=(echo.feet-hunter.Position).magnitude;
                float urgency=tunnel?Mathf.Min(12,Vector3.Distance(echo.feet,Player.Feet)) :
                    .45f*Vector3.Distance(echo.feet,BrineRoute.Breach(echo.lane));
                // Commit through a blow, but turn toward a much closer footstep between swings.
                float score=distance+urgency-(echo==guardPrey?1.6f:0);
                if(echo.fromGrate && echo.emergence<1.42f) score-=3;
                if(score<best) { best=score; target=echo; }
            }
            return target;
        }

        // Return true only when this blow removes an advancing attacker from the gate count.
        bool TickBrineGuard(float dt,bool tunnel)
        {
            bool dropped=false;
            var old=hunter.Position;
            if(guardSwingTime>=0)
            {
                float before=guardSwingTime;
                guardSwingTime+=dt;
                var at=guardPrey!=null?guardPrey.feet:old;
                hunter.GuardStep(old,at,dt,false);
                hunter.GuardBlow(guardSwingTime);
                if(before<.42f && guardSwingTime>=.42f && guardPrey!=null && drifters.ContainsKey(guardPrey)
                    && (guardPrey.feet-old).sqrMagnitude<2.3f*2.3f && !GuardWall(old,guardPrey.feet))
                    dropped=GuardImpact(guardPrey);
                if(guardSwingTime>=.88f) { guardSwingTime=-1; guardPrey=null; }
                return dropped;
            }
            guardPrey=Intercept(tunnel);
            var post=tunnel?Escort:Guard-Vector3.right*4;
            if(guardPrey!=null)
            {
                var enemy=guardPrey.feet;
                var line=enemy-old; line.y=0;
                post=enemy-line.normalized*1.12f;
                post.x=tunnel?Mathf.Clamp(post.x,284.7f,304):Mathf.Clamp(post.x,257,273);
                post.z=tunnel?Mathf.Clamp(post.z,-33.3f,-28.7f):Mathf.Clamp(post.z,-33.7f,-28.6f);
                if(allyIn<=0 && (!guardPrey.fromGrate || guardPrey.emergence>=.96f) && line.sqrMagnitude<1.72f*1.72f && !GuardWall(old,enemy))
                {
                    guardSwingTime=0; allyIn=4.4f;
                    hunter.GuardStep(old,enemy,dt,false);
                    soundscape.Play(old+Vector3.up,"guard-brace",.86f);
                    return false;
                }
            }
            hunter.GuardStep(post,guardPrey?.feet ?? BrineRoute.Mouth(1),dt,false,tunnel?4.5f:3.55f);
            guardStepDistance+=Vector3.Distance(hunter.Position,old);
            if(guardStepDistance>=1.78f) { guardStepDistance%=1.78f; PlayCreatureStep(hunter.Position); }
            return false;
        }

        static bool GuardWall(Vector3 from,Vector3 to) => Physics.Linecast(from+Vector3.up,to+Vector3.up,
            ~(1<<NightPlayer.PlayerLayer),QueryTriggerInteraction.Ignore);

        bool GuardImpact(BrineEcho victim)
        {
            var away=(victim.feet-hunter.Position).normalized;
            if(away.sqrMagnitude<.01f) away=Vector3.left;
            var contact=victim.feet+Vector3.up*1.13f;
            GuardHits++; CaughtDrainAttacker|=victim.fromGrate;
            soundscape.Play(contact,"guard-strike",.98f);
            SprayBrine(contact,away,26);
            if(--victim.life>0)
            {
                var land=victim.feet+away*.90f;
                if(!GuardWall(victim.feet,land)) victim.feet=land;
                victim.stride+=1.8f;
                PoseDrifter(victim);
                return false;
            }
            var body=drifters[victim];
            var skin=body.GetComponent<Renderer>();
            skin.GetPropertyBlock(brineCloth);
            brineCloth.SetFloat("_StepRate",0);
            skin.SetPropertyBlock(brineCloth);
            drifters.Remove(victim);
            State.brine.echoes.Remove(victim);
            downed.Add(new FallingCoat { skin=body,start=body.position,away=away,rotation=body.rotation });
            return true;
        }

        void TickGuardFalls(float dt)
        {
            for(int i=downed.Count-1;i>=0;i--)
            {
                var body=downed[i]; float prev=body.age;
                body.age+=dt;
                float fall=Mathf.Clamp01(body.age/.48f);
                float angle=fall*fall;
                var axis=Vector3.Cross(Vector3.up,body.away).normalized;
                body.skin.rotation=Quaternion.AngleAxis(86*angle,axis)*body.rotation;
                var pos=body.start+body.away*(1.02f*fall);
                pos.y=Mathf.Lerp(body.start.y,.10f,angle)+.19f*Mathf.Sin(fall*Mathf.PI);
                if(body.age>1.85f) pos.y-=.65f*(body.age-1.85f);
                body.skin.position=pos;
                if(prev<.48f && body.age>=.48f)
                {
                    soundscape.Play(pos,"guard-fall",.88f); SprayBrine(pos,body.away,13);
                }
                if(body.age>=2.35f) { Destroy(body.skin.gameObject); downed.RemoveAt(i); }
            }
        }

        void SprayBrine(Vector3 at,Vector3 away,int count)
        {
            if(!guardSpray)
            {
                var node=new GameObject("肩撞 · 溅出的盐水");
                node.transform.SetParent(transform,false);
                guardSpray=node.AddComponent<ParticleSystem>(); guardSpray.Stop();
                var main=guardSpray.main; main.loop=false; main.playOnAwake=false; main.maxParticles=110;
                main.simulationSpace=ParticleSystemSimulationSpace.World; main.gravityModifier=.87f;
                var emission=guardSpray.emission; emission.enabled=false;
                saltWater=new Material(Resources.Load<Shader>("BrineDrops"));
                var skin=guardSpray.GetComponent<ParticleSystemRenderer>();
                skin.sharedMaterial=saltWater; skin.renderMode=ParticleSystemRenderMode.Stretch;
                skin.velocityScale=.065f; skin.lengthScale=1.3f;
            }
            guardSpray.Play();
            for(int i=0;i<count;i++)
            {
                var fan=Random.insideUnitSphere; fan.y=Mathf.Abs(fan.y);
                var emit=new ParticleSystem.EmitParams {position=at+Random.insideUnitSphere*.09f,
                    velocity=away*Random.Range(.4f,2.6f)+fan*3.4f+Vector3.up*.8f,
                    startSize=Random.Range(.035f,.10f),startLifetime=Random.Range(.22f,.60f),
                    startColor=Color.Lerp(new(.29f,.42f,.39f,.72f),new(.86f,.94f,.82f,.93f),Random.value)};
                guardSpray.Emit(emit,1);
            }
        }
    }
}
