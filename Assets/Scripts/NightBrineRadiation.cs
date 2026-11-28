using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightGame
    {
        // Broken intake and the old return pipe under the cold store. These are gameplay fields.
        static readonly Vector3 intakeLeak = new(252.5f, 1.25f, -31.1f);
        static readonly Vector3 returnLeak = new(266.1f, 1.3f, 8.5f);
        AudioSource detectorSource;
        AudioClip detectorClick;
        readonly System.Random detectorPattern = new(1987);
        float sampleIn, clickIn;
        internal float BrineField { get; private set; }
        internal bool BrineShielded { get; private set; }

        internal float SampleBrineField(Vector3 feet, out bool shielded)
        {
            shielded = false;
            var run = State.brine;
            if (!run.Live) return 0;
            var probe = feet + Vector3.up * (Player.Crouched ? .71f : 1.26f);
            var intake = Mathf.InverseLerp(37f, 4f, Vector3.Distance(probe, intakeLeak));
            var backflow = .76f * Mathf.InverseLerp(16f, 2f, Vector3.Distance(probe, returnLeak));
            bool atIntake = intake >= backflow;
            float strength = Mathf.Max(intake, backflow);
            bool indoors = IsInsideBuilding(feet);
            if (strength > .10f)
            {
                var source = atIntake ? intakeLeak : returnLeak;
                shielded = indoors || Physics.Linecast(source, probe, ~(1 << NightPlayer.PlayerLayer),
                    QueryTriggerInteraction.Ignore);
            }
            if (shielded) strength *= .19f;
            // Opening a side sluice washes its strip of the yard clear, even between solid covers.
            if (run.Fighting && atIntake && run.divertTime > 0 && run.divertedLane >= 0 &&
                feet.x >= intakeLeak.x && feet.x <= 274.5f &&
                Mathf.Abs(feet.z - BrineRoute.Mouth(run.divertedLane).z) < 1.9f)
                strength *= .27f;
            float background = indoors ? .018f : .05f;
            return Mathf.Clamp01(background + strength * (1f + .08f * run.wave));
        }

        internal bool TickBrineRadiation(float dt)
        {
            var run = State.brine;
            if (!run.Live || ScreenState != NightScreen.Play || dt <= 0) return false;
            // A stalled frame cannot tell where the player spent that time. Only charge the sampled interval.
            dt = Mathf.Min(dt, .33f);
            if ((sampleIn -= dt) <= 0)
            {
                BrineField = SampleBrineField(Player.Feet, out bool covered);
                BrineShielded = covered;
                sampleIn = .14f;
            }
            // Residual contamination recedes on the move and in shelter. The dial reads the field immediately.
            float rate = BrineField > .40f ? (BrineField - .40f) * .054f : -(.40f - BrineField) * .14f;
            run.radiationDose = Mathf.Clamp01(run.radiationDose + rate * dt);
            if (run.radiationDose > .76f && rate > 0)
            {
                run.breath = Mathf.Max(0, run.breath - dt * Mathf.Lerp(.6f, 2.7f,
                    Mathf.InverseLerp(.76f, 1f, run.radiationDose)));
                if (run.breath <= 0)
                {
                    run.radiationLost = true;
                    EndBrine(false);
                    return true;
                }
            }

            if ((clickIn -= dt) <= 0)
            {
                if (!detectorSource) InstallBrineDetector();
                float variation = (float)detectorPattern.NextDouble();
                detectorSource.pitch = .88f + .27f * variation;
                detectorSource.volume = Mathf.Lerp(.20f, .52f, BrineField);
                detectorSource.Play();
                float density = .55f + BrineField * BrineField * 12f;
                clickIn = (.56f + 1.18f * (float)detectorPattern.NextDouble()) / density;
            }
            return false;
        }

        void InstallBrineDetector()
        {
            var radio = new GameObject("佩挂式辐射计");
            radio.transform.SetParent(transform, false);
            detectorSource = radio.AddComponent<AudioSource>();
            detectorSource.playOnAwake = false;
            detectorSource.spatialBlend = 0;
            detectorSource.panStereo = -.20f;
            const int hz = 24000;
            var click = new float[530];
            var hiss = new System.Random(420);
            for (int i = 0; i < click.Length; i++)
            {
                float t = i / (float)hz;
                float contact = (float)hiss.NextDouble() * 2f - 1f;
                float ring = Mathf.Sin(2f * Mathf.PI * 1760f * t);
                click[i] = Mathf.Min(1f, 1900f * t) *
                    (.67f * contact * Mathf.Exp(-t * 580f) + .27f * ring * Mathf.Exp(-t * 270f));
            }
            detectorClick = AudioClip.Create("Dosimeter contact", click.Length, 1, hz, false);
            detectorClick.SetData(click, 0);
            detectorSource.clip = detectorClick;
        }

        void ResetBrineDetector()
        {
            if (detectorSource) detectorSource.Stop();
            BrineField = sampleIn = clickIn = 0;
            BrineShielded = false;
        }

        void ReleaseBrineDetector()
        {
            if (detectorSource) detectorSource.Stop();
            if (detectorClick) Destroy(detectorClick);
        }
    }
}
