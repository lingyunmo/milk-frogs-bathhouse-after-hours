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
        public bool missedExit, gateReleased;
        public int divertedLane = -1;
        public float divertTime, divertCooldown;
        public string[] exposures = new string[4];
        public int wheel, rubbings, wave, released;
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
        }
    }

    [Serializable]
    internal sealed class BrineEcho
    {
        public Vector3 feet;
        public int lane, life;
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
        internal static string Notebook(BrineRun run)
        {
            string page = Inscription+"\n";
            for(int i=0;i<Marks.Length;i++)
                if((run.rubbings & (1<<i))!=0) page+="\n"+Marks[i].Place+"　"+Rubbing(i,run.wheel);
            return page;
        }

        internal const string CarryTitle="请勿托运", LostTitle="尸潮来袭", ClearTitle="地上烟花";
        internal static (string title,string caption) Epilogue(Ticket ticket, bool missedExit) => ticket switch
        {
            Ticket.CoreCargo => (CarryTitle,"车过石桥，铁匣在你膝上响了一下。封条仍写着「试验核聚变样芯」。司机把车厢的暖气关了。匣壁还是热的。"),
            Ticket.Overrun when missedExit => (LostTitle,"最后一个湿脚印倒在闸边。你听到了奶娃在东廊敲门。晚了一步。核闪从门缝漫进来时，它还在等你。"),
            Ticket.Overrun => (LostTitle,"第一声像有人在铁门外拍手。第二声从门里传来。浴场的核闪照白了整条堤岸。救援名单上，再也没人划掉你的名字。"),
            _ => (ClearTitle,"你滚出检修口。奶娃留在闸的另一边，替你压住回弹的门。浴场上空升起一簇白金色的花。隔河的人说放了烟火，只有你的袖口印着核警报的三叶章。")
        };

        internal static Vector3 Handwheel(int side) => new(268.4f,1.08f,side==0 ? -34.65f : -27.45f);
        internal static int WaveSize(int wave) => 4 + 2 * wave;
        internal static float Speed(BrineEcho echo, int wave) => 1.12f + .19f*wave + .12f*echo.lane;
        internal static Vector3 Mouth(int lane) => new(249+lane*.7f,.04f,-33.4f+lane*2.15f);
        internal static Vector3 Breach(int lane) => new(272.8f,.04f,-33.4f+lane*2.15f);
        internal static (string action,string place) Lead(BrineRun run) => run.Fighting
            ? ("守住隔离闸　"+(run.wave+1)+" / 3","南堤隔离闸　·　鼠标左键 开火")
            : (run.gateReleased ? "穿过泄洪廊" : "跟上奶娃，去东闸", "向东 · 拉开尽头的逃生栓　　"+Mathf.CeilToInt(run.escapeTime)+" 秒");
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
                for(int i=0;i<4;i++) prompts[i]="井 → 河　　"+(i+1)+" 号字轮\n"+BrineRoute.Notebook(State.brine);
                OpenRepair("旧线封门",BrineRoute.Dial,BrineRoute.Symbols,prompts,BrineRoute.Combination(State.brine.wheel),()=>
                {
                    State.brine.stage=BrineStage.Unsealed;
                    Emit(BrineRoute.Dial,.78f,"tool"); world.Refresh(); SaveProgress(); Notify("里面传来冰裂的声音。");
                });
                panel.wholeCode = new int[4];
            },()=>!State.detained && State.brine.stage==BrineStage.Quiet && State.brine.rubbings==15);
            Target(BrineRoute.Sample+new Vector3(-1.55f,-.10f,.35f),"翻看交运簿",.35f,()=>
                Notify("七月十七。冷库借浴场的回水保样。今夜核样升温，下游三道闸都在敲门。交运栏写着「一匣」，守闸栏只按了一只湿手印。",9),
                ()=>!State.detained && State.brine.stage is BrineStage.Unsealed or BrineStage.Carrying);
            Target(BrineRoute.Sample,"提起试验核聚变样芯",.75f,()=>
            {
                State.brine.stage=BrineStage.Carrying; world.Refresh(); SaveProgress();
                Notify("封条还完整。随车带走，或送往堤岸隔离闸。",7);
            },()=>!State.detained && State.brine.stage==BrineStage.Unsealed);
            Target(BrineRoute.Socket,"部署样芯 · 封锁车道",1.1f,()=>
                OpenRepair("堤岸隔离闸",BrineRoute.Socket,new[]{"合上匣盖","封路并部署"},
                    new[]{"核警报一响，接应车即刻撤走。闸内配有回流炮。确认留守？"},new[]{1},BeginBrineDefence),
                ()=>!State.detained && State.brine.stage==BrineStage.Carrying);
            for(int side=0;side<2;side++)
            {
                int lane=side*2;
                var at=BrineRoute.Handwheel(side);
                brineWheels[side]=Target(at,side==0 ? "扳南线回水闸" : "扳北线回水闸",.65f,()=>
                {
                    var r=State.brine;
                    r.divertedLane=lane; r.divertTime=5; r.divertCooldown=13;
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
            Notify(BrineRoute.Rubbing(i,State.brine.wheel)+"　·　B 查看堤墙摹本",4);
        }
    }
}
