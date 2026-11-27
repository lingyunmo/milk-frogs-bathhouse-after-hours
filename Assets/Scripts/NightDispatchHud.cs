using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed partial class NightInterface
    {
        VisualElement dispatchFeed, dispatchStrip, fieldWork;
        Label feedText, loadText, drainText, instrumentTitle, instrumentReadout, instrumentKeys;
        readonly Meter[] stationMeters = new Meter[4];
        readonly Label[] stationLabels = new Label[4];
        Meter instrumentMeter;
        NightDetentionLock synchronizer;
        int dialStation = -1, lastCount = -1, lastTeeth = -1;
        bool lastCarrying, lastDelivered;
        int lastDrain;

        void BuildDispatchHud(VisualElement layer)
        {
            dispatchStrip = Area(layer, "station-strip");
            for (int i = 0; i < stationMeters.Length; i++)
            {
                var column = Area(dispatchStrip, "station-channel");
                stationLabels[i] = Type(column, NightDispatch.Stations[i].Name, "station-name");
                stationMeters[i] = new Meter(column, "station-track", "station-fill", NightDispatch.Contacts);
            }
            dispatchFeed = Area(layer, "dispatch-feed");
            Type(dispatchFeed, "夜班调度", "dispatch-heading serif");
            feedText = Type(dispatchFeed, "", "dispatch-line");
            drainText = Type(dispatchFeed, "", "dispatch-line");
            loadText = Type(dispatchFeed, "", "dispatch-line");
            fieldWork = Area(layer, "field-instrument");
            instrumentTitle = Type(fieldWork, "", "dispatch-heading serif");
            instrumentReadout = Type(fieldWork, "", "instrument-reading");
            instrumentMeter = new Meter(fieldWork, "station-track", "station-fill");
            instrumentKeys = Type(fieldWork, "", "dispatch-line");
            Visible(dispatchStrip, false);
            Visible(dispatchFeed, false);
            Visible(fieldWork, false);
            synchronizer = null;
            dialStation = lastCount = lastTeeth = lastDrain = -1;
        }

        void TickDispatchHud()
        {
            var state = game.State;
            var job = state.dispatch;
            bool show = job.accepted && !state.detained && !state.beacon;
            Visible(dispatchStrip, show && !job.AllRunning);
            Visible(dispatchFeed, show && !state.RescueAccess);
            if (show)
            {
                int drain = state.Drained ? 7 : job.sealedLines;
                if (drain != lastDrain)
                {
                    int pending = (drain & 1) == 0 ? 0 : (drain & 2) == 0 ? 1 : 2;
                    Copy(drainText, state.Drained ? "街沟已排空" : "听漏 · " + NightReturnLines.Zones[pending] + "　/　开泵排水");
                    lastDrain = drain;
                }
                int count = job.Running;
                if (count != lastCount || job.teeth != lastTeeth || job.carrying != lastCarrying || job.delivered != lastDelivered)
                {
                    Copy(feedText, $"引路机组　{count} / 4");
                    Copy(loadText, job.teeth == NightDispatch.Teeth ? "桥杆已抬起"
                        : job.delivered ? $"摇起桥杆　{job.teeth} / {NightDispatch.Teeth}"
                        : job.carrying ? "把配重送到街尾桥杆　·　Q 放下" : "泵房取配重 → 街尾桥杆");
                    lastCount = count;
                    lastTeeth = job.teeth;
                    lastCarrying = job.carrying;
                    lastDelivered = job.delivered;
                }
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
