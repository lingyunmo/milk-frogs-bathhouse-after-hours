using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightGame
    {
        static readonly string[] PaFiles = { "intake", "feedback", "evacuate", "radiation", "gate", "rollcall" };
        static readonly string[] PaLines = {
            "东堤自动值守。核燃料已部署。东闸没有电。",
            "东闸供电恢复。警告。旧浴场正在回送信号。",
            "检测到核事故。请尽快撤离浴场区域。",
            "西侧探头，辐射指数九十七。四号楼，失去联系。",
            "东闸已开。跟着绿灯走。请勿回头。",
            "西区交班。出勤记录，零人。收到夜班回执，四十二份。日期，一九八七年七月十七日。"
        };
        AudioSource eastHorn;
        readonly AudioClip[] paClips=new AudioClip[PaFiles.Length];
        readonly Queue<int> paQueue=new();
        string paWords="";
        float paRemaining;
        float paAftershock=-1;
        int paAftershockStep;
        int speaking;
        internal string PaCaption
        {
            get
            {
                if(paRemaining<=0) return "";
                if(speaking!=5 || !eastHorn.clip) return paWords;
                float t=eastHorn.clip.length+.09f-paRemaining;
                return "东堤广播　"+(t<2.6f?"西区交班。出勤记录，零人。":t<5.1f?"收到夜班回执，四十二份。":"日期，一九八七年七月十七日。");
            }
        }

        void ResetPA()
        {
            if(eastHorn) eastHorn.Stop();
            paQueue.Clear(); paRemaining=0; paWords=""; paAftershock=-1;
        }

        void SpeakPA(int line)
        {
            if(!eastHorn)
            {
                var node=new GameObject("东堤公共广播");
                node.transform.SetParent(transform,false);
                eastHorn=node.AddComponent<AudioSource>();
                eastHorn.transform.position=NightGrid.Switch+Vector3.up*2.5f;
                eastHorn.spatialBlend=.18f; eastHorn.dopplerLevel=0; eastHorn.minDistance=10;
                eastHorn.playOnAwake=false; eastHorn.priority=8; eastHorn.volume=.91f;
                for(int i=0;i<paClips.Length;i++) paClips[i]=Resources.Load<AudioClip>("Audio/east-pa-"+PaFiles[i]);
            }
            if(paRemaining>0) { paQueue.Enqueue(line); return; }
            PlayAnnouncement(line);
        }

        void PlayAnnouncement(int line)
        {
            speaking=line;
            eastHorn.clip=paClips[line];
            if(eastHorn.clip) eastHorn.Play();
            paWords="东堤广播　"+PaLines[line];
            paRemaining=(eastHorn.clip ? eastHorn.clip.length : 4.2f)+.09f;
        }

        void TickPA(float dt)
        {
            if(paRemaining<=0) return;
            paRemaining-=dt;
            if(paRemaining<=0 && paQueue.Count>0) PlayAnnouncement(paQueue.Dequeue());
        }

        void ScheduleFalloutPA() { paAftershock=3.5f; paAftershockStep=0; }

        void TickFalloutPA(float dt)
        {
            TickPA(dt);
            if(paAftershock<0) return;
            paAftershock-=dt;
            if(paAftershock>0) return;
            SpeakPA(paAftershockStep==0?2:3);
            paAftershock=paAftershockStep++==0 ? 6.1f : -1;
        }
    }
}
