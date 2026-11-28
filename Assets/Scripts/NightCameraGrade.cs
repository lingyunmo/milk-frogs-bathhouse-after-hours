using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    // Two stages share one HDR frame. The world camera adds ambient occlusion while its depth is still bound.
    // The first-person torch renders on top, then a presenter camera lights the whole frame through bloom,
    // AgX tone mapping, the night grade, lens character and the blurred backdrop behind menus.
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    internal sealed class NightCameraGrade : MonoBehaviour
    {
        const int OcclusionPass = 0, OcclusionBlurPass = 1, OcclusionApplyPass = 2, PrefilterPass = 3,
            DownsamplePass = 4, UpsamplePass = 5, CompositePass = 6, MenuPass = 7;
        const int BloomLevels = 6, MenuLevels = 4;

        static readonly int MainTexture = Shader.PropertyToID("_MainTex");
        static readonly int BloomTexture = Shader.PropertyToID("_BloomTex");
        static readonly int OcclusionTexture = Shader.PropertyToID("_OcclusionTex");
        static readonly int LensDirtTexture = Shader.PropertyToID("_LensDirt");
        static readonly int BlurTexture = Shader.PropertyToID("_BlurTex");
        static readonly int Direction = Shader.PropertyToID("_Direction");
        static readonly int BloomSettings = Shader.PropertyToID("_Bloom");
        static readonly int OcclusionSettings = Shader.PropertyToID("_Occlusion");
        static readonly int GradeSettings = Shader.PropertyToID("_Grade");
        static readonly int LensSettings = Shader.PropertyToID("_Lens");
        static readonly int MenuSettings = Shader.PropertyToID("_Menu");
        static readonly int Scatter = Shader.PropertyToID("_Scatter");
        static readonly int ProjectionScale = Shader.PropertyToID("_Projection");
        static readonly int DepthTexel = Shader.PropertyToID("_DepthTexel");

        Camera world, presenter;
        readonly List<Camera> overlays = new();
        RenderTexture frame;
        Material grade;
        Texture2D lensDirt;
        bool glow = true, occlusion = true, grain = true;
        float exposure = 1, menu, menuTarget, fear, fearTarget, shock;

        internal bool Ready => grade;
        internal float MenuBlur => menu;

        internal void Configure(NightPreferences preferences)
        {
            glow = preferences.PracticalGlow;
            occlusion = preferences.AmbientOcclusion;
            grain = preferences.FilmGrain;
            exposure = preferences.Brightness;
        }

        // 1 while a menu covers the world, 0 in play.
        internal void SetMenu(bool covered) => menuTarget = covered ? 1 : 0;
        internal void SetFear(float amount) => fearTarget = Mathf.Clamp01(amount);
        internal void Shock() => shock = 1;

        // Applied to the composed 3D frame. Mouse aim, UI and subtitle positions remain stable.
        static readonly int PressureUV = Shader.PropertyToID("_PressureUV");
        internal void SetPressureOffset(Vector2 offset)
        {
            if (grade) grade.SetVector(PressureUV, new Vector4(offset.x, offset.y, 0, 0));
        }

        internal void Overlay(Camera camera)
        {
            if (!camera || overlays.Contains(camera)) return;
            overlays.Add(camera);
            camera.allowHDR = true;
            if (frame) camera.targetTexture = frame;
        }

        void Awake()
        {
            world = GetComponent<Camera>();
            var shader = Resources.Load<Shader>("NightCameraGrade");
            if (!shader || !shader.isSupported || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
                return;
            grade = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            lensDirt = LensDirt();
            world.allowHDR = true;
            world.depthTextureMode |= DepthTextureMode.Depth;

            var stage = new GameObject("Cinematic presenter");
            stage.transform.SetParent(transform, false);
            presenter = stage.AddComponent<Camera>();
            presenter.depth = 100;
            presenter.clearFlags = CameraClearFlags.Nothing;
            presenter.cullingMask = 0;
            presenter.allowHDR = false;
            presenter.allowMSAA = false;
            presenter.useOcclusionCulling = false;
            presenter.nearClipPlane = .1f;
            presenter.farClipPlane = .2f;
            stage.AddComponent<Presenter>().Owner = this;
        }

        void LateUpdate()
        {
            if (!grade) return;
            float dt = Time.unscaledDeltaTime;
            menu = Mathf.MoveTowards(menu, menuTarget, dt * (menuTarget > menu ? 3.4f : 4.5f));
            fear = Mathf.MoveTowards(fear, fearTarget, dt * (fearTarget > fear ? 1.6f : .55f));
            shock = Mathf.MoveTowards(shock, 0, dt * .9f);

            presenter.enabled = world.enabled;
            int width = Mathf.Max(1, world.enabled ? Screen.width : 1);
            int height = Mathf.Max(1, world.enabled ? Screen.height : 1);
            int samples = Mathf.Clamp(QualitySettings.antiAliasing, 1, 8);
            if (!frame || frame.width != width || frame.height != height || frame.antiAliasing != samples)
            {
                world.targetTexture = null;
                foreach (var overlay in overlays) if (overlay) overlay.targetTexture = null;
                if (frame) frame.Release();
                frame = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                {
                    name = "Night frame",
                    antiAliasing = samples,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                frame.Create();
            }
            world.targetTexture = frame;
            foreach (var overlay in overlays) if (overlay) overlay.targetTexture = frame;
        }

        // Stage one: occlusion belongs to the world only, before the torch model is drawn on top.
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!grade || !occlusion || QualitySettings.GetQualityLevel() < 3 || source.height < 360)
            {
                Graphics.Blit(source, destination);
                return;
            }
            int width = Mathf.Max(1, source.width / 2), height = Mathf.Max(1, source.height / 2);
            var ao = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            var scratch = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            try
            {
                ao.filterMode = scratch.filterMode = FilterMode.Bilinear;
                var projection = world.projectionMatrix;
                grade.SetVector(ProjectionScale, new Vector4(projection.m00, projection.m11));
                grade.SetVector(DepthTexel, new Vector4(1f / source.width, 1f / source.height));
                grade.SetVector(OcclusionSettings, new Vector4(.55f, .62f, .78f, 30));
                Graphics.Blit(Texture2D.whiteTexture, ao, grade, OcclusionPass);
                grade.SetVector(Direction, new Vector4(1f / width, 0));
                Graphics.Blit(ao, scratch, grade, OcclusionBlurPass);
                grade.SetVector(Direction, new Vector4(0, 1f / height));
                Graphics.Blit(scratch, ao, grade, OcclusionBlurPass);
                grade.SetTexture(OcclusionTexture, ao);
                Graphics.Blit(source, destination, grade, OcclusionApplyPass);
            }
            finally
            {
                grade.SetTexture(OcclusionTexture, null);
                RenderTexture.ReleaseTemporary(scratch);
                RenderTexture.ReleaseTemporary(ao);
            }
        }

        // Stage two: everything that should also touch the torch in the player's hand.
        internal string SurveyExposure()
        {
            if (!world) return "";
            var previous = RenderTexture.active;
            int width = 480, height = Mathf.Clamp(Mathf.RoundToInt(width / world.aspect), 160, 640);
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var photo = new Texture2D(width, height, TextureFormat.RGB24, false);
            var originalTarget = world.targetTexture;
            try
            {
                bool graded = Ready && frame;
                if (!graded) world.targetTexture = target;
                world.Render();
                if (graded) Present(target);
                RenderTexture.active = target;
                photo.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                photo.Apply();
                return System.Convert.ToBase64String(photo.EncodeToJPG(85));
            }
            finally
            {
                world.targetTexture = originalTarget;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Destroy(photo);
            }
        }

        void Present(RenderTexture destination)
        {
            if (!grade || !frame)
            {
                return;
            }
            bool high = QualitySettings.GetQualityLevel() >= 3;
            grade.SetVector(GradeSettings, new Vector4(1.85f * exposure, 1.03f, .94f, grain && high ? .042f : 0));
            grade.SetVector(LensSettings, new Vector4(.24f + .20f * fear, .0016f + .0058f * fear + .012f * shock, fear, shock));
            grade.SetTexture(LensDirtTexture, lensDirt);
            var bloom = Bloom(glow && high);
            grade.SetTexture(BloomTexture, bloom ? bloom : Texture2D.blackTexture);
            try
            {
                if (menu <= .001f)
                {
                    Graphics.Blit(frame, destination, grade, CompositePass);
                    return;
                }
                var graded = RenderTexture.GetTemporary(frame.width, frame.height, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
                var blur = Blur(graded);
                try
                {
                    Graphics.Blit(frame, graded, grade, CompositePass);
                    BlurInto(graded, blur);
                    grade.SetTexture(BlurTexture, blur[0]);
                    grade.SetVector(MenuSettings, new Vector4(Mathf.SmoothStep(0, 1, menu), menu));
                    Graphics.Blit(graded, destination, grade, MenuPass);
                }
                finally
                {
                    grade.SetTexture(BlurTexture, null);
                    foreach (var level in blur) RenderTexture.ReleaseTemporary(level);
                    RenderTexture.ReleaseTemporary(graded);
                }
            }
            finally
            {
                grade.SetTexture(BloomTexture, null);
                if (bloom) RenderTexture.ReleaseTemporary(bloom);
            }
        }

        RenderTexture Bloom(bool enabled)
        {
            if (!enabled) return null;
            var levels = new RenderTexture[BloomLevels];
            int width = frame.width / 2, height = frame.height / 2;
            grade.SetVector(BloomSettings, new Vector4(1.05f, .55f, .062f, .30f));
            grade.SetFloat(Scatter, .72f);
            int count = 0;
            for (; count < BloomLevels && width >= 8 && height >= 8; ++count, width /= 2, height /= 2)
            {
                levels[count] = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                levels[count].filterMode = FilterMode.Bilinear;
                Graphics.Blit(count == 0 ? frame : levels[count - 1], levels[count], grade,
                    count == 0 ? PrefilterPass : DownsamplePass);
            }
            for (int i = count - 2; i >= 0; --i)
            {
                var combined = RenderTexture.GetTemporary(levels[i].width, levels[i].height, 0, RenderTextureFormat.ARGBHalf,
                    RenderTextureReadWrite.Linear);
                combined.filterMode = FilterMode.Bilinear;
                grade.SetTexture(BloomTexture, levels[i]);
                Graphics.Blit(levels[i + 1], combined, grade, UpsamplePass);
                RenderTexture.ReleaseTemporary(levels[i + 1]);
                RenderTexture.ReleaseTemporary(levels[i]);
                levels[i] = combined;
                levels[i + 1] = null;
            }
            return levels[0];
        }

        static RenderTexture[] Blur(RenderTexture source)
        {
            var levels = new RenderTexture[MenuLevels];
            int width = source.width, height = source.height;
            for (int i = 0; i < MenuLevels; ++i)
            {
                width = Mathf.Max(1, width / 2);
                height = Mathf.Max(1, height / 2);
                levels[i] = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                levels[i].filterMode = FilterMode.Bilinear;
            }
            return levels;
        }

        void BlurInto(RenderTexture source, RenderTexture[] levels)
        {
            for (int i = 0; i < levels.Length; ++i)
                Graphics.Blit(i == 0 ? source : levels[i - 1], levels[i], grade, DownsamplePass);
            grade.SetTexture(BloomTexture, Texture2D.blackTexture);
            grade.SetFloat(Scatter, 1);
            for (int i = levels.Length - 2; i >= 0; --i)
                Graphics.Blit(levels[i + 1], levels[i], grade, UpsamplePass);
            grade.SetFloat(Scatter, .72f);
        }

        // Faint grease, a few dust specks and one thumb smear. Only lamps bright enough to bloom reveal them.
        static Texture2D LensDirt()
        {
            const int width = 384, height = 216;
            var random = new System.Random(4173);
            var level = new float[width * height];
            void Blob(float cx, float cy, float radius, float strength, float stretch)
            {
                int x0 = Mathf.Max(0, (int)(cx - radius * stretch - 2)), x1 = Mathf.Min(width - 1, (int)(cx + radius * stretch + 2));
                int y0 = Mathf.Max(0, (int)(cy - radius - 2)), y1 = Mathf.Min(height - 1, (int)(cy + radius + 2));
                for (int y = y0; y <= y1; ++y)
                for (int x = x0; x <= x1; ++x)
                {
                    float dx = (x - cx) / stretch, dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    if (d >= 1) continue;
                    float rim = Mathf.SmoothStep(.62f, .95f, d) * (1 - Mathf.SmoothStep(.95f, 1, d));
                    level[y * width + x] += strength * ((1 - d * d) * .55f + rim * .8f);
                }
            }
            for (int i = 0; i < 70; ++i)
            {
                float r = 1.5f + (float)(random.NextDouble() * random.NextDouble()) * 24;
                Blob((float)random.NextDouble() * width, (float)random.NextDouble() * height, r,
                    .05f + (float)random.NextDouble() * .22f, 1);
            }
            Blob(width * .74f, height * .30f, 34, .16f, 2.6f);
            Blob(width * .18f, height * .78f, 22, .11f, 1.9f);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; ++i)
            {
                byte v = (byte)(Mathf.Clamp01(.08f + level[i]) * 255);
                pixels[i] = new Color32(v, v, v, 255);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "Lens dirt",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        void OnDestroy()
        {
            if (world) world.targetTexture = null;
            foreach (var overlay in overlays) if (overlay) overlay.targetTexture = null;
            if (frame) frame.Release();
            if (grade) Destroy(grade);
            if (lensDirt) Destroy(lensDirt);
        }

        sealed class Presenter : MonoBehaviour
        {
            internal NightCameraGrade Owner;

            void OnRenderImage(RenderTexture source, RenderTexture destination)
            {
                if (Owner && Owner.Ready) Owner.Present(destination);
                else Graphics.Blit(source, destination);
            }
        }
    }
}
