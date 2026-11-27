using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    // A pin-point reticle that opens into a ring over usable objects. Holding the interaction key fills it.
    internal sealed class NightReticle : VisualElement
    {
        static readonly Color Ink = new(.93f, .90f, .82f, .80f);
        static readonly Color Edge = new(0, 0, 0, .42f);
        static readonly Color Brass = new(.89f, .74f, .49f, .95f);
        static readonly Color Track = new(.93f, .90f, .82f, .20f);
        float open, hold, shownOpen = -1, shownHold = -1;
        bool focused;

        internal NightReticle()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("reticle");
            generateVisualContent += Draw;
        }

        internal void Set(bool focus, float progress)
        {
            focused = focus;
            hold = focus ? Mathf.Clamp01(progress) : 0;
        }

        internal void Tick()
        {
            open = Mathf.MoveTowards(open, focused ? 1 : 0, Time.unscaledDeltaTime * 7.5f);
            if (Mathf.Abs(open - shownOpen) < .001f && Mathf.Abs(hold - shownHold) < .001f) return;
            shownOpen = open;
            shownHold = hold;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var paint = context.painter2D;
            Vector2 center = contentRect.center;
            float eased = open * open * (3 - 2 * open);

            // A dark halo keeps the dot legible against bright tile and lamp glare.
            paint.fillColor = Edge;
            paint.BeginPath();
            paint.Arc(center, 3.4f, 0, 360);
            paint.Fill();
            paint.fillColor = Color.Lerp(Ink, Brass, eased);
            paint.BeginPath();
            paint.Arc(center, Mathf.Lerp(1.9f, 1.6f, eased), 0, 360);
            paint.Fill();
            if (eased <= .01f) return;

            float radius = Mathf.Lerp(4, 13, eased);
            paint.lineWidth = 3.2f;
            paint.strokeColor = new Color(0, 0, 0, .30f * eased);
            paint.BeginPath();
            paint.Arc(center, radius, 0, 360);
            paint.Stroke();
            paint.lineWidth = 1.3f;
            paint.strokeColor = hold > 0 ? Track * new Color(1, 1, 1, eased) : Brass * new Color(1, 1, 1, .82f * eased);
            paint.BeginPath();
            paint.Arc(center, radius, 0, 360);
            paint.Stroke();
            if (hold <= 0) return;
            paint.lineWidth = 2.6f;
            paint.lineCap = LineCap.Round;
            paint.strokeColor = Brass;
            paint.BeginPath();
            paint.Arc(center, radius, -90, -90 + 360 * hold);
            paint.Stroke();
        }
    }
}
