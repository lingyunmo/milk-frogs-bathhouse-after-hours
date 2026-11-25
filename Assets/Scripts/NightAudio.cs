using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed class NightAudio
    {
        readonly Dictionary<string,AudioClip> clips=new();
        readonly AudioSource[] emitters=new AudioSource[10];
        readonly AudioSource generator, entrance;
        int index;

        internal NightAudio(Transform root)
        {
            clips["step"]=Clip("step",.19f,60,.50f,13);
            clips["hunter"]=Clip("hunter",.32f,39,.33f,38);
            clips["land"]=Clip("land",.28f,76,.55f,63);
            clips["tool"]=Clip("tool",.36f,314,.53f,52);
            clips["switch"]=Clip("switch",.44f,118,.62f,12);
            clips["torch-click"]=Clip("torch-click",.093f,310,.61f,102);
            clips["pickup"]=Clip("pickup",.17f,185,.83f,203);
            clips["paper"]=Clip("paper",.14f,570,.96f,191);
            clips["signal"]=Clip("signal",.8f,426,.025f,16);
            clips["drone"]=Clip("drone",3.4f,104,.14f,119);
            clips["truck"]=Clip("truck",2.3f,43,.24f,78);
            clips["laugh"]=Laugh();
            for(int i=0;i<emitters.Length;i++)emitters[i]=Source(root);
            generator=Source(root);
            generator.loop=true;
            generator.clip=Clip("motor",2,46,.077f,6,true);
            generator.transform.position=new Vector3(-10,-2.5f,20);
            generator.Play();
            entrance = Source(root);
            entrance.spatialBlend = 0;
            entrance.loop = true;
            entrance.clip = TitleTone();
            entrance.volume = .38f;
        }

        internal void SetTitle(bool visible)
        {
            if (visible)
            {
                generator.volume = 0;
                if (!entrance.isPlaying) entrance.Play();
            }
            else entrance.Stop();
        }

        static AudioClip TitleTone()
        {
            const int rate = 24000;
            const int length = 12;
            var wave = new float[rate * length];
            for (int i = 0; i < wave.Length; i++)
            {
                float t = (float)i / rate;
                float phase = t * 2 * Mathf.PI;
                float tide = .54f + .22f * Mathf.Sin(phase / length);
                wave[i] = tide * (.18f * Mathf.Sin(phase * 43) + .066f * Mathf.Sin(phase * 47.5f) +
                    .023f * Mathf.Sin(phase * 86 + .38f * Mathf.Sin(phase / 3)));
            }
            return Ready("Old ventilation", wave, rate);
        }

        internal void Tick(bool powered) => generator.volume=powered?.22f:0;

        static AudioSource Source(Transform root)
        {
            var go=new GameObject("Spatial sound");
            go.transform.SetParent(root);
            var a=go.AddComponent<AudioSource>();
            a.spatialBlend=1;
            a.minDistance=1.6f;
            a.maxDistance=36;
            a.dopplerLevel=0;
            a.playOnAwake=false;
            return a;
        }

        internal void Play(Vector3 position,string sound,float gain,bool duringPause=false)
        {
            var source=emitters[index++%emitters.Length];
            source.Stop();
            source.transform.position=position;
            source.volume=Mathf.Clamp01(gain);
            source.ignoreListenerPause = duringPause;
            source.pitch=sound=="step"?UnityEngine.Random.Range(.86f,1.12f):1;
            source.clip=clips[sound];
            source.Play();
        }

        static AudioClip Clip(string name,float seconds,float frequency,float grit,int seed,bool loop=false)
        {
            const int rate=24000;
            var random=new System.Random(seed);
            var data=new float[Mathf.RoundToInt(seconds*rate)];
            float low=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate,u=t/seconds;
                low=.88f*low+.12f*(float)(random.NextDouble()*2-1);
                float wave=Mathf.Sin(2*Mathf.PI*t*frequency)+.25f*Mathf.Sin(4*Mathf.PI*t*frequency);
                float envelope=loop?.62f:Mathf.Min(t/.013f,1)*Mathf.Pow(1-u,2);
                data[i]=envelope*(.37f*(1-grit)*wave+grit*low);
            }
            return Ready(name,data,rate);
        }

        static AudioClip Laugh()
        {
            const int rate=24000;
            var data=new float[rate*2];
            float phase=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate;
                float frequency=102+33*Mathf.Sin(t*4)+13*Mathf.Sin(2*Mathf.PI*t*3.25f);
                phase+=2*Mathf.PI*frequency/rate;
                float breath=Mathf.Pow(Mathf.Max(0,Mathf.Sin(2*Mathf.PI*t*3.25f)),.7f);
                data[i]=Mathf.Min(t/.03f,1)*Mathf.Min((2-t)/.16f,1)*breath*
                    (.22f*Mathf.Sin(phase)+.14f*Mathf.Sin(phase*3)+.066f*Mathf.Sin(phase*7));
            }
            return Ready("hunter laugh",data,rate);
        }

        static AudioClip Ready(string name,float[] samples,int hz)
        {
            var clip=AudioClip.Create(name,samples.Length,1,hz,false);
            clip.SetData(samples,0);
            return clip;
        }
    }
}
