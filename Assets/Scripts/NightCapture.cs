using System.Collections;
using System.IO;
using UnityEngine;

namespace AfterHours
{
    // Preview stills for pull requests. Start a player with
    //   AfterHours.exe -screen-fullscreen 0 -screen-width 1600 -screen-height 900 -afterhours-capture <folder>
    // and it walks through fixed shots, writes numbered PNGs and quits. Nothing is saved to the player's slot.
    internal sealed partial class NightGame
    {
        internal static bool Capturing { get; private set; }
        static readonly Vector3 OutOfShot = new(150, 0, -26);

        void Start()
        {
            string folder = CaptureFolder();
            if (folder == null) return;
            Capturing = true;
            // Keep shooting if another window takes focus; normal play still pauses in the background.
            Application.runInBackground = true;
            Directory.CreateDirectory(folder);
            StartCoroutine(CaptureTour(folder));
        }

        static string CaptureFolder()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; ++i)
                if (args[i] == "-afterhours-capture") return args[i + 1];
            return null;
        }

        void Stand(Vector3 feet, float yaw, float pitch)
        {
            Player.ResetPosition(feet, yaw);
            Player.LookDown(pitch);
            Atlas.Track(State, Player.Feet);
            Physics.SyncTransforms();
        }

        // Step just inside a room's doorway and look across it, the way a player first sees it.
        void Enter(int floor, string room, float pitch = 7, float sideways = 0)
        {
            var area = Atlas.Areas(floor).Find(a => a.Name == room);
            if (area?.Entrance is not Vector2 door) return;
            float level = (floor - 1) * 3.6f + .07f;
            float inward = door.y > 12 ? 1 : -1;
            var feet = new Vector3(door.x + sideways, level, door.y + inward * 1.1f);
            var far = new Vector3(area.Bounds.center.x, level, door.y > 12 ? area.Bounds.yMax : area.Bounds.yMin);
            float yaw = Mathf.Atan2(far.x - feet.x, far.z - feet.z) * Mathf.Rad2Deg;
            Stand(feet, yaw, pitch);
        }

        IEnumerator CaptureTour(string folder)
        {
            int shot = 0;
            IEnumerator Still(string name, float settle)
            {
                yield return new WaitForSecondsRealtime(settle);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"{++shot:00}-{name}.png"));
                yield return new WaitForSecondsRealtime(.25f);
            }

            yield return Still("title", 3.2f);
            hud.OpenPage("settings");
            yield return Still("title-options", 1.2f);
            hud.CloseSubpage();

            NewNight();
            hunter.Pose(OutOfShot, 0);
            yield return Still("arrival", 1.6f);
            yield return Still("locker-room", 5.2f);

            State.torch = true;
            State.charge = NightState.FullTorchCharge * .72f;
            State.cells = 2;
            world.Refresh();
            Player.SetTorch(true);
            LastPickup = ("torch", $"手电筒 · {TorchKey} 开关", LastPickup.Serial + 1);
            yield return Still("torch-pickup", 1.2f);

            Stand(new Vector3(7.5f, 3.67f, 12.1f), 270, 2);
            yield return Still("upper-hall-dark", 1.4f);

            Enter(1, "大浴池", 9, -1.2f);
            yield return Still("bath-hall", 1.6f);
            Preferences.SetAmbientOcclusion(false);
            ApplyPreferences();
            yield return Still("bath-hall-without-occlusion", .6f);
            Preferences.SetAmbientOcclusion(true);
            ApplyPreferences();

            Enter(1, "淋浴间", 6);
            yield return Still("shower-steam", 1.6f);

            Stand(new Vector3(6.2f, 3.67f, 12.1f), 270, 3);
            hunter.Pose(new Vector3(2.4f, 3.6f, 12.2f), 90);
            yield return Still("creature-in-torchlight", 1.4f);
            hunter.Pose(OutOfShot, 0);

            Enter(0, "发电机房", 10);
            yield return Still("generator-room", 5.4f);
            OpenRepair("飞轮校准", new Vector3(-10, -2.57f, 18.92f), new[] { "Ⅰ", "Ⅱ", "Ⅲ" },
                new[] { "机壳刻着 Ⅱ → Ⅰ → Ⅲ。先把第二道刻线推到指针下。" }, new[] { 1 }, () => { });
            panel.selected = 1;
            yield return Still("repair-panel", .8f);
            panel = null;

            State.power = true;
            world.Refresh();
            Player.SetTorch(false);
            Stand(new Vector3(12.5f, .07f, 12f), 270, 1);
            yield return Still("lower-hall-powered", 4.5f);
            Enter(1, "门厅", 6);
            yield return Still("lobby-powered", 1.4f);
            Enter(1, "前台室", 8);
            yield return Still("front-desk-powered", 1.4f);

            Player.SetTorch(true);
            Enter(2, "沙发厅", 8);
            yield return Still("sofa-lounge", 1.6f);

            Stand(new Vector3(18f, .07f, -9.5f), 98, 3);
            yield return Still("street", 1.6f);

            Stand(new Vector3(6.2f, 3.67f, 12.1f), 270, 3);
            hunter.Pose(new Vector3(4.6f, 3.6f, 12.1f), 90);
            Captured();
            yield return Still("capture-moment", .7f);
            yield return new WaitForSecondsRealtime(soundscape.CaptureDuration);
            SetScreen(NightScreen.Play);
            hunter.Pose(OutOfShot, 0);

            Stand(NightState.NewSpawn, 24, 4);
            State.motorPart = true;
            State.collected.Add(FaultNoteId);
            LastPickup = ("cell", "备用电池 × 1", LastPickup.Serial + 1);
            yield return Still("pickup-card", 1.0f);

            SetScreen(NightScreen.Pause);
            yield return Still("pause", 1.2f);
            hud.OpenPage("settings");
            yield return Still("pause-options", 1.0f);
            hud.CloseSubpage();
            SetScreen(NightScreen.Inventory);
            yield return Still("inventory", 1.6f);
            SetScreen(NightScreen.Map);
            yield return Still("map", 1.2f);
            SetScreen(NightScreen.Caught);
            yield return Still("caught", 1.6f);
            SetScreen(NightScreen.Escaped);
            yield return Still("escaped", 1.6f);

            Application.Quit();
        }
    }
}
