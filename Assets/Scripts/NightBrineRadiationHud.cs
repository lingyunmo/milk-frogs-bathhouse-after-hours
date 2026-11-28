using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed partial class NightInterface
    {
        VisualElement radiationUnit;
        Label radiationReadout;
        Meter doseStrip;
        NightRadiationDial radiationDial;
        NightSensorNoise sensorNoise;

        void BuildBrineRadiationHud(VisualElement layer)
        {
            radiationUnit = Area(objectiveBlock, "radiation-unit");
            radiationUnit.name = "brine-dosimeter";
            radiationDial = new NightRadiationDial();
            radiationUnit.Add(radiationDial);
            var copy = Area(radiationUnit, "radiation-copy");
            radiationReadout = Type(copy, "", "radiation-reading");
            var row = Area(copy, "radiation-dose");
            Type(row, "沾染", "radiation-caption");
            doseStrip = new Meter(row, "radiation-strip", "radiation-fill", 6);
            sensorNoise = new NightSensorNoise();
            layer.Insert(1, sensorNoise);
            Visible(radiationUnit, false);
            Visible(sensorNoise, false);
        }

        void TickBrineRadiationHud(BrineRun run)
        {
            Visible(radiationUnit, run.Live);
            bool flecks = run.Live && game.BrineField > .46f && game.Preferences.FilmGrain;
            Visible(sensorNoise, flecks);
            if (!run.Live) return;
            float field = game.BrineField;
            bool high = field >= .68f || (field > .40f && run.radiationDose >= .76f);
            Copy(radiationReadout, run.radiationDose >= .76f && field > .40f ? "沾染危险" :
                high ? "强辐射" : game.BrineShielded ? "已遮蔽" :
                field > .40f ? "辐射上升" : "辐射本底");
            radiationUnit.EnableInClassList("radiation-unit--high", high);
            doseStrip.Set(run.radiationDose, high);
            radiationDial.Read(field);
            if (flecks) sensorNoise.Read(field);
        }
    }

    internal sealed class NightRadiationDial : VisualElement
    {
        static readonly Color Face = new(.78f, .76f, .61f), Ink = new(.13f, .16f, .14f),
            Needle = new(.62f, .21f, .10f), Safe = new(.27f, .41f, .33f);
        float reading;

        internal NightRadiationDial()
        {
            AddToClassList("radiation-dial");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        internal void Read(float field)
        {
            // Mechanical damping and a small counting tremor. The needle settles when the field drops.
            float target = field + Mathf.Sin(Time.time * 23f) * .009f * field;
            float next = Mathf.Lerp(reading, Mathf.Clamp01(target), 1f - Mathf.Exp(-9f * Time.unscaledDeltaTime));
            if (Mathf.Abs(next - reading) < .00025f && Mathf.Abs(target - reading) < .001f) return;
            reading = next;
            MarkDirtyRepaint();
        }

        static void Line(Painter2D p, Vector2 start, Vector2 end, Color color, float width)
        {
            p.strokeColor = color; p.lineWidth = width;
            p.BeginPath(); p.MoveTo(start); p.LineTo(end); p.Stroke();
        }

        static void Disc(Painter2D p, Vector2 center, float radius, Color color)
        {
            p.fillColor = color; p.BeginPath(); p.Arc(center, radius, 0, 360); p.Fill();
        }

        static Vector2 Ray(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        void Paint(MeshGenerationContext ctx)
        {
            var size = contentRect.size;
            if (size.x < 2 || size.y < 2) return;
            var p = ctx.painter2D;
            float w = size.x, h = size.y;
            var pivot = new Vector2(w * .5f, h * .85f);
            float radius = h * .68f;
            p.fillColor = Face; p.BeginPath();
            p.MoveTo(new(1, 4)); p.LineTo(new(w - 1, 2)); p.LineTo(new(w - 2, h - 2));
            p.LineTo(new(2, h - 3)); p.ClosePath(); p.Fill();
            Line(p, new(3, 5), new(w - 3, 3), new(.95f, .90f, .72f), 1f);

            p.lineWidth = 2.4f; p.strokeColor = Safe;
            p.BeginPath(); p.Arc(pivot, radius, -156, -68); p.Stroke();
            p.strokeColor = Needle; p.BeginPath(); p.Arc(pivot, radius, -67, -24); p.Stroke();
            for (int i = 0; i <= 16; i++)
            {
                var direction = Ray(Mathf.Lerp(-156, -24, i / 16f));
                Line(p, pivot + direction * (radius - (i % 4 == 0 ? 5 : 2.4f)),
                    pivot + direction * (radius + 1.5f), Ink, i % 4 == 0 ? 1.3f : .9f);
            }
            // Stamped trefoil in the corner of the enamel, independent of font glyph coverage.
            var stamp = new Vector2(11, h - 10);
            Disc(p, stamp, 1.35f, Ink);
            p.strokeColor = Ink; p.lineWidth = 2.85f;
            for (int sector = 0; sector < 3; sector++)
            {
                p.BeginPath(); p.Arc(stamp, 4.8f, 32 + sector * 120, 91 + sector * 120); p.Stroke();
            }
            var ray = Ray(Mathf.Lerp(-156, -24, reading));
            Line(p, pivot + Vector2.right, pivot + ray * (radius + 2) + Vector2.right, new(.31f, .30f, .25f), 1.8f);
            Line(p, pivot - ray * 3f, pivot + ray * (radius + 2), Needle, 1.6f);
            Disc(p, pivot, 2.6f, Ink);
        }
    }

    // Tiny isolated sensor strikes live behind the interface. Leave the middle of the view and all lettering clear.
    internal sealed class NightSensorNoise : VisualElement
    {
        readonly Vector2[] flecks = new Vector2[24];
        int active, lastBand = -1;

        internal NightSensorNoise()
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList("screen");
            generateVisualContent += Paint;
        }

        internal void Read(float field)
        {
            int band = Mathf.FloorToInt(Time.time * 8);
            if (lastBand == band) return;
            lastBand = band;
            uint seed = unchecked((uint)band * 277803737u + 101u);
            active = Mathf.RoundToInt(Mathf.InverseLerp(.46f, 1f, field) * flecks.Length);
            for (int i = 0; i < active; i++)
            {
                float x = Noise(ref seed) * .21f;
                if ((i & 1) == 0) x = 1f - x;
                flecks[i] = new Vector2(x, .08f + Noise(ref seed) * .84f);
            }
            MarkDirtyRepaint();
        }

        static float Noise(ref uint seed)
        {
            seed = unchecked(seed * 1664525u + 1013904223u);
            return (seed >> 8) / 16777216f;
        }

        void Paint(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            p.strokeColor = new Color(.85f, .90f, .78f, .54f);
            p.lineWidth = 1f; p.BeginPath();
            for (int i = 0; i < active; i++)
            {
                var point = Vector2.Scale(contentRect.size, flecks[i]);
                p.MoveTo(point); p.LineTo(point + new Vector2(1f + i % 3, .4f));
            }
            if (active > 0) p.Stroke();
        }
    }
}
