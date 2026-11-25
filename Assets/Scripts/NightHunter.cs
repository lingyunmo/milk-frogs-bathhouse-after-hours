using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed class NightHunter
    {
        [Serializable]
        sealed class Model
        {
            public float[] weights, biases;
        }
        internal Vector3 Position => actor.position;
        internal bool Pursuing { get; private set; }
        readonly NightGame game;
        readonly FloorRoutes routing;
        readonly Transform actor, visual;
        readonly List<(SkinnedMeshRenderer mesh,int index)> laughShapes=new();
        SkinnedMeshRenderer gait;
        int strideA=-1,strideB=-1;
        Quaternion modelFacing;
        readonly Model model;
        readonly float[] hiddenA=new float[24],hiddenB=new float[16], features=new float[11];
        readonly List<Vector3> path=new();
        readonly Dictionary<int,float> visits=new();
        readonly Vector3[] posts=
        {
            new(-8,0,12),new(10,0,12),new(-6,0,17),new(-16,0,19),new(-1,0,4),
            new(-16,3.6f,12),new(11,3.6f,12),new(-12,3.6f,5),new(7,3.6f,19),
            new(-13,7.2f,12),new(9,7.2f,12),new(11,7.2f,19),
            new(-12,-3.6f,12),new(10,-3.6f,12),new(-8,-3.6f,20),
            new(1,0,-4),new(-24,0,10),new(-11,0,31),new(49,0,6),new(100,0,-17),new(155,0,-17)
        };
        Vector3 lastSeen, destination, unreachable;
        float sinceSeen=40, nextDecision, stride, retry, checkTime;
        int waypoint, lastPost=-1, cycle;
        struct Cue
        {
            internal Vector3 Place;
            internal float Loudness, Age;
        }
        readonly List<Cue> noises=new();

        internal NightHunter(NightGame game)
        {
            this.game=game;
            routing=new FloorRoutes();
            actor=new GameObject("奶娃").transform;
            actor.position=posts[1];
            model=JsonUtility.FromJson<Model>(Resources.Load<TextAsset>("Creature/Policy").text);
            if(model.weights.Length!=664 || model.biases.Length!=41)
                throw new InvalidOperationException("The hunter neural policy has an incompatible shape.");
            visual=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Creature/MilkFrog"),actor).transform;
            var renderers=visual.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float scale=1.50f/bounds.size.y;
            visual.localScale*=scale;
            visual.localPosition=-(bounds.center-actor.position)*scale+Vector3.up*.75f;
            var skin=new Material(Shader.Find("Standard")){mainTexture=Resources.Load<Texture2D>("Creature/Skin"),color=Color.white};
            foreach(var r in renderers)
            {
                if(r.name.StartsWith("01"))r.sharedMaterial=skin;
                else if(r.name.Contains("tooth"))r.sharedMaterial=Tint(new Color(.56f,.46f,.3f));
                else if(r.name.Contains("sulfur"))r.sharedMaterial=Tint(new Color(.89f,.71f,.14f),.4f);
                else if(r.name.Contains("Tongue")||r.name.Contains("gums"))r.sharedMaterial=Tint(new Color(.23f,.074f,.083f));
                else if(r.name.Contains("mouth"))r.sharedMaterial=Tint(new Color(.055f,.025f,.027f));
                else r.sharedMaterial=Tint(new Color(.25f,.17f,.06f));
                if(r is not SkinnedMeshRenderer morph)continue;
                for(int i=0;i<morph.sharedMesh.blendShapeCount;i++)
                {
                    string key=morph.sharedMesh.GetBlendShapeName(i);
                    if(key.EndsWith("Belly laugh"))laughShapes.Add((morph,i));
                    if(key.EndsWith("Stride A")){gait=morph;strideA=i;}
                    if(key.EndsWith("Stride B")){gait=morph;strideB=i;}
                }
            }
            foreach(var r in renderers)
                if(r.name.StartsWith("Scleral slit"))
                {
                    modelFacing=Quaternion.Euler(0,r.bounds.center.z<actor.position.z?180:0,0);
                    break;
                }
            visual.localRotation=modelFacing;
        }

        internal void LaughAt(Vector3 toward,float duration)
        {
            var flat=toward-Position;
            flat.y=0;
            if(flat.sqrMagnitude>.01f)actor.rotation=Quaternion.LookRotation(flat);
            foreach(var (mesh,index) in laughShapes)mesh.SetBlendShapeWeight(index,Mathf.SmoothStep(0,100,duration*4));
            if(gait){if(strideA>=0)gait.SetBlendShapeWeight(strideA,0);if(strideB>=0)gait.SetBlendShapeWeight(strideB,0);}
            visual.localRotation=modelFacing*Quaternion.Euler(Mathf.Sin(duration*22)*5,0,Mathf.Sin(duration*16)*5);
        }

        static Material Tint(Color color,float glow=0)
        {
            var m=new Material(Shader.Find("Standard")){color=color};
            if(glow>0){m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",color*glow);}
            return m;
        }

        internal void Reset()
        {
            actor.position=posts[1];
            foreach(var (mesh,index) in laughShapes)mesh.SetBlendShapeWeight(index,0);
            visual.localRotation=modelFacing;
            noises.Clear();
            visits.Clear();
            path.Clear();
            lastPost=-1;
            sinceSeen=40;
            retry=0;
            nextDecision=0;
            Pursuing=false;
        }

        internal void Hear(Vector3 place,float volume)
        {
            float distance=Vector3.Distance(place,Position);
            if(distance>volume*55) return;
            var direction=place-Position;
            bool walled=Physics.Raycast(Position+Vector3.up, direction.normalized,Mathf.Max(0,distance-.65f),~0,QueryTriggerInteraction.Ignore);
            float strength=volume/(1+.05f*distance)*(walled?.54f:1);
            if(strength<.083f)return;
            if(noises.Count==12)noises.RemoveAt(0);
            noises.Add(new Cue{Place=place,Loudness=strength});
        }

        void Layer(float[] input,float[] output,int offset,int bias)
        {
            for(int o=0;o<output.Length;o++)
            {
                float v=model.biases[bias+o];
                for(int i=0;i<input.Length;i++)v+=input[i]*model.weights[offset+o*input.Length+i];
                output[o]=(float)Math.Tanh(v);
            }
        }

        float Score(int kind,Vector3 p,float age,float strength,float recent,float repeats,bool sees)
        {
            Array.Clear(features,0,features.Length);
            features[kind]=1;
            features[4]=Mathf.Clamp01(Vector3.Distance(p,Position)/48);
            features[5]=Mathf.Clamp01(1-age/14);
            features[6]=strength;
            features[7]=recent;
            features[8]=Mathf.Abs(p.y-Position.y)<1.5f?1:0;
            features[9]=repeats;
            features[10]=sees?1:0;
            Layer(features,hiddenA,0,0);
            Layer(hiddenA,hiddenB,264,24);
            float result=model.biases[40];
            for(int i=0;i<hiddenB.Length;i++)result+=model.weights[648+i]*hiddenB[i];
            return result;
        }

        internal void Tick(float dt)
        {
            sinceSeen+=dt;
            for(int i=noises.Count-1;i>=0;i--)
            {
                var cue=noises[i];
                cue.Age+=dt;
                if(cue.Age>13)noises.RemoveAt(i);
                else noises[i]=cue;
            }
            Vector3 eyes=Position+Vector3.up*1.26f;
            Vector3 sight=game.Player.Eyes.position-eyes;
            bool clear=sight.magnitude<1.35f || Vector3.Dot(actor.forward,sight.normalized)>.39f;
            bool sees=sight.magnitude<(game.Player.TorchOn?21:game.Player.Crouched?7:12) && clear &&
                !Physics.Raycast(eyes,sight.normalized,out _,Mathf.Max(0,sight.magnitude-.2f),~(1 << NightPlayer.PlayerLayer),QueryTriggerInteraction.Ignore);
            if(sees){sinceSeen=0;lastSeen=game.Player.Feet;}
            // Finish the current physical stair flight before recalculating a new route.
            bool onStairs=Mathf.Abs(Position.y/3.6f-Mathf.Round(Position.y/3.6f))>.07f&&waypoint<path.Count;
            if((nextDecision-=dt)<=0 && !onStairs)
            {
                nextDecision=.32f;
                Decide(sees);
            }
            if(waypoint<path.Count)
            {
                Vector3 target=path[waypoint];
                Vector3 forward=target-Position;
                forward.y=0;
                if(forward.sqrMagnitude>.009f)
                    actor.rotation=Quaternion.RotateTowards(actor.rotation,Quaternion.LookRotation(forward),dt*430);
                game.OpenDoorsForHunter(Position);
                float speed=Pursuing?3.95f:1.62f;
                actor.position=Vector3.MoveTowards(Position,target,speed*dt);
                if(Vector3.Distance(Position,target)<.075f)waypoint++;
                stride+=dt*(Pursuing?16:8);
                float swing=Mathf.Sin(stride);
                if(gait)
                {
                    if(strideA>=0)gait.SetBlendShapeWeight(strideA,Mathf.Max(0,swing)*100);
                    if(strideB>=0)gait.SetBlendShapeWeight(strideB,Mathf.Max(0,-swing)*100);
                }
                visual.localRotation=modelFacing*Quaternion.Euler(swing*2,0,Mathf.Cos(stride)*3);
                if((checkTime-=dt)<=0){checkTime=Pursuing?.32f:.56f;game.PlayCreatureStep(Position);}
            }
            else visual.localRotation=Quaternion.Slerp(visual.localRotation,modelFacing,dt*10);
            if(sees&&Vector3.Distance(game.Player.Feet,Position)<.95f&&!game.Player.Vaulting)game.Captured();
        }

        void Decide(bool sees)
        {
            float best=float.NegativeInfinity;
            int post=-1, action=-1;
            Vector3 choice=Position;
            void Consider(int kind, Vector3 p, float age, float strength, float recent=0, float repeats=0, int index=-1)
            {
                float value=Score(kind,p,age,strength,recent,repeats,sees);
                if(value<=best || (Time.time<retry && Vector3.Distance(p,unreachable)<1.6f))return;
                best=value;choice=p;action=kind;post=index;
            }
            if(sees)Consider(3,lastSeen,0,1);
            else if(sinceSeen<13 && Vector3.Distance(Position,lastSeen)>1.2f)Consider(2,lastSeen,sinceSeen,.8f);
            foreach(var cue in noises)
                if(Vector3.Distance(cue.Place,Position)>1.25f)Consider(1,cue.Place,cue.Age,cue.Loudness);
            for(int i=0;i<posts.Length;i++)
            {
                if(Vector3.Distance(posts[i],Position)<1.5f)continue;
                float recent=visits.TryGetValue(i,out float when)?Mathf.Clamp01(1-(Time.time-when)/75f):0;
                Consider(0,posts[i],5,.2f,recent,(i==lastPost?.85f:0)+.12f*Mathf.PerlinNoise(i*.8f,cycle*.17f),i);
            }
            if(action<0)return;
            Pursuing=action==3;
            if(waypoint<path.Count&&Vector3.Distance(destination,choice)<1.1f)return;

            if(!routing.Find(Position,choice,path))
            {
                retry=Time.time+2.0f;
                unreachable=choice;
                if(post>=0)visits[post]=Time.time;
                Pursuing=false;
                return;
            }
            cycle++;
            destination=choice;
            waypoint=0;
            if(post>=0){lastPost=post;visits[post]=Time.time;}
        }
    }
}
