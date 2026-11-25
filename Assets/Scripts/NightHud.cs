
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed partial class NightInterface
    {
        readonly NightGame game;
        readonly UIDocument document;
        readonly PanelSettings settings;
        readonly Font displayFont;
        readonly Texture2D titleImage;
        readonly VisualElement root;
        const string InterfaceFolder = "Interface/";
        Label task, location, mapFloor, mapPlace, movement, toast, life, charge, cellCount, stoneCount;
        Label promptText;
        VisualElement prompt, progress, status, sights, repairSlot, titleBackdrop;
        NightTarget previousFocus;
        Meter stamina, torch, interaction;
        NightMapView minimap;
        NightPursuitVignette pursuitVignette;
        Button[] mapTabs;
        NightMapView fullMap;
        NightGame.RepairPanel shownRepair;
        int repairRow = -1, repairOption = -1;
        string subpage = "";
        float nextMapDraw;

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
            displayFont = Resources.Load<Font>(InterfaceFolder + "TitleTypeface");
            titleImage = Resources.Load<Texture2D>(InterfaceFolder + "AfterHoursTitle");
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

        static Label Type(VisualElement parent, string text, string classes)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            Classes(label, classes);
            parent.Add(label);
            return label;
        }

        static void Classes(VisualElement element, string classes)
        {
            foreach (string name in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries)) element.AddToClassList(name);
        }

        static Button Act(VisualElement parent, string title, Action run, string classes, string name = "")
        {
            var button = new Button(run) { text = title, name = name };
            Classes(button, classes);
            parent.Add(button);
            return button;
        }

        static void Visible(VisualElement element, bool shown)
        {
            var display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (element.style.display.value != display) element.style.display = display;
        }

        static void Copy(Label element, string value)
        {
            if (element.text != value) element.text = value;
        }

        internal void Show(NightScreen screen)
        {
            subpage = "";
            Draw(screen);
        }

        void Draw(NightScreen screen)
        {
            root.Clear();
            toast = null;
            titleBackdrop = null;
            previousFocus = null;
            if (screen == NightScreen.Play)
            {
                BuildHud();
                return;
            }

            if (screen == NightScreen.Capture) return;
            if (screen == NightScreen.Title)
            {
                BuildTitle();
                return;
            }

            var veil = Area(root, "screen veil");
            if (screen == NightScreen.Inventory)
            {
                BuildInventory(veil);
                return;
            }
            if (screen == NightScreen.Map)
            {
                veil.AddToClassList("atlas-veil");
                BuildMap(veil);
                return;
            }

            var card = Area(veil, "menu-card duty-menu");
            Reveal(card);
            if (screen == NightScreen.Pause) veil.AddToClassList("pause-veil");
            if (BuildSubpage(card)) return;
            if (screen == NightScreen.Pause)
            {
                Type(card, LocationName(), "overline");
                Type(card, "暂停", "panel-heading");
                Act(card, "继续游戏", Resume, "menu-action primary", "resume");
                Act(card, "保存进度", game.SaveHere, "menu-action", "save");
                Act(card, "选项", () => Open("settings"), "menu-action");
                Act(card, "操作", () => Open("controls"), "menu-action");
                Act(card, "返回标题", () => Open("leave"), "menu-action");
                toast = Type(card, "", "menu-notice");
                return;
            }

            bool safe = screen == NightScreen.Escaped;
            Type(card, safe ? "第一夜结束" : "第一夜", "overline");
            Type(card, safe ? "天亮以前" : "它找到你了", "panel-heading");
            Type(card, safe ? "车门关上了。浴场的灯从后窗消失。" : "走廊里只剩它的笑声。", "scene-caption");
            string action = safe ? "再玩一次" : game.ResumableSave != null ? "从存档继续" : "重试";
            Act(card, action, safe ? game.NewNight : game.ContinueNight, "menu-action primary", "retry");
            Act(card, "返回标题", () => game.SetScreen(NightScreen.Title), "menu-action");
        }

        void BuildTitle()
        {
            var theater = Area(root, "screen theater");
            titleBackdrop = Area(theater, "screen title-backdrop");
            titleBackdrop.style.backgroundImage = new StyleBackground(titleImage);
            var scrim = Area(theater, "title-scrim");
            var left = Area(scrim, "title-layout");
            Reveal(left);
            Type(left, "奶娃浴场", "title-brand");
            var logotype = Type(left, "打烊后", "title-name");
            if (displayFont)
            {
                logotype.style.unityFont = displayFont;
                logotype.style.unityFontDefinition = FontDefinition.FromFont(displayFont);
            }
            Type(left, "第一夜", "title-chapter");
            Area(left, "title-rule");

            if (subpage.Length != 0)
            {
                BuildSubpage(Area(scrim, "menu-card title-dialog duty-menu"));
                return;
            }

            var choices = Area(left, "title-choices");
            if (game.ResumableSave != null)
                Act(choices, "继续游戏", game.ContinueNight, "title-action primary", "continue-night");
            Act(choices, "开始新一夜", () =>
            {
                if (game.ResumableSave == null) game.NewNight();
                else Open("restart");
            }, game.ResumableSave == null ? "title-action primary" : "title-action", "new-night");

            var row = Area(left, "title-links");
            Act(row, "选项", () => Open("settings"), "text-button");
            Act(row, "操作", () => Open("controls"), "text-button");
            Act(row, "退出", () => Open("quit"), "text-button");
        }

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

        bool BuildSubpage(VisualElement card)
        {
            switch (subpage)
            {
                case "settings":
                    Type(card, "选项", "panel-heading");
                    var preferences = game.Preferences;
                    Slider(card, "鼠标灵敏度", .5f, 5, preferences.Sensitivity, preferences.SetSensitivity, v => v.ToString("0.0"));
                    Slider(card, "镜头晃动", 0, 1, preferences.CameraMotion, preferences.SetMotion, Percent);
                    Slider(card, "视野", 50, 82, preferences.FieldOfView, preferences.SetFov, v => Mathf.RoundToInt(v) + "°");
                    Slider(card, "音量", 0, 1, preferences.Volume, preferences.SetVolume, Percent);
                    var quality = new Toggle("灯光柔晕") { name = "practical-glow", value = preferences.PracticalGlow };
                    quality.AddToClassList("quality-toggle");
                    quality.RegisterValueChangedCallback(evt =>
                    {
                        preferences.SetPracticalGlow(evt.newValue);
                        game.ApplyPreferences();
                    });
                    card.Add(quality);
                    break;
                case "controls":
                    Type(card, "操作", "panel-heading");
                    var controls = Area(card, "control-list");
                    foreach (var entry in new (string keys, string what)[]
                    {
                        ("W A S D", "移动"), ("鼠标", "观察"), ("Shift", "按住奔跑"), ("C", "切换蹲行"),
                        ("E", "拾取 / 交互 / 翻窗"), ("F", "开关手电"), ("R", "更换电池"), ("G", "投掷石子"),
                        ("B", "背包"), ("M", "地图"), ("F5", "保存"), ("Esc", "暂停")
                    })
                    {
                        var row = Area(controls, "control-row");
                        Type(row, entry.keys, "key control-key");
                        Type(row, entry.what, "control-description");
                    }
                    break;
                case "restart":
                    Type(card, "开始新一夜？", "panel-heading");
                    Type(card, "下一次保存会覆盖本地进度。", "scene-caption");
                    Act(card, "开始", game.NewNight, "menu-action primary", "start-over");
                    break;
                case "leave":
                    Type(card, "返回标题？", "panel-heading");
                    Type(card, "下次会从最近一次保存的位置继续。", "scene-caption");
                    Act(card, "返回标题", () => game.SetScreen(NightScreen.Title), "menu-action primary");
                    break;
                case "quit":
                    Type(card, "退出游戏？", "panel-heading");
                    Act(card, "退出", Quit, "menu-action primary");
                    break;
                default:
                    return false;
            }

            Act(card, "返回", () => CloseSubpage(), "menu-action", "back");
            return true;
        }

        void Slider(VisualElement parent, string title, float min, float max, float value, Action<float> set, Func<float, string> format)
        {
            var row = Area(parent, "setting-row");
            var top = Area(row, "line");
            Type(top, title, "setting-name");
            var number = Type(top, format(value), "meter-value");
            var slider = new Slider(min, max) { value = value, tooltip = title };
            slider.AddToClassList("setting-slider");
            row.Add(slider);
            slider.RegisterValueChangedCallback(e =>
            {
                set(e.newValue);
                game.ApplyPreferences();
                Copy(number, format(e.newValue));
            });
        }

        void BuildHud()
        {
            var layer = Area(root, "screen play-hud");
            pursuitVignette = new NightPursuitVignette();
            pursuitVignette.AddToClassList("screen");
            layer.Add(pursuitVignette);
            var mission = Area(layer, "mission");
            location = Type(mission, "", "location");
            task = Type(mission, "", "objective");
            status = Type(mission, "脚步逼近", "danger");
            Visible(status, false);

            var mapBox = Area(layer, "minimap");
            var mapHeader = Area(mapBox, "line map-header");
            mapFloor = Type(mapHeader, "", "map-floor");
            Type(mapHeader, "N ↑", "map-north");
            minimap = new NightMapView(game, true);
            mapBox.Add(minimap);
            var mapFooter = Area(mapBox, "line map-footer");
            mapPlace = Type(mapFooter, "", "map-place");
            Type(mapFooter, "M", "key");

            sights = Area(layer, "crosshair");
            prompt = Area(layer, "prompt");
            var promptLine = Area(prompt, "prompt-line");
            Type(promptLine, "E", "key");
            promptText = Type(promptLine, "", "prompt-text");
            interaction = new Meter(prompt, "hold", "hold-fill");
            progress = interaction.Track;
            toast = Type(layer, "", "toast");
            repairSlot = Area(layer, "repair");
            shownRepair = null;
            repairRow = repairOption = -1;
            Visible(repairSlot, false);

            var equipment = Area(layer, "equipment");
            var staminaRow = Area(equipment, "line meter-label");
            Type(staminaRow, "体力", "meter-title");
            life = Type(staminaRow, "100%", "meter-value");
            stamina = new Meter(equipment, "meter-track", "stamina-fill", true);
            var batteryRow = Area(equipment, "line meter-label battery-label");
            Type(batteryRow, "手电", "meter-title");
            charge = Type(batteryRow, "", "meter-value");
            torch = new Meter(equipment, "meter-track", "torch-fill", true);
            movement = Type(equipment, "", "movement");

            var shortcuts = Area(layer, "shortcuts");
            var items = Area(shortcuts, "item-row");
            Slot(items, "F", "手电");
            cellCount = Slot(items, "R", "电池", counts: true);
            stoneCount = Slot(items, "G", "石子", counts: true);
            var input = Area(shortcuts, "hotkey-row");
            Bind(input, "Shift", "奔跑");
            Bind(input, "C", "蹲行");
            Bind(input, "B", "背包");
            Bind(input, "Esc", "暂停");
            nextMapDraw = 0;
        }

        Label Slot(VisualElement row, string key, string label, bool counts = false)
        {
            var box = Area(row, "item-slot");
            string icon = key == "F" ? "torch" : key == "R" ? "cell" : "stone";
            SetItemArt(Area(box, "quick-art"), icon);
            var top = Area(box, "line");
            Type(top, key, "key");
            var count = counts ? Type(top, "", "item-count") : null;
            Type(box, label, "item-name");
            return count;
        }

        static void Bind(VisualElement row, string key, string label)
        {
            var item = Area(row, "hotkey");
            Type(item, key, "key");
            Type(item, label, "hotkey-name");
        }

        void BuildMap(VisualElement veil)
        {
            var card = Area(veil, "atlas-card");
            Reveal(card);
            var header = Area(card, "line atlas-header");
            var left = Area(header, "atlas-title");
            Type(left, "位置 · " + LocationName(), "overline");
            Type(left, "浴场地图", "panel-heading");
            Act(header, "M　收起", Resume, "menu-action atlas-close", "close-map");
            var tabs = Area(card, "floor-tabs");
            mapTabs = new Button[NightAtlas.FloorNames.Length];
            for (int floor = 0; floor < mapTabs.Length; ++floor)
            {
                int sheet = floor;
                mapTabs[floor] = Act(tabs, NightAtlas.FloorNames[floor], () => OpenFloor(sheet), "floor-tab");
            }
            fullMap = new NightMapView(game, false);
            card.Add(fullMap);
            var footer = Area(card, "line atlas-footer");
            Type(footer, "滚轮缩放 · 拖动地图", "muted");
            var zoom = Area(footer, "atlas-tools");
            Act(zoom, "−", () => fullMap.Zoom(1 / 1.26f), "atlas-control", "map-zoom-out");
            Act(zoom, "+", () => fullMap.Zoom(1.26f), "atlas-control", "map-zoom-in");
            Act(zoom, "复位", fullMap.ResetView, "atlas-control", "map-reset");
            Type(footer, "N ↑", "map-north");
            OpenFloor(game.Atlas.Floor);
        }

        void OpenFloor(int floor)
        {
            for (int i = 0; i < mapTabs.Length; ++i) mapTabs[i].EnableInClassList("floor-tab--selected", i == floor);
            fullMap.SetSheet(floor);
        }

        static void Reveal(VisualElement element)
        {
            element.AddToClassList("duty-entrance");
            element.schedule.Execute(() => element.RemoveFromClassList("duty-entrance")).StartingIn(24);
        }

        void DrawRepair(NightGame.RepairPanel current)
        {
            Visible(repairSlot, current != null);
            if (current == null || shownRepair == current && repairRow == current.row && repairOption == current.selected) return;
            shownRepair = current;
            repairRow = current.row;
            repairOption = current.selected;
            repairSlot.Clear();
            Type(repairSlot, current.title, "repair-title");
            Type(repairSlot, current.prompts[current.row], "repair-detail");
            var options = Area(repairSlot, "repair-options");
            for (int i = 0; i < current.options.Length; ++i)
            {
                var option = Type(options, current.options[i], "repair-option");
                option.EnableInClassList("repair-option--selected", i == current.selected);
            }
            Type(repairSlot, "A / D 切换　·　空格确认　·　Esc 松手", "muted");
        }

        string LocationName() => NightAtlas.FloorNames[game.Atlas.Floor] + " · " + game.Atlas.Place;

        internal void Tick()
        {
            var screen = game.ScreenState;
            if (toast != null)
            {
                string message = game.Message;
                if (screen == NightScreen.Play && message.Length > 0 && toast.text != message) Reveal(toast);
                Copy(toast, message);
                Visible(toast, message.Length > 0);
            }
            if (screen == NightScreen.Title && titleBackdrop != null)
            {
                float motion = game.Preferences.CameraMotion * (.5f + .5f * Mathf.Sin(Time.unscaledTime * .19f));
                titleBackdrop.style.scale = new Scale(Vector3.one * (1 + motion * .034f));
            }
            if (screen != NightScreen.Play) return;

            var player = game.Player;
            var state = game.State;
            Copy(task, Objective(state));
            Copy(location, LocationName());
            Copy(mapFloor, NightAtlas.FloorNames[game.Atlas.Floor]);
            Copy(mapPlace, game.Atlas.Place);
            bool pursuing = game.InPursuit;
            Visible(status, pursuing);
            pursuitVignette.Tick(pursuing);
            stamina.Set(player.Stamina, player.Exhausted);
            Copy(life, Percent(player.Stamina));
            float amount = Mathf.Clamp01(state.charge / NightState.FullTorchCharge);
            torch.Set(amount, state.torch && amount < .11f);
            Copy(charge, state.torch ? Percent(amount) : "未拾取");
            Copy(movement, player.Crouched ? "蹲行" : player.Sprinting ? "奔跑" : player.Exhausted ? "喘息" : "步行");
            Copy(cellCount, state.cells.ToString());
            Copy(stoneCount, state.stones.ToString());
            var focus = game.Focus;
            var repair = game.RepairView;
            Visible(sights, repair == null);
            Visible(prompt, focus != null);
            if (focus != null)
            {
                if (focus != previousFocus) Reveal(prompt);
                Copy(promptText, focus.Label);
                Visible(progress, focus.Hold > 0);
                interaction.Set(game.Hold);
            }
            previousFocus = focus;
            DrawRepair(repair);

            if (Time.unscaledTime >= nextMapDraw)
            {
                nextMapDraw = Time.unscaledTime + .075f;
                minimap.MarkDirtyRepaint();
            }
        }

        static string Objective(NightState state)
        {
            if (state.rescueTime == 0) return "从车侧登车";
            if (state.beacon) return "等待救援车";
            if (state.contacted) return "去街尾翻起接应板";
            if (!state.torch) return "在更衣室找手电";
            if (!state.power && state.motorStage == 0 && !state.motorPart && !state.collected.Contains("receipt"))
                return "查看前台的故障单";
            if (!state.power) return "恢复浴场供电";
            if (!state.uplink) return "接通三楼线路";
            return "回前台发送求援";
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

            internal Meter(VisualElement parent, string track, string fillStyle, bool graduated = false)
            {
                Track = Area(parent, track);
                fill = Area(Track, "meter-fill " + fillStyle);
                if (!graduated) return;
                var ruling = Area(Track, "screen meter-ruling");
                ruling.generateVisualContent += context =>
                {
                    var painter = context.painter2D;
                    painter.strokeColor = new Color(.047f, .064f, .051f, .72f);
                    painter.lineWidth = 1;
                    for (int mark = 1; mark < 10; ++mark)
                    {
                        float x = ruling.contentRect.width * mark / 10f;
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
