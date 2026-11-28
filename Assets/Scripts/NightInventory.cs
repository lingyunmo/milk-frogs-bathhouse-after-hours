using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal enum NightItemId { Torch, Battery, Stone, Starter, Seal, Receipt, RelayFuse, AnnexDiagram, LinenKey, StaffKey, UplinkKey, WashSlip, Names, Founding, Tape, Print, Stub, Cylinder, LostPair }

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
        internal const string LostPhotoId = "reception-lost-photo";
        internal static readonly Vector3 LostPhotoPosition = new(9.55f, .950f, 3.41f);
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
                        "更衣室的值班手电。尾盖拧得很紧，换电池时得用点劲。", status: status,
                        actionLabel: Player.TorchOn ? "关灯" : "开灯", canUse: State.charge > 0 || Player.TorchOn);
                }
                if (State.cells > 0)
                {
                    string reason = BatteryBlockedReason();
                    yield return new NightInventoryItem(NightItemId.Battery, "备用电池", "物资",
                        "给手电换上就能用。纸壳受了潮，铜帽还是干的。", count: State.cells,
                        status: reason ?? "可换电", actionLabel: "更换电池", canUse: reason == null);
                }
                if (State.stones > 0)
                    yield return new NightInventoryItem(NightItemId.Stone, "石子", "物资",
                        "丢到远处弄出声响，可以把奶娃引开。", count: State.stones, status: "G 投掷");
                if (State.motorPart)
                    yield return new NightInventoryItem(NightItemId.Starter, "发电机启动件", "维修",
                        "装在地下发电机的飞轮旁。齿口上还带着黑油。");
                if (State.seal)
                    yield return new NightInventoryItem(NightItemId.Seal, "回水密封圈", "维修",
                        "二楼水槽回水管的垫圈。换之前先关进水阀。");
                if (State.collected.Contains(NightStreetRelay.FuseId) &&
                    !State.collected.Contains(NightStreetRelay.SeatedId))
                    yield return new NightInventoryItem(NightItemId.RelayFuse, "备用保险管", "维修",
                        "街口转接机的备用保险管。纸套上盖着值守点的蓝章。");
                foreach (var key in NightKeyDoors.Locks)
                {
                    if (!State.collected.Contains(key.KeyId) || (key.Slot == NightKeyDoors.KeySlot.Linen && State.story.exchanged)) continue;
                    var itemId = key.Slot switch
                    {
                        NightKeyDoors.KeySlot.Linen => NightItemId.LinenKey,
                        NightKeyDoors.KeySlot.Staff => NightItemId.StaffKey,
                        _ => NightItemId.UplinkKey
                    };
                    yield return new NightInventoryItem(itemId, key.KeyName, "钥匙",
                        key.Slot switch
                        {
                            NightKeyDoors.KeySlot.Linen => "开二楼毛巾库。钥匙圈上系着一截白毛巾。",
                            NightKeyDoors.KeySlot.Staff => "开三楼值班室。铝牌背面刻了个「早」。",
                            _ => "开三楼弱电间。匙柄上缠着电工胶布。"
                        },
                        status: NightKeyDoors.IsUnlocked(key, State) ? "已开锁" : "");
                }
                if (State.circuit.room == 4 && State.circuit.capsule)
                    yield return new NightInventoryItem(NightItemId.Cylinder,"送票铜筒","物资",
                        State.circuit.vented ? "已经泄压。口沿有水，票根上的回路号还清楚。" : "沉甸甸的黄铜筒。先在墙边松阀，筒盖才能进投递口。");
                if (State.circuit.room >= 2)
                    yield return new NightInventoryItem(NightItemId.Stub,"半张回执票","证物",State.circuit.Stub,status:"东堤旧票务所");
                if (State.collected.Contains(LostPhotoId))
                    yield return new NightInventoryItem(NightItemId.LostPair, "招领照 · 小布鞋", "证物",
                        "许师傅：鞋尖朝窗，晒到中午就干。搪瓷缸先借我用用。\n\n铅笔添了一行：杯子拿走了。鞋还在。", status: "一楼前台");
                if (NightShiftTrail.CanReadArchive(State))
                    yield return new NightInventoryItem(NightItemId.Print, "石桥旧站合影", "证物",
                        "接应板朝水塔。\n\n她每笑三声，岸上的孩子就往那边游。那天雨太大，黄布头套一摘，谁也认不出谁。", status:"旧照相馆");
                foreach (var entry in NightShiftTrail.Papers)
                    if (State.collected.Contains(entry.Id.ToString())) yield return entry;
                if (State.collected.Contains(FaultNoteId))
                    yield return new NightInventoryItem(NightItemId.Receipt, "夜班故障单", "记录",
                        "「地下发电机又停了，前台电话没电。副楼二层还在进水，合闸前记得先断开那一路。接线图放回留档室。——老许」",
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
