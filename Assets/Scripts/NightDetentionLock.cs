using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    // The pointer sweeps continuously. Each catch is held while the next one comes into view.
    internal sealed class NightDetentionLock : VisualElement
    {
        const float StartAngle = -210f;
        const float SweepAngle = 240f;
        const string Styles = "Interface/DetentionLock";
        static readonly float[] Stops = { .31f, .73f, .46f, .81f, .23f };

        readonly Action opened;
        readonly Action<bool> feedback;
        readonly VisualElement dial;
        readonly Label status;
        readonly Button confirm;
        readonly Label[] contacts;
        readonly int night;
        float sweep, feedbackTime, entrance;
        bool lastHit;

        internal int Seated { get; private set; }
        internal int ContactCount { get; }
        internal float NeedlePosition => 1 - Mathf.Abs(1 - sweep);
        internal float TargetCenter => Stops[(Seated + night - 2) % Stops.Length];
        internal float TargetRadius => .105f - (night - 2) * .009f - Seated * .0025f;

        bool InWindow => Mathf.Abs(NeedlePosition - TargetCenter) <= TargetRadius;

        internal NightDetentionLock(int night, Action opened, Action cancel, Action<bool> feedback)
        {
            this.night = Mathf.Clamp(night, 2, 5);
            this.opened = opened;
            this.feedback = feedback;
            ContactCount = this.night switch
            {
                2 => 3,
                5 => 5,
                _ => 4
            };
            name = "detention-lock";
            AddToClassList("detention-lock");
            var sheet = Resources.Load<StyleSheet>(Styles);
            if (sheet) styleSheets.Add(sheet);

            var plate = new VisualElement();
            plate.AddToClassList("detention-lock__plate");
            Add(plate);

            var title = new Label("检修门锁");
            title.AddToClassList("detention-lock__title");
            plate.Add(title);

            var help = new Label("指针进入双刻线时，按空格扣合锁簧。");
            help.AddToClassList("detention-lock__help");
            plate.Add(help);

            dial = new VisualElement { pickingMode = PickingMode.Ignore, name = "detention-dial" };
            dial.AddToClassList("detention-lock__dial");
            dial.generateVisualContent += DrawDial;
            plate.Add(dial);

            var contactRow = new VisualElement();
            contactRow.AddToClassList("detention-lock__contacts");
            plate.Add(contactRow);
            contacts = new Label[ContactCount];
            for (int i = 0; i < ContactCount; i++)
            {
                contacts[i] = new Label("◇");
                contacts[i].AddToClassList("detention-lock__contact");
                contactRow.Add(contacts[i]);
            }

            status = new Label { name = "detention-status" };
            status.AddToClassList("detention-lock__status");
            plate.Add(status);
            RefreshStatus();

            var actions = new VisualElement();
            actions.AddToClassList("detention-lock__actions");
            plate.Add(actions);
            confirm = new Button(Confirm) { name = "detention-confirm", text = "空格  扣合" };
            confirm.AddToClassList("detention-lock__confirm");
            actions.Add(confirm);
            var exit = new Button(cancel) { name = "detention-cancel", text = "Esc  退开" };
            exit.AddToClassList("detention-lock__cancel");
            actions.Add(exit);

            // Space is the latch control even when UI Toolkit would submit another focused button.
            RegisterCallback<NavigationSubmitEvent>(evt =>
            {
                if (Input.GetKeyDown(KeyCode.Space)) evt.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
        }

        internal void Tick(float delta)
        {
            if (Seated >= ContactCount) return;
            float dt = Mathf.Clamp(delta, 0, .05f);
            entrance = Mathf.Min(1, entrance + dt * 4.5f);
            if (Input.GetKeyDown(KeyCode.Space)) Confirm();
            if (feedbackTime > 0)
            {
                feedbackTime = Mathf.Max(0, feedbackTime - dt);
                if (feedbackTime == 0) RefreshStatus();
            }
            else
            {
                float secondsPerSweep = 2.60f - (night - 2) * .25f - Seated * .075f;
                sweep = Mathf.Repeat(sweep + dt / secondsPerSweep, 2);
            }
            confirm.EnableInClassList("detention-lock__confirm--ready", feedbackTime <= 0 && InWindow);
            dial.MarkDirtyRepaint();
        }

        internal void Confirm()
        {
            if (Seated >= ContactCount || feedbackTime > 0) return;

            lastHit = InWindow;
            feedbackTime = lastHit ? .28f : .43f;
            feedback?.Invoke(lastHit);
            if (lastHit)
            {
                contacts[Seated].text = "◈";
                contacts[Seated].AddToClassList("detention-lock__contact--seated");
                Seated++;
                sweep = 0;
            }
            RefreshStatus();
            dial.MarkDirtyRepaint();
            if (Seated == ContactCount) opened?.Invoke();
        }

        void RefreshStatus()
        {
            bool missed = feedbackTime > 0 && !lastHit;
            status.EnableInClassList("detention-lock__status--miss", missed);
            status.text = missed ? "锁簧滑开了。等指针再回来。" : $"锁簧  {Seated}/{ContactCount}";
        }

        void DrawDial(MeshGenerationContext context)
        {
            var paint = context.painter2D;
            Rect bounds = dial.contentRect;
            var center = bounds.center;
            float radius = Mathf.Min(bounds.width, bounds.height) * .465f;
            if (radius < 6) return;

            // The rim, recessed enamel and screw heads are all fixed to the gauge.
            Disc(paint, center + Vector2.up * 4, radius, new Color(.01f, .012f, .011f, .90f));
            Disc(paint, center, radius, new Color(.26f, .24f, .19f));
            Disc(paint, center, radius * .962f, new Color(.10f, .097f, .079f));
            Disc(paint, center, radius * .90f, new Color(.067f, .077f, .072f));
            Disc(paint, center + Vector2.down * 2, radius * .82f, new Color(.082f, .096f, .088f));
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 screw = center + new Vector2(side * .414f, .627f) * radius;
                Disc(paint, screw, radius * .046f, new Color(.38f, .33f, .25f));
                var groove = new Vector2(.032f, -.015f * side) * radius;
                Segment(paint, screw - groove, screw + groove, new Color(.10f, .087f, .069f), 1.4f);
            }

            paint.lineCap = LineCap.Butt;
            DrawArc(paint, center, radius * .61f, StartAngle, StartAngle + SweepAngle,
                new Color(.40f, .39f, .32f), 1.3f);
            for (int i = 0; i <= 24; i++)
            {
                float angle = StartAngle + SweepAngle * i / 24;
                bool major = i % 6 == 0;
                Segment(paint, center + Direction(angle) * radius * (major ? .68f : .72f),
                    center + Direction(angle) * radius * .81f,
                    major ? new Color(.67f, .63f, .51f) : new Color(.42f, .44f, .37f), major ? 1.8f : 1f);
            }

            float from = StartAngle + SweepAngle * (TargetCenter - TargetRadius);
            float to = StartAngle + SweepAngle * (TargetCenter + TargetRadius);
            var slot = InWindow ? new Color(1, .87f, .61f) : new Color(.84f, .68f, .41f);
            DrawArc(paint, center, radius * .74f, from, to, new Color(.43f, .33f, .20f), 8f);
            DrawArc(paint, center, radius * .74f, from, to, slot, 2.5f);
            Mark(paint, center, radius, from, slot);
            Mark(paint, center, radius, to, slot);

            float needleAngle = StartAngle + SweepAngle * NeedlePosition;
            var along = Direction(needleAngle);
            var across = new Vector2(-along.y, along.x);
            Color pointer = feedbackTime > 0 && !lastHit ? new Color(.92f, .45f, .35f) : new Color(.97f, .91f, .77f);
            paint.fillColor = Color.Lerp(new Color(.39f, .36f, .28f), pointer, entrance);
            paint.BeginPath();
            paint.MoveTo(center + along * radius * .83f);
            paint.LineTo(center + across * radius * .022f);
            paint.LineTo(center - along * radius * .19f);
            paint.LineTo(center - across * radius * .022f);
            paint.ClosePath();
            paint.Fill();

            Disc(paint, center, radius * .090f, new Color(.036f, .037f, .030f));
            Disc(paint, center, radius * .061f, new Color(.72f, .56f, .35f));
            Disc(paint, center, radius * .036f, new Color(.19f, .15f, .11f));
        }

        static Vector2 Direction(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        static void Mark(Painter2D paint, Vector2 center, float radius, float angle, Color color)
        {
            var ray = Direction(angle);
            Segment(paint, center + ray * radius * .63f, center + ray * radius * .84f, color, 3f);
        }

        static void Segment(Painter2D paint, Vector2 from, Vector2 to, Color color, float width)
        {
            paint.strokeColor = color;
            paint.lineWidth = width;
            paint.BeginPath();
            paint.MoveTo(from);
            paint.LineTo(to);
            paint.Stroke();
        }

        static void DrawArc(Painter2D paint, Vector2 center, float radius, float start, float end, Color color, float width)
        {
            paint.strokeColor = color;
            paint.lineWidth = width;
            paint.BeginPath();
            paint.Arc(center, radius, start, end);
            paint.Stroke();
        }

        static void Disc(Painter2D paint, Vector2 center, float radius, Color color)
        {
            paint.fillColor = color;
            paint.BeginPath();
            paint.Arc(center, radius, 0, 360);
            paint.Fill();
        }
    }
}
