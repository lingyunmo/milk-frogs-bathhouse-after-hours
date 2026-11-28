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
        internal Vector3 Facing => actor.forward;
        internal IReadOnlyList<Vector3> PlannedSteps => path;
        internal int NextStep => waypoint;
        internal Vector3? CurrentGoal => waypoint < path.Count ? destination : null;
        internal string ObservedIntent => activeIntent switch
        {
            Intent.Pursue => recognized ? "目击追逐" : tracking ? "循迹追逐" : "赶往感知位置",
            Intent.Search => "搜索最后目击点",
            Intent.Investigate => investigationSound switch {
                "step" => "调查脚步声", "land" => "调查落地声", "generator-trip" => "调查跳闸警铃", _ => "调查声响"
            },
            _ when roundLeg => roundStop switch {
                RoundStop.MainHall => "回主楼巡查", RoundStop.UpperHall => "上楼巡查",
                RoundStop.SideHall => "巡查侧廊",
                RoundStop.Street => streetDistrict switch { 0 => "巡查浴场街面", 1 => "巡查街尾站牌", _ => "巡查东堤" },
                _ => "巡逻"
            },
            _ => "附近巡逻"
        };
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
        const float InvestigationMemory = 24f;
        const float BlockedTraversalMemory = 14f;
        const float ProgressRadius = .36f;
        const float StallLimit = 1.8f;
        const float WaypointTolerance = .06f;
        const float PatrolAreaSize = 10f;
        const int PatrolRadius = 10;
        const float FixInterval = 3f;
        const float FixChance = .10f;
        const float FixDistance = 24f;
        const float TrackDistance = 14f;
        const float BreakAwaySeconds = 2f;
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
        readonly Dictionary<int,float> patrolRetryUntil=new();
        readonly List<int> patrolNodes = new();
        Vector3? positionFix;
        float fixUntil, nextFix, separation, patrolSpeed, nextFootstepClue, searchUntil;
        bool tracking, roundLeg;
        enum RoundStop { None, MainHall, UpperHall, SideHall, Street }
        RoundStop roundStop;
        int roundVariant, upperFloor, sideRotation, streetDistrict, failedRoundStops;
        float retryRoundAt;
        static readonly Vector2[] HallSectors = { new(-11, 12), new(5, 12), new(-5, -11) };
        // One street leg per building round. Rotate districts on arrival; sensor rolls stay local.
        static readonly Vector2[][] StreetDistricts = {
            new Vector2[] { new(30, -19), new(48, -17), new(-5, 32) },
            new Vector2[] { new(152, -18), new(160, -18), new(157, -2) },
            new Vector2[] { new(316, -18), new(311, 4), new(300, -14) }
        };
        static readonly Vector2[] AnnexSectors = { new(28, 12), new(31, 12), new(34, 12) };
        Vector2[] RoundSectors => roundStop == RoundStop.Street ? StreetDistricts[streetDistrict] :
            roundStop == RoundStop.SideHall && sideRotation == 2 ? AnnexSectors : HallSectors;

        static readonly Vector3[] NightSpawns =
        {
            new(10,0,12), new(-16,3.6f,12), new(9,7.2f,12), new(-24,0,10), new(10,-3.6f,12)
        };
        enum Intent { Patrol, Investigate, Search, Pursue }

        // Offsets are relative to the last visible direction of travel, never to the hidden player.
        static readonly Vector2[] SearchSpots = { new(0, 0), new(0, 2.9f), new(-2.3f, 1.5f), new(2.3f, 1.5f), new(0, -2.2f) };
        readonly List<(Intent Kind, Vector3 Place, float Rank, int Slot, string Sound)> goals = new();
        readonly List<Vector3> proposedPath = new();
        readonly List<(Vector3 Place, string Sound, float Until)> checkedSounds = new();
        readonly List<(Vector3 Place, Vector3 Direction, float Until)> blockedTraversals = new();
        Vector3 lastSeen, lastHeading, destination, unreachable;
        Vector3 progressAnchor;
        Quaternion scanFacing;
        float sinceSeen = 40, notice, scanTime, nextDecision, stride, retry, checkTime;
        float elapsed, timeWithoutProgress;
        bool recognized;
        int waypoint, lastPost = -1, cycle, searched, activeSlot;
        Intent activeIntent;
        string investigationSound;
        struct Cue
        {
            internal Vector3 Place;
            internal float Loudness, Age;
            internal string Sound;
            internal bool Footstep => Sound == "step";
        }
        readonly List<Cue> noises = new();

        internal NightHunter(NightGame game)
        {
            this.game=game;
            routing=new FloorRoutes();
            actor=new GameObject("奶娃").transform;
            actor.SetParent(game.transform);
            actor.position=NightSpawns[0];
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
            ResetProgress();
            SetExpression(0);
        }

        internal void JoinGuard(Vector3 position)
        {
            actor.gameObject.SetActive(true); Pursuing=false; listenTime=0; listenRipple.Stop();
            Pose(position,270); visual.localRotation=modelFacing; SetStride(0); visual.localScale=fullScale; visual.localPosition=fullOffset;
        }

        internal void GuardStep(Vector3 post,Vector3 threat,float dt,bool strike,float speed=3.45f)
        {
            var before=Position;
            actor.position=Vector3.MoveTowards(before,post,dt*speed);
            var facing=(post-before).sqrMagnitude>.1f ? post-before : threat-Position;
            facing.y=0;
            if(facing.sqrMagnitude>.01f)
                actor.rotation=Quaternion.RotateTowards(actor.rotation,Quaternion.LookRotation(facing),dt*300);
            float travel=(Position-before).magnitude;
            stride+=travel*2.35f;
            float step=travel>.001f ? Mathf.Sin(stride) : 0;
            SetStride(step);
            SetExpression(strike ? .78f : .03f);
            visual.localRotation=Quaternion.Euler(3.1f*Mathf.Abs(step),0,-2.8f*step)*modelFacing;
            visual.localPosition=fullOffset+Vector3.up*(.042f*Mathf.Abs(step));
            visual.localScale=fullScale;
        }

        // Feet plant, back arches, then the full weight rolls through the shoulder.
        internal void GuardBlow(float time)
        {
            float coil=Mathf.SmoothStep(0,1,Mathf.Clamp01(time/.30f))*(1-Mathf.SmoothStep(0,1,(time-.30f)/.16f));
            float drive=Mathf.SmoothStep(0,1,Mathf.Clamp01((time-.30f)/.16f))*(1-Mathf.SmoothStep(0,1,(time-.51f)/.37f));
            SetStride(-.67f*coil+.91f*drive);
            SetExpression(.12f+.56f*coil+.66f*drive);
            visual.localPosition=fullOffset+new Vector3(-.06f*coil,-.16f*coil+.04f*drive,-.18f*coil+.48f*drive);
            visual.localScale=Vector3.Scale(fullScale,new Vector3(1+.045f*coil,1-.075f*coil,1+.025f*coil));
            visual.localRotation=Quaternion.Euler(-14*coil+27*drive,-17*coil+12*drive,10*coil-15*drive)*modelFacing;
        }

        internal void ReleaseReturnLine()
        {
            Pursuing=false;
            actor.gameObject.SetActive(false);
        }

        internal void Reset()
        {
            EndCapture();
            actor.gameObject.SetActive(!game.State.circuit.Solved);
            actor.SetPositionAndRotation(NightSpawns[game.State.night - 1], Quaternion.identity);
            destination = lastSeen = Position;
            lastHeading = actor.forward;
            SetExpression(0);
            visual.localRotation=modelFacing;
            silhouette=1;
            visual.localScale=fullScale;
            visual.localPosition=fullOffset;
            noises.Clear();
            faultSite = null;
            checkedSounds.Clear();
            blockedTraversals.Clear();
            investigationSound = null;
            elapsed = 0;
            ResetProgress();
            visits.Clear();
            patrolRetryUntil.Clear();
            positionFix = null;
            nextFix = FixInterval;
            fixUntil = separation = searchUntil = 0;
            tracking = roundLeg = false;
            roundStop = RoundStop.None;
            upperFloor = UnityEngine.Random.Range(1, 3);
            roundVariant = UnityEngine.Random.Range(0, HallSectors.Length);
            sideRotation = UnityEngine.Random.Range(0, 4);
            streetDistrict = game.State.night % StreetDistricts.Length;
            failedRoundStops = 0;
            retryRoundAt = 0;
            nextFootstepClue = 0;
            cycle = 0;
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

        // The wired fault bells relay a fixed station address. They never relay the player's position.
        Vector3? faultSite;
        float faultUntil;
        internal void HearGeneratorTrip(Vector3 approach)
        {
            faultSite = approach;
            faultUntil = elapsed + 24f + Vector3.Distance(Position, approach) / 1.4f;
            nextDecision = 0;
            scanTime = 0;
        }

        internal void Hear(Vector3 place, float volume) => HearNoise(place, volume, "");

        internal void HearNoise(Vector3 place, float volume, string sound)
        {
            bool footstep = sound == "step";
            if (footstep && elapsed < nextFootstepClue) return;
            Vector3 listener = Position + Vector3.up * 1.26f;
            Vector3 source = place + Vector3.up * .28f;
            float distance = Vector3.Distance(listener, source);
            float range = volume * (footstep ? 40 : 54) * Mathf.Lerp(1, 1.12f, NightPressure);
            if (distance > range) return;

            bool obstructed = Physics.Linecast(listener, source, EnvironmentMask, QueryTriggerInteraction.Ignore);
            float transmission = obstructed ? (footstep ? .59f : .74f) : 1;
            if (Mathf.Abs(place.y - Position.y) > 2.75f && obstructed)
                transmission *= footstep ? .63f : .78f;
            float strength = Mathf.Clamp01(volume * transmission / (1 + .047f * distance));
            if (strength < .058f || obstructed && distance > range * .83f) return;

            // A cue on top of a workbench is investigated from the floor beside it.
            bool indoor = sound != "drone" && FloorRoutes.IsInsideBathhouse(place);
            float level = indoor ? 3.6f * Mathf.Floor((place.y + .18f) / 3.6f) : 0;
            bool nearSlope = FloorRoutes.IsOnStair(place) || !indoor && place.y < -.24f;
            if (Physics.Raycast(place + Vector3.up * .18f, Vector3.down, out var floor, 2.85f,
                ClearanceMask, QueryTriggerInteraction.Ignore) && floor.normal.y > .68f &&
                (nearSlope || Mathf.Abs(floor.point.y - level) <= .55f))
                place.y = floor.point.y;
            else place.y = level;
            if (!footstep && SoundRecentlyChecked(place, sound)) return;
            if (footstep) nextFootstepClue = elapsed + (obstructed ? 1.2f : .85f);

            // Repeated impacts and footsteps from one spot update a single clue.
            for (int i = noises.Count - 1; i >= 0; i--)
            {
                var earlier = noises[i];
                if (earlier.Sound != sound || earlier.Age > (footstep ? 1 : 3) ||
                    (earlier.Place - place).sqrMagnitude > 3.24f) continue;
                noises.RemoveAt(i);
                strength = Mathf.Max(strength, earlier.Loudness * .94f);
                break;
            }
            if (noises.Count == 12) noises.RemoveAt(0);
            noises.Add(new Cue { Place = place, Loudness = strength, Sound = sound });
            bool sameInvestigation = activeIntent == Intent.Investigate && waypoint < path.Count &&
                investigationSound == sound && (destination - place).sqrMagnitude <= 3.24f;
            if (!sameInvestigation && strength >= (footstep ? .08f : .14f)) nextDecision = Mathf.Min(nextDecision, .05f);
        }

        bool SoundRecentlyChecked(Vector3 place, string sound)
        {
            foreach (var checkedSound in checkedSounds)
                if (checkedSound.Sound == sound && (checkedSound.Place - place).sqrMagnitude <= 7.29f) return true;
            return false;
        }

        void RememberCheckedSound(Vector3 place, string sound, float duration)
        {
            if (sound == null || sound == "step" || SoundRecentlyChecked(place, sound)) return;
            if (checkedSounds.Count >= 8) checkedSounds.RemoveAt(0);
            checkedSounds.Add((place, sound, elapsed + duration));
            for (int i = noises.Count - 1; i >= 0; i--)
                if (noises[i].Sound == sound && (noises[i].Place - place).sqrMagnitude <= 7.29f)
                    noises.RemoveAt(i);
        }

        float NightPressure => (game.State.night - 1f) / (NightState.MaxNights - 1f);

        bool PlayerVisible(out float distance)
        {
            if (game.EmptyObservation) { distance = float.PositiveInfinity; return false; }
            Vector3 eye = Position + Vector3.up * 1.26f;
            Vector3 toward = game.Player.Eyes.position - eye;
            distance = toward.magnitude;
            if (game.Player.Concealed) return false;
            bool lit = game.Player.TorchOn;
            bool inBeam = lit && Vector3.Dot(game.Player.Eyes.forward, -toward.normalized) >= .90f;
            float reach = inBeam ? 18f : lit ? 14f : game.Player.Crouched ? 5.5f : 10f;
            reach *= Mathf.Lerp(1, 1.10f, NightPressure);
            if (distance > reach || (!inBeam && Vector3.Dot(actor.forward, toward.normalized) < .23f)) return false;
            if (game.Player.Crouched && !game.Player.TorchOn && Physics.Raycast(game.Player.Eyes.position, Vector3.up,
                .63f, ClearanceMask, QueryTriggerInteraction.Ignore))
                return false;
            return !Physics.Raycast(eye, toward.normalized, Mathf.Max(0, distance - .10f),
                EnvironmentMask, QueryTriggerInteraction.Ignore);
        }

        bool ObservePlayer(float dt)
        {
            bool visible = PlayerVisible(out float distance);
            float focusSeconds = Mathf.Lerp(.25f, .85f, Mathf.InverseLerp(1, 22, distance));
            if (game.Player.TorchOn) focusSeconds *= .64f;
            if (game.Player.Crouched && !game.Player.TorchOn) focusSeconds += .22f;
            focusSeconds *= Mathf.Lerp(1, .85f, NightPressure);
            notice = Mathf.MoveTowards(notice, visible ? 1 : 0, dt * (visible ? 1f / focusSeconds : 3f));
            bool sees = visible && notice >= 1f;
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

        static (Vector3 Center, bool Inside) AreaOf(Vector3 feet)
        {
            static float Center(float coordinate) =>
                (Mathf.Floor(coordinate / PatrolAreaSize) + .5f) * PatrolAreaSize;

            var center = new Vector3(Center(feet.x), FloorRoutes.LevelHeight(FloorRoutes.LevelAt(feet.y)), Center(feet.z));
            return (center, FloorRoutes.IsInsideBathhouse(feet));
        }

        // A remote hit is a snapshot. Until there is close contact or a new roll the destination stays put.
        void UpdateTracking(bool sees, float dt)
        {
            bool rollDue = elapsed >= nextFix;
            if (rollDue) nextFix += FixInterval;
            if (game.EmptyObservation || game.Player.Concealed)
            {
                tracking = Pursuing = false;
                positionFix = null;
                return;
            }

            bool navigating = !game.Player.Vaulting && !game.Player.InHideSpot;
            if (sees)
            {
                tracking = true;
                separation = 0;
                positionFix = null;
            }
            if (tracking && !sees)
            {
                bool near = (game.Player.Feet - Position).sqrMagnitude <= TrackDistance * TrackDistance;
                separation = near ? 0 : separation + dt;
                if (near && navigating)
                {
                    lastSeen = game.Player.Feet;
                    sinceSeen = 0;
                    searched = 0;
                }
                if (separation >= BreakAwaySeconds)
                {
                    tracking = Pursuing = false;
                    // Search time includes the walk to the last clue, never the distance to the hidden player.
                    searchUntil = elapsed + Mathf.Clamp(Vector3.Distance(Position, lastSeen) / 1.77f + 5f, 7f, 18f);
                    nextDecision = 0;
                }
            }
            if (positionFix.HasValue && (elapsed >= fixUntil || (Position - positionFix.Value).sqrMagnitude < 1f))
            {
                positionFix = null;
                nextDecision = 0;
            }
            if (!rollDue || tracking || !navigating || UnityEngine.Random.value >= FixChance) return;

            var feet = game.Player.Feet;
            float gap = Vector3.Distance(Position, feet);
            if (gap > FixDistance) return;
            if (gap <= TrackDistance)
            {
                tracking = true;
                separation = sinceSeen = 0;
                searched = 0;
                lastHeading = actor.forward;
                lastSeen = feet;
                positionFix = null;
            }
            else
            {
                positionFix = feet;
                fixUntil = elapsed + 10f + gap / 2.4f;
            }
            nextDecision = scanTime = 0;
        }

        void SetRoundStop(RoundStop stop)
        {
            roundStop = stop;
            roundLeg = false;
            failedRoundStops = 0;
            retryRoundAt = elapsed;
            int count = RoundSectors.Length;
            roundVariant = (roundVariant + UnityEngine.Random.Range(1, count)) % count;
        }

        void FinishRoundStop()
        {
            switch (roundStop)
            {
                case RoundStop.MainHall:
                    SetRoundStop(RoundStop.UpperHall);
                    break;
                case RoundStop.UpperHall:
                    upperFloor = upperFloor == 1 ? 2 : 1;
                    SetRoundStop(sideRotation % 2 == 0 ? RoundStop.SideHall : RoundStop.Street);
                    break;
                case RoundStop.SideHall:
                    SetRoundStop(RoundStop.Street);
                    break;
                case RoundStop.Street:
                    streetDistrict = (streetDistrict + 1) % StreetDistricts.Length;
                    sideRotation = (sideRotation + 1) % 4;
                    SetRoundStop(RoundStop.None);
                    break;
            }
        }

        void DeferRoundStop()
        {
            roundLeg = false;
            int count = RoundSectors.Length;
            roundVariant = (roundVariant + 1) % count;
            failedRoundStops++;
            // Walk locally between attempts. A sealed upper landing must not trap the hunter indoors.
            if (failedRoundStops >= count && roundStop == RoundStop.Street)
                FinishRoundStop();
            else if (failedRoundStops >= count && (roundStop == RoundStop.UpperHall || roundStop == RoundStop.SideHall))
                SetRoundStop(RoundStop.Street);
            else if (failedRoundStops >= count && roundStop == RoundStop.Street)
                FinishRoundStop();
            else
                retryRoundAt = elapsed + (failedRoundStops % count == 0 ? 14f : 3f);
        }

        internal void Tick(float dt)
        {
            elapsed += dt;
            for (int i = checkedSounds.Count - 1; i >= 0; i--)
                if (checkedSounds[i].Until <= elapsed) checkedSounds.RemoveAt(i);
            for (int i = blockedTraversals.Count - 1; i >= 0; i--)
                if (blockedTraversals[i].Until <= elapsed) blockedTraversals.RemoveAt(i);
            sinceSeen += dt;
            listenCooldown -= dt;
            listenRipple?.Tick(dt);
            for (int i = noises.Count - 1; i >= 0; i--)
            {
                var cue = noises[i];
                cue.Age += dt;
                if (cue.Age > (cue.Footstep ? 6f : 16f)) noises.RemoveAt(i);
                else noises[i] = cue;
            }

            bool sees = ObservePlayer(dt);
            UpdateTracking(sees, dt);
            bool following = tracking || positionFix.HasValue;
            bool finishingFlight = FloorRoutes.IsOnStair(Position) && waypoint < path.Count;
            if ((nextDecision -= dt) <= 0 && !finishingFlight && (scanTime <= 0 || following))
            {
                nextDecision = following ? .35f : .45f;
                Decide(sees);
            }

            if (following)
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
            while (waypoint < path.Count && (path[waypoint] - Position).sqrMagnitude < WaypointTolerance * WaypointTolerance)
                waypoint++;
            if (waypoint == path.Count)
            {
                ReachedGoal();
                return;
            }
            Vector3 target = path[waypoint];
            Vector3 direction = Vector3.ProjectOnPlane(target - Position, Vector3.up);
            if (direction.sqrMagnitude > .005f)
            {
                var facing = Quaternion.LookRotation(direction);
                actor.rotation = Quaternion.RotateTowards(actor.rotation, facing, dt * (Pursuing ? 225f : 160f));
                if (Quaternion.Angle(actor.rotation, facing) > 55f)
                {
                    SetStride(0);
                    return;
                }
            }
            float speed = Pursuing ? Mathf.Lerp(2.45f, 2.75f, NightPressure) : activeIntent == Intent.Patrol ? patrolSpeed : 1.77f;
            Vector3 next = Vector3.MoveTowards(Position, target, speed * dt);
            bool progressed = (next - progressAnchor).sqrMagnitude >= ProgressRadius * ProgressRadius;
            if (!progressed) timeWithoutProgress += dt;
            if (!FloorRoutes.CanMove(Position, next) || !PassDoor(next) || !progressed && timeWithoutProgress >= StallLimit)
            {
                RememberTraversal(Position, next);
                RememberBlocked(activeIntent, destination, activeSlot);
                if (roundLeg) DeferRoundStop();
                path.Clear();
                Pursuing = false;
                nextDecision = 0;
                SetStride(0);
                ResetProgress();
                return;
            }
            actor.position = next;
            if (progressed) ResetProgress();
            if ((Position - target).sqrMagnitude < WaypointTolerance * WaypointTolerance) waypoint++;
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

        void ResetProgress()
        {
            progressAnchor = Position;
            timeWithoutProgress = 0;
        }

        void RememberTraversal(Vector3 from, Vector3 next)
        {
            Vector3 direction = next - from;
            if (direction.sqrMagnitude < .00001f) return;
            if (blockedTraversals.Count >= 8) blockedTraversals.RemoveAt(0);
            blockedTraversals.Add((next, direction.normalized, elapsed + BlockedTraversalMemory));
        }

        bool CanTraverse(Vector3 from, Vector3 to)
        {
            if (!game.CanCrossKeyDoors(from, to)) return false;
            Vector3 delta = to - from;
            float lengthSquared = delta.sqrMagnitude;
            if (lengthSquared < .000001f) return true;
            if (!game.State.firstFireDoor && delta.z > 0 && from.z < NightWorld.FireDoorPosition.z)
            {
                // The basement push bar faces the corridor. Approaches from the stair side
                // cannot cross the closed leaf, including the hunter's body clearance.
                Vector3 door = NightWorld.FireDoorPosition;
                float contact = door.z - .34f;
                if (to.z >= contact && Mathf.Abs(from.y - door.y) < .65f)
                {
                    Vector3 approach = Vector3.Lerp(from, to, (contact - from.z) / delta.z);
                    if (Mathf.Abs(approach.x - door.x) < 1.27f) return false;
                }
            }
            float headingThreshold = .55f * Mathf.Sqrt(lengthSquared);
            foreach (var blocked in blockedTraversals)
            {
                if (Vector3.Dot(delta, blocked.Direction) < headingThreshold) continue;
                float t = Mathf.Clamp01(Vector3.Dot(blocked.Place - from, delta) / lengthSquared);
                if ((from + delta * t - blocked.Place).sqrMagnitude < .20f) return false;
            }
            return true;
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
            ResetProgress();
            if (activeIntent == Intent.Patrol)
            {
                lastPost = activeSlot;
                visits[activeSlot] = elapsed;
                if (roundLeg) FinishRoundStop();
                nextDecision = 0;
                return;
            }
            if (activeIntent == Intent.Pursue)
            {
                if (positionFix.HasValue && (positionFix.Value - destination).sqrMagnitude < .64f) positionFix = null;
                return;
            }
            if (activeIntent == Intent.Search) searched |= 1 << activeSlot;
            else
            {
                RememberCheckedSound(destination, investigationSound, InvestigationMemory);
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
            bool underway = waypoint < path.Count;
            bool keepPatrol = underway && activeIntent == Intent.Patrol;
            bool keepInvestigation = underway && activeIntent == Intent.Investigate;
            bool hasTrail = elapsed < searchUntil || sinceSeen < Mathf.Lerp(6f, 10f, NightPressure);
            if (faultSite.HasValue && (elapsed > faultUntil || sees || (Position - faultSite.Value).sqrMagnitude < 2.25f))
                faultSite = null;
            bool quiet = !tracking && !positionFix.HasValue && !faultSite.HasValue && notice <= 0 && !hasTrail && noises.Count == 0 && !keepInvestigation;
            if (quiet && roundStop == RoundStop.None) SetRoundStop(RoundStop.MainHall);
            if (quiet && roundStop != RoundStop.None && elapsed >= retryRoundAt)
            {
                if (keepPatrol && roundLeg) return;
                goals.Clear();
                bool street = roundStop == RoundStop.Street;
                Vector2 sector = RoundSectors[roundVariant];
                int floor = roundStop switch {
                    RoundStop.UpperHall => upperFloor,
                    RoundStop.SideHall => sideRotation == 0 ? -1 : 1,
                    _ => 0
                };
                Vector3 center = new(sector.x, FloorRoutes.LevelHeight(floor), sector.y);
                routing.CollectPatrolNodes(Position, center, !street, 5, 1, patrolNodes);
                foreach (int node in patrolNodes)
                {
                    Vector3 point = routing.PointAt(node);
                    if (street ? game.IsInsideBuilding(point) : Mathf.Abs(point.z - sector.y) > 1.25f) continue;
                    if (patrolRetryUntil.TryGetValue(node, out float until) && elapsed < until) continue;
                    float recent = visits.TryGetValue(node, out float visit) ? Mathf.Clamp01(1 - (elapsed - visit) / 75f) : 0;
                    Consider(Intent.Patrol, point, 5, .2f, node, recent, bias: UnityEngine.Random.value * .12f);
                }
                if (TryRoute(onRound: true)) return;
                DeferRoundStop();
            }
            if (quiet && keepPatrol) return;
            var localArea = AreaOf(Position);

            goals.Clear();
            void Consider(Intent kind, Vector3 point, float age, float strength, int slot = -1,
                float recent = 0, float repeats = 0, float bias = 0, string sound = null)
            {
                if (kind != Intent.Patrol && elapsed < retry && (point - unreachable).sqrMagnitude < 1.96f) return;
                float rank = Score((int)kind, point, age, strength, recent, repeats, sees) + bias;
                if (underway && kind == activeIntent && (point - destination).sqrMagnitude < .81f) rank += .10f;
                goals.Add((kind, point, rank, slot, sound));
            }

            // Once identified, a moving target outranks old distractions. The path still obeys doors and stairs.
            if (tracking || positionFix.HasValue)
                Consider(Intent.Pursue, tracking ? lastSeen : positionFix.Value, 0, 1, bias: 12f);
            else if (hasTrail)
            {
                Vector3 right = Vector3.Cross(Vector3.up, lastHeading);
                float forwardReach = game.IsInsideBuilding(lastSeen) ? 1f : 2.3f;
                for (int i = 0; i < SearchSpots.Length; i++)
                {
                    if ((searched & (1 << i)) != 0) continue;
                    Vector2 offset = SearchSpots[i];
                    Vector3 point = lastSeen + right * offset.x + lastHeading * (offset.y > 0 ? offset.y * forwardReach : offset.y);
                    if ((point - Position).sqrMagnitude < .36f) searched |= 1 << i;
                    else Consider(Intent.Search, point, sinceSeen, .90f, i,
                        bias: (elapsed < searchUntil && noises.Count == 0 ? 2f : 0f) + (i == 0 ? .34f : i == 1 ? .11f : 0));
                }
            }
            foreach (var cue in noises)
            {
                if ((cue.Place - Position).sqrMagnitude < .72f) continue;
                if (keepInvestigation && cue.Sound == investigationSound &&
                    (cue.Place - destination).sqrMagnitude < 1.44f) continue;
                Consider(Intent.Investigate, cue.Place, cue.Age, cue.Loudness,
                    bias: .18f + .30f * cue.Loudness - cue.Age * .035f, sound: cue.Sound);
            }
            if (faultSite.HasValue)
                Consider(Intent.Investigate, faultSite.Value, 0, 1, bias: 8f, sound: "generator-trip");
            int sensoryGoals = goals.Count;
            if (keepInvestigation && sensoryGoals == 0 && !sees && !hasTrail) return;
            if (keepPatrol)
            {
                // Only an observed trail, a sound or a sighting can compete with this patrol leg.
                Consider(Intent.Patrol, destination, 5, .2f, activeSlot);
            }
            else
            {
                AddPatrolGoals(localArea, 2);
                if (goals.Count == sensoryGoals) AddPatrolGoals(localArea, 1);
            }

            if (TryRoute(keepPatrol && roundLeg)) return;
            Pursuing = false;
            if (!underway) path.Clear();

            void AddPatrolGoals((Vector3 Center, bool Inside) region, int spacing)
            {
                routing.CollectPatrolNodes(Position, region.Center, region.Inside, PatrolRadius, spacing, patrolNodes);
                foreach (int node in patrolNodes)
                {
                    if (patrolRetryUntil.TryGetValue(node, out float until) && elapsed < until) continue;
                    Vector3 point = routing.PointAt(node);
                    float recent = visits.TryGetValue(node, out float visit) ? Mathf.Clamp01(1 - (elapsed - visit) / 75f) : 0;
                    float repeat = (lastPost >= 0 && (routing.PointAt(lastPost) - point).sqrMagnitude < 16 ? .85f : 0)
                        + .12f * Mathf.PerlinNoise(node * .8f, cycle * .17f);
                    Consider(Intent.Patrol, point, 5, .2f, node, recent, repeat);
                }
            }

            bool TryRoute(bool onRound)
            {
                goals.Sort(static (a, b) => b.Rank.CompareTo(a.Rank));
                Func<Vector3, Vector3, bool> allowed = !game.State.firstFireDoor || blockedTraversals.Count > 0 || game.HasLockedKeyDoors
                    ? CanTraverse : null;
                int attempts = 0;
                foreach (var goal in goals)
                {
                    bool sameImpact = goal.Kind == Intent.Investigate && activeIntent == Intent.Investigate &&
                        goal.Sound != "step" && goal.Sound == investigationSound;
                    float drift = goal.Kind == Intent.Pursue ? .68f : sameImpact ? 1.2f : .58f;
                    bool samePatrol = activeIntent == Intent.Patrol && activeSlot == goal.Slot &&
                        roundLeg == onRound;
                    if (underway && (goal.Place - destination).sqrMagnitude < drift * drift &&
                        (goal.Kind != Intent.Patrol || samePatrol))
                    {
                        roundLeg = goal.Kind == Intent.Patrol && onRound;
                        activeIntent = goal.Kind;
                        activeSlot = goal.Slot;
                        investigationSound = goal.Sound;
                        Pursuing = goal.Kind == Intent.Pursue;
                        return true;
                    }
                    if (++attempts > 3) break;
                    if (!routing.Find(Position, goal.Place, proposedPath, allowed))
                    {
                        RememberBlocked(goal.Kind, goal.Place, goal.Slot);
                        if (goal.Kind == Intent.Search) searched |= 1 << goal.Slot;
                        if (goal.Kind == Intent.Investigate) RememberCheckedSound(goal.Place, goal.Sound, 6f);
                        continue;
                    }

                    cycle++;
                    path.Clear();
                    path.AddRange(proposedPath);
                    destination = goal.Place;
                    waypoint = 0;
                    if (goal.Kind == Intent.Pursue)
                    {
                        // Replanning a moving target can snap the first grid point behind the hunter.
                        // Join the forward leg only if the whole shortcut is walkable on this floor.
                        for (int i = 1; i < Mathf.Min(5, path.Count); i++)
                        {
                            var next = path[i];
                            if (Mathf.Abs(next.y - Position.y) > .18f || !FloorRoutes.CanMove(Position, next) ||
                                (allowed != null && !allowed(Position, next))) break;
                            waypoint = i;
                        }
                    }
                    roundLeg = goal.Kind == Intent.Patrol && onRound;
                    activeIntent = goal.Kind;
                    activeSlot = goal.Slot;
                    investigationSound = goal.Sound;
                    Pursuing = goal.Kind == Intent.Pursue;
                    if (goal.Kind == Intent.Patrol)
                    {
                        patrolSpeed = Mathf.Lerp(1.46f, 2.30f, Mathf.InverseLerp(16, 64, Vector3.Distance(Position, destination)));
                    }
                    if (goal.Kind == Intent.Investigate && !sees && listenCooldown <= 0 &&
                        (destination - recentListenTarget).sqrMagnitude > 4)
                        BeginListening(destination);
                    return true;
                }
                return false;
            }
        }

        void RememberBlocked(Intent kind, Vector3 point, int slot)
        {
            if (kind == Intent.Patrol)
            {
                patrolRetryUntil[slot] = elapsed + 6f;
                return;
            }

            unreachable = point;
            retry = elapsed + 2.4f;
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
