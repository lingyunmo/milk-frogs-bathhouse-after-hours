using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        NightBrineBlast blast;
        ResourceRequest[] brinePreload;
        Coroutine brineWarmup;

        void EnsureBlastNode()
        {
            if (blast) return;
            var node = new GameObject("东堤核事故");
            node.transform.SetParent(root, false);
            node.SetActive(false);
            blast = node.AddComponent<NightBrineBlast>();
            blast.BeginPreparation(this,game,root);
        }

        internal void PreloadBrineMedia()
        {
            EnsureBlastNode();
            if (blast.Ready || brineWarmup != null) return;
            brinePreload ??= new[] {
                Resources.LoadAsync<Texture3D>("BrineCloudField"),
                Resources.LoadAsync<Texture3D>("BrineDustNoise"),
                Resources.LoadAsync<AudioClip>("Audio/brine-pressure"),
                Resources.LoadAsync<AudioClip>("Audio/brine-masonry")
            };
            brineWarmup=game.StartCoroutine(WarmIncident());
        }

        IEnumerator WarmIncident()
        {
            yield return null;
            while(!blast.GeometryReady)
            {
                double until=Time.realtimeSinceStartupAsDouble+.0015;
                do { blast.PrepareNextMember(); }
                while(!blast.GeometryReady && Time.realtimeSinceStartupAsDouble<until);
                yield return null;
            }
            foreach(var request in brinePreload) if(!request.isDone) yield return request;
            blast.PrepareWeather();
            brineWarmup=null;
        }

        internal void SetBrineBlast(bool devastated, bool playImpact = false)
        {
            if(brineWarmup!=null)
            {
                game.StopCoroutine(brineWarmup);
                brineWarmup=null;
            }
            if (devastated)
            {
                EnsureBlastNode();
                // A restored crisis can enter directly without traversing the arming sequence.
                while(!blast.GeometryReady) blast.PrepareNextMember();
                blast.PrepareWeather();
            }
            if (blast && blast.Ready) blast.Show(devastated, playImpact);
        }

        internal void FlareBrineBlast()
        {
            SetBrineBlast(true);
            blast.Flare();
        }
    }

    internal sealed partial class NightBrineBlast : MonoBehaviour
    {
        internal static readonly Vector3 Source = new(233, 0, -40);
        const float Settled = 24;
        static readonly int Age = Shader.PropertyToID("_Age"), Final = Shader.PropertyToID("_Final");
        NightGame game;
        NightWorld world;
        Material cloud, dust;
        Texture3D noise;
        ParticleSystem particles;
        Light flash, cloudLight, quayBounce;
        Color normalSky, normalHorizon, normalGround, normalFog;
        float normalFogDensity, appliedScatter = -1;
        float age, ashClock, dustClock, finale = -1;
        bool visible;
        uint random = 19870717;

        IEnumerator pendingArchitecture;
        internal bool GeometryReady => pendingArchitecture == null;
        internal bool Ready { get; private set; }
        internal void BeginPreparation(NightWorld owner,NightGame session,Transform district)
        {
            world=owner;
            game=session;
            pendingArchitecture=CaptureArchitecture(district);
        }

        internal void PrepareNextMember()
        {
            if(pendingArchitecture != null && !pendingArchitecture.MoveNext()) pendingArchitecture=null;
        }

        internal void PrepareWeather()
        {
            if(Ready) return;
            noise = Resources.Load<Texture3D>("BrineDustNoise");
            cloud = new Material(Resources.Load<Shader>("BrineFireCloud"));
            dust = new Material(Resources.Load<Shader>("BrineFallout"));
            BuildFront();
            dust.SetTexture("_Noise", noise);

            var volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volume.name = "翻卷的热云";
            volume.transform.SetParent(transform, false);
            volume.transform.position = Source + new Vector3(-3.8f, 10.65f, -.85f);
            volume.transform.localScale = new Vector3(48, 21.8f, 43);
            var collider = volume.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var skin = volume.GetComponent<Renderer>();
            skin.sharedMaterial = cloud;
            skin.shadowCastingMode = ShadowCastingMode.Off;
            skin.receiveShadows = false;
            skin.allowOcclusionWhenDynamic = false;

            flash = new GameObject("冲击反光").AddComponent<Light>();
            flash.transform.SetParent(transform, false);
            flash.type = LightType.Point;
            flash.shadows = LightShadows.None;
            flash.renderMode = LightRenderMode.ForcePixel;
            cloudLight = new GameObject("热云斜照").AddComponent<Light>();
            cloudLight.transform.SetParent(transform, false);
            cloudLight.type = LightType.Directional;
            cloudLight.shadows = LightShadows.Soft;
            cloudLight.shadowStrength = .77f;
            cloudLight.shadowBias = .06f;
            cloudLight.cullingMask = ~(1 << NightPlayer.ViewModelLayer);
            cloudLight.renderMode = LightRenderMode.ForcePixel;
            quayBounce = new GameObject("隔离闸火光反照").AddComponent<Light>();
            quayBounce.transform.SetParent(transform, false);
            quayBounce.transform.position = new Vector3(260,6,-22);
            quayBounce.range = 58;
            quayBounce.color = new Color(1,.63f,.40f);
            quayBounce.cullingMask = cloudLight.cullingMask;
            quayBounce.renderMode = LightRenderMode.ForcePixel;
            quayBounce.shadows = LightShadows.None;
            particles = new GameObject("贴地尘墙与降灰").AddComponent<ParticleSystem>();
            particles.transform.SetParent(transform, false);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 560;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var fade = particles.colorOverLifetime;
            var envelope = new Gradient();
            envelope.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f),
                        new GradientAlphaKey(.8f, .57f), new GradientAlphaKey(0, 1) });
            fade.enabled = true;
            fade.color = envelope;
            var growth = particles.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .58f, 1, 1.58f));
            var psr = particles.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = dust;
            psr.shadowCastingMode = ShadowCastingMode.Off;
            psr.receiveShadows = false;
            psr.maxParticleSize = .52f;
            Ready=true;
        }

        internal void Show(bool active, bool impact)
        {
            if (!active)
            {
                if (visible)
                {
                    RestoreArchitecture();
                    RestoreAtmosphere();
                }
                ResetFront();
                visible = false;
                gameObject.SetActive(false);
                return;
            }
            if (visible && !impact) return;
            if (!visible)
            {
                normalSky = RenderSettings.ambientSkyColor;
                normalHorizon = RenderSettings.ambientEquatorColor;
                normalGround = RenderSettings.ambientGroundColor;
                normalFog = RenderSettings.fogColor;
                normalFogDensity = RenderSettings.fogDensity;
            }
            visible = true;
            appliedScatter = -1;
            gameObject.SetActive(true);
            ResetFront();
            age = impact ? .015f : Settled;
            finale = -1;
            ashClock = dustClock = 0;
            particles.Clear();
            var main = particles.main;
            main.useUnscaledTime = false;
            particles.Play();
            PoseArchitecture(age);
            if (impact) StartFront();
            DrawWeather();
        }

        internal void Flare()
        {
            finale = 0;
            var main = particles.main;
            main.useUnscaledTime = true;
            StartFront();
            DrawWeather();
        }

        void Update()
        {
            bool ending = game.ScreenState is NightScreen.Escaped or NightScreen.Caught;
            if (game.ScreenState != NightScreen.Play && !ending && game.ScreenState != NightScreen.Observer)
            {
                ReleasePressure();
                return;
            }
            float dt = ending ? Time.unscaledDeltaTime : Time.deltaTime;
            float last = age;
            age += dt;
            if (finale >= 0) finale += dt;
            TickFront(dt);
            TickReception(dt);
            if (last < 15)
            {
                PoseArchitecture(age);
                TickCollapseDust(last, age);
                foreach (var plate in plates)
                    if (last < plate.Delay && age >= plate.Delay) Vent(plate.Origin);
            }
            if (ashClock <= 0)
            {
                Ash();
                ashClock = .13f;
            }
            ashClock -= dt;
            if ((dustClock -= dt) <= 0 && age > 2)
            {
                dustClock = 1.7f;
                var eye = game.Player.Eyes.position;
                foreach (var plate in plates)
                    if ((plate.Origin-eye).sqrMagnitude < 56*56)
                        Emit(plate.Origin+Vector3.up*.5f, new Vector3(.43f,.62f,-.15f),
                            new Color(.31f,.28f,.26f,.35f), 1.9f+Noise01()*1.5f, 4.7f);
            }
            DrawWeather();
        }

        void DrawWeather()
        {
            float ignition = Mathf.Clamp01(age / .19f) * Mathf.Exp(-age * 1.22f);
            float afterflash = finale >= 0 ? Mathf.Clamp01(finale / .37f) * Mathf.Exp(-finale * .48f) : 0;
            cloud.SetFloat(Age, age);
            cloud.SetFloat(Final, afterflash * 1.85f);
            var eyes = game.Player.Eyes;
            flash.transform.position = eyes.position + eyes.forward * 1.2f + Vector3.up * .6f;
            flash.color = Color.Lerp(new Color(1, .37f, .13f), new Color(1, .86f, .60f), Mathf.Max(ignition, afterflash));
            flash.range = 29;
            flash.intensity = 5.4f * ignition + 7f * afterflash;
            flash.enabled = flash.intensity > .018f;
            // A long raking key reaches roofs beyond the playable street. A fixed quay bounce
            // picks out the near side of the shattered panels and wet ground against that rim.
            Vector3 away = Vector3.ProjectOnPlane(eyes.position-Source,Vector3.up).normalized;
            if (away.sqrMagnitude < .1f) away = Vector3.right;
            cloudLight.transform.rotation = Quaternion.LookRotation(away + Vector3.down*.56f);
            float smoulder = .86f + .09f*Mathf.Sin(age*1.37f) + .05f*Mathf.Sin(age*2.14f+.6f);
            cloudLight.color = Color.Lerp(new Color(1,.58f,.34f), new Color(1,.79f,.51f), ignition+afterflash);
            cloudLight.intensity = smoulder*.49f + ignition*.82f + afterflash*.89f;
            quayBounce.intensity = 1.06f*smoulder + ignition*3.2f + afterflash*2.8f;
            float scattered = .40f + .24f*afterflash + .13f*ignition;
            if (Mathf.Abs(appliedScatter-scattered) < .015f) return;
            appliedScatter = scattered;
            RenderSettings.ambientSkyColor = Color.Lerp(normalSky, new Color(.32f,.27f,.255f), scattered);
            RenderSettings.ambientEquatorColor = Color.Lerp(normalHorizon, new Color(.23f,.18f,.168f), scattered);
            RenderSettings.ambientGroundColor = Color.Lerp(normalGround, new Color(.16f,.135f,.123f), scattered);
            RenderSettings.fogColor = Color.Lerp(normalFog,new Color(.245f,.174f,.144f),scattered);
            RenderSettings.fogDensity = normalFogDensity*.84f;
        }

        void RestoreAtmosphere()
        {
            RenderSettings.ambientSkyColor = normalSky;
            RenderSettings.ambientEquatorColor = normalHorizon;
            RenderSettings.ambientGroundColor = normalGround;
            RenderSettings.fogColor = normalFog;
            RenderSettings.fogDensity = normalFogDensity;
        }

        float Noise01()
        {
            random = unchecked(1664525u * random + 1013904223u);
            return (random >> 8) * (1f / 16777216f);
        }

        void Emit(Vector3 at, Vector3 velocity, Color tint, float size, float life)
        {
            var puff = new ParticleSystem.EmitParams { position = at, velocity = velocity, startColor = tint,
                startSize = size, startLifetime = life, rotation = Noise01() * 360 };
            particles.Emit(puff, 1);
        }

        void Vent(Vector3 at)
        {
            for (int i = 0; i < 8; i++)
                Emit(at + new Vector3((Noise01() - .5f) * 5, 0, (Noise01() - .5f) * 2),
                    new Vector3((Noise01() - .5f) * 2, .6f + Noise01() * 1.9f, -1 - Noise01()),
                    new Color(.44f, .39f, .34f, .56f), 1.4f + Noise01() * 1.7f, 2.7f + Noise01() * 2);
        }

        void Ash()
        {
            var eyes = game.Player.Eyes;
            bool indoors = world.IsInsideBuilding(game.Player.Feet);
            Vector3 centre = eyes.position + eyes.forward * (indoors ? 1.6f : 3.5f);
            float radius = indoors ? 1 : 4;
            for (int i = 0; i < (indoors ? 1 : 5); i++)
            {
                Vector3 at = centre + new Vector3((Noise01()-.5f)*radius*2, Noise01()*radius, (Noise01()-.5f)*radius*2);
                Emit(at, new Vector3(.18f+Noise01()*.3f, -.24f-Noise01()*.38f, .11f),
                    new Color(.76f,.74f,.68f, indoors ? .36f : .64f), .013f+Noise01()*.027f, 1.6f+Noise01()*2);
            }
        }

        void OnDestroy()
        {
            if (visible) RestoreAtmosphere();
            RestoreArchitecture();
            DisposeFront();
            DisposeStructure();
            foreach(var plate in plates) Destroy(plate.Mesh);
            foreach (var finish in finishes.Values) if (finish) Destroy(finish);
            if (cloud) Destroy(cloud);
            if (dust) Destroy(dust);
        }
    }
}
