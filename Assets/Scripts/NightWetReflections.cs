using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    [RequireComponent(typeof(Camera))]
    internal sealed class NightWetReflections : MonoBehaviour
    {
        readonly List<Bounds> pools = new();
        readonly Plane[] frustum = new Plane[6];
        Camera eye, mirror;
        RenderTexture reflection;
        Matrix4x4 capturedProjection;
        float capturedPlane = float.NegativeInfinity, capturedAt;
        Vector3 capturedFrom;
        Quaternion capturedRotation = Quaternion.identity;
        static readonly int Water = Shader.PropertyToID("_BathWater");
        static readonly int PlaneHeight = Shader.PropertyToID("_BathWaterPlane");
        static readonly int Project = Shader.PropertyToID("_BathWaterProjection");

        internal void AddPool(Bounds bounds) => pools.Add(bounds);

        void Awake()
        {
            eye = GetComponent<Camera>();
            Shader.SetGlobalFloat(PlaneHeight, -1000);
        }

        void OnPreCull()
        {
            Shader.SetGlobalFloat(PlaneHeight, -1000);
            if (QualitySettings.GetQualityLevel() < 3 || !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) return;
            GeometryUtility.CalculateFrustumPlanes(eye, frustum);
            float nearest = 25 * 25;
            float plane = -1000;
            Vector3 position = eye.transform.position;
            foreach (var pool in pools)
            {
                float above = position.y - pool.center.y;
                if (above <= .06f || above > 3.55f || !GeometryUtility.TestPlanesAABB(frustum, pool)) continue;
                float distance = pool.SqrDistance(position);
                if (distance >= nearest) continue;
                nearest = distance;
                plane = pool.center.y;
            }
            if (plane < -999) return;
            if (!mirror)
            {
                mirror = new GameObject("Wet floor reflection", typeof(Camera)).GetComponent<Camera>();
                mirror.transform.SetParent(transform, false);
                mirror.enabled = false;
            }
            // One shared reflection buffer for all coplanar wet patches in view.
            int width = Mathf.Clamp(eye.pixelWidth / 2, 64, 768);
            int height = Mathf.Max(32, Mathf.RoundToInt(width / eye.aspect));
            if (!reflection || reflection.width != width || reflection.height != height)
            {
                ReleaseTarget();
                reflection = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
                { name = "Reflected wet floor", useMipMap = true, autoGenerateMips = true,
                  filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
                reflection.Create();
            }
            float sinceCapture = Time.unscaledTime - capturedAt;
            float shift = (position - capturedFrom).sqrMagnitude;
            float turn = Quaternion.Angle(eye.transform.rotation, capturedRotation);
            // Retain the capture's projection when reusing it. Small camera movements then reproject
            // the puddle correctly; a changed floor, major turn, or teleport redraws immediately.
            bool steady = shift < .0025f && turn < 1;
            if (Mathf.Abs(plane - capturedPlane) < .025f && shift < .36f && turn < 10 &&
                sinceCapture < (steady ? 1f/15 : 1f/30))
            {
                BindSurface();
                return;
            }
            mirror.CopyFrom(eye);
            mirror.enabled = false;
            mirror.targetTexture = reflection;
            mirror.allowMSAA = false;
            mirror.depthTextureMode = DepthTextureMode.None;
            mirror.cullingMask &= ~((1 << NightPlayer.PlayerLayer) | (1 << NightPlayer.ViewModelLayer)
                | (1 << NightAtmosphere.EffectsLayer) | (1 << 5));
            mirror.useOcclusionCulling = false;
            mirror.farClipPlane = Mathf.Min(eye.farClipPlane, 50);
            mirror.ResetProjectionMatrix();
            var flip = Matrix4x4.Scale(new Vector3(1, -1, 1));
            flip.m13 = 2 * plane;
            var view = eye.worldToCameraMatrix * flip;
            mirror.transform.SetPositionAndRotation(flip.MultiplyPoint(position),
                Quaternion.LookRotation(flip.MultiplyVector(eye.transform.forward), -flip.MultiplyVector(eye.transform.up)));
            mirror.worldToCameraMatrix = view;
            var normal = view.MultiplyVector(Vector3.up);
            var point = view.MultiplyPoint(new Vector3(0, plane + .025f, 0));
            mirror.projectionMatrix = mirror.CalculateObliqueMatrix(new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal)));
            bool winding = GL.invertCulling;
            try
            {
                GL.invertCulling = !winding;
                mirror.Render();
            }
            finally { GL.invertCulling = winding; }
            capturedFrom = position;
            capturedRotation = eye.transform.rotation;
            capturedAt = Time.unscaledTime;
            capturedPlane = plane;
            capturedProjection = GL.GetGPUProjectionMatrix(mirror.projectionMatrix, true) * view;
            BindSurface();
        }

        void BindSurface()
        {
            Shader.SetGlobalTexture(Water, reflection);
            Shader.SetGlobalMatrix(Project, capturedProjection);
            Shader.SetGlobalFloat(PlaneHeight, capturedPlane);
        }

        void OnPostRender() => Shader.SetGlobalFloat(PlaneHeight, -1000);

        void ReleaseTarget()
        {
            capturedPlane = float.NegativeInfinity;
            if (!reflection) return;
            if (mirror) mirror.targetTexture = null;
            reflection.Release();
            Destroy(reflection);
        }

        void OnDestroy()
        {
            Shader.SetGlobalFloat(PlaneHeight, -1000);
            ReleaseTarget();
            if (mirror) Destroy(mirror.gameObject);
        }
    }
}
