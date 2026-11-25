
using UnityEngine;

namespace AfterHours
{
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    internal sealed class NightCameraGrade : MonoBehaviour
    {
        static readonly int GlowTexture = Shader.PropertyToID("_PracticalGlow");
        static readonly int BlurDirection = Shader.PropertyToID("_BlurDirection");
        Material grade;
        bool glowEnabled = true;

        internal void SetGlow(bool enabled) => glowEnabled = enabled;

        void Awake()
        {
            var shader = Resources.Load<Shader>("NightCameraGrade");
            if (shader && shader.isSupported)
                grade = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!grade)
            {
                Graphics.Blit(source, destination);
                return;
            }

            if (!glowEnabled || QualitySettings.GetQualityLevel() < 3 || source.height < 480 ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                grade.SetTexture(GlowTexture, Texture2D.blackTexture);
                Graphics.Blit(source, destination, grade, 0);
                return;
            }

            int width = Mathf.Max(1, source.width / 4);
            int height = Mathf.Max(1, source.height / 4);
            var glow = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            var scratch = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            try
            {
                glow.filterMode = scratch.filterMode = FilterMode.Bilinear;
                Graphics.Blit(source, glow, grade, 1);
                grade.SetVector(BlurDirection, new Vector4(1f / width, 0, 0, 0));
                Graphics.Blit(glow, scratch, grade, 2);
                grade.SetVector(BlurDirection, new Vector4(0, 1f / height, 0, 0));
                Graphics.Blit(scratch, glow, grade, 2);
                grade.SetTexture(GlowTexture, glow);
                Graphics.Blit(source, destination, grade, 0);
            }
            finally
            {
                grade.SetTexture(GlowTexture, null);
                RenderTexture.ReleaseTemporary(scratch);
                RenderTexture.ReleaseTemporary(glow);
            }
        }

        void OnDestroy()
        {
            if (grade) Destroy(grade);
        }
    }
}
