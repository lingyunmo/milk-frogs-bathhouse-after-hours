using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed partial class NightInterface
    {
        const float TitleCycle = 19.5f;
        const float LampStutter = .76f;
        const float CornerBlackout = 3.10f;
        const float CornerRelight = .58f;
        const float IntruderHold = 1.10f;

        VisualElement titleBackdrop, titleHaze, titleLamp, titleLampShade, titleLampCore, titleCornerLight, titleHallFrog;
        VisualElement titleBlackout, titleIntruder;
        float titleElapsed;

        void BuildTitleArt(VisualElement theater)
        {
            titleBackdrop = Area(theater, "title-backdrop");
            titleBackdrop.style.backgroundImage = new StyleBackground(titleImage);
            theater.RegisterCallback<GeometryChangedEvent>(e => LayoutTitleArt(e.newRect.size));

            titleHaze = Area(titleBackdrop, "screen title-haze");
            titleHaze.style.backgroundImage = new StyleBackground(NightInterfaceArt.Haze);
            titleLampShade = Area(titleBackdrop, "title-lamp-shade");
            titleLampShade.style.backgroundImage = new StyleBackground(NightInterfaceArt.Glow);
            titleLamp = Area(titleBackdrop, "title-lamp");
            titleLamp.style.backgroundImage = new StyleBackground(NightInterfaceArt.Glow);
            titleLampCore = Area(titleBackdrop, "title-lamp-core");
            titleLampCore.style.backgroundImage = new StyleBackground(NightInterfaceArt.Glow);
            titleCornerLight = Area(titleBackdrop, "title-corner-light");
            titleCornerLight.style.backgroundImage = new StyleBackground(NightInterfaceArt.Glow);
            var corner = Area(titleBackdrop, "title-corner");
            titleHallFrog = Area(corner, "title-hall-frog");
            titleHallFrog.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>(InterfaceFolder + "AfterHoursHallFrog"));
        }

        void BuildTitleIntruder(VisualElement theater)
        {
            titleBlackout = Area(theater, "screen title-blackout");
            titleIntruder = Area(theater, "title-intruder");
            titleIntruder.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>(InterfaceFolder + "AfterHoursIntruderClose"));
            DrawTitleAtmosphere(titleElapsed);
        }

        void ClearTitleArt()
        {
            titleBackdrop = titleHaze = titleLamp = titleLampShade = titleLampCore = titleCornerLight = titleHallFrog = null;
            titleBlackout = titleIntruder = null;
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
            if (before < LampStutter && phase >= LampStutter) game.PlayTitleLightFlicker(.67f);
            if (before < CornerBlackout && phase >= CornerBlackout) game.PlayTitleLightFlicker(.78f);
            if (before < CornerBlackout + CornerRelight && phase >= CornerBlackout + CornerRelight)
                game.PlayTitleContact();
            DrawTitleAtmosphere(titleElapsed);
        }

        void DrawTitleAtmosphere(float elapsed)
        {
            float phase = elapsed % TitleCycle;
            float relay = phase - LampStutter;
            float reveal = phase - CornerBlackout;
            float intruderCut = CornerRelight + IntruderHold;
            bool intruderVisible = reveal >= CornerRelight && reveal < intruderCut;
            bool blackout = reveal is >= 0 and < CornerRelight ||
                reveal >= intruderCut && reveal < intruderCut + .25f;
            float power = .87f + .13f * Mathf.PerlinNoise(elapsed * 3.1f, 2.7f);
            if (relay >= 0 && relay < .52f)
                power *= relay < .19f ? .16f : relay < .34f ? .94f : .28f;
            if (blackout) power *= .055f;
            else if (intruderVisible) power *= .62f;

            float level = Mathf.Lerp(.69f, 1f, power);
            titleBackdrop.style.unityBackgroundImageTintColor = new Color(level, level, level, 1);
            titleLampShade.style.opacity = Mathf.Pow(1 - power, .8f);
            titleLamp.style.opacity = power * .77f;
            bool reigniting = relay is >= .19f and < .34f || reveal >= CornerRelight && reveal < CornerRelight + .13f;
            titleLampCore.style.opacity = power * (reigniting ? .86f : .14f);

            float hallApproach = Mathf.SmoothStep(0, 1, (phase - .94f) / .62f);
            float hallRetreat = Mathf.SmoothStep(0, 1, (phase - 3.37f) / .64f);
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
