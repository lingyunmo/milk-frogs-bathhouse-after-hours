using System;
using UnityEngine;

namespace AfterHours
{
    [Flags]
    internal enum CallPath { None = 0, Aerial = 1, FrontDesk = 2, Relay = 4 }

    [Serializable]
    internal sealed class ReturnCircuit
    {
        const CallPath AllCalls = CallPath.Aerial | CallPath.FrontDesk | CallPath.Relay;
        public CallPath calls, firstCall;
        public bool mainSignal, spareSignal, heard, capsule, vented, coverLifted, compared;
        // Zero is dormant, 1–5 name the currently open workroom, six is a signed reply.
        public int room, punch, matched;
        internal bool Solved => room == 6;
        internal void LogCall(CallPath path)
        {
            if (calls==CallPath.None) firstCall=path;
            calls|=path;
        }
        internal string CallerEcho => firstCall switch
        {
            CallPath.Aerial => "耳边又响起你转天线时的载波啸叫。",
            CallPath.FrontDesk => "你从前台报过的站牌编号，一字不差地传回来。",
            CallPath.Relay => "街外转接机那两声换档，正跟在你的求救声后头。",
            _ => "听筒里，连你的呼吸都原样传了回来。"
        };
        internal bool Has(CallPath path) => (calls & path) == path;
        internal void Normalize()
        {
            calls &= AllCalls;
            room = Mathf.Clamp(room,0,6);
            punch = Mathf.Clamp(punch,0,2);
            matched = Mathf.Clamp(matched,0,3);
        }

        internal static readonly string[] Holes = { "左孔", "中孔", "右孔" };
        internal static readonly string[] Pipes = { "岸", "桥", "井" };
        internal int Beat(int i) => (punch + (i == 1 ? 2 : i == 2 ? 1 : 0)) % 3;
        internal string ReturnLabel => punch == 1 ? "乙" : "甲";
        internal string Receipt => "七月十七　／　" + Holes[punch] + "　／　" + Pipes[Beat(0)] + Pipes[Beat(1)] + Pipes[Beat(2)];

        internal static bool CompleteService(NightState s)
        {
            if (!s.RescueAccess || !s.contacted || !s.pump || s.dispatch.sealedLines != 7 ||
                !s.circuit.Has(AllCalls) || !s.circuit.mainSignal || !s.circuit.spareSignal ||
                !s.dropTaken || !s.survey.balanced || s.survey.bellReadyAt <= 0 ||
                s.story.choice == WitnessChoice.Undecided || !s.collected.Contains(NightEvents.DutyCaseKey) ||
                !s.collected.Contains("receipt")) return false;
            foreach (var paper in NightShiftTrail.Papers)
                if (!NightShiftTrail.Has(s,paper.Id)) return false;
            for (int night=1;night<=NightState.MaxNights;night++)
                if (s.collected.Contains(NightEvents.FreightBellKey(night))) return true;
            return false;
        }

        internal static (string action,string place) Lead(NightState s)
        {
            var t=s.circuit;
            string action=t.room switch
            {
                1 => "塔背后的窗亮了。去取一张空票",
                2 => "用透光架读出纸背的孔位",
                3 => t.heard ? "照刚才的顺序叩三根管" : "接上听筒，听哪根管先响",
                4 => !t.capsule ? "取出装票的气筒" : !t.vented ? "先放掉筒里的余压" : "把筒送回票根那一路",
                _ => t.compared ? "接起手边的话筒" : t.coverLifted ? "把留在身上的票根与旧联叠起来" : "掀开签收窗的铅封"
            };
            return (action, "东堤水塔后 · "+ReturnWing.Rooms[t.room-1]);
        }
        internal static string FinalCaption(NightState s) =>
            "司机把你打过"+Holes[s.circuit.punch]+"的票递给后座。老太太用黑手套接住，说：「那晚，多谢你让我别回头。」她在早班簿上签了袁素琴。";

        // This page stays in the player's notebook across a capture or a reload.
        internal string Stub => room switch
        {
            2 => "你亲手在正面打了"+Holes[punch]+"。熨印架吃纸时，墨面朝下。",
            3 => "透光水印："+ReturnLabel+"路。监听席能把气路里的敲击放大。"+(heard ? "刚才听见"+Pipes[Beat(0)]+"、"+Pipes[Beat(1)]+"、"+Pipes[Beat(2)]+"。" : ""),
            4 => "回执走"+ReturnLabel+"路。筒身刻着「装票、泄压、投递」。",
            _ => coverLifted ? "当晚的落地联也打着"+Holes[punch]+"。纸边印着一九八七年七月十七日。背面是十二家人的合契：棚改浴场，白天两路各用，打烊时接回老线。等她交班，才许拆。" : "外筒已经空了。签收窗的封蜡还完整。"
        };
    }

    internal sealed partial class NightGame
    {
        // Once a job is signed off, its remaining equipment can be serviced between orders.
        internal bool CanService(FieldOrder order) => WorkingOn(order) ||
            (!State.detained && !State.dispatch.orderHeld && NightFieldOrders.Done(order,State) && NightShiftTrail.Offered(State,order));

        float circuitAnnouncedUntil=-1;
        void CheckReturnCircuit()
        {
            if (State.detained || State.escaped || State.ended || State.circuit.room != 0 || !ReturnCircuit.CompleteService(State)) return;
            circuitAnnouncedUntil=Time.unscaledTime+8f;
            State.circuit.room=1;
            world.RefreshReturnWing(State);
            soundscape.Play(Player.Eyes.position,"signal",.45f);
        }

        bool AtCircuitRoom(int n) => State.circuit.room==n && !State.detained && !State.ended && !State.escaped;
        void CompleteRoom(int n,string feedback,string sound)
        {
            if (!AtCircuitRoom(n)) return;
            State.circuit.room++;
            circuitAnnouncedUntil=-1;
            world.RefreshReturnWing(State);
            if (State.circuit.Solved) hunter.ReleaseReturnLine();
            Emit(ReturnWing.Desk(n),.55f,sound);
            Notify(feedback,8);
            SaveProgress();
        }

        void BuildReturnWingTargets()
        {
            for(int i=0;i<3;i++)
            {
                int hole=i;
                Target(ReturnWing.Punch(i),"打"+ReturnCircuit.Holes[i],.42f,()=>
                {
                    State.circuit.punch=hole;
                    CompleteRoom(1,"票背还是空白。隔壁熨印架的白灯亮了。","switch");
                },()=>AtCircuitRoom(1));
            }
            Target(ReturnWing.Desk(2),"把票压上透光架",0,()=>
            {
                var t=State.circuit;
                OpenRepair("背印 · 透光架",ReturnWing.Desk(2),ReturnCircuit.Holes,
                    new[]{"纸正面是"+ReturnCircuit.Holes[t.punch]+"。墨面朝下，左右对调。移动镜架对准背孔。"},
                    new[]{2-t.punch},()=>CompleteRoom(2,"水印显出一个「"+t.ReturnLabel+"」。监听间的管帽松开了。","paper"));
            },()=>AtCircuitRoom(2));
            Target(ReturnWing.Desk(3),"接听三根传声管",.38f,()=>
            {
                var t=State.circuit;
                t.heard=true; t.matched=0;
                StopPipePlayback();
                pipeSequence=StartCoroutine(PlayPipeSequence(t));
                Notify("听筒里依次传来："+ReturnCircuit.Pipes[t.Beat(0)]+"、"+ReturnCircuit.Pipes[t.Beat(1)]+"、"+ReturnCircuit.Pipes[t.Beat(2)]+"。",6);
                SaveProgress();
            },()=>AtCircuitRoom(3));
            for(int i=0;i<3;i++)
            {
                int tap=i;
                Target(ReturnWing.Tap(i),"叩"+ReturnCircuit.Pipes[i]+"管",0,()=>
                {
                    var t=State.circuit;
                    Emit(ReturnWing.Tap(tap),.46f,"tool");
                    if(tap!=t.Beat(t.matched))
                    {
                        t.matched=0;
                        Notify("回了一声空响。重头叩，或者再听一次。");
                    }
                    else if(++t.matched==3)
                    {
                        StopPipePlayback();
                        CompleteRoom(3,"三根管同时静下来。隔壁气封间吐出一只黄铜筒。","switch");
                        return;
                    }
                    else Notify("管壁应了一声。");
                    SaveProgress();
                },()=>AtCircuitRoom(3) && State.circuit.heard);
            }
            Target(ReturnWing.Desk(4),"取走装票的气筒",.6f,()=>
            {
                State.circuit.capsule=true; world.RefreshReturnWing(State);
                Notify("铜筒里有气压。握柄上还缠着一截黑布。"); SaveProgress();
            },()=>AtCircuitRoom(4)&&!State.circuit.capsule);
            Target(ReturnWing.Bleed,"松开泄压阀",1.35f,()=>
            {
                State.circuit.vented=true; Emit(ReturnWing.Bleed,.9f,"pipe-leak");
                Notify("压力表回零。票背的回路号还能辨清。"); SaveProgress();
            },()=>AtCircuitRoom(4) && State.circuit.capsule && !State.circuit.vented);
            for(int i=0;i<2;i++)
            {
                string code=i==0?"甲":"乙";
                Target(ReturnWing.Mouth(i),"投递"+code+"路",.6f,()=>
                {
                    if(code!=State.circuit.ReturnLabel) { Notify("筒口顶住了。看看票根的水印。"); return; }
                    CompleteRoom(4,"筒子没有往前滚。地板下面，响起收件人掀盖的声音。","land");
                },()=>AtCircuitRoom(4)&&State.circuit.vented);
            }
            Target(ReturnWing.SealPull,"掀开签收窗铅封",1f,()=>
            {
                State.circuit.coverLifted=true; world.RefreshReturnWing(State);
                soundscape.Play(ReturnWing.Desk(5),"paper",.7f);
                Notify("铅封压着一九八七年的报纸。窗里刚落下的票，却打着你选的"+ReturnCircuit.Holes[State.circuit.punch]+"。自己的半张还在身上。",12);
                SaveProgress();
            },()=>AtCircuitRoom(5)&&!State.circuit.coverLifted);
            Target(ReturnWing.Desk(5),"重叠两联票根",0,()=>
            {
                var t=State.circuit;
                OpenRepair("两联骑缝孔",ReturnWing.Desk(5),ReturnCircuit.Holes,new[]{"把自己刚刚打过的那个孔压在旧联的透光点上。"},
                    new[]{t.punch},()=>
                    {
                        t.compared=true;
                        Notify("两个孔连毛刺都贴合。刚才投出的票，落在了八七年。那头的听筒还没挂。",9);
                        SaveProgress();
                    });
            },()=>AtCircuitRoom(5)&&State.circuit.coverLifted&&!State.circuit.compared);
            Target(ReturnWing.Handset,"拿起回线听筒",.35f,()=>
            {
                OpenRepair("七月十七 · 回线",ReturnWing.Handset,
                    new[]{"再听","让她上车"},new[]{"水那头传来你刚叩的三声。「十二个孩子都在车上。刚才喊救命的，是你？」",
                    State.circuit.CallerEcho+"她听见的是今晚的你。她摘下黄头套，又要回水里找第十三个人。",
                    "烧水时，两路管子各通一岸。打烊一泄压，两次末班车便听得见彼此。黄皮顺着你出声的地方追了一夜。现在，给她一个收班的回话。"},
                    new[]{0,0,1},()=>CompleteRoom(5,"「好，我上车。」黄头套瘪在听筒边。第一次，打烊后的管道里没有笑声。","signal"));
            },()=>AtCircuitRoom(5)&&State.circuit.compared);
        }

        Coroutine pipeSequence;
        void StopPipePlayback()
        {
            if(pipeSequence!=null) StopCoroutine(pipeSequence);
            pipeSequence=null;
            world.ShowSoundingPipe(-1);
        }
        System.Collections.IEnumerator PlayPipeSequence(ReturnCircuit t)
        {
            yield return null;
            for(int i=0;i<3 && AtCircuitRoom(3);i++)
            {
                int pipe=t.Beat(i);
                world.ShowSoundingPipe(pipe);
                soundscape.Play(ReturnWing.Tap(pipe),pipe==0?"tool":pipe==1?"signal":"switch",.65f);
                yield return new WaitForSeconds(.36f);
                world.ShowSoundingPipe(-1);
                yield return new WaitForSeconds(.29f);
            }
            pipeSequence=null;
        }
    }
}
