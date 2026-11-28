using UnityEngine;

namespace AfterHours
{
    internal static class NightReturnLines
    {
        // Two numbered taps on each return line. The dispatch sheet names the circuit only.
        internal static readonly string[] Zones = { "地下设备层", "一楼浴区", "副楼二层" };
        internal static readonly Vector3[] Taps =
        {
            new(-11.2f,-2.38f,10.24f), new(4,-2.38f,10.24f),
            new(-12,1.22f,13.74f), new(9,1.22f,13.74f),
            new(24.7f,4.82f,10.26f), new(37.4f,4.82f,10.26f)
        };
        internal static Vector3 Face(int i) => i / 2 == 1 ? Vector3.back : Vector3.forward;
        internal static string Label(int i) => "拧回水阀 · " + Zones[i/2] + (i%2 == 0 ? "西" : "东");
        internal static bool Leaks(DispatchState job, int tap) =>
            (job.sealedLines & (1 << (tap/2))) == 0 && ((job.leakPattern >> (tap/2)) & 1) == tap%2;
    }

    internal sealed partial class NightGame
    {
        float dripIn;
        void BuildReturnTargets()
        {
            for (int i=0;i<NightReturnLines.Taps.Length;i++)
            {
                int tap=i;
                Target(NightReturnLines.Taps[i] + NightReturnLines.Face(i)*.17f,
                    NightReturnLines.Label(i),1.4f,()=>CloseReturnTap(tap),
                    ()=>CanService(FieldOrder.Drain) && (State.dispatch.sealedLines & (1 << (tap/2))) == 0);
            }
        }

        void CloseReturnTap(int tap)
        {
            var point=NightReturnLines.Taps[tap];
            if (!NightReturnLines.Leaks(State.dispatch,tap))
            {
                Emit(point,1.08f,"tool");
                Notify("阀杆反顶回来。隔墙另一头，水还在响。");
                return;
            }
            State.dispatch.sealedLines |= 1 << (tap/2);
            Emit(point,.53f,"switch");
            world.RefreshReturnLines();
            Notify(State.dispatch.sealedLines==7 ? "最后一股水声断了。街沟的水退下去。" : "表针回正，管里的响声停了。");
            SaveProgress();
        }

        void SoundReturnLines(float dt)
        {
            if (!CanService(FieldOrder.Drain) || State.dispatch.sealedLines == 7 || (dripIn-=dt)>0) return;
            float nearest=15f;
            int tap=-1;
            for (int i=0;i<NightReturnLines.Taps.Length;i++)
            {
                if (!NightReturnLines.Leaks(State.dispatch,i)) continue;
                var delta=Player.Feet-NightReturnLines.Taps[i];
                if (Mathf.Abs(delta.y+1.22f)>2) continue;
                float distance=delta.magnitude;
                if (distance<nearest) { nearest=distance; tap=i; }
            }
            dripIn=.68f + .048f * nearest;
            if (tap>=0) soundscape.Play(NightReturnLines.Taps[tap],"pipe-leak",.71f);
        }
    }

    internal sealed partial class NightWorld
    {
        readonly GameObject[] leakPlumes = new GameObject[6];
        void DressReturnLines()
        {
            for (int i=0;i<NightReturnLines.Taps.Length;i++)
            {
                Vector3 p=NightReturnLines.Taps[i], face=NightReturnLines.Face(i);
                var iron=Trim(p,paintedIron);
                iron.Pipe(p-Vector3.right*.75f,p+Vector3.right*.75f,.047f);
                foreach (int side in new[]{-1,1})
                    iron.Pipe(p+Vector3.right*.44f*side-face*.15f,p+Vector3.right*.44f*side,.065f);
                iron.Pipe(p,p+face*.15f,.044f);
                var handle=Trim(p,brass);
                var hub=p+face*.15f;
                for(int j=0;j<12;j++)
                {
                    Vector3 Rim(int n) => hub + (Vector3.up*Mathf.Cos(n*Mathf.PI/6)+Vector3.right*Mathf.Sin(n*Mathf.PI/6))*.154f;
                    handle.Pipe(Rim(j),Rim(j+1),.017f);
                    if(j%4==0) handle.Pipe(hub,Rim(j),.012f);
                }
                var steam=atmosphere.AddSteam(p+face*.22f,new(.12f,.05f,.12f),new(.82f,.86f,.83f,.48f),6);
                if(steam)
                {
                    var main=steam.main;
                    main.startSize=new ParticleSystem.MinMaxCurve(.14f,.32f);
                    main.startLifetime=new ParticleSystem.MinMaxCurve(1.2f,2.1f);
                    main.maxParticles=16;
                    leakPlumes[i]=steam.gameObject;
                }
            }
        }

        internal void RefreshReturnLines()
        {
            for (int i=0;i<leakPlumes.Length;i++)
                if(leakPlumes[i]) leakPlumes[i].SetActive(game.CanService(FieldOrder.Drain) &&
                    NightReturnLines.Leaks(game.State.dispatch,i));
        }
    }
}
