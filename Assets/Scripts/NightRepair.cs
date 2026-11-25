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
            internal int[] answer;
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
            if(panel.selected!=panel.answer[panel.row])
            {
                Emit(panel.source,.58f,"tool");
                Notify("接触不良，检查机壳上的标记。");
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
