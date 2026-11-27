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
        internal Vector3 LaughSource => Position + actor.forward * .48f + Vector3.up * (Height * .79f);
        internal bool Pursuing { get; private set; }
        readonly NightGame game;
        readonly FloorRoutes routing;
        readonly Transform actor, visual;
        readonly HunterListenRipple listenRipple;
        readonly Vector3 fullScale, fullOffset;
        readonly Light captureFill;
        Camera captureCamera;
        Vector3 captureOffset;
        float playFieldOfView, silhouette = 1;
        const float Height = 2.24f;
        const float ListenPoseDuration = .48f;
        const float ListenCooldown = 4.5f;
        const int EnvironmentMask = ~(1 << NightPlayer.PlayerLayer);
        const int ClearanceMask = EnvironmentMask & ~(1 << FloorRoutes.DoorLayer);
        Vector3 recentListenTarget;
        Vector3 listenDirection;
        float listenTime, listenCooldown;
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
            new(1,0,-4),new(-24,0,10),new(-11,0,31),new(49,0,6),new(100,0,-17),new(155,0,-17),
            new(39,0,-7),new(80,0,-3),new(116,0,-23),new(136,0,-6),
            new(30,0,34),new(48,0,14),new(155,0,4)
        };
        static readonly int[] StartingPosts = { 1, 5, 10, 16, 13 };
        enum Intent { Patrol, Investigate, Search, Pursue }

        // Offsets are relative to the last visible direction of travel, never to the hidden player.
        static readonly Vector2[] SearchSpots = { new(0, 0), new(0, 2.9f), new(-2.3f, 1.5f), new(2.3f, 1.5f), new(0, -2.2f) };
        readonly List<(Intent Kind, Vector3 Place, float Rank, int Slot)> goals = new();
        readonly List<Vector3> proposedPath = new();
        Vector3 lastSeen, lastHeading, destination, unreachable;
        Quaternion scanFacing;
        float sinceSeen = 40, notice, scanTime, nextDecision, stride, retry, checkTime;
        bool recognized;
        int waypoint, lastPost = -1, cycle, searched, activeSlot;
        Intent activeIntent;
        struct Cue
        {
            internal Vector3 Place;
            internal float Loudness, Age;
            internal bool Footstep;
        }
        readonly List<Cue> noises = new();

        internal NightHunter(NightGame game)
        {
            this.game=game;
            routing=new FloorRoutes();
            actor=new GameObject("奶娃").transform;
            actor.SetParent(game.transform);
            actor.position=posts[1];
            listenRipple = HunterListenRipple.Create(game.transform);
            model=JsonUtility.FromJson<Model>(Resources.Load<TextAsset>("Creature/Policy").text);
            if(model.weights.Length!=664 || model.biases.Length!=41)
                throw new InvalidOperationException("The hunter neural policy has an incompatible shape.");
            visual=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Creature/MilkFrog"),actor).transform;
            var renderers=visual.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float scale=Height/bounds.size.y;
            fullScale=visual.localScale*scale;
            fullOffset=-(bounds.center-actor.position)*scale+Vector3.up*(Height*.5f);
            visual.localScale=fullScale;
            visual.localPosition=fullOffset;
            captureFill=new GameObject("捕获照明").AddComponent<Light>();
            captureFill.transform.SetParent(actor,false);
            captureFill.type=LightType.Spot;
            captureFill.spotAngle=96;
            captureFill.innerSpotAngle=52;
            captureFill.range=4.8f;
            captureFill.color=new Color(.99f,.89f,.76f);
            captureFill.intensity=1.46f;
            captureFill.shadows=LightShadows.Soft;
            captureFill.enabled=false;
            // Damp amphibian hide: fine pores and creases catch the hand torch without changing the painted colour.
            var hide=Resources.Load<Shader>("CreatureSkin");
            var skin=new Material(hide&&hide.isSupported?hide:Shader.Find("Standard")){mainTexture=Resources.Load<Texture2D>("Creature/Skin"),color=Color.white};
            skin.SetFloat("_Glossiness",.34f);
            skin.SetTexture("_Pores",SkinPores());
            var mucosa=Resources.Load<Texture2D>("Creature/Mucosa");
            var lining=OralSurface(mucosa,new Color(.92f,.71f,.62f),.13f);
            lining.SetFloat("_DepthTint",1);
            var tongue=OralSurface(mucosa,new Color(.75f,.38f,.33f),.16f);
            var enamel=Tint(new Color(.73f,.62f,.42f));
            enamel.SetFloat("_Glossiness",.17f);
            var iris=Tint(new Color(.66f,.48f,.14f),.075f);
            iris.mainTexture=Resources.Load<Texture2D>("Creature/Iris");
            var eyeBulb=Tint(new Color(.62f,.41f,.13f),.14f);
            var eyelid=Tint(new Color(.21f,.15f,.07f));
            foreach(var r in renderers)
            {
                if(r.name.StartsWith("01"))r.sharedMaterial=skin;
                else if(r.name=="Mouth lining")r.sharedMaterial=lining;
                else if(r.name.Contains("tooth"))r.sharedMaterial=enamel;
                else if(r.name.Contains("sulfur"))r.sharedMaterial=eyeBulb;
                else if(r.name.Contains("Scleral slit"))r.sharedMaterial=iris;
                else if(r.name.StartsWith("Tongue"))r.sharedMaterial=tongue;
                else r.sharedMaterial=eyelid;
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
            listenTime = 0;
            listenRipple?.Stop();
            var flat=toward-Position;
            flat.y=0;
            if(flat.sqrMagnitude>.01f)actor.rotation=Quaternion.LookRotation(flat);
            // The recording starts three laughs at .08, .73 and 1.38 seconds.
            float fromFirst=duration-.08f;
            float pulse=0;
            if(fromFirst>=0 && fromFirst<1.88f)
            {
                float beat=fromFirst%.65f;
                pulse=Mathf.SmoothStep(0,1,beat/.035f)*(1-Mathf.SmoothStep(0,1,(beat-.41f)/.17f));
            }
            float pose=Mathf.SmoothStep(0,1,duration/.17f)*(1-.79f*Mathf.SmoothStep(0,1,(duration-1.96f)/.35f));
            SetExpression(pose*(.77f+.23f*pulse));
            if(gait){if(strideA>=0)gait.SetBlendShapeWeight(strideA,0);if(strideB>=0)gait.SetBlendShapeWeight(strideB,0);}
            visual.localRotation=Quaternion.Euler(pose*(2.1f+pulse*4.7f),0,pulse*Mathf.Sin(duration*16)*3)*modelFacing;
        }

        internal void FrameCapture(Transform eyes,float elapsed)
        {
            Vector3 target=Position+Vector3.up*(Height*.61f);
            if (!captureCamera)
            {
                captureCamera=eyes.GetComponent<Camera>();
                playFieldOfView=captureCamera.fieldOfView;
                captureOffset=CaptureSpace(target,actor.forward);
                if(captureOffset.sqrMagnitude<2.1f*2.1f)
                {
                    // A side angle can keep the face in frame when a wall is right behind the player.
                    foreach(float angle in new[] {-28f,28f,-47f,47f})
                    {
                        var candidate=CaptureSpace(target,Quaternion.Euler(0,angle,0)*actor.forward);
                        if(candidate.sqrMagnitude>captureOffset.sqrMagnitude)captureOffset=candidate;
                    }
                }
            }
            float distance=Mathf.Min(captureOffset.magnitude,Mathf.SmoothStep(2.24f,2.73f,elapsed/.60f));
            Vector3 direction=captureOffset.normalized;
            eyes.SetPositionAndRotation(target+direction*distance,Quaternion.LookRotation(-direction));
            captureCamera.fieldOfView=Mathf.Lerp(84,67,Mathf.InverseLerp(1.15f,2.35f,distance));

            // A short fill near the lens keeps the face and hands readable even in unpowered rooms.
            captureFill.transform.SetPositionAndRotation(eyes.position+Vector3.up*.24f-eyes.right*.14f,
                Quaternion.LookRotation(target-eyes.position-Vector3.up*.24f+eyes.right*.14f));
            captureFill.enabled=true;
        }

        static Vector3 CaptureSpace(Vector3 target,Vector3 facing)
        {
            Vector3 offset=facing*2.72f+Vector3.up*.17f;
            return Physics.SphereCast(target,.13f,offset.normalized,out var wall,offset.magnitude,
                EnvironmentMask,QueryTriggerInteraction.Ignore) ?
                offset.normalized*Mathf.Max(.04f,wall.distance-.04f) : offset;
        }

        internal void EndCapture()
        {
            captureFill.enabled=false;
            if (!captureCamera) return;
            captureCamera.fieldOfView=playFieldOfView;
            captureCamera=null;
        }

        void SetExpression(float weight)
        {
            foreach(var (mesh,index) in laughShapes)mesh.SetBlendShapeWeight(index,Mathf.Clamp01(weight)*100);
        }

        static Texture2D SkinPores()
        {
            const int size=256;
            var random=new System.Random(8117);
            var height=new float[size*size];
            // Overlapping soft dimples of several sizes, tiled so the detail map repeats seamlessly.
            for(int i=0;i<1400;i++)
            {
                float cx=(float)random.NextDouble()*size, cy=(float)random.NextDouble()*size;
                float r=1.2f+(float)(random.NextDouble()*random.NextDouble())*7.5f;
                float depth=(.35f+(float)random.NextDouble()*.65f)*(r<3?.6f:1);
                int span=Mathf.CeilToInt(r*2);
                for(int dy=-span;dy<=span;dy++)
                for(int dx=-span;dx<=span;dx++)
                {
                    float d=Mathf.Sqrt(dx*dx+dy*dy)/r;
                    if(d>=2)continue;
                    int x=((int)cx+dx+size)%size, y=((int)cy+dy+size)%size;
                    height[y*size+x]-=depth*Mathf.Exp(-d*d*1.6f);
                }
            }
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                float hx=height[y*size+(x+1)%size]-height[y*size+(x+size-1)%size];
                float hy=height[((y+1)%size)*size+x]-height[((y+size-1)%size)*size+x];
                var n=new Vector3(-hx*.9f,-hy*.9f,1).normalized;
                pixels[y*size+x]=new Color32((byte)((n.x*.5f+.5f)*255),(byte)((n.y*.5f+.5f)*255),(byte)((n.z*.5f+.5f)*255),255);
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true){name="Skin pores",wrapMode=TextureWrapMode.Repeat};
            texture.SetPixels32(pixels);
            texture.Apply(true,true);
            return texture;
        }

        static Material OralSurface(Texture2D texture, Color tint,float smoothness)
        {
            var material=new Material(Resources.Load<Shader>("OralLining")) { color=tint, mainTexture=texture };
            material.SetFloat("_Glossiness",smoothness);
            return material;
        }

        static Material Tint(Color color,float glow=0)
        {
            var m=new Material(Shader.Find("Standard")){color=color};
            if(glow>0){m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",color*glow);}
            return m;
        }

        // Preview stills only: stand the creature somewhere without waking the policy.
        internal void Pose(Vector3 feet, float yaw)
        {
            actor.SetPositionAndRotation(feet, Quaternion.Euler(0, yaw, 0));
            SetExpression(0);
        }

        internal void Reset()
        {
            EndCapture();
            actor.position = posts[StartingPosts[game.State.night - 1]];
            SetExpression(0);
            visual.localRotation=modelFacing;
            silhouette=1;
            visual.localScale=fullScale;
            visual.localPosition=fullOffset;
            noises.Clear();
            visits.Clear();
            path.Clear();
            lastPost=-1;
            sinceSeen = 40;
            recognized = false;
            notice = scanTime = 0;
            waypoint = searched = 0;
            activeIntent = Intent.Patrol;
            retry=0;
            nextDecision=0;
            Pursuing=false;
            listenTime = listenCooldown = 0;
            recentListenTarget = new Vector3(float.PositiveInfinity, 0, 0);
            listenRipple?.Stop();
        }

        internal void Hear(Vector3 place, float volume) => HearNoise(place, volume, "");

        internal void HearNoise(Vector3 place, float volume, string sound)
        {
            bool footstep = sound == "step";
            Vector3 listener = Position + Vector3.up * 1.26f;
            Vector3 source = place + Vector3.up * .28f;
            float distance = Vector3.Distance(listener, source);
            float range = volume * (footstep ? 45 : 58) * Mathf.Lerp(1, 1.12f, NightPressure);
            if (distance > range) return;

            bool obstructed = Physics.Linecast(listener, source, EnvironmentMask, QueryTriggerInteraction.Ignore);
            float transmission = obstructed ? (footstep ? .36f : .63f) : 1;
            if (Mathf.Abs(place.y - Position.y) > 2.75f && obstructed)
                transmission *= footstep ? .63f : .78f;
            float strength = Mathf.Clamp01(volume * transmission / (1 + .047f * distance));
            if (strength < .075f || obstructed && distance > range * .83f) return;

            // A cue on top of a workbench is investigated from the floor beside it.
            bool indoor = NightAtlas.Building.Contains(new Vector2(place.x, place.z));
            float level = indoor ? 3.6f * Mathf.Floor((place.y + .18f) / 3.6f) : 0;
            bool nearSlope = FloorRoutes.IsOnStair(place) || !indoor && place.y < -.24f;
            if (Physics.Raycast(place + Vector3.up * .18f, Vector3.down, out var floor, 2.85f,
                ClearanceMask, QueryTriggerInteraction.Ignore) && floor.normal.y > .68f &&
                (nearSlope || Mathf.Abs(floor.point.y - level) <= .55f))
                place.y = floor.point.y;
            else place.y = level;

            // Consecutive footsteps in the same area are a trail, rather than competing sounds.
            if (footstep)
            {
                for (int i = noises.Count - 1; i >= 0; i--)
                {
                    var earlier = noises[i];
                    if (!earlier.Footstep || earlier.Age > 1 || (earlier.Place - place).sqrMagnitude > 3.24f) continue;
                    noises.RemoveAt(i);
                    strength = Mathf.Max(strength, earlier.Loudness * .94f);
                    break;
                }
            }
            if (noises.Count == 12) noises.RemoveAt(0);
            noises.Add(new Cue { Place = place, Loudness = strength, Footstep = footstep });
            if (!footstep && strength > .23f) nextDecision = Mathf.Min(nextDecision, .05f);
        }

        float NightPressure => (game.State.night - 1f) / (NightState.MaxNights - 1f);

        bool PlayerVisible(out float distance)
        {
            Vector3 eye = Position + Vector3.up * 1.26f;
            Vector3 toward = game.Player.Eyes.position - eye;
            distance = toward.magnitude;
            float reach = game.Player.TorchOn ? 21f : game.Player.Crouched ? 7f : 12f;
            reach *= Mathf.Lerp(1, 1.10f, NightPressure);
            if (distance > reach || distance > 1.28f && Vector3.Dot(actor.forward, toward.normalized) < .39f) return false;
            return !Physics.Raycast(eye, toward.normalized, Mathf.Max(0, distance - .10f),
                EnvironmentMask, QueryTriggerInteraction.Ignore);
        }

        bool ObservePlayer(float dt)
        {
            bool visible = PlayerVisible(out float distance);
            float focusRate = Mathf.Lerp(5.8f, 2.4f, Mathf.InverseLerp(1, 17, distance));
            notice = Mathf.MoveTowards(notice, visible ? 1 : 0, dt * (visible ? focusRate : 4.1f));
            bool sees = visible && (notice >= .58f || recognized || distance <= 1.28f);
            if (sees)
            {
                Vector3 motion = Vector3.ProjectOnPlane(game.Player.Body.velocity, Vector3.up);
                lastHeading = motion.sqrMagnitude > .12f ? motion.normalized : actor.forward;
                lastSeen = game.Player.Feet;
                sinceSeen = 0;
                searched = 0;
            }
            if (sees != recognized) nextDecision = 0;
            recognized = sees;
            return sees;
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
            sinceSeen += dt;
            listenCooldown -= dt;
            listenRipple?.Tick(dt);
            for (int i = noises.Count - 1; i >= 0; i--)
            {
                var cue = noises[i];
                cue.Age += dt;
                if (cue.Age > (cue.Footstep ? 7.5f : 13f)) noises.RemoveAt(i);
                else noises[i] = cue;
            }

            bool sees = ObservePlayer(dt);
            bool finishingFlight = FloorRoutes.IsOnStair(Position) && waypoint < path.Count;
            if ((nextDecision -= dt) <= 0 && !finishingFlight && (scanTime <= 0 || sees))
            {
                nextDecision = sees ? .22f : .37f;
                Decide(sees);
            }

            if (sees)
            {
                scanTime = 0;
                if (listenTime > 0)
                {
                    listenTime = 0;
                    listenRipple?.Stop();
                }
            }
            if (listenTime > 0) HoldListenPose(dt);
            else if (scanTime > 0)
            {
                scanTime = Mathf.Max(0, scanTime - dt);
                actor.rotation = Quaternion.RotateTowards(actor.rotation, scanFacing, dt * 135f);
                SetStride(0);
            }
            else if (waypoint < path.Count) FollowPath(dt);
            else
            {
                SetStride(0);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, modelFacing, dt * 10f);
            }
            FitSilhouette(dt);

            if (sees && (game.Player.Feet - Position).sqrMagnitude < .95f * .95f &&
                !game.Player.Vaulting && PlayerVisible(out _))
                game.Captured();
        }

        void FollowPath(float dt)
        {
            Vector3 target = path[waypoint];
            Vector3 direction = Vector3.ProjectOnPlane(target - Position, Vector3.up);
            if (direction.sqrMagnitude > .005f)
                actor.rotation = Quaternion.RotateTowards(actor.rotation, Quaternion.LookRotation(direction), dt * 430);
            float speed = Pursuing ? Mathf.Lerp(3.22f, 3.41f, NightPressure) : activeIntent == Intent.Patrol ? 1.46f : 1.77f;
            Vector3 next = Vector3.MoveTowards(Position, target, speed * dt);
            if (!FloorRoutes.CanMove(Position, next) || !PassDoor(next))
            {
                RememberBlocked(destination);
                path.Clear();
                Pursuing = false;
                nextDecision = 0;
                return;
            }
            actor.position = next;
            if ((Position - target).sqrMagnitude < .06f * .06f) waypoint++;
            if (waypoint == path.Count) ReachedGoal();

            stride += dt * (Pursuing ? 16 : 8);
            float swing = Mathf.Sin(stride);
            SetStride(swing);
            visual.localRotation = modelFacing * Quaternion.Euler(swing * 2, 0, Mathf.Cos(stride) * 3);
            if ((checkTime -= dt) <= 0)
            {
                checkTime = Pursuing ? .32f : .56f;
                game.PlayCreatureStep(Position);
            }
        }

        bool PassDoor(Vector3 next)
        {
            Vector3 advance = next - Position;
            if (advance.sqrMagnitude < .000001f ||
                !Physics.SphereCast(Position + Vector3.up * .86f, .27f, advance.normalized,
                    out var hit, advance.magnitude + .05f, 1 << FloorRoutes.DoorLayer, QueryTriggerInteraction.Ignore))
                return true;
            game.OpenDoorsForHunter(Position);
            return !hit.collider || !hit.collider.gameObject.activeInHierarchy;
        }

        void SetStride(float swing)
        {
            if (!gait) return;
            if (strideA >= 0) gait.SetBlendShapeWeight(strideA, Mathf.Max(0, swing) * 100);
            if (strideB >= 0) gait.SetBlendShapeWeight(strideB, Mathf.Max(0, -swing) * 100);
        }

        void ReachedGoal()
        {
            if (activeIntent == Intent.Patrol || activeIntent == Intent.Pursue) return;
            if (activeIntent == Intent.Search) searched |= 1 << activeSlot;
            else
            {
                for (int i = noises.Count - 1; i >= 0; i--)
                    if ((noises[i].Place - destination).sqrMagnitude < 2.56f) noises.RemoveAt(i);
            }
            scanTime = activeIntent == Intent.Search ? .58f : .48f;
            float angle = (cycle & 1) == 0 ? -68f : 68f;
            scanFacing = Quaternion.AngleAxis(angle, Vector3.up) * actor.rotation;
            nextDecision = 0;
        }

        void FitSilhouette(float dt)
        {
            // Keep the hull and route graph independent of the render mesh. In doorway-sized
            // gaps the creature draws its shoulders in while remaining taller than the player.
            Vector3 low=Position+Vector3.up*1.04f;
            Vector3 high=Position+Vector3.up*1.39f;
            float width=1;
            if(Physics.CheckCapsule(low,high,1.01f,ClearanceMask,QueryTriggerInteraction.Ignore))
                width=Physics.CheckCapsule(low,high,.80f,ClearanceMask,QueryTriggerInteraction.Ignore)? .63f:.80f;
            float next=Mathf.MoveTowards(silhouette,width,dt*(width < silhouette ? 4.2f:1.7f));
            if(next==silhouette)return;
            silhouette=next;
            var compression=new Vector3(silhouette,1,silhouette);
            visual.localScale=Vector3.Scale(fullScale,compression);
            visual.localPosition=Vector3.Scale(fullOffset,compression);
        }

        void Decide(bool sees)
        {
            goals.Clear();
            bool underway = waypoint < path.Count;
            void Consider(Intent kind, Vector3 point, float age, float strength, int slot = -1, float recent = 0, float repeats = 0, float bias = 0)
            {
                if (Time.time < retry && (point - unreachable).sqrMagnitude < 1.96f) return;
                float rank = Score((int)kind, point, age, strength, recent, repeats, sees) + bias;
                if (underway && kind == activeIntent && (point - destination).sqrMagnitude < .81f) rank += .10f;
                goals.Add((kind, point, rank, slot));
            }

            if (sees) Consider(Intent.Pursue, lastSeen, 0, 1, bias: .20f);
            else if (sinceSeen < Mathf.Lerp(10.5f, 16f, NightPressure))
            {
                Vector3 right = Vector3.Cross(Vector3.up, lastHeading);
                for (int i = 0; i < SearchSpots.Length; i++)
                {
                    if ((searched & (1 << i)) != 0) continue;
                    Vector3 point = lastSeen + right * SearchSpots[i].x + lastHeading * SearchSpots[i].y;
                    if ((point - Position).sqrMagnitude < .36f) searched |= 1 << i;
                    else Consider(Intent.Search, point, sinceSeen, .90f, i, bias: i == 0 ? .34f : i == 1 ? .11f : 0);
                }
            }
            foreach (var cue in noises)
            {
                if ((cue.Place - Position).sqrMagnitude < .72f) continue;
                Consider(Intent.Investigate, cue.Place, cue.Age, cue.Loudness,
                    bias: cue.Footstep ? 0 : .14f * cue.Loudness);
            }
            for (int i = 0; i < posts.Length; i++)
            {
                if ((posts[i] - Position).sqrMagnitude < 2.25f) continue;
                float recent = visits.TryGetValue(i, out float visit) ? Mathf.Clamp01(1 - (Time.time - visit) / 75f) : 0;
                float repeat = (i == lastPost ? .85f : 0) + .12f * Mathf.PerlinNoise(i * .8f, cycle * .17f);
                Consider(Intent.Patrol, posts[i], 5, .2f, i, recent, repeat);
            }

            goals.Sort(static (a, b) => b.Rank.CompareTo(a.Rank));
            int attempts = 0;
            foreach (var goal in goals)
            {
                float drift = goal.Kind == Intent.Pursue ? .68f : .58f;
                if (underway && (goal.Place - destination).sqrMagnitude < drift * drift)
                {
                    activeIntent = goal.Kind;
                    activeSlot = goal.Slot;
                    Pursuing = goal.Kind == Intent.Pursue;
                    return;
                }
                if (++attempts > 3) break;
                if (!routing.Find(Position, goal.Place, proposedPath))
                {
                    RememberBlocked(goal.Place);
                    if (goal.Kind == Intent.Search) searched |= 1 << goal.Slot;
                    if (goal.Kind == Intent.Patrol) visits[goal.Slot] = Time.time;
                    continue;
                }

                cycle++;
                path.Clear();
                path.AddRange(proposedPath);
                destination = goal.Place;
                waypoint = 0;
                activeIntent = goal.Kind;
                activeSlot = goal.Slot;
                Pursuing = goal.Kind == Intent.Pursue;
                if (goal.Kind == Intent.Patrol)
                {
                    lastPost = goal.Slot;
                    visits[goal.Slot] = Time.time;
                }
                if (goal.Kind == Intent.Investigate && !sees && listenCooldown <= 0 &&
                    (destination - recentListenTarget).sqrMagnitude > 4)
                    BeginListening(destination);
                return;
            }
            Pursuing = false;
            if (!underway) path.Clear();
        }

        void RememberBlocked(Vector3 point)
        {
            unreachable = point;
            retry = Time.time + 2.4f;
        }

        void BeginListening(Vector3 toward)
        {
            recentListenTarget = toward;
            listenDirection = Vector3.ProjectOnPlane(toward - Position, Vector3.up);
            listenTime = ListenPoseDuration;
            listenCooldown = ListenCooldown;
            if (gait)
            {
                if (strideA >= 0) gait.SetBlendShapeWeight(strideA, 0);
                if (strideB >= 0) gait.SetBlendShapeWeight(strideB, 0);
            }

            var eye = game.Player.Eyes.position;
            var visibleChest = Position + Vector3.up * .92f - eye;
            float distance = visibleChest.magnitude;
            if (listenRipple && distance < 8.5f &&
                !Physics.Raycast(eye, visibleChest.normalized, distance, EnvironmentMask, QueryTriggerInteraction.Ignore) &&
                Physics.Raycast(Position + Vector3.up * .29f, Vector3.down, out var ground, .65f,
                    EnvironmentMask, QueryTriggerInteraction.Ignore) && ground.normal.y > .85f)
                listenRipple.PlayAt(ground);
        }

        void HoldListenPose(float dt)
        {
            listenTime = Mathf.Max(0, listenTime - dt);
            float phase = 1 - listenTime / ListenPoseDuration;
            float lean = Mathf.SmoothStep(0, 1, phase * 3) * (1 - Mathf.SmoothStep(0, 1, (phase - .56f) / .44f));
            if (listenDirection.sqrMagnitude > .04f)
                actor.rotation = Quaternion.RotateTowards(actor.rotation, Quaternion.LookRotation(listenDirection), dt * 248);
            visual.localRotation = modelFacing * Quaternion.Euler(-6f * lean, 0, 12f * lean);
        }
    }
}
