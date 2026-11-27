using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed partial class NightInterface
    {
        const float TitleCycle = 19.5f;
        const float CoverCut = 1.25f;
        const float CornerBlackout = 3.82f;
        const float CornerRelight = .58f;
        const float IntruderHold = 1.10f;
        static readonly float[] LampBursts = { 1.54f, 1.81f, 2.12f };

        NightTitleSparks titleSparks;
        VisualElement titleBackdrop, titleHaze, titleLamp, titleLampShade, titleLampCore, titleCornerLight, titleHallFrog;
        VisualElement titleBlackout;
        NightTitleIntruder titleIntruder;
        float titleElapsed;

        void BuildTitleArt(VisualElement theater)
        {
            titleBackdrop = Area(theater, "title-backdrop");
            theater.RegisterCallback<GeometryChangedEvent>(e => LayoutTitleArt(e.newRect.size));

            titleHaze = Area(titleBackdrop, "screen title-haze");
            titleHaze.style.backgroundImage = new StyleBackground(NightInterfaceArt.Haze);
            titleLampShade = Area(titleBackdrop, "title-lamp-shade");
            titleLampShade.style.backgroundImage = new StyleBackground(NightInterfaceArt.Glow);
            titleLamp = Area(titleBackdrop, "title-lamp");
            titleLamp.style.backgroundImage = new StyleBackground(NightInterfaceArt.Glow);
            titleLampCore = Area(titleBackdrop, "title-lamp-core");
            titleLampCore.style.backgroundImage = new StyleBackground(NightInterfaceArt.Glow);
            titleSparks = new NightTitleSparks();
            titleBackdrop.Add(titleSparks);
            titleCornerLight = Area(titleBackdrop, "title-corner-light");
            titleCornerLight.style.backgroundImage = new StyleBackground(NightInterfaceArt.Glow);
            var corner = Area(titleBackdrop, "title-corner");
            titleHallFrog = Area(corner, "title-hall-frog");
            titleHallFrog.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>(InterfaceFolder + "AfterHoursHallFrog"));
        }

        void BuildTitleIntruder(VisualElement theater)
        {
            titleBlackout = Area(theater, "screen title-blackout");
            titleIntruder = new NightTitleIntruder(Resources.Load<Texture2D>(InterfaceFolder + "AfterHoursIntruderClose"));
            theater.Add(titleIntruder);
            DrawTitleAtmosphere(titleElapsed);
        }

        void ClearTitleArt()
        {
            titleBackdrop = titleHaze = titleLamp = titleLampShade = titleLampCore = titleCornerLight = titleHallFrog = null;
            titleBlackout = null;
            titleIntruder = null;
            titleSparks = null;
        }

        void LayoutTitleArt(Vector2 viewport)
        {
            if (titleBackdrop == null || !titleImage || viewport.x < 1 || viewport.y < 1) return;
            float scale = Mathf.Max(viewport.x / titleImage.width, viewport.y / titleImage.height);
            float width = titleImage.width * scale;
            float height = titleImage.height * scale;
            titleBackdrop.style.width = width;
            titleBackdrop.style.height = height;
            // Bias a narrow crop toward the doorjamb while keeping the bulb in the frame.
            titleBackdrop.style.left = (viewport.x - width) * .92f;
            titleBackdrop.style.top = (viewport.y - height) * .5f;
        }

        void TickTitle()
        {
            if (titleBackdrop == null) return;
            float before = titleElapsed % TitleCycle;
            if (Application.isFocused || NightGame.Capturing)
                titleElapsed += Mathf.Min(Time.unscaledDeltaTime, .075f);
            float phase = titleElapsed % TitleCycle;
            if (before < LampBursts[0] && phase >= LampBursts[0]) game.PlayTitleLightFlicker(.67f);
            for (int burst = 0; burst < LampBursts.Length; burst++)
            {
                float start = LampBursts[burst];
                if (before < start && phase >= start)
                    game.PlayTitleSpark(burst == 1 ? .8f : 1f);
            }
            if (before < CornerBlackout && phase >= CornerBlackout) game.PlayTitleLightFlicker(.78f);
            if (before < CornerBlackout + CornerRelight && phase >= CornerBlackout + CornerRelight)
                game.PlayTitleContact();
            DrawTitleAtmosphere(titleElapsed);
        }

        void DrawTitleAtmosphere(float elapsed)
        {
            float phase = elapsed % TitleCycle;
            var plate = phase < CoverCut && titlePoster ? titlePoster : titleImage;
            if (titleBackdrop.style.backgroundImage.value.texture != plate)
                titleBackdrop.style.backgroundImage = new StyleBackground(plate);

            int burst = LampBursts.Length - 1;
            while (burst > 0 && phase < LampBursts[burst]) burst--;
            float arcAge = phase - LampBursts[burst];
            titleSparks.SetBurst(burst, arcAge);
            float reveal = phase - CornerBlackout;
            float intruderCut = CornerRelight + IntruderHold;
            bool intruderVisible = reveal >= CornerRelight && reveal < intruderCut;
            bool blackout = reveal is >= 0 and < CornerRelight ||
                reveal >= intruderCut && reveal < intruderCut + .25f;
            float power = .87f + .13f * Mathf.PerlinNoise(elapsed * 3.1f, 2.7f);
            bool fault = phase >= LampBursts[0] && phase < LampBursts[^1] + NightTitleSparks.BurstDuration;
            if (fault)
                power = arcAge < .078f ? 1f : arcAge < .158f ? .13f : .27f;
            if (blackout) power *= .055f;
            else if (intruderVisible) power *= .62f;

            float level = Mathf.Lerp(fault ? .54f : .69f, 1f, power);
            titleBackdrop.style.unityBackgroundImageTintColor = new Color(level, level, level, 1);
            titleLampShade.style.opacity = Mathf.Pow(1 - power, .8f);
            titleLamp.style.opacity = power * .77f;
            bool reigniting = fault && arcAge < .078f ||
                reveal >= CornerRelight && reveal < CornerRelight + .13f;
            titleLampCore.style.opacity = power * (reigniting ? .86f : .14f);

            float hallApproach = Mathf.SmoothStep(0, 1, (phase - 2.44f) / .62f);
            float hallRetreat = Mathf.SmoothStep(0, 1, (phase - 4.17f) / .64f);
            float hallPeek = hallApproach * (1 - hallRetreat);
            float hallLight = Mathf.Lerp(.35f, .81f, power);
            titleCornerLight.style.opacity = hallPeek * power * .31f;
            titleHallFrog.style.left = Length.Percent(Mathf.Lerp(94, 14, hallPeek));
            titleHallFrog.style.opacity = hallPeek * Mathf.Lerp(.54f, 1, power);
            titleHallFrog.style.unityBackgroundImageTintColor =
                new Color(hallLight, hallLight * .92f, hallLight * .81f, 1);

            titleBlackout.style.opacity = blackout ? .91f : intruderVisible ? .46f : 0f;
            game.SetTitleBlackout(blackout);
            // The stage stays readable while the whole foreground pose cuts in at once.
            titleIntruder.style.opacity = intruderVisible ? 1f : 0f;

            float drift = .5f + .5f * Mathf.Sin(elapsed * .19f);
            titleBackdrop.style.scale = new Scale(Vector3.one * (1.015f + game.Preferences.CameraMotion * drift * .034f));
            titleHaze.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, -elapsed * 9f % 2048);
            titleHaze.style.opacity = .27f + .07f * Mathf.Sin(elapsed * .37f);
        }
    }
}
