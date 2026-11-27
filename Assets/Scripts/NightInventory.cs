using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal enum NightItemId { Torch, Battery, Stone, Starter, Seal, Receipt, RelayFuse, AnnexDiagram, LinenKey, StaffKey, UplinkKey }

    internal readonly struct NightInventoryItem
    {
        internal readonly NightItemId Id;
        internal readonly string Name;
        internal readonly string Kind;
        internal readonly string Description;
        internal readonly int Count;
        internal readonly string Status;
        internal readonly string ActionLabel;
        internal readonly bool CanUse;

        internal NightInventoryItem(NightItemId id, string name, string kind, string description,
            int count = 1, string status = "", string actionLabel = "", bool canUse = false)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Description = description;
            Count = count;
            Status = status;
            ActionLabel = actionLabel;
            CanUse = canUse;
        }
    }

    internal sealed partial class NightGame
    {
        const string FaultNoteId = "receipt";
        const float ReplacementMinimumSpent = 16;

        internal IEnumerable<NightInventoryItem> InventoryItems
        {
            get
            {
                if (State.torch)
                {
                    float charge = Mathf.Clamp01(State.charge / NightState.FullTorchCharge);
                    string status = State.charge <= 0 ? "电量耗尽" :
                        (Player.TorchOn ? "亮着" : "已关闭") + " · " + Mathf.CeilToInt(charge * 100) + "%";
                    yield return new NightInventoryItem(NightItemId.Torch, "手电筒", "物资",
                        "筒身刻了个「更」字。拇指搭上开关，正好落进前一个人磨出的凹痕。", status: status,
                        actionLabel: Player.TorchOn ? "关灯" : "开灯", canUse: State.charge > 0 || Player.TorchOn);
                }
                if (State.cells > 0)
                {
                    string reason = BatteryBlockedReason();
                    yield return new NightInventoryItem(NightItemId.Battery, "备用电池", "物资",
                        "纸皮有点潮，两头的铜帽还亮着。", count: State.cells,
                        status: reason ?? "可换电", actionLabel: "更换电池", canUse: reason == null);
                }
                if (State.stones > 0)
                    yield return new NightInventoryItem(NightItemId.Stone, "石子", "物资",
                        "三两步外是瓷砖，再远是铁皮。同一颗石子，落下去是两种动静。", count: State.stones, status: "G 投掷");
                if (State.motorPart)
                    yield return new NightInventoryItem(NightItemId.Starter, "发电机启动件", "维修",
                        "齿口沾着黑油。转半圈便咬住，松手又弹回来。");
                if (State.seal)
                    yield return new NightInventoryItem(NightItemId.Seal, "回水密封圈", "维修",
                        "拇指大的黑胶圈，边缘压着一圈白色水垢。旧的那只大概早就硬了。");
                if (State.collected.Contains(NightStreetRelay.FuseId) &&
                    !State.collected.Contains(NightStreetRelay.SeatedId))
                    yield return new NightInventoryItem(NightItemId.RelayFuse, "备用保险管", "维修",
                        "瓷管两头包铜，细丝还连着。纸套上有枚蓝章：「街口值守」。");
                foreach (var key in NightKeyDoors.Locks)
                {
                    if (!State.collected.Contains(key.KeyId)) continue;
                    var itemId = key.Slot switch
                    {
                        NightKeyDoors.KeySlot.Linen => NightItemId.LinenKey,
                        NightKeyDoors.KeySlot.Staff => NightItemId.StaffKey,
                        _ => NightItemId.UplinkKey
                    };
                    yield return new NightInventoryItem(itemId, key.KeyName, "钥匙",
                        key.Slot switch
                        {
                            NightKeyDoors.KeySlot.Linen => "匙圈上系着一截毛巾边。白线漂洗得发脆。",
                            NightKeyDoors.KeySlot.Staff => "铝牌有两面。正面刻着「值班」，背面有人拿钉子划了个「早」。",
                            _ => "窄齿磨得很亮，电工胶布裹着匙柄。揭开一角，底下写着「弱电」。"
                        },
                        status: NightKeyDoors.IsUnlocked(key, State) ? "已开锁" : "");
                }
                if (State.collected.Contains(FaultNoteId))
                    yield return new NightInventoryItem(NightItemId.Receipt, "夜班故障单", "记录",
                        "交班簿末页，老许的字。「楼底那台停了，电话也跟着哑。副楼又进水。图还在老地方，别拿来垫茶缸。要是前台没人，街口也有人守线。」",
                        status: "前台 · 夜班记录");
                if (State.collected.Contains(AnnexDiagramId))
                    yield return new NightInventoryItem(NightItemId.AnnexDiagram, "外线接线图", "记录",
                        AnnexDiagramText, status: "副楼三楼 · 留档室");
            }
        }

        internal bool UseInventoryItem(NightItemId id)
        {
            if (ScreenState != NightScreen.Inventory) return false;
            return id switch
            {
                NightItemId.Torch => ToggleTorch(),
                NightItemId.Battery => ReplaceCell(),
                _ => false
            };
        }

        internal bool ToggleTorch()
        {
            if (!State.torch)
            {
                Notify("手里是空的。");
                return false;
            }
            if (!Player.TorchOn && State.charge <= 0)
            {
                Notify("开关响了，灯没亮。");
                return false;
            }
            Player.SetTorch(!Player.TorchOn);
            PlayHandheld("torch-click", .36f);
            return true;
        }

        internal bool ReplaceCell()
        {
            string reason = BatteryBlockedReason();
            if (reason != null)
            {
                Notify(reason);
                return false;
            }
            State.charge = NightState.FullTorchCharge;
            State.cells--;
            Player.SetTorch(false);
            PlayHandheld("pickup", .38f);
            Notify("尾盖拧紧了。");
            return true;
        }

        void PlayHandheld(string sound, float gain)
        {
            var eyes = Player.Eyes;
            soundscape.Play(eyes.position + eyes.forward * .4f, sound, gain,
                duringPause: ScreenState == NightScreen.Inventory);
        }

        string BatteryBlockedReason()
        {
            if (!State.torch) return "还没拿手电";
            if (State.cells <= 0) return "没有备用电池";
            if (State.charge >= NightState.FullTorchCharge - ReplacementMinimumSpent)
                return "当前电池电量充足";
            return null;
        }
    }
}
