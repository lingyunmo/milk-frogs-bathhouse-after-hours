using UnityEngine;
using UnityEngine.Rendering;

namespace AfterHours
{
    internal sealed class HunterListenRipple : MonoBehaviour
    {
        const float Duration = .76f;
        static readonly int Progress = Shader.PropertyToID("_Progress");
        Mesh mesh;
        Material material;
        MeshRenderer surface;
        float age;

        internal static HunterListenRipple Create(Transform owner)
        {
            var shader = Resources.Load<Shader>("HunterListenRipple");
            if (!shader || !shader.isSupported) return null;

            var root = new GameObject("Listening ripple");
            root.transform.SetParent(owner, false);
            var effect = root.AddComponent<HunterListenRipple>();
            effect.material = new Material(shader) { name = "Listening ripple" };
            effect.mesh = new Mesh
            {
                name = "Listening ripple plane",
                vertices = new[] { new Vector3(-1,0,-1), new Vector3(-1,0,1), new Vector3(1,0,1), new Vector3(1,0,-1) },
                uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
                triangles = new[] { 0,1,2,0,2,3 }
            };
            effect.mesh.UploadMeshData(true);
            root.AddComponent<MeshFilter>().sharedMesh = effect.mesh;
            effect.surface = root.AddComponent<MeshRenderer>();
            effect.surface.sharedMaterial = effect.material;
            effect.surface.shadowCastingMode = ShadowCastingMode.Off;
            effect.surface.receiveShadows = false;
            effect.surface.enabled = false;
            return effect;
        }

        internal void PlayAt(RaycastHit ground)
        {
            age = 0;
            transform.SetPositionAndRotation(ground.point + ground.normal * .025f,
                Quaternion.FromToRotation(Vector3.up, ground.normal));
            transform.localScale = Vector3.one * 2.15f;
            material.SetFloat(Progress, 0);
            surface.enabled = true;
        }

        internal void Tick(float dt)
        {
            if (!surface.enabled) return;
            age += dt;
            if (age >= Duration) Stop();
            else material.SetFloat(Progress, age / Duration);
        }

        internal void Stop() => surface.enabled = false;

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
        }
    }
}
