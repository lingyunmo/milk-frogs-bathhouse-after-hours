using UnityEngine;

namespace AfterHours
{
    internal enum BrineInquiryStep { Sealed, Key, Open, Fault, Card, Online }

    internal static class BrineInquiry
    {
        internal static readonly Rect Room = Rect.MinMaxRect(175, -8.5f, 182, -1.5f);
        internal static readonly Vector3 Door = new(177, 1.24f, -8.62f);
        internal static readonly Vector3 Key = new(194.55f, 1.47f, 7.66f);
        internal static readonly Vector3 Computer = new(179.5f, 1.08f, -2.39f);
        internal static readonly Vector3 Recorder = new(175.75f, 1.08f, -4.68f);
        internal static readonly Vector3 Display = new(179.46f, 2.17f, -1.82f);
        internal static bool CanCross(BrineRun run, Vector3 from, Vector3 to)
        {
            if (run.inquiry >= BrineInquiryStep.Open || Mathf.Min(from.y, to.y) > 2.4f ||
                Mathf.Max(from.x, to.x) < Door.x - 1.27f || Mathf.Min(from.x, to.x) > Door.x + 1.27f) return true;
            float a = from.z - Room.yMin, b = to.z - Room.yMin;
            if (Mathf.Min(a, b) > .31f || Mathf.Max(a, b) < -.31f) return true;
            float t = Mathf.Abs(b - a) < .0001f ? 0 : Mathf.Clamp01(-a / (b - a));
            return Mathf.Abs(Mathf.Lerp(from.x, to.x, t) - Door.x) > 1.27f;
        }

        internal const string LockNotice = "铅封上有个红章：要查墙上的老号码，到堤口防疫监测哨。";
        internal const string DoorNote = "钥匙借去量水了。还在水文站正门右手的黄板上，谁夜班谁带回来。";
        internal const string DisplayTitle = "巡检回放   /   87·07·17";
        internal const string DisplayWarning = "严禁把样芯接入南堤隔离闸";
        internal static (string action, string place)? NearbyLead(NightState state, Vector3 feet, Transform eyes)
        {
            var run = state.brine;
            if (state.detained || run.stage != BrineStage.Quiet) return null;
            bool LookingAt(Vector3 p, float range)
            {
                Vector3 delta = p - eyes.position;
                return delta.sqrMagnitude < range * range && Vector3.Dot(delta.normalized, eyes.forward) > .52f;
            }
            if (run.inquiry == BrineInquiryStep.Sealed && LookingAt(Key, 3.4f))
                return ("取下黄板上的值班匙", "水文站正门   /   防疫监测哨");
            if (!Room.Contains(new Vector2(feet.x, feet.z)) && !LookingAt(Door, 8.1f))
            {
                if (run.CanRecordMarks)
                    for (int i = 0; i < BrineRoute.Marks.Length; i++)
                        if ((run.rubbings & (1 << i)) == 0 && LookingAt(BrineRoute.Marks[i].Point, 4.1f))
                            return ("拓下交运号", BrineRoute.Marks[i].Place);
                return null;
            }
            return run.inquiry switch
            {
                BrineInquiryStep.Sealed => ("看看门边的借匙牌", "堤口   /   防疫监测哨"),
                BrineInquiryStep.Key => ("打开监测哨", "手里的黄签钥匙"),
                BrineInquiryStep.Open => ("检查巡检终端", "大屏下方   /   台式终端"),
                BrineInquiryStep.Fault => ("拆一块备用回放板", "西墙   /   退役录像机"),
                BrineInquiryStep.Card => ("装回板卡，切到回放", "大屏下方   /   台式终端"),
                _ when run.rubbings == 15 => ("拨开四位字轮", "照相馆东侧   /   冷样保管间"),
                _ => ("查找旧墙上的交运号", "四处留影   /   背包里有巡检抄页")
            };
        }

        internal static string Transcript(BrineRun run)
        {
            string page = DisplayTitle + "\n「最后一班巡完堤，把四段交运号留在外墙。看准井口，从进水头往下抄。」";
            for (int i = 0; i < BrineRoute.Marks.Length; i++)
                page += "\n" + BrineRoute.Marks[i].Place + "   " + ((run.rubbings & (1 << i)) == 0 ? "待勘" : BrineRoute.FieldRecord(i, run.wheel));
            return page + "\n\n收存处：照相馆以东，冷样保管间。\n" + DisplayWarning + "。合闸后，桥上不再放行。";
        }
    }

    internal sealed partial class NightGame
    {
        void BuildBrineInquiryTargets()
        {
            bool At(BrineInquiryStep step) => !State.detained && State.brine.stage == BrineStage.Quiet && State.brine.inquiry == step;
            void Advance(BrineInquiryStep next, Vector3 source, string sound, string line)
            {
                State.brine.inquiry = next;
                Emit(source, .43f, sound);
                world.Refresh();
                SaveProgress();
                Notify(line, 7);
            }
            Target(BrineInquiry.Door, "翻看借匙牌", .18f, () => Notify(BrineInquiry.DoorNote, 8),
                () => At(BrineInquiryStep.Sealed));
            Target(BrineInquiry.Key, "摘下监测哨铜匙", .38f, () =>
                Advance(BrineInquiryStep.Key, BrineInquiry.Key, "pickup", "钥匙牌翻过来，背面画着堤口那座双警示牌的小屋。"),
                () => At(BrineInquiryStep.Sealed));
            Target(BrineInquiry.Door, "拧开监测哨门锁", .65f, () =>
                Advance(BrineInquiryStep.Open, BrineInquiry.Door, "tool", "铁门松开了。大屏下的旧终端还在通电自检。"),
                () => At(BrineInquiryStep.Key));
            Target(BrineInquiry.Computer, "掀开终端检修盖", .50f, () =>
                Advance(BrineInquiryStep.Fault, BrineInquiry.Computer, "tool", "回放板烧穿了。蓝签上写着：「西墙退役录像机，同型号。许」"),
                () => At(BrineInquiryStep.Open));
            Target(BrineInquiry.Recorder, "抽出备用回放板", .65f, () =>
                Advance(BrineInquiryStep.Card, BrineInquiry.Recorder, "tool", "板卡抽出来了。终端那只烧黑的槽里，露着同样的蓝边。"),
                () => At(BrineInquiryStep.Fault));
            Target(BrineInquiry.Computer, "装回板卡并切换信号", .55f, () =>
                OpenRepair("巡检终端 · 输入选择", BrineInquiry.Computer, new[] { "现场镜头", "堤防回放", "测试格线" },
                    new[] { "画面跳出一行值班批注：「镜头早泡坏了。要找交运号，放昨晚巡堤的录像。」" },
                    new[] { 1 }, () =>
                    {
                        soundscape.Play(BrineInquiry.Display, "signal", .70f);
                        Advance(BrineInquiryStep.Online, BrineInquiry.Computer, "tool", "大屏亮起，定格在四面墙上。巡检机吐出一张抄页。");
                    }), () => At(BrineInquiryStep.Card));
            Target(BrineInquiry.Display, "查看巡检定格", .22f,
                () => Notify(BrineInquiry.Transcript(State.brine), 14), () => At(BrineInquiryStep.Online), 3.4f);
        }
    }
}
