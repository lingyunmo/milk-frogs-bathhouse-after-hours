using System;
using UnityEngine;

namespace AfterHours
{
    internal enum WitnessChoice { Undecided, Broadcast, Seal }

    [Serializable]
    internal sealed class ShiftEvidence
    {
        public bool exchanged, receipt, ledger, boardAligned;
        public WitnessChoice choice;
    }

    internal static class NightShiftTrail
    {
        internal static readonly Vector3 Cabinet = new(194, .94f, 23);
        internal static Vector3 StaffKey => Cabinet + new Vector3(.21f, .008f, -.17f);
        internal static readonly Vector3 UplinkKey = new(-9.30f, 8.10f, 18.03f);
        internal static readonly Vector3 Laundry = new(33.95f, 1.04f, 38.64f);
        internal static readonly Vector3 StaffDesk = new(-8.46f, 8.12f, 18);
        internal static readonly Vector3 VoiceDesk = new(12.57f, 8.37f, 19.47f);
        internal static readonly Vector3 SealDesk = new(294.85f, .86f, -5.53f);
        internal static readonly (FieldOrder Order, Vector3 At)[] Stations =
        {
            (FieldOrder.Drain, new(8.66f, 4.72f, 4.18f)),
            (FieldOrder.Lights, NightDispatch.Receiver),
            (FieldOrder.Barrier, NightDispatch.LoadDepot + new Vector3(.74f, .90f, 0)),
            (FieldOrder.Ledger, StaffDesk),
            (FieldOrder.Radio, new(7.8f, 8.23f, 22)),
            (FieldOrder.Survey, new(195.16f, .88f, 19.8f)),
            (FieldOrder.Archive, new(254.87f, .91f, 18.8f))
        };
        internal static readonly NightInventoryItem[] Papers =
        {
            new(NightItemId.WashSlip, "收洗联 · 七月十七", "证物",
                "安置点送来的十二条全是大号。末行另挤了一条小的，右角打着结。过秤十三条。经手人只按了个湿手印。", status:"后巷洗衣店"),
            new(NightItemId.Founding, "浴票第一号", "记录",
                "一九八九年。堤上十二家凑钱，把避水时借住的洗衣棚改作公共浴场。开张那天没剪彩，先空放了一池热水。", status:"街边洗衣店"),
            new(NightItemId.Names, "借衣簿 · 末页", "证物",
                "袁素琴，临时洗衣工。借走庙会的黄布头套一只、黑手套一双，用来逗上岸的孩子。末班点名时，临工不占床号。归还一栏空着。扉页写着：有姓名报弱电间；有底片，封好交日班。", status:"守闸宿舍"),
            new(NightItemId.Tape, "关门试音记录", "证物",
                "白班放水，喇叭无声。拉下闭店闸，回水管一震，废广播竟响了三声。老许剪断了线。隔晚，门外的孩子说他也听到了。",status:"东堤闸井房")
        };

        internal static bool DoorUsed(NightState s, string key) =>
            s.collected.Contains(key) && s.collected.Contains(key + "-used");
        internal static bool CabinetOpen(NightState s) => DoorUsed(s, "key-linen") && s.Drained &&
            (s.dispatch.AllRunning || s.dispatch.teeth >= NightDispatch.Teeth);
        internal static bool KeyAvailable(NightState s, string id) => id switch
        {
            "key-staff" => s.story.exchanged,
            "key-uplink" => s.story.ledger,
            _ => true
        };
        internal static bool Offered(NightState s, FieldOrder order) => order switch
        {
            FieldOrder.Drain => DoorUsed(s,"key-linen"),
            FieldOrder.Lights or FieldOrder.Barrier => DoorUsed(s,"key-linen") && s.Drained,
            FieldOrder.Ledger => s.story.exchanged && DoorUsed(s,"key-staff"),
            FieldOrder.Radio => s.story.ledger && DoorUsed(s,"key-uplink"),
            FieldOrder.Survey => s.story.ledger && s.contacted && DoorUsed(s,"key-uplink"),
            FieldOrder.Archive => s.story.ledger && s.survey.ReportReady && s.contacted && DoorUsed(s,"key-uplink"),
            _ => false
        };
        internal static (string action, string place) Next(NightState s)
        {
            if (!s.collected.Contains("key-linen")) return s.torch ? ("看看长凳上的白布结","二楼 · 更衣室") : ("长凳底下还有一点光","二楼 · 更衣室");
            if (!DoorUsed(s,"key-linen")) return ("试试布条上那个房间","二楼 · 毛巾库");
            if (!s.Drained) return ("钢架上压着一本交班簿","二楼 · 毛巾库");
            if (!CabinetOpen(s)) return ("前台响了一声，街边泵房也亮了灯","一楼 · 前台　／　街边泵房");
            if (!s.story.exchanged) return ("去听听哪只锁弹开了","东堤 · 水文站交接柜");
            if (!s.collected.Contains("key-staff")) return ("取走柜里的早班匙","东堤 · 水文站交接柜");
            if (!s.story.ledger) return ("查早班簿上缺的那一页","主楼三层 · 值班室");
            if (!s.collected.Contains("key-uplink")) return ("簿子底下压着一把缠胶布的钥匙","主楼三层 · 值班室");
            if (!s.contacted || !DoorUsed(s,"key-uplink")) return ("去找那部还能拨出去的电话","主楼三层 · 弱电间");
            if (!s.survey.ReportReady) return ("司机在等东堤的水尺","水文站 · 器材台");
            if (!s.survey.archive) return ("暗房还压着一张没印的底片","旧照相馆 · 印相台");
            if (s.RoadSurveyComplete) return ("照着相片翻正旧站牌", "街尾 · 反光接应板");
            return !s.dispatch.AllRunning ? ("街灯还没全亮","一楼 · 前台电台") : ("给车放行","街边泵房 · 绞盘交班牌");
        }

        internal static bool Has(NightState s, NightItemId id) => s.collected.Contains(id.ToString());
        internal static bool CanReadArchive(NightState s) => s.survey.archive && Offered(s,FieldOrder.Archive);
        internal static bool CanTestify(NightState s) => CanReadArchive(s) && s.story.choice == WitnessChoice.Undecided && Has(s,NightItemId.Names);
        internal static (string title, string caption) Ending(NightState s) => s.story.choice switch
        {
            WitnessChoice.Broadcast => ("有人应名","车载电台念出了袁素琴。司机把车停了片刻。最后一排响起三下敲窗声，像有人终于答了到。"),
            WitnessChoice.Seal => ("请日班亲启","早班邮袋搁在副座。司机摸到那封硬硬的底片，没拆，只在交接栏里端正地签了自己的名字。"),
            _ when Has(s,NightItemId.Names) => ("未寄出的名字","你把借衣簿夹在车票里。窗外的招牌慢慢退远。那个名字一直留在你口袋里。"),
            _ => ("末班乘客","车票背面沾了一块水印。你数了两遍，照片上总比名单多一个人。司机已经关了广播。")
        };
    }

    internal sealed partial class NightGame
    {
        void BuildShiftTrail()
        {
            foreach (var station in NightShiftTrail.Stations)
            {
                var order = station.Order;
                bool Offered() => !State.detained && !State.beacon && NightShiftTrail.Offered(State,order) && !NightFieldOrders.Done(order,State);
                Target(station.At, "接取" + NightFieldOrders.Name(order), .85f, () =>
                {
                    if (!Offered() || State.dispatch.orderHeld) return;
                    var job = State.dispatch;
                    if (!job.accepted) job.leakPattern = UnityEngine.Random.Range(0,8);
                    job.accepted = job.orderHeld = true;
                    job.tracked = order;
                    job.docket |= 1 << (int)order;
                    Emit(station.At, .32f, order == FieldOrder.Lights ? "signal" : "paper");
                    hud.ReceiveDispatch();
                    string message = order switch
                    {
                        FieldOrder.Drain => "交班簿：回水一路漏到街沟。听三路阀门，或换水槽的圈，再开地下泵。",
                        FieldOrder.Lights => "「四个角都黑了。哪盏先亮，我就往哪边开。」",
                        FieldOrder.Barrier => "泵工手记：配重留在脚边。背去街尾，挂上再摇。",
                        FieldOrder.Ledger => "早班簿夹着半张复写纸：十二人。收洗数那一角撕了。另一联送过后巷。",
                        FieldOrder.Radio => "耳机里两声忙音。老电工留字：线断了还可以用街尾的天线。",
                        FieldOrder.Survey => "司机：拿三段测深和三处洪痕来。柜台里的相机还能用。",
                        _ => "相袋背面：两个站牌喊一个站名。照铜钉重拍三处旧址，到宿舍灯台合片。"
                    };
                    Notify(message,6.5f);
                    world.Refresh();
                    SaveProgress();
                }, () => Offered() && !State.dispatch.orderHeld);
                Target(station.At, "当前不可接取", 0, () => Notify("先办完手里的" + NightFieldOrders.Name(State.dispatch.tracked)),
                    () => !State.detained && State.dispatch.orderHeld && State.dispatch.tracked != order && !NightFieldOrders.Done(order, State));
            }

            Target(NightShiftTrail.Cabinet + Vector3.back*.24f, "插回毛巾库钥匙", .85f, () =>
            {
                State.story.exchanged = true;
                Emit(NightShiftTrail.Cabinet,.55f,"switch");
                world.Refresh();
                Notify("旧匙卡在锁芯里。旁边那把刻着「三楼早班」。");
                SaveProgress();
            }, () => !State.detained && NightShiftTrail.CabinetOpen(State) && !State.story.exchanged);
            Target(NightShiftTrail.StaffDesk,"核对交班簿",0,()=>OpenRepair("收洗数 · 七月十七",NightShiftTrail.StaffDesk,
                new[]{"十二条","十三条","十四条"},new[]{"入册十二人。后巷的实收联上，当夜过秤几条？"},new[]{1},
                ()=>{ State.story.ledger=true; SaveProgress(); }),()=>WorkingOn(FieldOrder.Ledger) && State.story.receipt);
            for (int i=0; i<NightShiftTrail.Papers.Length; i++)
            {
                int index=i;
                var paper=NightShiftTrail.Papers[i];
                Target(PaperPosition(i),"翻阅"+paper.Name,.28f,()=>
                {
                    State.collected.Add(paper.Id.ToString());
                    if(index==0) State.story.receipt=true;
                    soundscape.Play(PaperPosition(index),"paper",.45f);
                    Notify(paper.Description,10);
                    world.Refresh();
                    SaveProgress();
                },()=>!State.detained && !NightShiftTrail.Has(State,paper.Id) && (index switch
                {
                    0 => WorkingOn(FieldOrder.Ledger),
                    1 => State.Drained,
                    2 or 3 => State.survey.ReportReady,
                    _ => false
                }));
            }
            void Choice(Vector3 at,string label,WitnessChoice choice) => Target(at,label,1.5f,()=>
            {
                State.story.choice=choice;
                Emit(at,.7f,choice==WitnessChoice.Broadcast?"signal":"paper");
                Notify(choice==WitnessChoice.Broadcast ? "频道里，有人把她的名字又念了一遍。" : "封口的火漆凝住了。");
                SaveProgress();
            },()=>!State.detained && NightShiftTrail.CanTestify(State));
            Choice(NightShiftTrail.VoiceDesk,"播出原名 · 袁素琴",WitnessChoice.Broadcast);
            Choice(NightShiftTrail.SealDesk,"封存原片 · 交给早班",WitnessChoice.Seal);
        }

        internal static Vector3 PaperPosition(int i) => i switch
        {
            0 => NightShiftTrail.Laundry, 1 => new(35.20f,.88f,38.64f),
            2 => new(284.55f,.97f,.80f), _ => new(315.32f,.89f,24.5f)
        };

        void FinishShiftOrder()
        {
            var s=State;
            var job=s.dispatch;
            if (!job.orderHeld || !NightFieldOrders.Done(job.tracked,s)) return;
            job.orderHeld=false;
            world.Refresh();
            string next = job.tracked switch
            {
                FieldOrder.Drain => "前台传来一声电铃。泵房的交班灯也亮了。",
                FieldOrder.Barrier or FieldOrder.Lights when !s.story.exchanged => "电台：东堤水文站，交接柜门已开。请带原匙交班。",
                FieldOrder.Ledger => "纸页滑开，一把缠胶布的钥匙掉在桌上。",
                FieldOrder.Radio => "司机问：水退到哪一级了？我只认东堤站送来的水尺。",
                FieldOrder.Survey => "司机：堤上两块都是老站牌。暗房有旧址合影，帮我认认当年的接应板。",
                FieldOrder.Archive => "合影里那块站牌朝着水塔。照片背面还有几行小字。",
                _ => NightShiftTrail.Next(s).action
            };
            Notify("办结"+NightFieldOrders.Name(job.tracked)+"　"+next,7);
        }
    }
    internal sealed partial class NightWorld
    {
        Transform shiftCabinetHinge;
        GameObject surrenderedKey;
        void DressShiftTrail()
        {
            var at = NightShiftTrail.Cabinet;
            var basePoint = new Vector3(at.x, 0, at.z);
            Box("钥匙交接柜底座",basePoint+Vector3.up*.45f,new(.98f,.90f,.55f),paintedIron);
            Box("交接柜钥匙托盘",at-Vector3.up*.025f,new(.99f,.065f,.59f),steel);
            Box("交接柜后板",at+new Vector3(0,.24f,.25f),new(.99f,.54f,.06f),paintedIron);
            foreach(int side in new[]{-1,1})
                Box("柜侧",at+new Vector3(side*.48f,.24f,0),new(.045f,.54f,.56f),paintedIron);
            Box("柜顶",at+Vector3.up*.50f,new(.99f,.045f,.57f),steel);
            shiftCabinetHinge = new GameObject("交接柜合页").transform;
            shiftCabinetHinge.SetParent(root,false);
            shiftCabinetHinge.position=at+new Vector3(-.48f,.245f,-.292f);
            Box("交接柜门",shiftCabinetHinge.position+Vector3.right*.476f,new(.95f,.51f,.037f),paintedIron,shiftCabinetHinge);
            Box("柜门铜拉手",shiftCabinetHinge.position+new Vector3(.83f,0,-.035f),new(.055f,.13f,.06f),brass,shiftCabinetHinge);
            var lockAt=at+new Vector3(-.25f,.027f,-.18f);
            var barrel=Shape("还匙锁芯",PrimitiveType.Cylinder,lockAt,new(.12f,.025f,.12f),brass,Vector3.zero);
            barrel.GetComponent<Collider>().enabled=false;
            Box("锁芯匙槽",lockAt+Vector3.up*.026f,new(.037f,.003f,.009f),steel);
            surrenderedKey=MakeRoomKey("锁芯里的毛巾库原匙",lockAt+Vector3.up*.113f,Quaternion.Euler(0,12,-90));
            WallSign("交接　／　原匙归位",new(at.x,.72f,at.z-.285f),0,new(.72f,.17f),.073f);
            var paperMat=Material(new Color(.85f,.77f,.61f));
            paperMat.mainTexture=Resources.Load<Texture2D>("Interface/PreviewMeshes/FaultPaper");
            foreach(var station in NightShiftTrail.Stations)
            {
                if(station.Order==FieldOrder.Lights) continue;
                if(station.Order==FieldOrder.Barrier)
                    Box("绞盘交接箱",station.At-Vector3.up*.47f,new(.67f,.89f,.52f),paintedIron);
                Folio(station.At, NightFieldOrders.Name(station.Order),paperMat);
            }
            for(int i=0;i<NightShiftTrail.Papers.Length;i++)
            {
                var paper=NightShiftTrail.Papers[i];
                var note=Folio(NightGame.PaperPosition(i),paper.Name,paperMat);
                pickups.Add((paper.Id.ToString(),note));
            }
        }

        GameObject Folio(Vector3 top,string title,Material paper)
        {
            var cover=Box(title,top-Vector3.up*.022f,new(.34f,.04f,.245f),wood);
            var sheet=Box("夹页",top+Vector3.up*.003f,new(.317f,.008f,.225f),paper);
            sheet.transform.SetParent(cover.transform,true);
            sheet.GetComponent<Collider>().enabled=false;
            cover.GetComponent<Collider>().enabled=false;
            return cover;
        }

        void RefreshShiftCabinet(NightState s)
        {
            if(surrenderedKey) surrenderedKey.SetActive(s.story.exchanged);
            if(shiftCabinetHinge) shiftCabinetHinge.localRotation=Quaternion.Euler(0,NightShiftTrail.CabinetOpen(s)?102:0,0);
        }
    }
}
