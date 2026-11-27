using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed class NightAudio
    {
        readonly Dictionary<string,AudioClip> clips=new();
        readonly AudioSource[] emitters=new AudioSource[10];
        const float TitleAmbience = .62f;
        const float RoomAmbience = .46f;
        const float PursuitAmbience = .18f;
        readonly AudioSource generator, entrance, ambience, titleLight, titleSpark, titleContact;
        readonly AudioSource captureLaugh, menu;
        readonly AudioClip menuClick, menuTick;
        float nextMenuTick;
        int index;

        internal float CaptureDuration => captureLaugh.clip ? captureLaugh.clip.length : 2.35f;

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
            for(int i=0;i<emitters.Length;i++)emitters[i]=Source(root);
            generator=Source(root);
            generator.loop=true;
            generator.clip=Clip("motor",2,46,.077f,6,true);
            generator.transform.position=new Vector3(-10,-2.5f,20);
            generator.Play();
            entrance = Source(root, "Title ambience");
            entrance.spatialBlend = 0;
            entrance.loop = true;
            entrance.clip = Resources.Load<AudioClip>("Audio/after-hours-title");
            entrance.volume = TitleAmbience;

            ambience = Source(root, "Bathhouse ambience");
            ambience.spatialBlend = 0;
            ambience.loop = true;
            ambience.clip = Resources.Load<AudioClip>("Audio/bathhouse-ambience");
            ambience.volume = 0f;

            titleLight = Source(root, "Title light ballast");
            titleLight.spatialBlend = 0;
            titleLight.panStereo = .48f;
            titleLight.clip = LightBallast();

            titleSpark = Source(root, "Title lamp spark");
            titleSpark.spatialBlend = 0;
            titleSpark.panStereo = .14f;
            titleSpark.clip = LampSpark();

            titleContact = Source(root, "Title near contact");
            titleContact.spatialBlend = 0;
            titleContact.volume = .57f;
            titleContact.clip = NearContact();

            captureLaugh = Source(root, "Hunter capture laugh");
            captureLaugh.clip = Resources.Load<AudioClip>("Audio/haunted-laugh-v1");
            captureLaugh.volume = .72f;
            captureLaugh.ignoreListenerPause = true;

            menu = Source(root, "Menu feedback");
            menu.spatialBlend = 0;
            menu.ignoreListenerPause = true;
            menuClick = Clip("Menu confirm", .105f, 268, .57f, 62);
            menuTick = Clip("Menu adjustment", .05f, 630, .66f, 95);
        }

        internal void PlayCaptureLaugh(Vector3 position)
        {
            if (!captureLaugh.clip) return;
            captureLaugh.transform.position = position;
            captureLaugh.Play();
        }

        internal void StopCaptureLaugh() => captureLaugh.Stop();

        internal void PlayMenuSound() => menu.PlayOneShot(menuClick, .75f);

        internal void PlayMenuTick()
        {
            if (Time.unscaledTime < nextMenuTick) return;
            nextMenuTick = Time.unscaledTime + .075f;
            menu.PlayOneShot(menuTick, .47f);
        }

        internal void SetScreen(NightScreen screen)
        {
            if (screen == NightScreen.Title)
            {
                generator.volume = 0;
                ambience.Stop();
                ambience.volume = 0f;
                entrance.volume = TitleAmbience;
                if (entrance.clip && !entrance.isPlaying) entrance.Play();
                return;
            }
            entrance.Stop();
            titleLight.Stop();
            titleSpark.Stop();
            titleContact.Stop();
            // The shared listener pauses this source in menus and preserves its playhead.
            if (screen == NightScreen.Play && ambience.clip && !ambience.isPlaying) ambience.Play();
            if (screen == NightScreen.Capture) ambience.volume = PursuitAmbience;
        }

        internal void PlayTitleLightFlicker(float strength)
        {
            titleLight.volume = .56f * Mathf.Clamp01(strength);
            titleLight.Play();
        }

        internal void PlayTitleSpark(float strength)
        {
            titleSpark.volume = .7f * Mathf.Clamp01(strength);
            titleSpark.Play();
        }

        internal void SetTitleBlackout(bool dark)
        {
            float target = dark ? .018f : TitleAmbience;
            float rate = dark ? 10f : 1.36f;
            float delta = Mathf.Min(Time.unscaledDeltaTime, .075f);
            entrance.volume = Mathf.MoveTowards(entrance.volume, target, rate * delta);
        }

        internal void PlayTitleContact() => titleContact.Play();

        static AudioClip NearContact()
        {
            const int rate = 24000;
            var wave = new float[rate * 68 / 100];
            var noise = new System.Random(312);
            float fast = 0, slow = 0, phase = 0;
            for (int i = 0; i < wave.Length; i++)
            {
                float t = (float)i / rate;
                float white = 2f * (float)noise.NextDouble() - 1f;
                fast = Mathf.Lerp(fast, white, .28f);
                slow = Mathf.Lerp(slow, white, .036f);
                float texture = fast - slow;
                float onset = Mathf.Min(t * 350f, 1f);
                float scrape = onset * (.22f * Mathf.Exp(-t * 61f) +
                    .12f * Mathf.Exp(-Mathf.Abs(t - .026f) * 77f));
                phase += 2 * Mathf.PI * (46 + 66 * Mathf.Exp(-t * 21f)) / rate;
                float knock = .39f * onset * Mathf.Sin(phase) * Mathf.Exp(-t * 29f);
                float breath = Mathf.SmoothStep(0, 1, (t - .055f) / .10f) *
                    (1 - Mathf.SmoothStep(0, 1, (t - .24f) / .42f));
                float rasp = .55f + .45f * Mathf.Sin(t * 2 * Mathf.PI * 37f + fast * 1.8f);
                wave[i] = knock + scrape * (white - fast) + breath * (.23f * texture + .027f * rasp * slow);
            }
            return Ready("Close scrape, breath and knock", wave, rate);
        }

        static AudioClip LightBallast()
        {
            const int rate = 24000;
            var wave = new float[rate * 24 / 100];
            var noise = new System.Random(178);
            var filtered = 0f;
            for (int i = 0; i < wave.Length; i++)
            {
                float t = (float)i / rate;
                float white = 2f * (float)noise.NextDouble() - 1f;
                filtered = .72f * filtered + .28f * white;
                float power = Mathf.Min(t * 180f, 1f) * Mathf.Pow(1f - (float)i / wave.Length, 2f);
                float contact = Mathf.Exp(-t * 64f) + .48f * Mathf.Exp(-Mathf.Abs(t - .072f) * 105f);
                float phase = t * 2f * Mathf.PI * 100f;
                wave[i] = power * (.10f * Mathf.Sin(phase) + .036f * Mathf.Sin(3f * phase)
                    + contact * .25f * (white - filtered));
            }
            return Ready("Broken light ballast", wave, rate);
        }

        static AudioClip LampSpark()
        {
            const int rate = 24000;
            var wave = new float[rate * 18 / 100];
            var noise = new System.Random(529);
            float[] popTimes = { .002f, .029f, .067f, .119f };
            float[] popStrengths = { 1f, .57f, .83f, .42f };
            for (int pop = 0; pop < popTimes.Length; pop++)
            {
                int start = Mathf.RoundToInt(popTimes[pop] * rate);
                int end = Mathf.Min(start + rate / 25, wave.Length);
                float filtered = 0f;
                float ringFrequency = 1320f + pop * 175f;
                for (int i = start; i < end; i++)
                {
                    float time = (float)(i - start) / rate;
                    float white = 2f * (float)noise.NextDouble() - 1f;
                    filtered = Mathf.Lerp(filtered, white, .24f);
                    float envelope = Mathf.Min(time * 2600f, 1f) * Mathf.Exp(-time * 158f);
                    float ring = Mathf.Sin(time * 2f * Mathf.PI * ringFrequency);
                    wave[i] += popStrengths[pop] * envelope * (.44f * (white - filtered) + .10f * ring);
                }
            }
            return Ready("Lamp arc crackle", wave, rate);
        }

        internal void Tick(bool powered, bool pursuing, float dt)
        {
            generator.volume = powered ? .22f : 0f;
            float target = pursuing ? PursuitAmbience : RoomAmbience;
            ambience.volume = Mathf.MoveTowards(ambience.volume, target, dt * .5f);
        }

        static AudioSource Source(Transform root, string name = "Spatial sound")
        {
            var go=new GameObject(name);
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

        static AudioClip Ready(string name,float[] samples,int hz)
        {
            var clip=AudioClip.Create(name,samples.Length,1,hz,false);
            clip.SetData(samples,0);
            return clip;
        }
    }
}
