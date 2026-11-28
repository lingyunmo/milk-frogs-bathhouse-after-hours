using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    // Called by the isolated -brine-qa suite with its profile and save backup already in place.
    internal static class AfterHoursBrineRadiationSmoke
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Check(bool ok, string issue)
        {
            if (!ok) throw new Exception("Dosimeter: " + issue);
        }

        internal static void Verify(NightGame game)
        {
            game.NewNight();
            var ui = (NightInterface)typeof(NightGame).GetField("hud", Private).GetValue(game);
            var doc = game.GetComponentInChildren<UIDocument>();
            var feet = new Vector3(257f, .07f, -31.1f);
            Check(game.SampleBrineField(feet, out _) == 0, "ordinary play has radiation");
            ui.Tick();
            Check(doc.rootVisualElement.Q("brine-dosimeter").style.display.value == DisplayStyle.None, "visible before deployment");
            game.State.brine.stage = BrineStage.Carrying;
            typeof(NightGame).GetMethod("BeginBrineDefence", Private).Invoke(game, null);
            game.Player.ResetPosition(feet, 0);
            Physics.SyncTransforms();
            var run = game.State.brine;
            float open = game.SampleBrineField(feet, out bool hidden);
            Check(open > .83f && !hidden, "intake inaccessible or obstructed");
            float covered;
            var shield = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                shield.transform.SetPositionAndRotation(new Vector3(255f, 1.30f, -31.1f), Quaternion.identity);
                shield.transform.localScale = new Vector3(.35f, 2.7f, 2f);
                Physics.SyncTransforms();
                covered = game.SampleBrineField(feet, out hidden);
                Check(hidden && covered < .28f, "solid cover failed at the same distance");
            }
            finally
            {
                shield.SetActive(false);
                UnityEngine.Object.Destroy(shield);
                Physics.SyncTransforms();
            }
            var south = BrineRoute.Mouth(0) + Vector3.right * 15;
            float southDry = game.SampleBrineField(south, out _);
            run.divertTime = 4; run.divertedLane = 0;
            Check(game.SampleBrineField(south, out _) < southDry * .5f, "flushing has no effect on its lane");
            run.divertTime = 0; run.divertedLane = -1;
            for (int i = 0; i < 740; i++) game.TickBrineRadiation(.05f);
            Check(run.radiationDose > .9f && run.breath < 100 && run.breath > 0, "prolonged exposure has no health cost");
            ui.Tick();
            Check(doc.rootVisualElement.Q("brine-dosimeter").style.display.value == DisplayStyle.Flex, "missing combat instrument");
            var source = game.transform.Find("佩挂式辐射计").GetComponent<AudioSource>();
            Check(source.clip && source.clip.samples > 0 && !source.ignoreListenerPause, "missing counter audio");
            float dose = run.radiationDose, health = run.breath;
            game.SetScreen(NightScreen.Pause);
            game.TickBrineRadiation(.5f);
            Check(run.radiationDose == dose && run.breath == health && doc.rootVisualElement.Q("brine-dosimeter") == null,
                "paused dose or HUD still live");
            NightSave.Write(game.State);
            game.ContinueNight();
            run = game.State.brine;
            Check(Mathf.Abs(run.radiationDose - dose) < .0001f, "save lost dose");

            game.Player.ResetPosition(new Vector3(295f, .07f, -31f), 0);
            Physics.SyncTransforms();
            for (int i = 0; i < 480; i++) game.TickBrineRadiation(.05f);
            float inGallery = game.BrineField;
            Check(inGallery < .1f && run.radiationDose < .01f && Mathf.Abs(run.breath - health) < .01f,
                "sheltered gallery did not clear exposure or health kept dropping");
            bool stampedBefore = PlayerPrefs.HasKey(NightTickets.ProfileKey);
            int tickets = NightTickets.Mask;
            try
            {
                game.Player.ResetPosition(feet, 0);
                Physics.SyncTransforms();
                run.radiationDose = 1f; run.breath = .1f;
                for (int i = 0; i < 8 && run.Live; i++) game.TickBrineRadiation(.05f);
                Check(run.stage == BrineStage.Overrun && run.radiationLost && !run.Live && game.BrineField == 0, "terminal exposure did not close detector");
            }
            finally
            {
                if (stampedBefore) PlayerPrefs.SetInt(NightTickets.ProfileKey, tickets);
                else PlayerPrefs.DeleteKey(NightTickets.ProfileKey);
                PlayerPrefs.Save();
            }
            Debug.Log($"RADIATION PASS intake {open:F2}, behind barrier {covered:F2}, maximum dose {dose:F2}, gallery {inGallery:F2}; cover, flush, health, pause and save.");
        }
    }
}
