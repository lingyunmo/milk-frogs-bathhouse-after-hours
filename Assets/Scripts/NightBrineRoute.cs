using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal enum BrineStage { Quiet, Unsealed, Carrying, Defence, Flight, Overrun, Clear }
    [Serializable]
    internal sealed class BrineRun
    {
        public BrineStage stage;
        public bool missedExit, gateReleased, radiationLost;
        public int divertedLane = -1;
        public float divertTime, divertCooldown;
        public string[] exposures = new string[4];
        public int wheel, rubbings, wave, released, tideSeed;
        public float heat, radiationDose;
        public bool overheated;
        internal int ShellLane
        {
            get
            {
                BrineEcho first=null;
                foreach(var echo in echoes)
                    if(echo.armoured && (first==null || echo.feet.x>first.feet.x)) first=echo;
                return first?.lane ?? -1;
            }
        }
        public float spawnIn, shutter = 100, breath = 100, escapeTime;
        public List<BrineEcho> echoes = new();
        internal bool Fighting => stage == BrineStage.Defence;
        internal bool Committed => stage >= BrineStage.Defence;
        internal bool Live => Fighting || stage == BrineStage.Flight;
        internal void Normalize()
        {
            echoes ??= new List<BrineEcho>();
            if(exposures==null || exposures.Length!=4) Array.Resize(ref exposures,4);
            rubbings &= 15;
            wheel = Mathf.Clamp(wheel,0,3);
            heat=Mathf.Clamp01(heat);
            radiationDose=float.IsFinite(radiationDose) ? Mathf.Clamp01(radiationDose) : 0;
        }
    }

    internal enum DrifterKind { Walker, Runner, Dredger }

    [Serializable]
    internal sealed class BrineEcho
    {
        public Vector3 feet, home;
        public int lane, life;
        public DrifterKind kind;
        public bool armoured;
        public float stride;
    }

    internal static class BrineRoute
    {
        internal static readonly Rect Room = Rect.MinMaxRect(263, 10, 274, 18);
        internal static readonly Rect Gallery = Rect.MinMaxRect(284,-34,306,-28);
        internal static readonly Vector3 Dial = new(269.6f,1.42f,9.77f), Sample = new(270,1.02f,15.4f),
            Socket = new(273,1.04f,-30.5f), Exit = new(304.6f,1.32f,-31);
        internal static readonly (string Place, Vector3 Point, string Left, string Right, int A, int B)[] Marks =
        {
            ("水文站南墙",new(190,1.72f,7.76f),"桥","灯",0,2),
            ("照相馆南墙",new(256,1.72f,4.76f),"井","月",1,0),
            ("守闸宿舍南墙",new(287.9f,1.72f,-10.24f),"河","灯",3,2),
            ("闸井房南墙",new(309.3f,1.72f,11.76f),"桥","月",2,3)
        };
        internal static readonly string[] Symbols = {"4","K","P","7"};
        // Follow the joined banks from the well to the river. At each joint take the upstream character.
        internal static int[] Combination(int wheel) => new[]{Index(Marks[1].A,wheel),Index(Marks[3].B,wheel),
            Index(Marks[0].A,wheel),Index(Marks[2].B,wheel)};
        static int Index(int glyph,int wheel) => (glyph + wheel) % Symbols.Length;
        internal static string Rubbing(int i,int wheel)
        {
            var m=Marks[i];
            return m.Left+"  |  "+Symbols[Index(m.A,wheel)]+Symbols[Index(m.B,wheel)]+"  |  "+m.Right;
        }
        internal const string Inscription = "旧线从井到河。同印相接，每段只抄进水那头的头一个字。\n四段分别留在测站、相馆、宿舍、井房的外墙。";
        static readonly string[] MarginNotes = {
            "测站底稿 · 八七年的最后一通电话收到两遍。第二遍，整队人已上车。",
            "暗房底稿 · 同一张底片重印两次，第二张的人全朝着管口。",
            "宿舍交班 · 每晚打烊，值守床就少一人。河对面却总能点齐他们。",
            "闸井封条 · 试验站已拆。样芯还在替回水线留着最后一夜。"
        };
        internal static string FieldRecord(int i,int wheel) => Rubbing(i,wheel)+"　"+MarginNotes[i];

        internal static string Notebook(BrineRun run, bool fieldNotes=true)
        {
            string page = Inscription+"\n";
            for(int i=0;i<Marks.Length;i++)
                if((run.rubbings & (1<<i))!=0) page+="\n"+Marks[i].Place+"　"+(fieldNotes ? FieldRecord(i,run.wheel) : Rubbing(i,run.wheel));
            return page;
        }

        internal const string CarryTitle="请勿托运", LostTitle="尸潮来袭", ClearTitle="核禁区外";
        internal static (string title,string caption) Epilogue(Ticket ticket, BrineRun run) => ticket switch
        {
            Ticket.CoreCargo => (CarryTitle,"车过石桥，路边的辐射监测灯由绿转红。司机熄了火。封条完整，车上的人却都听见了匣子里的点名。"),
            Ticket.Overrun when run.radiationLost => (LostTitle,"离门还剩几步，胸前的辐射计已响成一声。奶娃拖着你的袖口往背风处挪。监测带上，你的那一栏停在红区。"),
            Ticket.Overrun when run.missedExit => (LostTitle,"东闸开着。奶娃在另一头敲了三下，等你的第四声。核爆掀开廊顶时，录音在这里断了。"),
            Ticket.Overrun => (LostTitle,"南堤停止回话。隔离闸的最后一段录音里，早已撤走的人还在报数。第二次核爆之后，再没有人接班。"),
            _ => (ClearTitle,"你在东堤外翻过身。核爆云遮住了整条街。奶娃把闸压到最后一刻。隔离线外，接你的值班员在名单末尾写下：一人生还。")
        };

        internal static Vector3 Handwheel(int side) => new(268.4f,1.08f,side==0 ? -34.65f : -27.45f);
        internal static int WaveSize(int wave) => 8 + 6 * wave;
        internal static float Speed(BrineEcho echo, int wave) => echo.kind switch
        {
            DrifterKind.Runner => 2.65f + .14f * wave,
            DrifterKind.Dredger => 1.28f + .08f * wave,
            _ => 1.42f + .12f * wave + .10f * echo.lane
        };
        internal static BrineEcho NextEcho(BrineRun run)
        {
            int n=run.released, wave=run.wave;
            bool shell=n%7==3;
            bool runner=!shell && n>0 && (n+wave)%4==1;
            int lane=shell ? 2*((run.tideSeed+wave+n/7)%2) : (n*2+n/3+wave+run.tideSeed)%3;
            return new BrineEcho { lane=lane, feet=Mouth(lane), stride=n*1.73f,
                kind=shell?DrifterKind.Dredger:runner?DrifterKind.Runner:DrifterKind.Walker,
                armoured=shell, life=shell?3:runner || wave>0?2:1 };
        }
        internal static float SpawnDelay(int wave,int released) =>
            released%5==0 ? 2.8f : released%5<=wave+1 ? .52f : 1.8f-wave*.15f;
        internal static Vector3 Mouth(int lane) => new(249+lane*.7f,.04f,-33.4f+lane*2.15f);
        internal static Vector3 Breach(int lane) => new(272.8f,.04f,-33.4f+lane*2.15f);
        internal static (string action,string place) Lead(BrineRun run) => run.Fighting
            ? ("守住隔离闸　"+(run.wave+1)+" / 3",run.ShellLane switch { 0=>"南线硬壳　·　扳南手轮", 2=>"北线硬壳　·　扳北手轮", _=>"南堤隔离闸　·　守住三道来水口" })
            : run.gateReleased ? ("穿过泄洪廊","向东 · 拉开尽头的逃生栓　　"+Mathf.CeilToInt(run.escapeTime)+" 秒")
            : ("跟上奶娃，去东闸","隔离闸东侧 · 等奶娃拉开闸门");
    }

    internal sealed partial class NightGame
    {
        void BuildBrineTargets()
        {
            for(int i=0;i<BrineRoute.Marks.Length;i++)
            {
                int mark=i;
                Target(BrineRoute.Marks[i].Point,"拓下旧线记号",.65f,()=>RecordMark(mark),
                    ()=>!State.detained && State.brine.stage==BrineStage.Quiet && (State.brine.rubbings & (1<<mark))==0);
            }
            Target(BrineRoute.Dial,"读封门铭牌",.18f,()=>Notify(BrineRoute.Inscription,9),
                ()=>!State.detained && State.brine.stage==BrineStage.Quiet && State.brine.rubbings!=15);
            Target(BrineRoute.Dial,"拨动四位字轮",.36f,()=>
            {
                var prompts=new string[4];
                for(int i=0;i<4;i++) prompts[i]="井 → 河　　"+(i+1)+" 号字轮\n"+BrineRoute.Notebook(State.brine,false);
                OpenRepair("旧线封门",BrineRoute.Dial,BrineRoute.Symbols,prompts,BrineRoute.Combination(State.brine.wheel),()=>
                {
                    State.brine.stage=BrineStage.Unsealed;
                    Emit(BrineRoute.Dial,.78f,"tool"); world.Refresh(); SaveProgress(); Notify("里面传来冰裂的声音。");
                });
                panel.wholeCode = new int[4];
            },()=>!State.detained && State.brine.stage==BrineStage.Quiet && State.brine.rubbings==15);
            Target(BrineRoute.Sample+new Vector3(-1.55f,-.10f,.35f),"翻看交运簿",.35f,()=>
                Notify("七月十七。试验站撤走时，把核样芯留在八七年的旧回水线上。每晚落闸，失踪的夜班工便沿管线回来。南堤的黄衣守闸人，一直没下班。",9),
                ()=>!State.detained && State.brine.stage is BrineStage.Unsealed or BrineStage.Carrying);
            Target(BrineRoute.Sample,"提起试验核聚变样芯",.75f,()=>
            {
                State.brine.stage=BrineStage.Carrying; world.Refresh(); SaveProgress();
                Notify("交运联有两栏：原匣随车；留在南堤，封死回水线。守闸栏只有一枚湿手印。",7);
            },()=>!State.detained && State.brine.stage==BrineStage.Unsealed);
            Target(BrineRoute.Socket,"部署样芯 · 封锁车道",1.1f,()=>
                OpenRepair("堤岸隔离闸",BrineRoute.Socket,new[]{"合上匣盖","封路并部署"},
                    new[]{"封条背面写着：一旦接入旧线，整条街都会受冲击。车队撤离，留守者只走东闸。接入样芯？"},new[]{1},BeginBrineDefence),
                ()=>!State.detained && State.brine.stage==BrineStage.Carrying);
            for(int side=0;side<2;side++)
            {
                int lane=side*2;
                var at=BrineRoute.Handwheel(side);
                brineWheels[side]=Target(at,side==0 ? "扳南线回水闸" : "扳北线回水闸",.65f,()=>
                {
                    var r=State.brine;
                    r.divertedLane=lane; r.divertTime=4; r.divertCooldown=7;
                    soundscape.Play(at,"pipe-leak",1);
                    Notify("水头顶住了这一线。",3);
                },()=>State.brine.Fighting && State.brine.divertCooldown<=0);
            }
            brineExit=Target(BrineRoute.Exit,"拉开逃生栓",.65f,()=>EndBrine(true),
                ()=>State.brine.stage==BrineStage.Flight && State.brine.gateReleased);
        }

        void RecordMark(int i)
        {
            if(State.detained || State.brine.stage!=BrineStage.Quiet) return;
            State.brine.rubbings|=1<<i;
            Emit(BrineRoute.Marks[i].Point,.32f,"paper"); SaveProgress();
            Notify(BrineRoute.Rubbing(i,State.brine.wheel)+"　·　B 查阅摹本与底稿",4);
        }
    }
}
