using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace AfterHours
{
    internal static class AfterHoursFrontSmoke
    {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string message)
        {
            if(!value) throw new InvalidOperationException("Pressure front: "+message);
        }

        internal static IEnumerator Verify(NightGame game)
        {
            game.NewNight();
            var district=GameObject.Find("First night runtime").transform;
            var world=(NightWorld)typeof(NightGame).GetField("world",Hidden).GetValue(game);
            var wall=district.Find("旧脱水车间/正面窗下墙").GetComponent<MeshRenderer>();
            var matrix=wall.transform.localToWorldMatrix;
            var fog=RenderSettings.fogColor;
            world.PreloadBrineMedia();
            var blast=district.GetComponentInChildren<NightBrineBlast>(true);
            Check(blast && !blast.gameObject.activeSelf,"preparation made the incident visible");
            yield return null;
            world.SetBrineBlast(false);
            world.PreloadBrineMedia();
            world.PreloadBrineMedia();
            float started=Time.realtimeSinceStartup;
            int warmFrames=0;
            while(!blast.Ready)
            {
                Check(Time.realtimeSinceStartup-started<24,"preload timed out");
                Check(wall.enabled && wall.transform.localToWorldMatrix==matrix && RenderSettings.fogColor==fog,
                    "arming altered the intact building or sky");
                warmFrames++;
                yield return null;
            }
            Check(district.GetComponentsInChildren<NightBrineBlast>(true).Length==1,"duplicate warmup components");
            Check(blast.GeometryReady && !blast.gameObject.activeSelf && wall.enabled,"geometry preparation leaked into play");
            var cloud=Resources.Load<Texture3D>("BrineCloudField");
            Check(cloud && cloud.width==128 && cloud.depth==128,"missing native pyro volume");
            typeof(NightPlayer).GetMethod("SetFeet",Hidden).Invoke(game.Player,new object[]{new Vector3(273,.08f,-40f),104f});
            world.SetBrineBlast(true,true);
            var tick=typeof(NightBrineBlast).GetMethod("TickFront",Hidden);
            var feedback=typeof(NightBrineBlast).GetMethod("TickReception",Hidden);
            var update=typeof(NightBrineBlast).GetMethod("Update",Hidden);
            var camera=game.Player.Eyes;
            var orientation=camera.rotation;
            var location=camera.position;
            tick.Invoke(blast,new object[]{1.94f});
            Check(blast.FrontRadius<40 && !blast.PressurePassed && !blast.PressureFeedbackActive,"shock arrived ahead of the street front");
            tick.Invoke(blast,new object[]{.19f});
            feedback.Invoke(blast,new object[]{.02f});
            Check(blast.FrontRadius>40 && blast.PressurePassed && blast.PressureFeedbackActive,"street front passed without a response");
            Check(camera.rotation==orientation && camera.position==location,"pressure overwrote player aim or position");
            var impact=blast.transform.Find("到达镜头的声浪").GetComponent<AudioSource>();
            Check(impact.clip && impact.clip.length>2 && impact.volume>.1f,"missing audible dynamic impact");
            feedback.Invoke(blast,new object[]{.84f});
            Check(!blast.PressureFeedbackActive,"hearing impairment outlasted the dust");
            world.FlareBrineBlast();
            tick.Invoke(blast,new object[]{2.15f});
            Check(blast.PressureFeedbackActive,"finale pressure did not rearm");
            game.SetScreen(NightScreen.Pause);
            update.Invoke(blast,null);
            Check(!blast.PressureFeedbackActive,"pause retained lens offset or hearing filter");
            world.SetBrineBlast(false);
            world.SetBrineBlast(true);
            Check(!blast.PressurePassed && !blast.PressureFeedbackActive && blast.FrontRadius==0,
                "loading a devastated district replayed the initial shock");
            game.NewNight();
            Debug.Log($"PRESSURE PASS {warmFrames} preparation frames, intact architecture, delayed arrival at 40 m, clear aim, pause and restore.");
        }
    }
}
