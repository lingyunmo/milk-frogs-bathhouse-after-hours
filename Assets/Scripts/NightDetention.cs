using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal static class NightDetention
    {
        internal static readonly (string Room, Vector3 Feet, float Yaw)[] Sites =
        {
            ("检修工坊", new(-14f, -3.54f, 7.65f), 0),
            ("备品库", new(-2.5f, -3.54f, 7.65f), 0),
            ("集水间", new(10.5f, -3.54f, 16.30f), 180),
            ("发电机房", new(-10.5f, -3.54f, 16.30f), 180)
        };

        internal static (string Room, Vector3 Feet, float Yaw) Site(int night) => Sites[night - 2];

        internal static bool Locks(string room, NightState state) => state.detained && room == Site(state.night).Room;
    }

    internal sealed partial class NightGame
    {
        void SetDetentionCheckpoint()
        {
            var site = NightDetention.Site(State.night);
            State.spawn = site.Feet;
            State.yaw = site.Yaw;
        }

        void FinishCapture()
        {
            if (ScreenState != NightScreen.Capture) return;
            hunter.EndCapture();
            if (State.ended)
            {
                ResumableSave = null;
                SetScreen(NightScreen.Caught);
                return;
            }

            hunter.Reset();
            world.Refresh();
            Physics.SyncTransforms();
            Player.ResetPosition(WalkableSavePosition(State.spawn), State.yaw);
            Player.SetTorch(false);
            Atlas.Track(State, Player.Feet);
            if (grade) grade.SetFear(0);
            SetScreen(NightScreen.Play);
            hud.Arrive(true);
        }

        internal void OpenDetentionLock()
        {
            if (ScreenState == NightScreen.Play && State.detained)
                SetScreen(NightScreen.Detention);
        }

        internal void CloseDetentionLock()
        {
            if (ScreenState == NightScreen.Detention) SetScreen(NightScreen.Play);
        }

        internal void DetentionFeedback(bool seated) =>
            soundscape.Play(Player.Eyes.position + Player.Eyes.forward, seated ? "switch" : "tool", .56f);

        internal void ReleaseDetention()
        {
            if (ScreenState != NightScreen.Detention || !State.detained) return;
            string room = NightDetention.Site(State.night).Room;
            if (!State.openDoors.Contains(room)) State.openDoors.Add(room);
            State.detained = false;
            releaseGrace = 4.2f;
            world.Refresh();
            Physics.SyncTransforms();
            if (SaveProgress()) Notify("门开了。");
            SetScreen(NightScreen.Play);
        }
    }

    internal sealed partial class NightWorld
    {
        readonly List<(string Room, GameObject Housing)> detentionLocks = new();
        readonly List<(string Room, string AdjoiningRoom, GameObject Gate)> detentionSideGates = new();

        void DressDetentionRooms()
        {
            var diode = Material(new Color(.53f, .25f, .067f));
            diode.EnableKeyword("_EMISSION");
            diode.SetColor("_EmissionColor", new Color(1f, .45f, .12f) * 1.7f);
            foreach (var site in NightDetention.Sites)
            {
                var door = Doors.Find(other => other.Name == site.Room);
                float inward = site.Yaw > 0 ? 1 : -1;
                var point = door.Position + new Vector3(-.42f, 1.25f, .11f * inward);
                var body = Box("卡簧锁盒", point, new Vector3(.28f, .24f, .09f), steel);
                body.GetComponent<Collider>().enabled = false;
                var indicator = Box("锁盒指示灯", point + new Vector3(.074f, .058f, .051f * inward),
                    new Vector3(.047f, .027f, .016f), diode);
                indicator.transform.SetParent(body.transform, true);
                indicator.GetComponent<Collider>().enabled = false;
                var catchPlate = Box("卡簧导轨", point + new Vector3(-.031f, -.018f, .052f * inward),
                    new Vector3(.126f, .079f, .016f), brass);
                catchPlate.transform.SetParent(body.transform, true);
                catchPlate.GetComponent<Collider>().enabled = false;
                var lamp = new GameObject("锁盒微光").AddComponent<Light>();
                lamp.transform.SetParent(body.transform, true);
                lamp.transform.position = point + new Vector3(-.02f, .22f, .36f * inward);
                lamp.type = LightType.Point;
                lamp.color = new Color(1f, .57f, .28f);
                lamp.range = 2.75f;
                lamp.intensity = .72f;
                lamp.shadows = LightShadows.None;
                detentionLocks.Add((site.Room, body));
                body.SetActive(false);
                game.Target(point + Vector3.forward * (.13f * inward), "拨动卡簧锁", 0, game.OpenDetentionLock,
                    () => NightDetention.Locks(site.Room, game.State) &&
                        Mathf.Abs(game.Player.Feet.y - door.Position.y) < .65f &&
                        (game.Player.Feet.z - door.Position.z) * inward > .05f, 1.8f);
            }

            // These passages are open in ordinary play. The active holding room closes both exits.
            AddDetentionSideGate("检修工坊", "备品库", new Vector3(-8, -3.6f, 5));
            AddDetentionSideGate("发电机房", null, new Vector3(-5, -3.6f, 19));
        }

        void AddDetentionSideGate(string room, string adjoiningRoom, Vector3 feet)
        {
            const float width = 1.48f, height = 2.54f;
            Vector3 center = feet + Vector3.up * (height * .5f);
            var gate = Box("拘留侧闸", center, new Vector3(.14f, height, width), steel);
            Surface(gate, steel, 1.12f);
            foreach (float side in new[] { -1f, 1f })
            foreach (float offset in new[] { -.77f, 0, .77f })
            {
                var brace = Box("侧闸横撑", center + new Vector3(side * .079f, offset, 0),
                    new Vector3(.035f, .052f, width), steel);
                brace.transform.SetParent(gate.transform, true);
                brace.GetComponent<Collider>().enabled = false;
                Surface(brace, steel, 1.12f);
            }
            gate.SetActive(false);
            detentionSideGates.Add((room, adjoiningRoom, gate));
        }

        void RefreshDetentionRooms()
        {
            foreach (var (room, housing) in detentionLocks)
                housing.SetActive(NightDetention.Locks(room, game.State));
            foreach (var (room, adjoiningRoom, gate) in detentionSideGates)
                gate.SetActive(NightDetention.Locks(room, game.State) || NightDetention.Locks(adjoiningRoom, game.State));
        }
    }
}
