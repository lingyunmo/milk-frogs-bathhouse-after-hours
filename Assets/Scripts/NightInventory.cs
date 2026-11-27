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
                        "更衣凳上留下的旧手电。只在开灯时耗电。", status: status,
                        actionLabel: Player.TorchOn ? "关灯" : "开灯", canUse: State.charge > 0 || Player.TorchOn);
                }
                if (State.cells > 0)
                {
                    string reason = BatteryBlockedReason();
                    yield return new NightInventoryItem(NightItemId.Battery, "备用电池", "物资",
                        "换上一节，手电可再亮约 6 分钟。", count: State.cells,
                        status: reason ?? "可换电", actionLabel: "更换电池", canUse: reason == null);
                }
                if (State.stones > 0)
                    yield return new NightInventoryItem(NightItemId.Stone, "石子", "物资",
                        "往空处投掷，奶娃会听见落地声。", count: State.stones, status: "G 投掷");
                if (State.motorPart)
                    yield return new NightInventoryItem(NightItemId.Starter, "发电机启动件", "维修",
                        "装进地下设备层的发电机。");
                if (State.seal)
                    yield return new NightInventoryItem(NightItemId.Seal, "回水密封圈", "维修",
                        "关掉盥洗室的进水阀，再换上密封圈。");
                if (State.collected.Contains(NightStreetRelay.FuseId) &&
                    !State.collected.Contains(NightStreetRelay.SeatedId))
                    yield return new NightInventoryItem(NightItemId.RelayFuse, "备用保险管", "维修",
                        "后场检修棚拾得。街对面值守点的转接机正缺这支保险管。");
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
                        "开" + key.Room + "的门。对准门锁按 F。",
                        status: NightKeyDoors.IsUnlocked(key, State) ? "已开锁" : "");
                }
                if (State.collected.Contains(FaultNoteId))
                    yield return new NightInventoryItem(NightItemId.Receipt, "夜班故障单", "记录",
                        "地下设备层负责供电。维修副楼的旁路还没接好，机修间和留档室都有线索。接通外线后，可回前台或去街外值守点求援。",
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
                Notify("先从更衣凳取手电。");
                return false;
            }
            if (!Player.TorchOn && State.charge <= 0)
            {
                Notify("手电没电了。按 R 换电。");
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
            Notify($"已换电池。按 {TorchKey} 开手电。");
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
