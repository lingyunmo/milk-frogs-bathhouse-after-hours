using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed partial class NightInterface
    {
        VisualElement dispatchStrip, fieldWork;
        Label instrumentTitle, instrumentReadout, instrumentKeys;
        readonly Meter[] stationMeters = new Meter[4];
        readonly Label[] stationLabels = new Label[4];
        Meter instrumentMeter;
        NightDetentionLock synchronizer;
        int dialStation = -1;
        float dispatchHeardUntil = -1;

        void BuildDispatchHud(VisualElement layer)
        {
            dispatchStrip = Area(layer, "station-strip");
            for (int i = 0; i < stationMeters.Length; i++)
            {
                var column = Area(dispatchStrip, "station-channel");
                stationLabels[i] = Type(column, NightDispatch.Stations[i].Name, "station-name");
                stationMeters[i] = new Meter(column, "station-track", "station-fill", NightDispatch.Contacts);
            }
            BuildSurveyHud(layer);
            fieldWork = Area(layer, "field-instrument");
            instrumentTitle = Type(fieldWork, "", "dispatch-heading serif");
            instrumentReadout = Type(fieldWork, "", "instrument-reading");
            instrumentMeter = new Meter(fieldWork, "station-track", "station-fill");
            instrumentKeys = Type(fieldWork, "", "dispatch-line");
            Visible(dispatchStrip, false);
            Visible(fieldWork, false);
            synchronizer = null;
            dialStation = -1;
        }

        internal void ReceiveDispatch() => dispatchHeardUntil = Time.unscaledTime + 4f;

        static string OrderName(FieldOrder order) => order switch
        {
            FieldOrder.Survey => "东堤水尺",
            FieldOrder.Archive => "八七年旧照",
            FieldOrder.Lights => "街区引路灯",
            FieldOrder.Drain => "街沟排水",
            FieldOrder.Barrier => "街尾路障",
            _ => "联络司机"
        };

        static readonly float[] CallSignHeights = { 4, 10, 17, 8, 4 };

        static void DrawCallSign(MeshGenerationContext context)
        {
            var pen = context.painter2D;
            pen.strokeColor = new Color(.88f, .73f, .51f);
            pen.lineWidth = 2;
            pen.lineCap = LineCap.Round;
            for (int i = 0; i < CallSignHeights.Length; i++)
            {
                float half = CallSignHeights[i] * .5f;
                float x = 2 + i * 4;
                pen.BeginPath();
                pen.MoveTo(new Vector2(x, 10 - half));
                pen.LineTo(new Vector2(x, 10 + half));
                pen.Stroke();
            }
        }

        void BuildOrderBook(VisualElement card)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("order-book");
            card.Add(scroll);
            var state = game.State;
            var selected = NightFieldOrders.Tracking(state);
            Button first = null;
            foreach (FieldOrder order in NightFieldOrders.List)
            {
                if (order == FieldOrder.Archive && !state.survey.camera) continue;
                bool done = NightFieldOrders.Done(order, state);
                var row = Act(scroll.contentContainer, "", () =>
                {
                    game.TrackOrder(order);
                    Open("");
                    if (order is FieldOrder.Lights or FieldOrder.Survey or FieldOrder.Archive) OpenFloor(NightAtlas.Street);
                    if (order == FieldOrder.Barrier)
                    {
                        var job = state.dispatch;
                        OpenFloor(game.Atlas.FloorAt(job.delivered || job.carrying ? NightDispatch.Barrier : job.loadFeet));
                    }
                }, "order-entry", "order-" + order);
                var copy = Area(row, "order-copy");
                Type(copy, order == FieldOrder.Archive ? "八七年旧照　·　选做" : OrderName(order), "order-name serif");
                if (!done)
                {
                    var lead = NightFieldOrders.Direction(order, state);
                    string note = order == FieldOrder.Drain && !(state.power && state.sinkStage == 2)
                        ? lead.place + "　/　或修水槽，开排水泵"
                        : order == FieldOrder.Radio && state.power && state.annexIsolated && !state.annexRelay
                            ? lead.place + "　/　也可修复副楼中继机"
                            : order == FieldOrder.Lights ? "机位已标在街区地图上" : lead.place;
                    Type(copy, lead.action + "　·　" + note, "order-note");
                }
                Type(row, done ? "完工" : selected == order ? "跟踪中" : "跟踪", "order-stamp");
                row.EnableInClassList("order-entry--tracked", !done && selected == order);
                row.SetEnabled(!done);
                if (selected == order && !done) first = row;
            }
            Focus(first ?? root.Q<Button>("switch-orders"));
        }

        void TickDispatchHud()
        {
            var state = game.State;
            TickSurveyHud();
            var job = state.dispatch;
            bool show = job.accepted && !state.detained && !state.beacon;
            Visible(dispatchStrip, show && !job.AllRunning);
            if (show)
            {
                for (int i = 0; i < stationMeters.Length; i++)
                {
                    stationMeters[i].Set(job.steps[i] / (float)NightDispatch.Contacts);
                    stationLabels[i].EnableInClassList("station-live", job.steps[i] == NightDispatch.Contacts);
                    stationLabels[i].EnableInClassList("station-selected", i == game.ActiveGenerator);
                }
            }

            if (dialStation != game.ActiveGenerator)
            {
                synchronizer?.RemoveFromHierarchy();
                synchronizer = null;
                dialStation = game.ActiveGenerator;
                if (dialStation >= 0)
                {
                    synchronizer = new NightDetentionLock(2, null, game.CloseDispatch, game.GeneratorStroke,
                        dialStation, job.steps[dialStation]);
                    synchronizer.AddToClassList("field-synchronizer");
                    root.Add(synchronizer);
                }
            }
            if (Application.isFocused) synchronizer?.Tick(Time.deltaTime);
            Visible(fieldWork, game.Winding || game.Tuning);
            if (game.Winding)
            {
                Copy(instrumentTitle, "街尾绞盘");
                Copy(instrumentReadout, $"棘齿　{job.teeth} / {NightDispatch.Teeth}");
                instrumentMeter.Set((job.teeth + game.WoundFraction) / NightDispatch.Teeth);
                Copy(instrumentKeys, game.Player.Exhausted ? "松开 F，喘口气。" : "按住 F 摇动　·　松手回气　·　Esc 离开");
            }
            if (game.Tuning)
            {
                Copy(instrumentTitle, "街尾定向天线");
                Copy(instrumentReadout, $"{Mathf.RoundToInt(game.Bearing):D3}°　{(game.ChannelLock > 0 ? "正在接通" : "寻找载波")}");
                instrumentMeter.Set(game.Reception);
                Copy(instrumentKeys, "A / D 转向　·　信号最强时稳住　·　Esc 离开");
            }
        }
    }
}
