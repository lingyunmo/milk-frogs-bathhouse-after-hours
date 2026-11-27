using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed class NightTitleSparks : VisualElement
    {
        internal const float BurstDuration = .215f;
        float age = -1;
        int burst;

        internal NightTitleSparks()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("title-lamp-sparks");
            generateVisualContent += Draw;
        }

        internal bool BurstVisible => age >= 0 && age < BurstDuration;

        internal void SetBurst(int index, float elapsed)
        {
            bool wasVisible = BurstVisible;
            age = elapsed;
            burst = index;
            if (BurstVisible || wasVisible) MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            if (!BurstVisible || contentRect.height <= 0) return;

            var paint = context.painter2D;
            paint.lineCap = LineCap.Round;
            float scale = contentRect.height;
            var bulb = new Vector2(contentRect.width * .5f, scale * .22f);

            for (int spark = 0; spark < 12; spark++)
            {
                float spread = Mathf.Repeat(spark * .618034f + burst * .282f, 1f);
                float speed = Mathf.Repeat(spark * .414214f + burst * .173f, 1f);
                float flight = age - (spark % 4) * .012f;
                float life = Mathf.Lerp(.11f, .18f, speed);
                if (flight <= .002f || flight >= life) continue;

                var velocity = new Vector2(Mathf.Lerp(-2.36f, 2.36f, spread), Mathf.Lerp(-.62f, 1.51f, speed));
                Vector2 tip = Position(bulb, velocity, flight, scale);
                Vector2 tail = Position(bulb, velocity, Mathf.Max(0, flight - .035f - speed * .025f), scale);
                float strength = Mathf.Pow(1 - flight / life, .56f);
                float thickness = Mathf.Max(2.1f, scale * .007f) * Mathf.Lerp(.65f, 1.15f, speed);

                paint.lineWidth = thickness * 3.2f;
                paint.strokeColor = new Color(1f, .41f, .12f, strength * .54f);
                Stroke(paint, tail, tip);
                paint.lineWidth = thickness;
                paint.strokeColor = new Color(1f, .89f, .66f, Mathf.Min(1, strength * 1.5f));
                Stroke(paint, tail, tip);
                paint.fillColor = new Color(1f, .96f, .85f, strength);
                paint.BeginPath();
                paint.Arc(tip, thickness * .63f, 0, 360);
                paint.Fill();
            }
        }

        static Vector2 Position(Vector2 bulb, Vector2 velocity, float time, float scale) =>
            bulb + new Vector2(velocity.x * time, (velocity.y + 3.1f * time) * time) * scale;

        static void Stroke(Painter2D paint, Vector2 from, Vector2 to)
        {
            paint.BeginPath();
            paint.MoveTo(from);
            paint.LineTo(to);
            paint.Stroke();
        }
    }
}
