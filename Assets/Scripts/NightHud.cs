
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed partial class NightInterface
    {
        readonly NightGame game;
        readonly UIDocument document;
        readonly PanelSettings settings;
        readonly Font serifFont, displayFont;
        readonly Texture2D titlePoster, titleImage;
        readonly VisualElement root;
        const string InterfaceFolder = "Interface/";
        static readonly int[] FloorOrder = { NightAtlas.Street, 3, 2, 1, 0 };

        Label task, objectiveTag, location, mapFloor, mapPlace, toast, charge, cellCount, stoneCount;
        Label promptText, pickupText, areaFloor, areaName, chapterOverline, chapterTitle;
        VisualElement objectiveBlock, prompt, promptHold, status, repairSlot, torchSlot, runHint, crouchHint;
        VisualElement staminaBlock, pickupCard, pickupArt, areaCard, minimapRing, north, curtain, chapter, letterbox;
        VisualElement grainLayer, toastBox;
        NightReticle reticle;
        NightDetentionLock detentionLock;
        Meter stamina, torch;
        NightMapView minimap;
        NightPursuitVignette pursuitVignette;
        Button[] mapTabs;
        NightMapView fullMap;
        readonly List<(Button Row, Func<bool> On)> switches = new();
        readonly Dictionary<string, float> areaSeen = new();
        NightGame.RepairPanel shownRepair;
        int repairRow = -1, repairOption = -1, shownPickup;
        string subpage = "", shownObjective = "", shownArea = "";
        float nextMapDraw, objectiveSince = -99, staminaSeen = -99, pickupUntil = -99, areaUntil = -99, arrival = -99;
        float screenOpened;
        bool arrivalIsNew;

        internal NightInterface(NightGame game)
        {
            this.game = game;
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "After Hours Interface";
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(InterfaceFolder + "AfterHoursTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = .86f;

            var canvas = new GameObject("After Hours UI");
            canvas.transform.SetParent(game.transform, false);
            canvas.SetActive(false);
            document = canvas.AddComponent<UIDocument>();
            document.panelSettings = settings;
            canvas.SetActive(true);
            root = document.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>(InterfaceFolder + "AfterHours"));
            root.styleSheets.Add(Resources.Load<StyleSheet>(InterfaceFolder + "NightDuty"));
            var font = Resources.Load<Font>("Typeface/AfterHours");
            root.style.unityFont = font;
            root.style.unityFontDefinition = FontDefinition.FromFont(font);
            serifFont = Resources.Load<Font>("Typeface/AfterHoursSerif");
            displayFont = Resources.Load<Font>(InterfaceFolder + "TitleTypeface");
            titlePoster = Resources.Load<Texture2D>(InterfaceFolder + "AfterHoursTitle");
            titleImage = Resources.Load<Texture2D>(InterfaceFolder + "AfterHoursCorridor");
            root.AddToClassList("after-hours");
            root.pickingMode = PickingMode.Ignore;
            root.RegisterCallback<GeometryChangedEvent>(e => root.EnableInClassList("compact", e.newRect.width < 980));
        }

        internal void Dispose()
        {
            if (document) document.panelSettings = null;
            if (settings) UnityEngine.Object.Destroy(settings);
        }

        static VisualElement Area(VisualElement parent, string classes)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            Classes(element, classes);
            parent.Add(element);
            return element;
        }

        Label Type(VisualElement parent, string text, string classes)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            Classes(label, classes);
            Serif(label, classes);
            parent.Add(label);
            return label;
        }

        static void Classes(VisualElement element, string classes)
        {
            foreach (string name in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)) element.AddToClassList(name);
        }

        void Serif(VisualElement element, string classes)
        {
            if (!serifFont || !classes.Contains("serif")) return;
            element.style.unityFont = serifFont;
            element.style.unityFontDefinition = FontDefinition.FromFont(serifFont);
        }

        Button Act(VisualElement parent, string title, Action run, string classes, string name = "")
        {
            var button = new Button(() =>
            {
                game.PlayMenuSound();
                run();
            }) { text = title, name = name };
            Classes(button, classes);
            Serif(button, classes);
            parent.Add(button);
            return button;
        }

        Button Item(VisualElement parent, string title, Action run, string name = "", bool primary = false) =>
            Act(parent, title, run, primary ? "menu-item serif menu-item--primary" : "menu-item serif", name);

        static void Visible(VisualElement element, bool shown)
        {
            var display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display.value != display) element.style.display = display;
        }

        static void Copy(Label element, string value)
        {
            if (element.text != value) element.text = value;
        }

        static void Focus(VisualElement element) => element?.schedule.Execute(() => element.Focus()).StartingIn(40);

        void Keys(VisualElement parent, string keys, string what)
        {
            var hint = Area(parent, "key-hint");
            foreach (string key in keys.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                Type(hint, key, key == "/" ? "key-join" : "key");
            Type(hint, what, "key-hint-text");
        }

        internal void Show(NightScreen screen)
        {
            subpage = "";
            if (screen == NightScreen.Title) titleElapsed = 0;
            Draw(screen);
        }

        void Draw(NightScreen screen)
        {
            root.Clear();
            switches.Clear();
            toast = null;
            toastBox = null;
            ClearTitleArt();
            grainLayer = null;
            reticle = null;
            detentionLock = null;
            curtain = chapter = letterbox = null;
            screenOpened = Time.unscaledTime;
            switch (screen)
            {
                case NightScreen.Play:
                    BuildHud();
                    return;
                case NightScreen.Capture:
                    letterbox = Area(root, "screen letterbox");
                    Area(letterbox, "letterbox-bar letterbox-bar--top");
                    Area(letterbox, "letterbox-bar letterbox-bar--bottom");
                    Reveal(letterbox);
                    return;
                case NightScreen.Title:
                    BuildTitle();
                    return;
                case NightScreen.Detention:
                    detentionLock = new NightDetentionLock(game.State.night, game.ReleaseDetention,
                        game.CloseDetentionLock, game.DetentionFeedback);
                    root.Add(detentionLock);
                    return;
            }

            var veil = Area(root, "screen veil");
            if (screen == NightScreen.Inventory) BuildInventory(veil);
            else if (screen == NightScreen.Map) BuildMap(veil);
            else if (screen == NightScreen.Pause) BuildPause(veil);
            else BuildEnding(veil, screen == NightScreen.Escaped);
        }

        // ------------------------------------------------------------------ Title

        void BuildTitle()
        {
            var theater = Area(root, "screen theater");
            BuildTitleArt(theater);
            Area(theater, "screen title-scrim-left").style.backgroundImage = new StyleBackground(NightInterfaceArt.LeftScrim);
            Area(theater, "screen title-scrim-bottom").style.backgroundImage = new StyleBackground(NightInterfaceArt.BottomScrim);
            Area(theater, "screen title-vignette").style.backgroundImage = new StyleBackground(NightInterfaceArt.Vignette);
            grainLayer = Area(theater, "screen film-grain");
            grainLayer.style.backgroundImage = new StyleBackground(NightInterfaceArt.Grain);
            BuildTitleIntruder(theater);

            var stage = Area(theater, "screen title-stage");
            var block = Area(stage, "title-block");
            block.EnableInClassList("title-block--resume", game.ResumableSave != null);
            Type(block, "MILK FROG'S BATHHOUSE", "title-overline");
            Type(block, "奶娃浴场", "title-brand serif");
            var logotype = Type(block, "打烊后", "title-name");
            if (displayFont)
            {
                logotype.style.unityFont = displayFont;
                logotype.style.unityFontDefinition = FontDefinition.FromFont(displayFont);
            }
            var chapterRow = Area(block, "title-chapter-row");
            Area(chapterRow, "title-chapter-rule");
            Type(chapterRow, game.ResumableSave?.NightLabel ?? "五夜逃生", "title-chapter serif");
            Area(chapterRow, "title-chapter-rule");
            Stagger(block, 0);

            var footer = Area(stage, "title-footer");
            Type(footer, "单人逃生", "title-edition");
            var hints = Area(footer, "key-hints");
            Keys(hints, "↑ / ↓", "选择");
            Keys(hints, "Enter", "确认");
            Keys(hints, "Esc", "返回");

            if (subpage.Length != 0)
            {
                var sheet = Area(stage, "sheet title-sheet");
                Reveal(sheet);
                BuildSubpage(sheet);
                return;
            }

            var menu = Area(stage, "title-menu");
            Button first = null;
            if (game.ResumableSave != null)
                first = Item(menu, "继续游戏", game.ContinueNight, "continue-night", true);
            var fresh = Item(menu, "开始新游戏", () =>
            {
                if (game.ResumableSave == null) game.NewNight();
                else Open("restart");
            }, "new-night", game.ResumableSave == null);
            Item(menu, "选项", () => Open("settings"), "options");
            Item(menu, "操作", () => Open("controls"), "controls");
            Item(menu, "退出", () => Open("quit"), "quit");
            Stagger(menu, 5);
            Focus(first ?? fresh);
        }

        // Children fade up one after another for a slow, deliberate title reveal.
        static void Stagger(VisualElement parent, int offset)
        {
            int index = offset;
            foreach (var child in parent.Children())
            {
                child.AddToClassList("entrance");
                var element = child;
                element.schedule.Execute(() => element.RemoveFromClassList("entrance")).StartingIn(90 + 85 * index++);
            }
        }

        internal void OpenPage(string page) => Open(page);

        void Open(string page)
        {
            subpage = page;
            Draw(game.ScreenState);
        }

        internal bool CloseSubpage()
        {
            if (subpage.Length == 0) return false;
            PlayerPrefs.Save();
            subpage = "";
            Draw(game.ScreenState);
            return true;
        }

        // ------------------------------------------------------------------ Pause and sheets

        void BuildPause(VisualElement veil)
        {
            veil.AddToClassList("pause-veil");
            Area(veil, "screen menu-scrim").style.backgroundImage = new StyleBackground(NightInterfaceArt.LeftScrim);
            var column = Area(veil, "menu-column");
            Reveal(column);
            Type(column, LocationName(), "overline");
            if (subpage.Length == 0)
            {
                Type(column, "暂停", "menu-heading serif");
                Area(column, "menu-rule");
            }
            BuildBrief(veil);
            var footer = Area(veil, "menu-footer");
            Keys(footer, "Esc", subpage.Length == 0 ? "返回游戏" : "返回");
            if (subpage.Length != 0)
            {
                BuildSubpage(Area(column, "sheet"));
                return;
            }
            var list = Area(column, "menu-list");
            var resume = Item(list, "继续游戏", Resume, "resume", true);
            Item(list, "保存进度", game.SaveHere, "save");
            Item(list, "选项", () => Open("settings"), "options");
            Item(list, "操作", () => Open("controls"), "controls");
            Item(list, "返回标题", () => Open("leave"), "leave");
            toast = Type(column, "", "menu-notice");
            Focus(resume);
        }

        void BuildBrief(VisualElement veil)
        {
            var state = game.State;
            var brief = Area(veil, "pause-brief");
            Reveal(brief);
            Type(brief, "当前目标", "overline");
            Type(brief, Objective(state), "brief-objective serif");
            Area(brief, "brief-rule");
            int explored = 0, total = 0;
            for (int floor = 0; floor < NightAtlas.FloorNames.Length; ++floor)
                foreach (var area in game.Atlas.Areas(floor))
                {
                    total++;
                    if (state.explored.Contains(area.Key)) explored++;
                }
            Type(brief, "调查进度", "overline");
            Type(brief, $"已走过 {explored} / {total} 处", "brief-line");
            Area(brief, "brief-rule");
            Type(brief, "随身", "overline");
            string light = state.torch ? Percent(Mathf.Clamp01(state.charge / NightState.FullTorchCharge)) : "未拾取";
            Type(brief, $"手电 {light}　·　电池 {state.cells}　·　石子 {state.stones}", "brief-line");
        }

        void BuildSubpage(VisualElement card)
        {
            var preferences = game.Preferences;
            Button primary = null;
            switch (subpage)
            {
                case "settings":
                    Type(card, "选项", "sheet-heading serif");
                    var scroll = new ScrollView(ScrollViewMode.Vertical);
                    scroll.AddToClassList("sheet-scroll");
                    card.Add(scroll);
                    var rows = scroll.contentContainer;
                    Type(rows, "操作", "sheet-section");
                    Slider(rows, "鼠标灵敏度", .5f, 5, preferences.Sensitivity, preferences.SetSensitivity, v => v.ToString("0.0"));
                    Slider(rows, "镜头晃动", 0, 1, preferences.CameraMotion, preferences.SetMotion, Percent);
                    Slider(rows, "视野", 50, 82, preferences.FieldOfView, preferences.SetFov, v => Mathf.RoundToInt(v) + "°");
                    Type(rows, "画面", "sheet-section");
                    Slider(rows, "亮度", .7f, 1.45f, preferences.Brightness, preferences.SetBrightness, v => Mathf.RoundToInt(v * 100) + "%");
                    Switch(rows, "灯光柔晕", "practical-glow", () => preferences.PracticalGlow, preferences.SetPracticalGlow);
                    Switch(rows, "环境光遮蔽", "ambient-occlusion", () => preferences.AmbientOcclusion, preferences.SetAmbientOcclusion);
                    Switch(rows, "胶片颗粒", "film-grain", () => preferences.FilmGrain, preferences.SetFilmGrain);
                    Switch(rows, "全屏", "fullscreen", () => Screen.fullScreen, on =>
                        Screen.fullScreenMode = on ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
                    Type(rows, "声音", "sheet-section");
                    Slider(rows, "音量", 0, 1, preferences.Volume, preferences.SetVolume, Percent);
                    break;
                case "controls":
                    Type(card, "操作", "sheet-heading serif");
                    var grid = Area(card, "control-grid");
                    foreach (var (keys, what) in new (string, string)[]
                    {
                        ("W A S D", "移动"), ("鼠标", "观察"), ("Shift", "按住奔跑"), ("C", "切换蹲行"),
                        (NightGame.InteractKey.ToString(), "拾取 / 交互 / 翻窗"),
                        (NightGame.TorchKey.ToString(), "开关手电"), ("R", "更换电池"), ("G", "投掷石子"),
                        ("B", "背包"), ("M", "地图"), ("F5", "保存"), ("Esc", "暂停"), ("Alt + Enter", "切换全屏")
                    })
                    {
                        var row = Area(grid, "control-row");
                        var caps = Area(row, "control-keys");
                        foreach (string key in keys.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                            Type(caps, key, key == "+" ? "key-join" : "key");
                        Type(row, what, "control-description");
                    }
                    break;
                case "restart":
                    Type(card, "开始新一夜？", "sheet-heading serif");
                    Type(card, "下一次保存会覆盖本地进度。", "sheet-caption");
                    break;
                case "leave":
                    Type(card, "返回标题？", "sheet-heading serif");
                    Type(card, "下次会从最近一次保存的位置继续。", "sheet-caption");
                    break;
                case "quit":
                    Type(card, "退出游戏？", "sheet-heading serif");
                    Type(card, "浴场的灯会一直亮到天亮。", "sheet-caption");
                    break;
            }

            var actions = Area(card, "sheet-actions");
            if (subpage == "restart") primary = Act(actions, "开始", game.NewNight, "sheet-action sheet-action--primary serif", "start-over");
            if (subpage == "leave") primary = Act(actions, "返回标题", () => game.SetScreen(NightScreen.Title), "sheet-action sheet-action--primary serif", "leave-confirm");
            if (subpage == "quit") primary = Act(actions, "退出", Quit, "sheet-action sheet-action--primary serif", "quit-confirm");
            var back = Act(actions, "返回", () => CloseSubpage(), "sheet-action serif", "back");
            Focus(primary ?? back);
        }

        void Slider(VisualElement parent, string title, float min, float max, float value, Action<float> set, Func<float, string> format)
        {
            var row = Area(parent, "setting-row");
            Type(row, title, "setting-name");
            var slider = new Slider(min, max) { value = value, tooltip = title };
            slider.AddToClassList("setting-slider");
            row.Add(slider);
            var number = Type(row, format(value), "setting-value");
            var tracker = slider.Q(className: "unity-base-slider__tracker");
            var fill = tracker != null ? Area(tracker, "setting-fill") : null;
            void Fill(float v)
            {
                if (fill != null) fill.style.width = Length.Percent(Mathf.InverseLerp(min, max, v) * 100);
            }
            Fill(value);
            slider.RegisterValueChangedCallback(e =>
            {
                set(e.newValue);
                game.ApplyPreferences();
                game.PlayMenuTick();
                Copy(number, format(e.newValue));
                Fill(e.newValue);
            });
        }

        void Switch(VisualElement parent, string title, string name, Func<bool> on, Action<bool> set)
        {
            Button row = null;
            row = Act(parent, title, () =>
            {
                set(!on());
                game.ApplyPreferences();
                row.EnableInClassList("switch--on", on());
            }, "switch-row", name);
            var track = Area(row, "switch-track");
            Area(track, "switch-knob");
            row.EnableInClassList("switch--on", on());
            switches.Add((row, on));
        }

        // ------------------------------------------------------------------ Endings

        void BuildEnding(VisualElement veil, bool safe)
        {
            Classes(veil, safe ? "ending-veil ending--dawn" : "ending-veil ending--caught");
            Area(veil, "screen ending-vignette").style.backgroundImage = new StyleBackground(NightInterfaceArt.Vignette);
            var column = Area(veil, "ending");
            Type(column, safe ? game.State.NightLabel + "　·　完" : game.State.NightLabel, "ending-overline");
            Type(column, safe ? "天亮以前" : "最后一夜", "ending-title serif");
            Area(column, "ending-rule");
            Type(column, safe ? "车门关上了。浴场的灯从后窗消失。" : "铁门合上了。走廊里只剩它的笑声。", "ending-caption serif");
            var actions = Area(column, "ending-actions");
            var retry = Item(actions, "重新开始", game.NewNight, "retry", true);
            Item(actions, "返回标题", () => game.SetScreen(NightScreen.Title), "title");
            Stagger(column, 0);
            Focus(retry);
        }

        // ------------------------------------------------------------------ Map

        void BuildMap(VisualElement veil)
        {
            veil.AddToClassList("atlas-veil");
            var card = Area(veil, "atlas-card");
            Reveal(card);
            var header = Area(card, "atlas-header");
            var titles = Area(header, "atlas-titles");
            Type(titles, "位置 · " + LocationName(), "overline");
            Type(titles, "浴场地图", "atlas-heading serif");
            Act(header, "M　收起", Resume, "chip-button", "close-map");
            var body = Area(card, "atlas-body");
            var rail = Area(body, "floor-rail");
            mapTabs = new Button[NightAtlas.FloorNames.Length];
            foreach (int floor in FloorOrder)
            {
                int sheet = floor;
                if (floor == 1) Area(rail, "floor-rail-gap");
                mapTabs[floor] = Act(rail, NightAtlas.FloorNames[floor], () => OpenFloor(sheet), "floor-tab serif", "floor-" + floor);
                mapTabs[floor].EnableInClassList("floor-tab--here", floor == game.Atlas.Floor);
                if (floor == NightAtlas.Street) Area(rail, "floor-rail-gap");
            }
            fullMap = new NightMapView(game, false);
            body.Add(fullMap);
            var footer = Area(card, "atlas-footer");
            var legend = Area(footer, "atlas-legend");
            Legend(legend, "legend-swatch--here", "当前位置");
            Legend(legend, "legend-swatch--seen", "已走过");
            Legend(legend, "legend-swatch--unknown", "未探索");
            var tools = Area(footer, "atlas-tools");
            Type(tools, "滚轮缩放　·　按住拖动", "muted");
            Act(tools, "−", () => fullMap.Zoom(1 / 1.26f), "chip-button atlas-control", "map-zoom-out");
            Act(tools, "+", () => fullMap.Zoom(1.26f), "chip-button atlas-control", "map-zoom-in");
            Act(tools, "复位", fullMap.ResetView, "chip-button atlas-control", "map-reset");
            OpenFloor(game.Atlas.Floor);
        }

        void Legend(VisualElement parent, string swatch, string text)
        {
            var entry = Area(parent, "legend");
            Area(entry, "legend-swatch " + swatch);
            Type(entry, text, "legend-text");
        }

        void OpenFloor(int floor)
        {
            for (int i = 0; i < mapTabs.Length; ++i) mapTabs[i].EnableInClassList("floor-tab--selected", i == floor);
            fullMap.SetSheet(floor);
        }

        static void Reveal(VisualElement element)
        {
            element.AddToClassList("entrance");
            element.schedule.Execute(() => element.RemoveFromClassList("entrance")).StartingIn(24);
        }

        // ------------------------------------------------------------------ Play

        void BuildHud()
        {
            var layer = Area(root, "screen play-hud");
            pursuitVignette = new NightPursuitVignette();
            pursuitVignette.AddToClassList("screen");
            layer.Add(pursuitVignette);
            Area(layer, "corner-shade corner-shade--top-left").style.backgroundImage = new StyleBackground(NightInterfaceArt.Shade);
            Area(layer, "corner-shade corner-shade--bottom-left").style.backgroundImage = new StyleBackground(NightInterfaceArt.Shade);

            objectiveBlock = Area(layer, "objective");
            var tagline = Area(objectiveBlock, "objective-tagline");
            Area(tagline, "objective-diamond");
            objectiveTag = Type(tagline, "当前目标", "objective-tag");
            task = Type(objectiveBlock, shownObjective, "objective-text serif");
            location = Type(objectiveBlock, "", "objective-place");
            status = Area(objectiveBlock, "danger");
            Area(status, "danger-dot");
            Type(status, "脚步逼近", "danger-text");
            Visible(status, false);

            var mapBox = Area(layer, "minimap");
            minimapRing = Area(mapBox, "minimap-ring");
            minimap = new NightMapView(game, true);
            minimapRing.Add(minimap);
            Area(minimapRing, "screen minimap-glass");
            north = Type(mapBox, "N", "minimap-north");
            var caption = Area(mapBox, "minimap-caption");
            mapFloor = Type(caption, "", "map-floor serif");
            mapPlace = Type(caption, "", "map-place");
            Type(caption, "M", "key");

            areaCard = Area(layer, "area-card");
            var floorRow = Area(areaCard, "area-floor-row");
            Area(floorRow, "area-rule");
            areaFloor = Type(floorRow, "", "area-floor");
            Area(floorRow, "area-rule");
            areaName = Type(areaCard, "", "area-name serif");

            reticle = new NightReticle();
            layer.Add(reticle);
            prompt = Area(layer, "prompt");
            Type(prompt, NightGame.InteractKey.ToString(), "key prompt-key");
            promptText = Type(prompt, "", "prompt-text");
            promptHold = Type(prompt, "按住", "prompt-hold");

            var subtitle = Area(layer, "subtitle-shade");
            subtitle.style.backgroundImage = new StyleBackground(NightInterfaceArt.Shade);
            toast = Type(subtitle, "", "subtitle");
            toastBox = subtitle;
            Visible(subtitle, false);

            pickupCard = Area(layer, "pickup");
            pickupArt = Area(pickupCard, "pickup-art");
            var copy = Area(pickupCard, "pickup-copy");
            Type(copy, "获得", "pickup-tag");
            pickupText = Type(copy, "", "pickup-text serif");
            if (game.LastPickup.Serial > 0)
            {
                SetItemArt(pickupArt, game.LastPickup.Art);
                Copy(pickupText, game.LastPickup.Text);
            }

            repairSlot = Area(layer, "repair");
            shownRepair = null;
            repairRow = repairOption = -1;
            Visible(repairSlot, false);

            staminaBlock = Area(layer, "stamina");
            stamina = new Meter(staminaBlock, "stamina-track", "stamina-fill");
            stamina.Track.name = "stamina-meter";

            var equipment = Area(layer, "equipment");
            torchSlot = Slot(equipment, NightGame.TorchKey.ToString(), "手电", "torch", out charge);
            torchSlot.AddToClassList("torch-slot");
            charge.name = "torch-charge";
            torch = new Meter(torchSlot.Q(className: "slot-body"), "torch-track", "torch-fill", 5);
            Slot(equipment, "R", "电池", "cell", out cellCount);
            Slot(equipment, "G", "石子", "stone", out stoneCount);

            var hints = Area(layer, "hint-row");
            runHint = Bind(hints, "Shift", "奔跑");
            crouchHint = Bind(hints, "C", "蹲行");
            Bind(hints, "B", "背包");
            Bind(hints, "M", "地图");
            Bind(hints, "Esc", "暂停");

            curtain = Area(layer, "screen curtain");
            chapter = Area(layer, "chapter");
            chapterOverline = Type(chapter, "", "chapter-overline");
            Area(chapter, "chapter-rule");
            chapterTitle = Type(chapter, "", "chapter-title serif");
            UpdateArrival();
            nextMapDraw = 0;
        }

        VisualElement Slot(VisualElement row, string key, string label, string icon, out Label value)
        {
            var box = Area(row, "slot");
            SetItemArt(Area(box, "slot-art"), icon);
            var body = Area(box, "slot-body");
            var top = Area(body, "slot-top");
            value = Type(top, "", "slot-value");
            Type(top, key, "key slot-key");
            Type(body, label, "slot-name");
            return box;
        }

        VisualElement Bind(VisualElement row, string key, string label)
        {
            var item = Area(row, "hotkey");
            Type(item, key, "key");
            Type(item, label, "hotkey-name");
            return item;
        }

        // Fades in from black on a new night with the chapter card, or briefly names the place on continue.
        internal void Arrive(bool newNight)
        {
            arrival = Time.unscaledTime;
            arrivalIsNew = newNight;
            areaSeen.Clear();
            shownArea = game.Atlas.Current?.Key ?? "";
            if (chapter == null) return;
            Copy(chapterOverline, game.State.NightLabel);
            Copy(chapterTitle, newNight && !game.State.detained ? "打烊后" : LocationName());
            UpdateArrival();
        }

        void UpdateArrival()
        {
            if (curtain == null) return;
            float t = Time.unscaledTime - arrival;
            float dark = arrivalIsNew ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.9f, 3.1f, t))
                : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .9f, t));
            float words = arrivalIsNew
                ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, 1.5f, t)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3.9f, 5.2f, t)))
                : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.15f, .7f, t)) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.1f, 2.9f, t)));
            curtain.style.opacity = dark;
            Visible(curtain, dark > .002f);
            chapter.style.opacity = words;
            chapter.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50 + (1 - words) * 6));
            Visible(chapter, words > .002f);
        }

        void DrawRepair(NightGame.RepairPanel current)
        {
            Visible(repairSlot, current != null);
            if (current == null || shownRepair == current && repairRow == current.row && repairOption == current.selected) return;
            if (shownRepair != current) Reveal(repairSlot);
            shownRepair = current;
            repairRow = current.row;
            repairOption = current.selected;
            repairSlot.Clear();
            var heading = Area(repairSlot, "repair-heading");
            Type(heading, "检修", "repair-overline");
            Type(heading, $"第 {current.row + 1} 步 / 共 {current.answer.Length} 步", "repair-step");
            Type(repairSlot, current.title, "repair-title serif");
            Type(repairSlot, current.prompts[current.row], "repair-detail");
            var options = Area(repairSlot, "repair-options");
            for (int i = 0; i < current.options.Length; ++i)
            {
                var plate = Area(options, "repair-option");
                plate.EnableInClassList("repair-option--selected", i == current.selected);
                Type(plate, current.options[i], "repair-option-text serif");
            }
            var keys = Area(repairSlot, "repair-keys");
            Keys(keys, "A / D", "切换");
            Keys(keys, "空格", "确认");
            Keys(keys, "Esc", "松手");
        }

        string LocationName() => NightAtlas.FloorNames[game.Atlas.Floor] + " · " + game.Atlas.Place;

        internal void Tick()
        {
            var screen = game.ScreenState;
            float now = Time.unscaledTime;
            foreach (var (row, on) in switches) row.EnableInClassList("switch--on", on());
            if (toast != null)
            {
                string message = game.Message;
                var box = toastBox ?? toast;
                if (message.Length > 0 && toast.text != message) Reveal(box);
                Copy(toast, message);
                Visible(box, message.Length > 0);
            }
            if (grainLayer != null)
            {
                // Jump the grain tile every frame like film, rather than sliding it.
                int frame = Time.frameCount;
                grainLayer.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Left, (frame * 71) % 192);
                grainLayer.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Top, (frame * 113) % 192);
            }
            if (screen == NightScreen.Title) TickTitle();
            if (screen == NightScreen.Detention && Application.isFocused)
                detentionLock?.Tick(Mathf.Min(Time.unscaledDeltaTime, .075f));
            if (screen != NightScreen.Play || reticle == null) return;

            var player = game.Player;
            var state = game.State;

            string objective = Objective(state);
            if (objective != shownObjective)
            {
                bool first = shownObjective.Length == 0;
                shownObjective = objective;
                if (!first) objectiveSince = now;
            }
            Copy(task, objective);
            bool updated = now - objectiveSince < 3.2f;
            objectiveBlock.EnableInClassList("objective--updated", updated);
            Copy(objectiveTag, state.NightLabel + (updated ? "　·　目标更新" : "　·　当前目标"));
            objectiveBlock.EnableInClassList("objective--quiet", now - Mathf.Max(objectiveSince, arrival) > 14 && !game.InPursuit);
            Copy(location, LocationName());
            Copy(mapFloor, NightAtlas.FloorNames[game.Atlas.Floor]);
            Copy(mapPlace, game.Atlas.Place);

            var area = game.Atlas.Current;
            string key = area != null && !area.Route && !area.Stair ? area.Key : "";
            if (key != shownArea)
            {
                shownArea = key;
                if (key.Length > 0 && now - arrival > 5.5f && (!areaSeen.TryGetValue(key, out float seen) || now - seen > 45))
                {
                    areaSeen[key] = now;
                    Copy(areaFloor, NightAtlas.FloorNames[game.Atlas.Floor]);
                    Copy(areaName, area.Name);
                    areaUntil = now + 3.3f;
                }
            }
            areaCard.EnableInClassList("area-card--shown", now < areaUntil);

            bool pursuing = game.InPursuit;
            Visible(status, pursuing);
            if (pursuing) status.style.opacity = .62f + .38f * Mathf.Sin(now * 6.4f);
            pursuitVignette.Tick(pursuing);

            if (player.Stamina < .995f || player.Sprinting || player.Exhausted) staminaSeen = now;
            staminaBlock.EnableInClassList("stamina--shown", now - staminaSeen < 1.3f);
            stamina.Set(player.Stamina, player.Exhausted || player.Stamina < .20f);

            float amount = state.torch ? Mathf.Clamp01(state.charge / NightState.FullTorchCharge) : 0f;
            torch.Set(amount, state.torch && amount < .11f);
            Copy(charge, state.torch ? Percent(amount) : "—");
            torchSlot.EnableInClassList("torch-slot--lit", state.torch && player.TorchOn);
            torchSlot.EnableInClassList("torch-slot--low", state.torch && amount < .11f);
            runHint.EnableInClassList("hotkey--active", player.Sprinting);
            crouchHint.EnableInClassList("hotkey--active", player.Crouched);
            Copy(cellCount, "× " + state.cells);
            Copy(stoneCount, "× " + state.stones);

            var pickup = game.LastPickup;
            if (pickup.Serial != shownPickup)
            {
                shownPickup = pickup.Serial;
                SetItemArt(pickupArt, pickup.Art);
                Copy(pickupText, pickup.Text);
                pickupUntil = now + 3.4f;
            }
            pickupCard.EnableInClassList("pickup--shown", now < pickupUntil);

            var focus = game.Focus;
            var repair = game.RepairView;
            Visible(reticle, repair == null);
            reticle.Set(focus != null, game.Hold);
            reticle.Tick();
            Visible(prompt, focus != null);
            if (focus != null)
            {
                Copy(promptText, focus.Label);
                Visible(promptHold, focus.Hold > 0 && game.Hold <= 0);
            }
            DrawRepair(repair);

            // Heading-up minimap: the plan turns under a fixed arrow, and north circles the bezel.
            float yaw = player.transform.eulerAngles.y;
            minimap.style.rotate = new Rotate(new Angle(-yaw, AngleUnit.Degree));
            float ring = minimapRing.layout.width;
            if (ring > 1)
            {
                float angle = -yaw * Mathf.Deg2Rad, radius = ring * .5f + 1;
                north.style.left = ring * .5f + Mathf.Sin(angle) * radius - 9;
                north.style.top = ring * .5f - Mathf.Cos(angle) * radius - 9;
            }
            if (now >= nextMapDraw)
            {
                nextMapDraw = now + .075f;
                minimap.MarkDirtyRepaint();
            }
            UpdateArrival();
        }

        static string Objective(NightState state)
        {
            if (state.detained) return "拨开门上的卡簧锁";
            if (state.rescueTime == 0) return "从车侧登车";
            if (state.beacon) return "等待救援车";
            if (state.contacted) return "去街尾翻起接应板";
            if (!state.torch) return "在更衣室找手电";
            if (!state.power && state.motorStage == 0 && !state.motorPart && !state.collected.Contains("receipt"))
                return "查看前台的故障单";
            if (!state.power) return "恢复浴场供电";
            if (!state.annexIsolated) return "去副楼二楼隔离受潮线路";
            if (!state.collected.Contains(NightGame.AnnexDiagramId) && !state.annexRelay) return "找留档室的外线接线图";
            if (!state.annexRelay) return "接通副楼三楼的中继机";
            if (!state.uplink) return "接通三楼弱电间，或到街外值守点";
            return "到前台或街外值守点求援";
        }

        void Resume() => game.SetScreen(NightScreen.Play);
        static string Percent(float value) => Mathf.CeilToInt(Mathf.Clamp01(value) * 100) + "%";

        static void Quit()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        sealed class Meter
        {
            internal VisualElement Track { get; }
            readonly VisualElement fill;
            float last = -1;

            internal Meter(VisualElement parent, string track, string fillStyle, int segments = 0)
            {
                Track = Area(parent, track);
                fill = Area(Track, "meter-fill " + fillStyle);
                if (segments <= 1) return;
                var ruling = Area(Track, "screen meter-ruling");
                ruling.generateVisualContent += context =>
                {
                    var painter = context.painter2D;
                    painter.strokeColor = new Color(.02f, .025f, .022f, .95f);
                    painter.lineWidth = 2;
                    for (int mark = 1; mark < segments; ++mark)
                    {
                        float x = ruling.contentRect.width * mark / segments;
                        painter.BeginPath();
                        painter.MoveTo(new Vector2(x, 0));
                        painter.LineTo(new Vector2(x, ruling.contentRect.height));
                        painter.Stroke();
                    }
                };
            }

            internal void Set(float fraction, bool low = false)
            {
                if (!Mathf.Approximately(last, fraction))
                {
                    last = fraction;
                    fill.style.width = Length.Percent(fraction * 100);
                }
                fill.EnableInClassList("meter-fill--low", low);
            }
        }
    }
}
