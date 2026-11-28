using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    // Stable bit positions in the local profile. A new night replaces the run file only.
    internal enum Ticket { Passenger, Name, Broadcast, Deposit, LastNight, Return }
    internal static class NightTickets
    {
        internal const string ProfileKey="AfterHours.TicketStubs.v1";
        internal const string TrueTitle="来电人已上车";
        internal static readonly (Ticket Id,string Title,string Hint,string Summary)[] Book =
        {
            (Ticket.Passenger,"末班乘客","站牌上留着一张没写姓名的票。","到了清晨，你仍叫不出照片里那个人的名字。"),
            (Ticket.Name,"未寄出的名字","守闸人的枕头下压着半页纸。","一个人的名字，跟着车票进了你的口袋。"),
            (Ticket.Broadcast,"有人应名","弱电间的话筒还挂在原处。","你报出了那个名字，最后一排有人答到。"),
            (Ticket.Deposit,"请日班亲启","交班桌上有一块还软的封蜡。","司机签收了那封交给早班的底片。"),
            (Ticket.LastNight,"最后一夜","地下室的门闩又落下一格。","第五夜，走廊的灯再也没亮。"),
            (Ticket.Return,TrueTitle,"？？？","最后那通求救电话终于等到了回话。")
        };
        internal static int Mask => PlayerPrefs.GetInt(ProfileKey,0);
        internal static bool Stamped(Ticket ticket) => (Mask & (1<<(int)ticket))!=0;
        internal static Ticket Result(NightState s) => s.escaped && s.circuit.Solved ? Ticket.Return :
            s.ended && !s.escaped ? Ticket.LastNight : s.story.choice switch
            {
                WitnessChoice.Broadcast=>Ticket.Broadcast,
                WitnessChoice.Seal=>Ticket.Deposit,
                _ when NightShiftTrail.Has(s,NightItemId.Names)=>Ticket.Name,
                _=>Ticket.Passenger
            };

        internal static void Record(NightState s)
        {
            if(s==null || (!s.escaped && !s.ended)) return;
            var ticket=Result(s);
            if(Stamped(ticket)) return;
            PlayerPrefs.SetInt(ProfileKey,Mask | (1<<(int)ticket));
            PlayerPrefs.Save();
        }
    }

    internal sealed partial class NightInterface
    {
        Button TicketTab(VisualElement stage,bool open) => Act(stage, open?"末班车票夹  −":"末班车票夹  ＋",
            ()=>Open(open?"":"tickets"),"ticket-tab serif","ticket-tab");

        void BuildTicketDrawer(VisualElement stage)
        {
            var toggle=TicketTab(stage,true);
            var card=Area(stage,"ticket-wallet");
            var top=Area(card,"ticket-head");
            Type(top,"留存票根","ticket-caption serif");
            int count=0;
            foreach(var page in NightTickets.Book) if(NightTickets.Stamped(page.Id)) count++;
            Type(top,count+" / "+NightTickets.Book.Length,"ticket-count");
            var pages=new ScrollView(ScrollViewMode.Vertical);
            pages.AddToClassList("ticket-scroll");
            card.Add(pages);
            foreach(var page in NightTickets.Book)
            {
                bool stamped=NightTickets.Stamped(page.Id);
                bool sealedTicket=page.Id==Ticket.Return && !stamped;
                var row=Area(pages.contentContainer,"ticket-stub");
                row.name="ticket-"+(int)page.Id;
                var copy=Area(row,"ticket-copy");
                Type(copy,sealedTicket?"？？？":page.Title,"ticket-name serif");
                Type(copy,sealedTicket?"？？？":stamped?page.Summary:page.Hint,"ticket-line");
                if(!sealedTicket) Type(row,stamped?"已验":"待寻",stamped?"ticket-ink ticket-ink--used":"ticket-ink");
            }
            Reveal(card);
            Focus(toggle);
        }
    }
}
