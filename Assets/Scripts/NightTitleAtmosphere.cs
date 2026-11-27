using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed partial class NightInterface
    {
        const float TitleCycle = 19.5f;
        const float LampStutter = .76f;
        const float CornerReveal = 2.35f;

        VisualElement titleBackdrop, titleHaze, titleLamp, titleLampShade, titleLampCore, titleCornerLight, titleHallFrog, titleIntruder;
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
            titleIntruder = Area(theater, "title-intruder");
            titleIntruder.style.backgroundImage = new StyleBackground(Resources.Load<Texture2D>(InterfaceFolder + "AfterHoursIntruder"));
            DrawTitleAtmosphere(titleElapsed);
        }

        void ClearTitleArt()
        {
            titleBackdrop = titleHaze = titleLamp = titleLampShade = titleLampCore = titleCornerLight = titleHallFrog = titleIntruder = null;
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
            if (before < CornerReveal && phase >= CornerReveal) game.PlayTitleLightFlicker();
            DrawTitleAtmosphere(titleElapsed);
        }

        void DrawTitleAtmosphere(float elapsed)
        {
            float phase = elapsed % TitleCycle;
            float relay = phase - LampStutter;
            float reveal = phase - CornerReveal;
            float power = .87f + .13f * Mathf.PerlinNoise(elapsed * 3.1f, 2.7f);
            if (relay >= 0 && relay < .52f)
                power *= relay < .19f ? .16f : relay < .34f ? .94f : .28f;
            if (reveal >= 0 && reveal < 1.19f)
                power *= reveal < .20f ? .12f : reveal < .95f ? 1f : .15f;

            float level = Mathf.Lerp(.69f, 1f, power);
            titleBackdrop.style.unityBackgroundImageTintColor = new Color(level, level, level, 1);
            titleLampShade.style.opacity = Mathf.Pow(1 - power, .8f);
            titleLamp.style.opacity = power * .77f;
            bool reigniting = relay is >= .19f and < .34f || reveal is >= .20f and < .36f;
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

            // The second figure lives in screen space, independent of the hallway and doorframe.
            float closeApproach = Mathf.SmoothStep(0, 1, (reveal - .15f) / .23f);
            float closeRetreat = Mathf.SmoothStep(0, 1, (reveal - .88f) / .26f);
            float foregroundPeek = closeApproach * (1 - closeRetreat);
            titleIntruder.style.opacity = foregroundPeek * Mathf.Lerp(.31f, 1, power);
            titleIntruder.style.translate = new Translate(Length.Percent(27 * (1 - foregroundPeek)),
                Length.Percent(32 * (1 - foregroundPeek)), 0);

            float drift = .5f + .5f * Mathf.Sin(elapsed * .19f);
            titleBackdrop.style.scale = new Scale(Vector3.one * (1.015f + game.Preferences.CameraMotion * drift * .034f));
            titleHaze.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, -elapsed * 9f % 2048);
            titleHaze.style.opacity = .27f + .07f * Mathf.Sin(elapsed * .37f);
        }
    }
}
