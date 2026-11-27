using System;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightGame
    {
        void BuildObjectives()
        {
            Vector3 torchBench = new(-2,4.12f,21);
            Target(torchBench,"拿起手电",0,()=>
            {
                State.torch=true;
                State.charge=NightState.FullTorchCharge;
                Player.SetTorch(true);
                world.Refresh();
                PickupFeedback(torchBench, $"手电筒 · {TorchKey} 开关", art: "torch");
                SaveProgress();
            },()=>!State.torch);

            Supply(FaultNoteId, new(9,1.00f,3.74f), "读夜班故障单", "故障单 · B 查看", sound: "paper");
            Supply("motor", new(-14,-2.30f,4.46f), "取发电机启动件", "发电机启动件", () => State.motorPart = true);
            Supply("seal", new(8,4.72f,4.20f), "取回水接头密封圈", "回水密封圈", () => State.seal = true);
            Cell("cell-reception", new(10,1.015f,3.71f), "前台");
            Cell("cell-storage", new(-2,-2.53f,4.67f), "备品库");
            Cell("cell-staff", new(-9,8.15f,18), "值班室");
            Cell("cell-parking", new(49,.17f,4.8f), "停车棚");
            foreach (var supply in NightMainRing.Batteries)
                Cell(supply.Id, supply.Target, supply.Place);
            foreach (var supply in NightWorld.StreetSupplies)
                Cell(supply.Id, supply.Position, supply.Location);
            foreach (var supply in NightWorld.StreetRoomSupplies)
                Cell(supply.Id, supply.Position, supply.Location);
            BuildStreetRelayObjectives();
            BuildNightEventObjectives();
            BuildAnnexObjectives();
            Supply("stones", new(24,.13f,-10), "捡三颗石子", "石子 × 3", () => State.stones += 3);

            Vector3 motor=new(-10,-2.57f,18.92f);
            Target(motor,"安装启动件",3.2f,()=>Repair(motor,.48f,()=>{State.motorStage++;State.motorPart=false;}),()=>State.motorStage==0&&State.motorPart);
            Target(motor,"对准飞轮刻线",0,()=>OpenRepair("飞轮校准",motor,
                new[]{"Ⅰ","Ⅱ","Ⅲ"},new[]{"油泥下有三个冲印：Ⅱ、Ⅰ、Ⅲ。指针停在第一道缺口前。","咔。一道齿扣住了。冲印还剩 Ⅰ、Ⅲ。","轴里只剩一道空响。最后一个冲印是 Ⅲ。"},
                new[]{1,0,2},()=>Repair(motor,.30f,()=>State.motorStage++)),()=>State.motorStage==1);
            Target(motor,"点动发电机",4.6f,()=>Repair(motor,1.25f,()=>State.motorStage++,"switch"),()=>State.motorStage==2);
            Vector3 breaker=new(8,-2.45f,3.38f);
            Target(breaker,"给前台和弱电间送电",2.2f,()=>Repair(breaker,.70f,()=>
            {
                State.power=true;
                world.StartPowerRestoration();
            },"switch"),()=>State.motorStage==3&&!State.power);

            Vector3 sink=new(1,4.36f,2.46f);
            Target(sink,"关掉水槽进水阀",2.5f,()=>Repair(sink,.44f,()=>State.sinkStage++),()=>State.sinkStage==0);
            Target(sink,"换密封圈，拧紧回水接头",4.5f,()=>Repair(sink,.65f,()=>{State.sinkStage++;State.seal=false;}),()=>State.sinkStage==1&&State.seal);
            Vector3 pump=new(1,-2.54f,20.08f);
            Target(pump,"排空后场积水",3,()=>Repair(pump,1.0f,()=>State.pump=true,"switch"),()=>State.power&&State.sinkStage==2&&!State.pump);

            Vector3 network=new(12,8.38f,19.47f);
            Target(network,"检查标签，接好浴场上行线",0,()=>OpenRepair("弱电间接线",network,
                new[]{"前台","外线","监控"},new[]{"这条粗线穿墙通到街上，旧线套印着「外」。","细线从楼下电话引来，线头还缠着前台的号码签。"},
                new[]{1,0},()=>Repair(network,.36f,()=>State.networkStage=1)),()=>State.networkStage==0);
            Target(network,"打开路由器，确认链路灯",3.6f,()=>Repair(network,.42f,()=>State.uplink=true,"signal"),()=>State.power&&State.networkStage==1&&!State.uplink);
            Vector3 terminal=new(9,1.12f,3.94f);
            Target(terminal,"检查终端线路",0,()=>Notify("拨号声到了副楼就断了。",6),
                ()=>State.power&&State.uplink&&!State.annexRelay&&!State.contacted);
            Target(terminal,"发送街名与站牌编号",3.1f,()=>Repair(terminal,.73f,()=>
            {
                State.contacted=true;
                Notify("听筒里传来人声：「街尾那块反光板，翻起来我才看得见你。」",7);
            },"signal"),()=>State.power&&State.uplink&&State.annexRelay&&!State.contacted);
            Target(terminal,"请求一次补给",1.7f,()=>
            {
                State.dropTime=21;
                State.rearDrop=State.pump;
                Emit(NightWorld.DropPoint(State)+Vector3.up*6,.92f,"drone");
                Notify(State.rearDrop?"投递点 · 后场货门":"投递点 · 东侧停车棚",6);
                SaveProgress();
            },()=>State.contacted&&State.dropTime<0,1.9f);

            foreach(bool rear in new[]{false,true})
            {
                var point=NightWorld.DropPoint(new NightState{rearDrop=rear});
                Target(point,"打开补给箱",1.2f,()=>
                {
                    State.dropTaken=true;
                    State.cells+=2;
                    State.stones+=2;
                    world.Refresh();
                    PickupFeedback(point, "电池 × 2　石子 × 2", art: "cell");
                    SaveProgress();
                },()=>State.dropTime==0&&!State.dropTaken&&State.rearDrop==rear);
            }

            Vector3 pole=new(160,1.20f,-14.76f);
            Target(pole,"阅读站牌背面的应急说明",0,()=>Notify("站牌背面的小字：「夜班车凭反光接应板停车。未收到值守回话，请勿翻板。」",7),()=>!State.contacted,2.3f);
            Target(pole,"翻起反光接应板",1.25f,()=>Repair(pole,.95f,()=>
            {
                State.beacon=true;
                State.rescueTime=27;
                Notify("街的另一头，有人回了两下远光。",6);
            },"tool"),()=>State.contacted&&!State.beacon,2.3f);
            Target(new Vector3(159.2f,1.10f,-18.76f),"登上救援车",1.15f,()=>
            {
                State.escaped=true;
                SaveProgress();
                SetScreen(NightScreen.Escaped);
            },()=>State.rescueTime==0,2.3f);

            Vault("翻出弱电间北窗",new Vector3(10,8.46f,23.9f),new Vector3(10,5.47f,25.3f),.68f);
            Vault("踩到二楼露台",new Vector3(10,6.08f,25.5f),new Vector3(10,3.67f,26.5f),.51f);
            Vault("翻出沙发厅西窗",new Vector3(-19.9f,4.82f,5),new Vector3(-21.21f,1.87f,5),.23f);
            Vault("沿雨棚下到西通道",new Vector3(-21.7f,2.55f,5),new Vector3(-23,0.07f,5),.39f);
            Vault("翻过折叠间北窗",new Vector3(-11.5f,4.82f,23.9f),new Vector3(-11.5f,1.87f,25.2f),.70f);
            Vault("下到卸货区",new Vector3(-11.5f,2.64f,26.35f),new Vector3(-11.5f,.07f,27.6f),.54f);
            Vault("翻出淋浴间东窗",new Vector3(15.91f,1.34f,19),new Vector3(16.90f,.07f,19),.43f, () => Player.Feet.x < 16);
            Vault("翻进淋浴间",new Vector3(16.09f,1.34f,19),new Vector3(15.01f,.07f,19),.29f, () => Player.Feet.x >= 16);
        }

        void Vault(string label, Vector3 from, Vector3 to, float sound, Func<bool> available = null)
        {
            Target(from,label,0,()=>
            {
                if (Player.Vault(to)) LandingNoise = sound;
                else Notify("落脚处被挡住了。");
            }, () => !State.detained && (available == null || available()), 1.48f);
        }

        void Repair(Vector3 point,float noise,Action advance,string sound="tool")
        {
            advance();
            world.Refresh();
            Emit(point,noise,sound);
            SaveProgress();
        }

        void Cell(string id, Vector3 position, string location) =>
            Supply(id, position, "取电池 · " + location, "备用电池 × 1", () => State.cells++);

        void Supply(string id, Vector3 position, string label, string feedback, Action collect = null, string sound = "pickup")
        {
            Target(position, label, 0, () =>
            {
                State.collected.Add(id);
                collect?.Invoke();
                world.Refresh();
                PickupFeedback(position, feedback, sound, SupplyArt(id));
                SaveProgress();
            }, () => !State.collected.Contains(id));
        }

        void PickupFeedback(Vector3 position, string feedback, string sound = "pickup", string art = "")
        {
            soundscape.Play(position, sound, .58f);
            if (string.IsNullOrEmpty(art)) Notify(feedback, 2.75f);
            else LastPickup = (art, feedback, LastPickup.Serial + 1);
        }

        static string SupplyArt(string id) => id switch
        {
            FaultNoteId or AnnexDiagramId => "receipt",
            "motor" or NightStreetRelay.FuseId => "starter",
            "seal" => "seal",
            "stones" => "stone",
            _ => id.StartsWith("cell", StringComparison.Ordinal) ? "cell" : ""
        };
    }
}
