namespace AfterHours
{
    internal enum FieldOrder { Lights, Drain, Barrier, Radio, Survey, Archive, Ledger }

    // One source of directions for the field HUD, map docket and pause screen.
    internal static class NightFieldOrders
    {
        internal static readonly FieldOrder[] List = { FieldOrder.Lights, FieldOrder.Drain, FieldOrder.Barrier, FieldOrder.Radio, FieldOrder.Survey, FieldOrder.Archive, FieldOrder.Ledger };

        internal static string Name(FieldOrder order) => order switch
        {
            FieldOrder.Ledger => "「第十三条毛巾」",
            FieldOrder.Survey => "「水线为证」",
            FieldOrder.Archive => "「底片上的人」",
            FieldOrder.Lights => "「街灯次第亮」",
            FieldOrder.Drain => "「积水退去」",
            FieldOrder.Barrier => "「放车过桥」",
            _ => "「听见回话」"
        };

        internal static bool Done(FieldOrder order, NightState state) => order switch
        {
            FieldOrder.Ledger => state.story.ledger,
            FieldOrder.Survey => state.survey.ReportReady,
            FieldOrder.Archive => state.survey.archive,
            FieldOrder.Lights => state.dispatch.AllRunning,
            FieldOrder.Drain => state.Drained,
            FieldOrder.Barrier => state.dispatch.teeth >= NightDispatch.Teeth,
            _ => state.contacted
        };

        internal static FieldOrder Tracking(NightState state) => state.dispatch.tracked;

        internal static (string action, string place) Direction(FieldOrder order, NightState state)
        {
            var job = state.dispatch;
            switch (order)
            {
                case FieldOrder.Ledger: return state.story.receipt
                    ? ("把收洗数填回缺页", "主楼三层 · 值班室")
                    : ("找盖过章的那一联", "后巷洗衣店 · 收付台");
                case FieldOrder.Survey: return RiverSurvey.Lead(state);
                case FieldOrder.Archive:
                    if (!state.survey.camera) return RiverSurvey.Lead(state);
                    for (int i=3;i<6;i++)
                        if (!RiverSurvey.Recorded(state,i)) return ("按铜钉的位置复拍旧址", RiverSurvey.Plates[i].Name + "　·　V 取景");
                    if (!state.survey.spliced) return ("拼好玻璃底片", "守闸宿舍 · 看片灯台");
                    return ("印出夜班合影", "旧照相馆 · 印相台");
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
            if (state.CanBoard) return ("上救援车", "街尾 · 站牌");
            if (state.beacon) return ("听街口的车喇叭", "街尾 · 站牌");
            if (state.dispatch.orderHeld) return Direction(Tracking(state), state);
            if (state.RescueAccess && state.contacted) return ("翻起接应板", "街尾 · 站牌");
            return NightShiftTrail.Next(state);
        }
    }
}
