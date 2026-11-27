using System;
using UnityEngine;

namespace AfterHours
{
    internal static class NightDispatch
    {
        internal const int Contacts = 10, Teeth = 8;
        internal static readonly (string Name, Vector3 Feet)[] Stations =
        {
            ("西北 · 后墙", new(-24, 0, 36)), ("西南 · 街口", new(-22, 0, -27)),
            ("东北 · 货场", new(158, 0, 34)), ("东南 · 路尾", new(162, 0, -29))
        };
        internal static Vector3 Panel(int i) => Stations[i].Feet + new Vector3(0, .91f, -.56f);
        internal static Vector3 Approach(int i) => Stations[i].Feet + Vector3.back * 1.55f;
        internal static readonly Vector3 Receiver = new(7.00f, 1.02f, 6.03f);
        internal static readonly Vector3 LoadDepot = new(50.95f, .04f, 17.28f);
        internal static readonly Vector3 Barrier = new(146, 0, -20);
        internal static readonly Vector3 Winch = Barrier + new Vector3(0, 1.10f, -.54f);
        internal static readonly Vector3 ReceiverDish = new(160.66f, 1.01f, 9.62f);
        internal static float SweepSeconds(int progress) => Mathf.Lerp(3.4f, 1.28f, Mathf.Clamp01(progress / 9f));
    }

    [Serializable]
    internal sealed class DispatchState
    {
        public bool accepted, carrying, delivered;
        public int[] steps = new int[4];
        public int teeth, leakPattern, sealedLines;
        public float playSeconds;
        public Vector3 loadFeet = NightDispatch.LoadDepot;
        internal bool RoadReady => accepted && AllRunning && teeth >= NightDispatch.Teeth;
        internal bool AllRunning => Running == NightDispatch.Stations.Length;
        internal int Running
        {
            get
            {
                int count = 0;
                foreach (int step in steps) if (step == NightDispatch.Contacts) count++;
                return count;
            }
        }
        internal void Normalize()
        {
            if (steps == null || steps.Length != NightDispatch.Stations.Length)
                Array.Resize(ref steps, NightDispatch.Stations.Length);
            for (int i = 0; i < steps.Length; i++) steps[i] = Mathf.Clamp(steps[i], 0, NightDispatch.Contacts);
            if (!NightWorld.ContainsPlayable(loadFeet) || loadFeet == Vector3.zero) loadFeet = NightDispatch.LoadDepot;
            if (delivered) carrying = false;
            leakPattern &= 7;
            sealedLines &= 7;
            teeth = delivered ? Mathf.Clamp(teeth, 0, NightDispatch.Teeth) : 0;
            if (!float.IsFinite(playSeconds) || playSeconds < 0) playSeconds = 0;
        }
    }

    internal sealed partial class NightGame
    {
        internal int ActiveGenerator { get; private set; } = -1;
        internal bool Winding { get; private set; }
        internal bool Tuning { get; private set; }
        internal float WoundFraction { get; private set; }
        internal float Bearing { get; private set; }
        internal float Reception => Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(Bearing, RadioHeading)) / 62f);
        internal float ChannelLock { get; private set; }
        internal bool WorkingDispatch => ActiveGenerator >= 0 || Winding || Tuning;
        static float RadioHeading => Quaternion.LookRotation(NightEvents.BackupBeacon - NightDispatch.ReceiverDish).eulerAngles.y;
        float nextRattle, workingTime, tunerClick, radioCallIn;
        NightTarget loadTarget;

        void BuildDispatchObjectives()
        {
            BuildReturnTargets();
            Target(NightDispatch.Receiver, "接听夜班调度", 1.5f, AcceptDispatch,
                () => !State.detained && !State.dispatch.accepted && !State.beacon);
            for (int slot = 0; slot < NightDispatch.Stations.Length; slot++)
            {
                int i = slot;
                Target(NightDispatch.Panel(i), "校准机组 · " + NightDispatch.Stations[i].Name, .5f, () => StartGenerator(i),
                    () => CanWork && State.dispatch.steps[i] < NightDispatch.Contacts);
            }
            loadTarget = Target(NightDispatch.LoadDepot + Vector3.up * .34f, "背起绞盘配重", .85f, PickUpLoad,
                () => CanWork && !State.dispatch.delivered && !State.dispatch.carrying);
            Target(NightDispatch.Winch, "挂上配重", 1.15f, DeliverLoad, () => CanWork && State.dispatch.carrying);
            Target(NightDispatch.Winch, "握住绞盘曲柄", .5f, StartWinch,
                () => CanWork && State.dispatch.delivered && State.dispatch.teeth < NightDispatch.Teeth);
            Target(NightDispatch.ReceiverDish, "转动维修站定向天线", .6f, StartTuning,
                () => CanWork && State.power && State.annexIsolated && !State.contacted);
        }

        bool CanWork => State.dispatch.accepted && !State.detained && !State.beacon;
        void AcceptDispatch()
        {
            State.dispatch.accepted = true;
            State.dispatch.leakPattern = UnityEngine.Random.Range(0,8);
            world.RefreshDispatch();
            world.RefreshReturnLines();
            Emit(NightDispatch.Receiver, .42f, "signal");
            Notify("「四角亮灯，桥杆升起来，我就进街。路口有积水，听听哪路管子在漏。」", 7);
            SaveProgress();
        }

        void StartGenerator(int index)
        {
            CloseDispatch();
            ActiveGenerator = index;
            Emit(NightDispatch.Approach(index), .24f, "tool");
        }

        internal void CloseDispatch()
        {
            if (Winding) world.SetBarrierLift(State.dispatch.teeth);
            ActiveGenerator = -1;
            Winding = Tuning = false;
            workingTime = nextRattle = WoundFraction = ChannelLock = tunerClick = 0;
        }

        internal void GeneratorStroke(bool hit)
        {
            int i = ActiveGenerator;
            if (i < 0 || !CanWork) return;
            var job = State.dispatch;
            job.steps[i] = Mathf.Clamp(job.steps[i] + (hit ? 1 : -1), 0, NightDispatch.Contacts);
            var site = NightDispatch.Approach(i);
            Emit(site, hit ? .5f : 1.3f, hit ? "switch" : "signal");
            if (!hit) hunter.HearGeneratorTrip(site);
            if (job.steps[i] == NightDispatch.Contacts)
            {
                world.RefreshDispatch();
                Notify(job.AllRunning ? "四盏引路灯，全亮了。" : "机油味里，亮起一盏小灯。");
                CloseDispatch();
            }
            SaveProgress();
        }

        void TickDispatchWork(float dt)
        {
            if (ActiveGenerator >= 0)
            {
                workingTime += dt;
                if ((nextRattle -= dt) <= 0)
                {
                    float load = Mathf.Clamp01((workingTime + State.dispatch.steps[ActiveGenerator] * 1.5f) / 34f);
                    Emit(NightDispatch.Approach(ActiveGenerator), Mathf.Lerp(.26f, 1.18f, load), "tool");
                    nextRattle = Mathf.Lerp(2.7f, 1.15f, load);
                }
            }
            else if (Winding) Wind(Input.GetKey(InteractKey), dt);
            else if (Tuning) Tune(Input.GetAxisRaw("Horizontal"), dt);
        }

        void PickUpLoad()
        {
            State.dispatch.carrying = true;
            RefreshLoad();
            Emit(Player.Feet, .68f, "tool");
            Notify("铁块贴住了背。Q 可以把它放下。");
            SaveProgress();
        }

        internal void RefreshLoad()
        {
            var job = State.dispatch;
            if (loadTarget != null) loadTarget.Position = job.loadFeet + Vector3.up * .34f;
            Player.CarryingLoad = job.carrying;
            Player.HaulTrained = job.delivered;
            world.RefreshDispatch();
        }

        void SetLoadDown()
        {
            // Only leave the case on a supported floor at the player's feet. The prop is nonblocking.
            if (Player.Vaulting || !Player.Body.isGrounded) return;
            if (!Physics.Raycast(Player.Feet + Vector3.up * .15f, Vector3.down, out var hit, .5f,
                    ~(1 << NightPlayer.PlayerLayer), QueryTriggerInteraction.Ignore) || hit.normal.y < .85f) return;
            State.dispatch.loadFeet = hit.point;
            State.dispatch.carrying = false;
            RefreshLoad();
            Emit(hit.point, .69f, "tool");
            SaveProgress();
        }

        void ReturnLoadOnCapture()
        {
            State.dispatch.carrying = false;
            State.dispatch.loadFeet = NightDispatch.LoadDepot;
            RefreshLoad();
        }

        void DeliverLoad()
        {
            State.dispatch.carrying = false;
            State.dispatch.delivered = true;
            RefreshLoad();
            Emit(NightDispatch.Winch, .83f, "tool");
            Notify("配重就位。肩头一轻，能多跑一段了。");
            SaveProgress();
        }

        void StartWinch()
        {
            CloseDispatch();
            Winding = true;
        }

        internal void Wind(bool pulling, float dt)
        {
            var job = State.dispatch;
            if (!Winding || !CanWork) return;
            if (!pulling || Player.Exhausted)
            {
                if (!pulling) Player.RestAtWinch(dt);
                return;
            }
            Player.StrainAtWinch(dt);
            WoundFraction += dt / 2.55f;
            world.SetBarrierLift(job.teeth + WoundFraction);
            if (WoundFraction < 1) return;
            WoundFraction -= 1;
            job.teeth++;
            Emit(NightDispatch.Winch, .94f, "tool");
            if (job.teeth == NightDispatch.Teeth)
            {
                CloseDispatch();
                Notify("棘爪扣死。桥杆悬在了头顶。");
            }
            world.RefreshDispatch();
            SaveProgress();
        }

        void StartTuning()
        {
            CloseDispatch();
            Bearing = 5f;
            world.SetAerial(Bearing);
            Tuning = true;
        }

        internal void Tune(float input, float dt)
        {
            Bearing = Mathf.Repeat(Bearing + input * 57f * dt, 360f);
            world.SetAerial(Bearing);
            float strength = Reception;
            ChannelLock = Mathf.Clamp01(ChannelLock + dt * (strength > .91f ? .42f : -.8f));
            if ((tunerClick -= dt) <= 0)
            {
                soundscape.Play(NightDispatch.ReceiverDish, "signal", Mathf.Lerp(.08f, .54f, strength));
                tunerClick = Mathf.Lerp(1.25f, .26f, strength);
            }
            if (ChannelLock < 1) return;
            State.contacted = true;
            CloseDispatch();
            Emit(NightDispatch.ReceiverDish, .76f, "signal");
            Notify("沙声里有人应了一句：「灯亮、桥开，我就进街。」", 6);
            SaveProgress();
        }
    }
}
