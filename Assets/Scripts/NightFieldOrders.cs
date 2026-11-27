namespace AfterHours
{
    internal enum FieldOrder { Lights, Drain, Barrier, Radio }

    // One source of directions for the field HUD, map docket and pause screen.
    internal static class NightFieldOrders
    {
        internal static readonly FieldOrder[] List = { FieldOrder.Lights, FieldOrder.Drain, FieldOrder.Barrier, FieldOrder.Radio };

        internal static string Name(FieldOrder order) => order switch
        {
            FieldOrder.Lights => "引路灯",
            FieldOrder.Drain => "街沟排水",
            FieldOrder.Barrier => "街尾路障",
            _ => "联络司机"
        };

        internal static bool Done(FieldOrder order, NightState state) => order switch
        {
            FieldOrder.Lights => state.dispatch.AllRunning,
            FieldOrder.Drain => state.Drained,
            FieldOrder.Barrier => state.dispatch.teeth >= NightDispatch.Teeth,
            _ => state.contacted
        };

        internal static FieldOrder Tracking(NightState state)
        {
            var selected = state.dispatch.tracked;
            if (!Done(selected, state)) return selected;
            foreach (var order in List) if (!Done(order, state)) return order;
            return FieldOrder.Radio;
        }

        internal static (string action, string place) Direction(FieldOrder order, NightState state)
        {
            var job = state.dispatch;
            switch (order)
            {
                case FieldOrder.Lights:
                    return ("启动街区的四台机组", "M 地图　·　机组位置与夜班工单");
                case FieldOrder.Drain:
                    if (state.power && state.sinkStage == 2) return ("启动排水泵", "地下室 · 循环泵房");
                    int line = (job.sealedLines & 1) == 0 ? 0 : (job.sealedLines & 2) == 0 ? 1 : 2;
                    return ("听水声，关上漏水的阀门", NightReturnLines.Zones[line]);
                case FieldOrder.Barrier:
                    if (job.delivered) return ("摇绞盘，抬起路障", "街尾 · 桥杆");
                    if (job.carrying) return ("把配重挂上绞盘", "街尾 · 桥杆");
                    return (job.loadFeet == NightDispatch.LoadDepot ? "去街边泵房取配重" : "取回放下的配重", "配重位置已标在地图上");
                default:
                    if (!state.power)
                    {
                        if (state.motorStage == 0 && !state.motorPart) return ("取发电机启动件", "地下室 · 检修工坊");
                        if (state.motorStage == 3) return ("合上配电开关", "地下室 · 配电间");
                        string action = state.motorStage == 0 ? "装上启动件" : state.motorStage == 1 ? "校准飞轮" : "启动发电机";
                        return (action, "地下室 · 发电机房");
                    }
                    if (!state.annexIsolated) return ("切断受潮的线路", "副楼二层 · 机修间");
                    if (!state.annexRelay) return ("用定向天线联络司机", "街尾 · 维修站");
                    if (!state.uplink) return ("接通外线", "主楼三层 · 弱电间");
                    return ("给司机打电话", "一楼 · 前台");
            }
        }

        internal static (string action, string place) Current(NightState state)
        {
            if (state.detained) return ("拨开门上的卡簧锁", "地下室 · 门边的橙灯");
            if (state.rescueTime == 0) return ("上救援车", "街尾 · 站牌");
            if (state.beacon) return ("等救援车靠站", "街尾 · 站牌");
            if (!state.dispatch.accepted)
                return !state.torch ? ("拿起长凳上的手电", "二楼 · 更衣室") : ("接听夜班电台", "一楼 · 前台");
            if (state.RescueAccess && state.contacted) return ("翻起接应板", "街尾 · 站牌");
            return Direction(Tracking(state), state);
        }
    }
}
