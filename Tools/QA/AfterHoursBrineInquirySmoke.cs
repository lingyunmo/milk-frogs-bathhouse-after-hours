using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class AfterHoursBrineSmoke
    {
        IEnumerator PreviewBrineInquiry(string folder, NightInterface ui)
        {
            game.NewNight();
            game.State.torch = true;
            game.State.charge = 200;
            game.Player.SetTorch(true);
            typeof(NightInterface).GetField("arrival", Flags).SetValue(ui, Time.unscaledTime - 20f);
            var porch = new Vector3(177, .07f, -9.91f);
            var operatorFeet = new Vector3(179.5f, .07f, -3.57f);
            var cameraFeet = new Vector3(178.72f, .07f, -5.20f);
            IEnumerator Picture(string name, Vector3 feet, Vector3 look)
            {
                Aim(feet, look);
                Call("FocusTarget", .03f);
                typeof(NightGame).GetField("messageTime", Flags).SetValue(game, 0f);
                game.Atlas.Track(game.State, game.Player.Feet);
                var shot = CaptureField(Path.Combine(folder, name), ui, false);
                while (shot.MoveNext()) yield return shot.Current;
            }

            var frame = Picture("nuclear-monitor-hut.jpg", new(178.2f, .07f, -13.55f), new(178.54f, 1.67f, -8.49f));
            while (frame.MoveNext()) yield return frame.Current;
            Use(Target("摘下监测哨铜匙"), new(194.55f, .07f, 6.43f));
            Use(Target("拧开监测哨门锁"), porch);
            frame = Picture("nuclear-monitor-offline.jpg", cameraFeet, BrineInquiry.Display - Vector3.up * .33f);
            while (frame.MoveNext()) yield return frame.Current;
            Use(Target("掀开终端检修盖"), operatorFeet);
            Use(Target("抽出备用回放板"), new(177.12f, .07f, -4.68f));
            Use(Target("装回板卡并切换信号"), operatorFeet);
            Enter(1);
            frame = Picture("nuclear-monitor-online.jpg", cameraFeet, BrineInquiry.Display - Vector3.up * .33f);
            while (frame.MoveNext()) yield return frame.Current;
        }

        void CheckBrineInquiry()
        {
            BrineRun old = JsonUtility.FromJson<BrineRun>("{\"rubbings\":1}");
            old.Normalize();
            Need(old.CanRecordMarks && old.rubbings == 1, "An old partially copied wall was lost");
            var late = new BrineRun { stage = BrineStage.Carrying };
            late.Normalize();
            Need(late.inquiry == BrineInquiryStep.Online && late.stage == BrineStage.Carrying, "An old sample carrier was demoted");
            var wall = BrineRoute.Marks[3].Point;
            game.State.survey.camera = true;
            Call("ToggleFinder");
            Aim(wall + new Vector3(0, -1.65f, -3.7f), wall);
            game.ExposeSurvey();
            Need(game.State.brine.rubbings == 0 && game.State.brine.exposures.All(string.IsNullOrEmpty)
                && targets.Where(t => t.Label == "拓下旧线记号").All(t => !t.Available()), "Wall bypassed the locked archive");
            Call("ToggleFinder");

            bool BlockedAtDoor() => Physics.Raycast(new Vector3(177, 1.4f, -9.05f), Vector3.forward, 1.1f,
                ~(1 << NightPlayer.PlayerLayer), QueryTriggerInteraction.Ignore);
            var threshold = new Vector3(177, .07f, -9.91f);
            Use(Target("翻看借匙牌"), threshold);
            Need(BrineInquiry.NearbyLead(game.State, game.Player.Feet, game.Player.Eyes)?.action == "看看门边的借匙牌",
                "The main shift objective still covers the monitoring hut");
            Aim(new(155,.07f,-18),new(153,1.45f,-18));
            Need(!BrineInquiry.NearbyLead(game.State, game.Player.Feet, game.Player.Eyes).HasValue, "Local clues escaped the hut area");
            Need(BlockedAtDoor() && !game.CanCrossKeyDoors(threshold, threshold + Vector3.forward * 2.6f), "Locked hut admits player or hunter");
            Use(Target("摘下监测哨铜匙"), new(194.55f, .07f, 6.43f));
            Need(game.InventoryItems.Any(i => i.Id == NightItemId.MonitorKey), "Key never entered the backpack");
            Use(Target("拧开监测哨门锁"), threshold);
            Physics.SyncTransforms();
            Need(!BlockedAtDoor() && game.CanCrossKeyDoors(threshold, threshold + Vector3.forward * 2.6f) && !game.InventoryItems.Any(i => i.Id == NightItemId.MonitorKey), "Hut remains blocked or key unspent");
            var operatorFeet = new Vector3(179.5f, .07f, -3.57f);
            Use(Target("掀开终端检修盖"), operatorFeet);
            Use(Target("抽出备用回放板"), new(177.12f, .07f, -4.68f));
            game.ContinueNight();
            Need(game.State.brine.inquiry == BrineInquiryStep.Card && game.InventoryItems.Any(i => i.Id == NightItemId.PlaybackCard),
                "Resuming a repair lost the board");
            Use(Target("装回板卡并切换信号"), operatorFeet);
            Enter(0);
            Need(!game.State.brine.CanRecordMarks && game.RepairView != null, "Failed signal choice unlocked camera");
            Enter(1);
            Need(game.State.brine.CanRecordMarks && !game.InventoryItems.Any(i => i.Id == NightItemId.PlaybackCard)
                && game.InventoryItems.Any(i => i.Id == NightItemId.BrineBook && i.Description.Contains(BrineInquiry.DisplayWarning)),
                "Restored terminal or its warning is missing");
            Use(Target("查看巡检定格"), operatorFeet);
            Need(BrineInquiry.NearbyLead(game.State, game.Player.Feet, game.Player.Eyes)?.action == "查找旧墙上的交运号",
                "Restored terminal still asks to repair it");
            var screen = GameObject.Find("巡检回放屏");
            Need(screen != null && screen.activeInHierarchy && screen.GetComponentsInChildren<Renderer>().Length >= 10,
                "Repaired screen never switched on in the room");
            foreach (var mark in BrineRoute.Marks)
                Need(screen.GetComponentsInChildren<TextMesh>().Any(t => t.text == mark.Place), "Archive screen lacks " + mark.Place);
            Debug.Log("BRINE_INQUIRY_OK sealed door, normal reticle interactions, camera gate, repair, archive, legacy saves");
        }
    }
}
