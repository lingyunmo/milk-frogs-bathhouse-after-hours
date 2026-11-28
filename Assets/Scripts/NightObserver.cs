using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace AfterHours
{
    internal sealed partial class NightGame
    {
        internal NightWatch Watch { get; private set; }
        internal bool EmptyObservation => Watch != null && Watch.Rehearsal;
        internal bool CanObserveLive => ScreenState is (NightScreen.Play or NightScreen.Pause) &&
            !State.detained && !State.circuit.Solved && !WorkingDispatch && !Framing && panel == null && !Capturing;

        internal void EnterObserver()
        {
            if (ScreenState == NightScreen.Title) Begin(null, observation: true);
            else if (CanObserveLive) OpenWatch(false);
        }

        void OpenWatch(bool rehearsal)
        {
            Watch = new NightWatch(transform, Player.Eyes.GetComponent<Camera>(), hunter, rehearsal);
            SetScreen(NightScreen.Observer);
        }

        internal void ExitObserver() => SetScreen(EmptyObservation ? NightScreen.Title : NightScreen.Play);

        void TickObserver(float dt)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F8)) { ExitObserver(); return; }
            if (EmptyObservation && Input.GetKeyDown(KeyCode.Space)) Watch.SetPaused(!Watch.Paused);
            AdvanceWatch(dt);
            if (Watch != null) Watch.Frame(Time.unscaledDeltaTime);
        }

        internal void AdvanceWatch(float dt)
        {
            if (EmptyObservation)
            {
                if (Watch.Paused) return;
                float remaining = Mathf.Min(dt, .25f) * Watch.Speed;
                while (remaining > 0)
                {
                    float step = Mathf.Min(.04f, remaining);
                    hunter.Tick(step);
                    Watch.Record();
                    remaining -= step;
                }
                soundscape.Tick(State.power, false, dt);
                return;
            }
            State.dispatch.playSeconds += dt;
            Player.Tick(this, dt, controls: false);
            Atlas.Track(State, Player.Feet);
            releaseGrace = Mathf.Max(0, releaseGrace - dt);
            hunter.Tick(dt);
            if (ScreenState != NightScreen.Observer) return;
            Watch.Record();
            TickWorld(dt);
        }
    }

    internal sealed class NightWatch
    {
        internal readonly NightHunter Hunter;
        internal readonly bool Rehearsal;
        internal bool Paused { get; private set; }
        internal int Speed { get; private set; } = 1;
        internal readonly Queue<Vector3> Trail = new();
        internal readonly Camera Camera;
        readonly AudioListener playerListener;
        readonly bool listenerWasOn, ranInBackground;
        Vector3 lastPrint;
        float bearing, orbit = 126, elevation = 16;

        internal NightWatch(Transform parent, Camera playerCamera, NightHunter hunter, bool rehearsal)
        {
            Hunter = hunter;
            Rehearsal = rehearsal;
            ranInBackground = Application.runInBackground;
            if (rehearsal) Application.runInBackground = true;
            playerListener = playerCamera.GetComponent<AudioListener>();
            listenerWasOn = playerListener.enabled;
            playerListener.enabled = false;
            var rig = new GameObject("巡楼观察镜头");
            rig.transform.SetParent(parent, false);
            Camera = rig.AddComponent<Camera>();
            Camera.clearFlags = playerCamera.clearFlags;
            Camera.backgroundColor = playerCamera.backgroundColor;
            Camera.cullingMask = playerCamera.cullingMask;
            Camera.fieldOfView = 66;
            Camera.nearClipPlane = .055f;
            Camera.farClipPlane = playerCamera.farClipPlane;
            rig.AddComponent<AudioListener>();
            var fill = rig.AddComponent<Light>();
            fill.type = LightType.Spot;
            fill.range = 11;
            fill.spotAngle = 88;
            fill.intensity = 1.15f;
            fill.color = new Color(.82f, .91f, 1);
            bearing = Heading;
            lastPrint = hunter.Position;
            Trail.Enqueue(lastPrint);
            Frame(0);
        }

        float Heading => Mathf.Atan2(Hunter.Facing.x, Hunter.Facing.z) * Mathf.Rad2Deg;

        internal void Record()
        {
            Vector3 feet = Hunter.Position;
            if ((feet - lastPrint).sqrMagnitude < .36f) return;
            lastPrint = feet;
            if (Trail.Count >= 240) Trail.Dequeue();
            Trail.Enqueue(feet);
        }

        internal void CycleSpeed() { if (Rehearsal) Speed = Speed == 1 ? 2 : Speed == 2 ? 4 : 1; }

        internal void SetPaused(bool paused)
        {
            if (!Rehearsal) return;
            Paused = paused;
            Time.timeScale = paused ? 0 : 1;
            AudioListener.pause = paused;
        }

        internal void Frame(float dt)
        {
            if (Input.GetMouseButton(1))
            {
                orbit += Input.GetAxisRaw("Mouse X") * 3;
                elevation = Mathf.Clamp(elevation - Input.GetAxisRaw("Mouse Y") * 2, -8, 44);
            }
            bearing = Mathf.LerpAngle(bearing, Heading, 1 - Mathf.Exp(-4f * dt));
            Vector3 pivot = Hunter.Position + Vector3.up * 1.32f;
            Vector3 Offset(float yaw)
            {
                Vector3 direction = Quaternion.Euler(-elevation, yaw, 0) * Vector3.forward;
                const int mask = ~((1 << NightPlayer.PlayerLayer) | (1 << NightPlayer.ViewModelLayer));
                float reach = Physics.SphereCast(pivot, .13f, direction, out var hit, 3.9f, mask,
                    QueryTriggerInteraction.Ignore) ? Mathf.Max(.08f, hit.distance - .05f) : 3.9f;
                return direction * reach;
            }
            float angle = bearing + orbit;
            Vector3 offset = Offset(angle);
            if (offset.sqrMagnitude < 4f)
            {
                Vector3 left = Offset(angle - 80), right = Offset(angle + 80);
                if (left.sqrMagnitude > offset.sqrMagnitude) offset = left;
                if (right.sqrMagnitude > offset.sqrMagnitude) offset = right;
            }
            Camera.fieldOfView = Mathf.Lerp(80, 62, Mathf.InverseLerp(1.1f, 3.9f, offset.magnitude));
            Camera.transform.SetPositionAndRotation(pivot + offset, Quaternion.LookRotation(-offset));
        }

        internal void Close()
        {
            if (Camera)
            {
                Camera.gameObject.SetActive(false);
                Object.Destroy(Camera.gameObject);
            }
            if (playerListener) playerListener.enabled = listenerWasOn;
            if (Rehearsal) Application.runInBackground = ranInBackground;
        }
    }

    internal sealed partial class NightInterface
    {
        NightMapView watchMap;
        Label watchIntent, watchPlace, watchGoal;
        Button watchSpeed, watchPause;
        float nextWatchDraw;

        void BuildObserver()
        {
            var watch = game.Watch;
            var view = Area(root, "screen watch-view");
            var panel = Area(view, "watch-station");
            Type(panel, watch.Rehearsal ? "巡楼观察" : "现场观察 · 玩家留在原处", "watch-kicker");
            watchIntent = Type(panel, "", "watch-intent serif");
            watchPlace = Type(panel, "", "watch-place");
            var frame = Area(panel, "watch-chart");
            watchMap = new NightMapView(game, true, watch);
            frame.Add(watchMap);
            var legend = Area(panel, "watch-legend");
            Type(legend, "━  已走", "watch-footprints");
            Type(legend, "┄  预定路线", "watch-plan");
            watchGoal = Type(panel, "", "watch-goal");
            var controls = Area(view, "watch-controls");
            Type(controls, "右键拖动 · 环视", "watch-help");
            watchPause = watchSpeed = null;
            if (watch.Rehearsal)
            {
                watchPause = Act(controls, "", () => { watch.SetPaused(!watch.Paused); RefreshObserver(true); }, "sheet-action serif", "watch-pause");
                watchSpeed = Act(controls, "", () => { watch.CycleSpeed(); RefreshObserver(true); }, "sheet-action serif", "watch-speed");
            }
            Act(controls, watch.Rehearsal ? "返回标题   Esc" : "回到角色   F8", game.ExitObserver, "sheet-action serif", "watch-back");
            RefreshObserver(true);
        }

        void RefreshObserver(bool force = false)
        {
            if (!force && Time.unscaledTime < nextWatchDraw) return;
            nextWatchDraw = Time.unscaledTime + .12f;
            NightWatch watch = game.Watch;
            var hunter = watch.Hunter;
            string Place(Vector3 feet)
            {
                int floor = game.Atlas.FloorAt(feet);
                var area = game.Atlas.Areas(floor).Find(a => a.Bounds.Contains(new Vector2(feet.x, feet.z)));
                return NightAtlas.FloorNames[floor] + " · " + (area?.Name ?? (floor == NightAtlas.Street ? "道路" : "走廊"));
            }
            Copy(watchIntent, hunter.ObservedIntent);
            Copy(watchPlace, Place(hunter.Position));
            Copy(watchGoal, hunter.CurrentGoal is Vector3 target ? "去向   " + Place(target) : "原地停留");
            if (watchPause != null) watchPause.text = watch.Paused ? "继续   Space" : "暂停   Space";
            if (watchSpeed != null) watchSpeed.text = watch.Speed + "×  速度";
            watchMap.MarkDirtyRepaint();
        }
    }
}
