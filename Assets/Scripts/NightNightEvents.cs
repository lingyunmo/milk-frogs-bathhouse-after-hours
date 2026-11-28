using UnityEngine;

namespace AfterHours
{
    internal static class NightEvents
    {
        internal const string DutyCaseKey = "night-duty-case";
        internal const float BackupArrivalTime = 18f;
        internal static readonly Vector3 DutyCase = new(-9.67f, 8.19f, 18.025f);
        internal static readonly Vector3 DutyCaseTarget = DutyCase + new Vector3(0, .12f, .18f);
        internal static readonly Vector3 FreightSwitch = new(14.65f, 1.18f, 24.31f);
        internal static readonly Vector3 FreightHorn = new(-15.8f, 2.17f, 25.15f);
        internal static readonly Vector3 FreightSearchPoint = new(FreightHorn.x, 0, FreightHorn.z);
        internal static readonly Vector3 BackupBeacon = new(147f, 1.25f, -12f);

        internal static bool Available(NightState state, int minimumNight) =>
            (state.night >= minimumNight || state.RescueAccess) && !state.detained && !state.ended;

        internal static string FreightBellKey(int night) => $"night-freight-bell-{night}";
    }

    internal sealed partial class NightGame
    {
        void BuildNightEventObjectives()
        {
            Target(NightEvents.DutyCaseTarget, "拆开值班补给箱", 1.45f, OpenDutyCase,
                () => NightEvents.Available(State, 2) && !State.collected.Contains(NightEvents.DutyCaseKey));

            Target(NightEvents.FreightSwitch, "按下货门试铃", .85f, RingFreightBell,
                () => NightEvents.Available(State, 3) && !State.collected.Contains(NightEvents.FreightBellKey(State.night)));
            Target(NightEvents.FreightSwitch, "查看熔断的试铃开关", 0,
                () => Notify("今晚的试铃保险片已经断了。"),
                () => NightEvents.Available(State, 3) && State.collected.Contains(NightEvents.FreightBellKey(State.night)));

            Target(NightEvents.BackupBeacon, "查看备用接应灯", 0,
                () => Notify("指示窗还是黑的。灯座下面写着「凭回执亮灯」。"),
                () => NightEvents.Available(State, 4) && !State.contacted);
            Target(NightEvents.BackupBeacon, "扳开备用接应灯", 1.2f, SendBackupBeacon,
                () => NightEvents.Available(State, 4) && State.contacted && State.RescueAccess && !State.circuit.spareSignal);
        }

        void OpenDutyCase()
        {
            State.collected.Add(NightEvents.DutyCaseKey);
            State.cells++;
            State.stones += 2;
            world.Refresh();
            Emit(NightEvents.DutyCase, .63f, "tool");
            PickupFeedback(NightEvents.DutyCaseTarget, "备用电池 × 1　石子 × 2", art: "cell");
            SaveProgress();
        }

        void RingFreightBell()
        {
            State.collected.Add(NightEvents.FreightBellKey(State.night));
            world.Refresh();
            soundscape.Play(NightEvents.FreightSwitch, "switch", .36f);
            soundscape.Play(NightEvents.FreightHorn, "signal", 1f);
            // The sound belongs to the horn. Give the hunter a goal on the ground below it.
            hunter?.Hear(NightEvents.FreightSearchPoint, 1.12f);
            Notify("卸货门旁的试铃响了。");
            SaveProgress();
        }

        void SendBackupBeacon() =>
            Repair(NightEvents.BackupBeacon, 1.12f, () =>
            {
                State.circuit.spareSignal = true;
                if (State.beacon) { Notify("备用灯亮了。司机在原站等你。"); return; }
                State.beacon = true;
                State.rescueTime = NightEvents.BackupArrivalTime;
                Notify("东边的站牌亮起来了。", 5);
            }, "signal");
    }

    internal sealed partial class NightWorld
    {
        Transform dutyCase, dutyCaseHinge, freightSwitch, freightHorn, backupBeacon;
        GameObject dutyCaseContents, freightReadyLight, backupReadyLight, backupBeaconLight;

        void DressNightEvents()
        {
            var amber = Material(new Color(.91f, .52f, .16f));
            amber.EnableKeyword("_EMISSION");
            amber.SetColor("_EmissionColor", new Color(.93f, .39f, .07f) * 1.2f);

            var green = Material(new Color(.44f, .80f, .49f));
            green.EnableKeyword("_EMISSION");
            green.SetColor("_EmissionColor", new Color(.27f, .76f, .49f) * 1.4f);

            var point = NightEvents.DutyCase;
            dutyCase = NightEventMount("值班补给箱", point);
            EventPart(dutyCase, "铁盒底", point + new Vector3(0, -.091f, 0), new Vector3(.48f, .035f, .35f), steel);
            foreach (int side in new[] { -1, 1 })
            {
                EventPart(dutyCase, "铁盒侧沿", point + new Vector3(.225f * side, 0, 0),
                    new Vector3(.030f, .18f, .35f), steel);
                EventPart(dutyCase, "铁盒短边", point + new Vector3(0, 0, .160f * side),
                    new Vector3(.42f, .18f, .030f), steel);
            }
            dutyCaseHinge = NightEventMount("补给盒盖轴", point + new Vector3(0, .09f, -.16f), dutyCase);
            EventPart(dutyCaseHinge, "补给盒盖", point + new Vector3(0, .096f, 0),
                new Vector3(.482f, .035f, .35f), steel);
            EventPart(dutyCaseHinge, "铅封搭扣", point + new Vector3(0, .062f, .182f),
                new Vector3(.085f, .08f, .025f), brass);
            dutyCaseContents = NightEventMount("补给盒内物资", point, dutyCase).gameObject;
            EventPart(dutyCaseContents.transform, "备用电池", point + new Vector3(-.08f, -.03f, -.02f),
                new Vector3(.18f, .11f, .11f), olive);
            for (int i = 0; i < 2; i++)
            {
                var stone = Shape("配发石子", PrimitiveType.Sphere, point + new Vector3(.085f + i * .07f, -.048f, .02f),
                    new Vector3(.056f, .044f, .050f), plaster, new Vector3(0, i * 40f, 0));
                stone.transform.SetParent(dutyCaseContents.transform, true);
            }

            point = NightEvents.FreightSwitch;
            freightSwitch = NightEventMount("货门试铃开关", point);
            EventPart(freightSwitch, "试铃盒", point + new Vector3(0, 0, -.10f), new Vector3(.38f, .48f, .16f), steel);
            EventPart(freightSwitch, "试铃按钮", point + new Vector3(0, -.085f, .012f),
                new Vector3(.124f, .115f, .075f), red);
            EventPart(freightSwitch, "试铃指示玻璃", point + new Vector3(0, .126f, -.008f),
                new Vector3(.100f, .055f, .04f), emergencyGlass);
            freightReadyLight = EventPart(freightSwitch, "试铃回路待用灯", point + new Vector3(0, .126f, .016f),
                new Vector3(.083f, .038f, .023f), amber);

            point = NightEvents.FreightHorn;
            freightHorn = NightEventMount("卸货门试铃", point);
            EventPart(freightHorn, "试铃立柱", new Vector3(point.x, 1.0f, point.z),
                new Vector3(.116f, 2.06f, .116f), steel);
            EventPart(freightHorn, "试铃背板", point, new Vector3(.36f, .31f, .16f), steel);
            var bell = Shape("试铃金属罩", PrimitiveType.Sphere, point + new Vector3(0, 0, .18f),
                new Vector3(.42f, .38f, .26f), brass, Vector3.zero);
            bell.transform.SetParent(freightHorn, true);

            point = NightEvents.BackupBeacon;
            backupBeacon = NightEventMount("街边备用接应灯", point);
            EventPart(backupBeacon, "接应灯立柱", new Vector3(point.x, .62f, point.z + .15f),
                new Vector3(.12f, 1.24f, .12f), steel);
            EventPart(backupBeacon, "接应灯外盒", point + new Vector3(0, .09f, .15f),
                new Vector3(.46f, .53f, .26f), steel);
            EventPart(backupBeacon, "接应灯拨杆", point + new Vector3(0, -.033f, -.017f),
                new Vector3(.156f, .07f, .128f), brass);
            EventPart(backupBeacon, "接应灯玻罩", point + new Vector3(0, .39f, .073f),
                new Vector3(.29f, .19f, .19f), emergencyGlass);
            backupReadyLight = EventPart(backupBeacon, "接应灯有回执提示", point + new Vector3(0, .235f, .009f),
                new Vector3(.10f, .051f, .027f), amber);
            backupBeaconLight = EventPart(backupBeacon, "接应灯已启用", point + new Vector3(0, .39f, .073f),
                new Vector3(.23f, .16f, .20f), green);
        }

        void RefreshNightEvents()
        {
            var state = game.State;
            dutyCase.gameObject.SetActive(state.night >= 2 || state.RescueAccess);
            bool supplied = state.collected.Contains(NightEvents.DutyCaseKey);
            dutyCaseHinge.localRotation = Quaternion.Euler(supplied ? -77f : 0, 0, 0);
            dutyCaseContents.SetActive(!supplied);

            freightSwitch.gameObject.SetActive(state.night >= 3 || state.RescueAccess);
            freightHorn.gameObject.SetActive(state.night >= 3 || state.RescueAccess);
            freightReadyLight.SetActive(!state.collected.Contains(NightEvents.FreightBellKey(state.night)));

            backupBeacon.gameObject.SetActive(state.night >= 4 || state.RescueAccess);
            backupReadyLight.SetActive(state.contacted && state.RescueAccess && !state.circuit.spareSignal);
            backupBeaconLight.SetActive(state.circuit.spareSignal);
        }

        Transform NightEventMount(string name, Vector3 point, Transform parent = null)
        {
            var mount = new GameObject(name).transform;
            mount.SetParent(parent ? parent : root, false);
            mount.position = point;
            return mount;
        }

        GameObject EventPart(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            var part = Box(name, center, size, material, parent);
            part.GetComponent<Collider>().enabled = false;
            return part;
        }
    }
}
