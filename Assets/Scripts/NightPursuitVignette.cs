
using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed class NightPursuitVignette : VisualElement
    {
        const int Segments = 48;
        static readonly float[] Radius = { .40f, .58f, .79f };
        static readonly Color32[] Tint =
        {
            new(25, 9, 7, 0), new(61, 20, 13, 63), new(24, 7, 5, 245)
        };
        float intensity;

        internal NightPursuitVignette()
        {
            pickingMode = PickingMode.Ignore;
            style.opacity = 0;
            generateVisualContent += DrawEdge;
        }

        internal void Tick(bool pursuing)
        {
            intensity = Mathf.MoveTowards(intensity, pursuing ? 1 : 0, Time.unscaledDeltaTime * (pursuing ? 2.6f : 1.4f));
            float breath = .76f + .17f * Mathf.Sin(Time.unscaledTime * 5);
            style.opacity = intensity * breath;
        }

        void DrawEdge(MeshGenerationContext context)
        {
            if (contentRect.width < 1 || contentRect.height < 1) return;
            var mesh = context.Allocate(Segments * Radius.Length, Segments * (Radius.Length - 1) * 6);
            Vector2 extent = contentRect.size;
            Vector2 center = extent * .5f;
            for (int ring = 0; ring < Radius.Length; ++ring)
            for (int step = 0; step < Segments; ++step)
            {
                float phase = step * (2 * Mathf.PI / Segments);
                Vector2 point = center + Vector2.Scale(extent,
                    new Vector2(Mathf.Cos(phase), Mathf.Sin(phase))) * Radius[ring];
                mesh.SetNextVertex(new Vertex { position = new Vector3(point.x, point.y, Vertex.nearZ), tint = Tint[ring] });
            }

            for (int ring = 0; ring < Radius.Length - 1; ++ring)
            for (int step = 0; step < Segments; ++step)
            {
                ushort a = (ushort)(ring * Segments + step);
                ushort b = (ushort)(ring * Segments + (step + 1) % Segments);
                ushort c = (ushort)(a + Segments);
                ushort d = (ushort)(b + Segments);
                mesh.SetNextIndex(a);
                mesh.SetNextIndex(c);
                mesh.SetNextIndex(d);
                mesh.SetNextIndex(a);
                mesh.SetNextIndex(d);
                mesh.SetNextIndex(b);
            }
        }
    }
}
