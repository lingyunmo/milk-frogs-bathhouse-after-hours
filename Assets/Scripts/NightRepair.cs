using System;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightGame
    {
        internal sealed class RepairPanel
        {
            internal string title;
            internal string[] options, prompts;
            internal int[] answer, wholeCode;
            internal int row, selected;
            internal Vector3 source;
            internal Action Complete;
        }
        RepairPanel panel;

        void OpenRepair(string title, Vector3 source, string[] options, string[] prompts, int[] answer, Action complete)
        {
            panel=new RepairPanel{title=title,source=source,options=options,prompts=prompts,answer=answer,Complete=complete};
        }

        void ReadRepairInput()
        {
            if(Input.GetKeyDown(KeyCode.LeftArrow)||Input.GetKeyDown(KeyCode.A))
                panel.selected=(panel.selected+panel.options.Length-1)%panel.options.Length;
            if(Input.GetKeyDown(KeyCode.RightArrow)||Input.GetKeyDown(KeyCode.D))
                panel.selected=(panel.selected+1)%panel.options.Length;
            if (Input.GetKeyDown(KeyCode.Space)) ConfirmRepair();
        }

        void ConfirmRepair()
        {
            if(panel.title=="堤岸隔离闸" && panel.selected==0) { panel=null; Notify("封条还在。你把匣盖合上了。"); return; }
            if (panel.wholeCode != null)
            {
                panel.wholeCode[panel.row]=panel.selected;
                if (panel.row+1 == panel.answer.Length && !System.Linq.Enumerable.SequenceEqual(panel.wholeCode,panel.answer))
                {
                    panel.row=panel.selected=0; Emit(panel.source,.8f,"signal"); Notify("锁舌弹回。四只字轮一起归零。"); return;
                }
            }
            else if(panel.selected!=panel.answer[panel.row])
            {
                Emit(panel.source,.58f,"tool");
                Notify(panel.title switch
                {
                    "收洗数 · 七月十七" => "收洗联的章和这个数对不上。",
                    "背印 · 透光架" => "光没穿过孔。墨面朝下时，左右正好相反。",
                    "两联骑缝孔" => "边缘没有贴上。看一眼票根，哪个孔才是你自己打的？",
                    "七月十七 · 回线" => "听筒里还有人。听清楚再回答。",
                    "旧站牌 · 接应方向" => "司机：这边看不见。相片背面写着朝哪儿。",
                    _ => "接触不良，检查机壳上的标记。"
                });
                return;
            }
            if(++panel.row<panel.answer.Length)
            {
                Emit(panel.source,.34f,"tool");
                return;
            }
            var complete=panel.Complete;
            panel=null;
            complete();
        }

    }
}
