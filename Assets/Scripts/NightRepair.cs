using System;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightGame
    {
        sealed class RepairPanel
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
            if(!Input.GetKeyDown(KeyCode.Space))return;
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

        void DrawRepair(float width,float height)
        {
            var area=new Rect(width*.5f-Mathf.Min(286,width*.43f),height*.5f-105,Mathf.Min(572,width*.86f),207);
            Fill(area,new Color(.033f,.049f,.055f,.91f));
            Fill(new Rect(area.x,area.y,area.width,2),Amber);
            GUI.Label(new Rect(area.x+22,area.y+15,area.width-44,30),panel.title,label);
            GUI.Label(new Rect(area.x+22,area.y+49,area.width-44,40),panel.prompts[panel.row],quiet);
            float column=(area.width-44)/panel.options.Length;
            for(int i=0;i<panel.options.Length;i++)
            {
                var rect=new Rect(area.x+22+column*i,area.y+97,column-4,38);
                Fill(rect,i==panel.selected?new Color(.43f,.31f,.18f):new Color(.15f,.19f,.20f));
                GUI.Label(rect,panel.options[i],centered);
            }
            GUI.Label(new Rect(area.x+22,area.y+151,area.width-44,35),"A / D 切换   ·   空格确认   ·   Esc 松手",quiet);
        }
    }
}
