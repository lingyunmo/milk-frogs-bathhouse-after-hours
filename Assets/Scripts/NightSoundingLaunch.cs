using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal static class SoundingReach
    {
        internal const float Length = 156f, Cruise = 3.05f, Width = 6.8f;
        internal static readonly Vector3 Console = new(201.5f,1.05f,21.5f);
        internal static readonly string[] Names = { "芦苇弯", "旧桥墩", "泄洪口" };
        internal static float Centre(float m, int leg)
        {
            return leg switch
            {
                0 => 4.8f*Mathf.Sin(m*.050f),
                1 => 3.4f*Mathf.Sin(m*.069f)+1.5f*Mathf.Sin(m*.12f),
                _ => 5.1f*Mathf.Sin(m*.052f+.44f)
            };
        }
        internal static float Buoy(int leg) => Centre(Length,leg) + (leg==1 ? 2.6f : -2.6f);
        internal static Vector2 Shoal(int index,int leg)
        {
            float m = 32 + index*42 + 5*leg;
            return new(Centre(m,leg)+((index+leg)%2==0 ? 1.9f : -1.9f),m);
        }
    }

    internal sealed partial class NightGame
    {
        internal bool Piloting { get; private set; }
        internal float LaunchKnock { get; private set; }
        float sideways;

        void BuildSoundingTarget()
        {
            Target(SoundingReach.Console,"接管测深艇",.5f,()=>
            {
                CloseDispatch();
                Piloting=true;
                soundscape.Play(SoundingReach.Console,"switch",.44f);
            },()=>WorkingOn(FieldOrder.Survey) && State.survey.camera && State.survey.soundings<3);
        }

        internal void SteerLaunch(float rudder, bool throttle, bool lowerLead, float dt)
        {
            if (!Piloting || dt<=0) return;
            var film=State.survey;
            int leg=film.soundings;
            float m=film.boatMetres;
            float crossflow=Mathf.Sin(m*.18f+leg*1.56f)*(.32f+leg*.20f);
            float speed=Time.unscaledTime<LaunchKnock ? .9f : SoundingReach.Cruise;
            float next=Mathf.Min(SoundingReach.Length,m+(throttle?dt*speed:0));
            sideways=Mathf.MoveTowards(sideways,Mathf.Clamp(rudder,-1,1)*4.7f,dt*11);
            float x=film.boatOffset+(sideways + (throttle ? crossflow : 0))*dt;
            float centre=SoundingReach.Centre(next,leg);
            bool scrape=Mathf.Abs(x-centre)>SoundingReach.Width-.45f;
            for(int i=0;i<3;i++)
            {
                Vector2 bar=SoundingReach.Shoal(i,leg);
                if(Mathf.Abs(next-bar.y)<2.6f && Mathf.Abs(x-bar.x)<2.15f) scrape=true;
            }
            // Steering remains live when grounded. Back the boat away from the bar before applying power.
            film.boatOffset=Mathf.Clamp(x,centre-SoundingReach.Width+.40f,centre+SoundingReach.Width-.40f);
            if(throttle && scrape)
            {
                next=Mathf.Max(0,m-dt*1.65f);
                if(Time.unscaledTime>=LaunchKnock)
                {
                    LaunchKnock=Time.unscaledTime+1.25f;
                    Emit(SoundingReach.Console,.64f,"tool");
                }
            }
            film.boatMetres=next;
            if(!lowerLead || next<SoundingReach.Length-3 || Mathf.Abs(film.boatOffset-SoundingReach.Buoy(leg))>1.65f) return;
            film.soundings++;
            film.boatMetres=0;
            film.boatOffset=film.soundings<3 ? SoundingReach.Centre(0,film.soundings) : 0;
            sideways=0;
            Emit(SoundingReach.Console,.48f,"signal");
            if(film.soundings==3)
            {
                CloseDispatch();
                Notify("三个断面的水深都回来了。");
            }
            SaveProgress();
        }
    }

    // A scrolling plan view on the physical CRT. The radar and the boat share the same river geometry.
    internal sealed class SoundingRaster : VisualElement
    {
        readonly NightGame game;
        public SoundingRaster(NightGame game)
        {
            this.game=game;
            pickingMode=PickingMode.Ignore;
            generateVisualContent+=Draw;
        }

        void Draw(MeshGenerationContext ctx)
        {
            var film=game.State.survey;
            int leg=film.soundings;
            var rect=contentRect;
            if(!game.Piloting || leg>=3 || rect.width<2 || rect.height<2) return;
            var pen=ctx.painter2D;
            float near=film.boatMetres-9;
            float far=near+48;
            Vector2 Plot(float x,float m) => new((.5f+x/30)*rect.width,(far-m)/48*rect.height);
            void Line(Vector2 a,Vector2 b) { pen.BeginPath(); pen.MoveTo(a); pen.LineTo(b); pen.Stroke(); }
            void Ring(Vector2 p,float radius)
            {
                pen.BeginPath(); pen.Arc(p,radius,0,360); pen.Stroke();
            }
            Color green = new(.53f,.90f,.59f);
            pen.lineWidth=1; pen.strokeColor=new Color(.40f,.80f,.58f,.17f);
            for(int x=-12;x<=12;x+=4) Line(Plot(x,near),Plot(x,far));
            for(float z=Mathf.Ceil(near/6)*6;z<=far;z+=6) Line(Plot(-15,z),Plot(15,z));
            for(int side=-1;side<=1;side+=2)
            {
                pen.BeginPath(); pen.MoveTo(Plot(side*15,near));
                for(float z=near;z<=far;z+=1) pen.LineTo(Plot(SoundingReach.Centre(z,leg)+side*SoundingReach.Width,z));
                pen.LineTo(Plot(side*15,far)); pen.ClosePath();
                pen.fillColor=new Color(.21f,.55f,.38f,.22f); pen.Fill();
                pen.strokeColor=green; pen.lineWidth=1.4f; pen.Stroke();
            }
            for(int i=0;i<3;i++)
            {
                Vector2 bar=SoundingReach.Shoal(i,leg);
                if(bar.y<near || bar.y>far) continue;
                var p=Plot(bar.x,bar.y);
                pen.strokeColor=new(.82f,.62f,.31f); pen.lineWidth=2;
                pen.BeginPath(); pen.MoveTo(Plot(bar.x-2.15f,bar.y));
                pen.LineTo(Plot(bar.x,bar.y+2.6f)); pen.LineTo(Plot(bar.x+2.15f,bar.y)); pen.LineTo(Plot(bar.x,bar.y-2.6f)); pen.ClosePath();
                pen.fillColor=new(.66f,.42f,.18f,.5f); pen.Fill(); pen.Stroke();
                Line(p-Vector2.right*4,p+Vector2.right*4);
            }
            if(far>SoundingReach.Length)
            {
                var p=Plot(SoundingReach.Buoy(leg),SoundingReach.Length);
                pen.strokeColor=new(.99f,.83f,.46f); pen.lineWidth=2;
                Ring(p,6); Ring(p,9+2*Mathf.Sin(Time.unscaledTime*4));
            }
            var boat=Plot(film.boatOffset,film.boatMetres);
            pen.BeginPath(); pen.MoveTo(boat+new Vector2(0,-7)); pen.LineTo(boat+new Vector2(-5,5));
            pen.LineTo(boat+new Vector2(5,5)); pen.ClosePath(); pen.fillColor=Color.white; pen.Fill();
        }
    }
}
