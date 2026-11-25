using System.IO;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightGame
    {
        static readonly Color Amber=new(.94f,.78f,.49f);
        static readonly Color Paper=new(.94f,.93f,.89f);
        GUIStyle heading, label, quiet, centered, button;
        Texture2D pixel;
        Font typeface;

        void SetupHud()
        {
            typeface=Resources.Load<Font>("Typeface/AfterHours");
            pixel=Texture2D.whiteTexture;
        }

        GUIStyle Style(int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var style=new GUIStyle(GUI.skin.label)
            {
                font=typeface,
                fontSize=size,
                alignment=alignment,
                wordWrap=true
            };
            style.normal.textColor=color;
            return style;
        }

        void OnGUI()
        {
            if(Player==null || State==null)return;
            if(heading==null)
            {
                heading=Style(43,Paper);
                label=Style(18,Paper);
                quiet=Style(14,new Color(.76f,.78f,.74f));
                centered=Style(18,Paper,TextAnchor.MiddleCenter);
                button=Style(19,Paper);
                button.padding=new RectOffset(19,12,0,0);
            }

            float scale=Mathf.Clamp(Screen.height/800f,.64f,1.55f);
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            float width=Screen.width/scale,height=Screen.height/scale;
            if(menu||State.escaped)DrawMenu(width,height);
            else if(!caught)DrawGame(width,height);
        }

        void Fill(Rect area,Color color)
        {
            GUI.color=color;
            GUI.DrawTexture(area,pixel);
            GUI.color=Color.white;
        }

        bool MenuButton(float x,float y,float w,string text)
        {
            var rect=new Rect(x,y,w,49);
            Fill(rect,rect.Contains(Event.current.mousePosition)?new Color(.33f,.28f,.18f):new Color(.13f,.16f,.16f));
            Fill(new Rect(x,y,2,49),Amber);
            return GUI.Button(rect,text,button);
        }

        void DrawMenu(float w,float h)
        {
            Fill(new Rect(0,0,w,h),new Color(.012f,.019f,.027f,.87f));
            float x=Mathf.Max(32,w*.10f), y=Mathf.Max(38,h*.16f);
            GUI.Label(new Rect(x,y,w-x-15,70),State.escaped?"天亮以前":caught?"听见它笑了":running?"暂停":"奶娃浴场：打烊后",heading);
            GUI.Label(new Rect(x,y+73,w-x-16,44),State.escaped?"车门关上，浴场的灯消失在后窗。":caught?"回到上一次留下的进度。":running?"第一夜":"第一夜   /   夜班故障",quiet);
            float bw=Mathf.Min(340,w-x-24);
            y+=136;
            if(!running)
            {
                if(MenuButton(x,y,bw,"开始第一夜"))Begin(null);
                if(File.Exists(NightSave.FilePath)&&MenuButton(x,y+60,bw,"继续存档"))
                {
                    var saved=NightSave.Read();
                    if(saved!=null)Begin(saved);
                    else Notify("存档无法读取，仍可开始新一夜。");
                }
            }
            else if(State.escaped)
            {
                if(MenuButton(x,y,bw,"再开始一夜"))Begin(null);
            }
            else if(caught)
            {
                if(MenuButton(x,y,bw,"从上一次进度继续"))Begin(NightSave.Read());
            }
            else
            {
                if(MenuButton(x,y,bw,"继续"))SetMenu(false);
                if(MenuButton(x,y+60,bw,"保存当前进度"))SaveHere();
            }
            if(MenuButton(x,y+126,bw,"退出游戏"))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying=false;
#else
                Application.Quit();
#endif
            }
            GUI.Label(new Rect(x,h-102,w-x-22,64),"WASD 移动 · 鼠标观察 · C 蹲行 · Shift 奔跑\nE 交互 · F 手电 · R 换电 · G 石子 · F5 保存",quiet);
            if(messageTime>0)GUI.Label(new Rect(x,y+188,w-x-12,51),message,label);
        }

        void DrawGame(float w,float h)
        {
            Fill(new Rect(w/2-2,h/2-2,4,4),Paper);
            GUI.Label(new Rect(25,19,w-50,29),Objective(),label);
            if(panel!=null)DrawRepair(w,h);
            if(focus!=null && panel==null)
            {
                float barWidth=Mathf.Min(440,w-48);
                GUI.Label(new Rect((w-barWidth)/2,h*.65f,barWidth,50),$"E   {focus.Label}",centered);
                if(held!=null&&held.Hold>0)
                {
                    Fill(new Rect((w-barWidth)/2,h*.65f+54,barWidth,4),new Color(.35f,.37f,.35f,.8f));
                    Fill(new Rect((w-barWidth)/2,h*.65f+54,barWidth*Mathf.Clamp01(holdTime/held.Hold),4),Amber);
                }
            }
            if(messageTime>0)GUI.Label(new Rect(25,h*.78f,w-50,45),message,centered);
            if(State.torch)
            {
                GUI.Label(new Rect(25,h-70,340,33),$"手电   {Mathf.CeilToInt(State.charge/3.6f)}%    电池 × {State.cells}",quiet);
                Fill(new Rect(26,h-38,134,4),new Color(.36f,.4f,.40f));
                Fill(new Rect(26,h-38,134*Mathf.Clamp01(State.charge/360),4),Amber);
            }
            GUI.Label(new Rect(25,h-32,260,26),$"石子 × {State.stones}"+(Player.Crouched?"     蹲行":""),quiet);
        }

        string Objective()
        {
            if(State.rescueTime==0)return "从车侧登车";
            if(State.beacon)return "接应灯已亮";
            if(State.contacted)return "到街道尽头翻起接应板";
            if(!State.torch)return "更衣室";
            if(!State.power)return "恢复浴场供电";
            if(!State.uplink)return "让三楼线路上线";
            return "回前台发送求援";
        }
    }
}
