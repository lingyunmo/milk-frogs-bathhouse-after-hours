using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AfterHours
{
    [Serializable]
    internal sealed class NightState
    {
        internal const int MaxNights = 5;
        static readonly string[] NightLabels = { "第一夜", "第二夜", "第三夜", "第四夜", "第五夜" };

        public int schema=1;
        public int shiftEdition;
        public int night = 1;
        public bool detained, ended;
        internal string NightLabel => NightLabels[Mathf.Clamp(night, 1, MaxNights) - 1];
        public List<string> collected=new();
        public List<string> openDoors=new();
        public List<string> explored=new();
        public int cells=0, stones=3;
        public float charge;
        internal const float FullTorchCharge = 360;
        public bool torch, motorPart, seal, firstFireDoor, power, pump, uplink, contacted, beacon, escaped;
        internal bool Drained => pump || dispatch.sealedLines == 7;
        internal bool RescueAccess => RoadSurveyComplete && story.boardAligned;
        internal bool RoadSurveyComplete => dispatch.RoadReady && Drained && survey.ReportReady && survey.archive && story.ledger && story.exchanged && NightShiftTrail.DoorUsed(this, "key-uplink");
        internal bool CanBoard => beacon && rescueTime == 0 && RescueAccess;
        public ShiftEvidence story = new();
        public ReturnCircuit circuit = new();
        public SurveyFilm survey = new();
        public DispatchState dispatch = new();
        public bool annexIsolated, annexRelay;
        public int motorStage, sinkStage, networkStage;
        public float dropTime=-1, rescueTime=-1;
        public bool rearDrop, dropTaken;
        public Vector3 spawn=NewSpawn;
        public float yaw=24;
        internal static readonly Vector3 NewSpawn=new(-3.28f,3.67f,18.25f);
    }

    internal static class NightSave
    {
        internal static string FilePath => Path.Combine(Application.persistentDataPath,"first-night.json");
        internal static void Write(NightState state)
        {
            string destination=FilePath;
            string temp=destination+".writing";
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(temp,JsonUtility.ToJson(state,true));
            if(File.Exists(destination)) File.Replace(temp,destination,destination+".bak");
            else File.Move(temp,destination);
        }

        internal static NightState Read()
        {
            foreach(string path in new[]{FilePath,FilePath+".bak"})
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    var save=JsonUtility.FromJson<NightState>(File.ReadAllText(path));
                    if (save == null || save.schema != 1 || save.collected == null || save.openDoors == null)
                        continue;
                    save.dispatch ??= new DispatchState();
                    save.dispatch.Normalize();
                    save.survey ??= new SurveyFilm();
                    save.survey.Normalize();
                    save.circuit ??= new ReturnCircuit();
                    save.circuit.Normalize();
                    save.story ??= new ShiftEvidence();
                    // Old desks gave out the later keys before any investigation. Re-seat only
                    // those locks in an unfinished legacy run; preserve repairs, photos and supplies.
                    if (save.shiftEdition == 0 && !save.escaped && !save.ended)
                    {
                        foreach (var spec in NightKeyDoors.Locks)
                        {
                            if (spec.Slot == NightKeyDoors.KeySlot.Linen) continue;
                            save.collected.Remove(spec.KeyId);
                            save.collected.Remove(spec.UnlockId);
                            save.openDoors.Remove(spec.Room);
                            save.openDoors.Remove(spec.Room + "侧门");
                        }
                        save.shiftEdition = 1;
                    }
                    if (save.dispatch.orderHeld && NightFieldOrders.Done(save.dispatch.tracked, save))
                        save.dispatch.orderHeld = false;
                    if (!save.escaped && save.rescueTime > 27) save.rescueTime = 27;
                    // A pre-survey save may have signalled the bus already. Keep its road and radio work.
                    if (save.beacon && !save.escaped && !save.RescueAccess)
                    {
                        save.beacon = false;
                        save.rescueTime = -1;
                    }
                    save.explored ??= new List<string>();
                    save.night = Mathf.Clamp(save.night, 1, NightState.MaxNights);
                    if (save.escaped || save.ended || save.night == 1) save.detained = false;
                    if (!NightWorld.ContainsPlayable(save.spawn)) save.spawn = NightState.NewSpawn;
                    if (!float.IsFinite(save.yaw)) save.yaw = 24;
                    return save;
                }
                catch(Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
                {
                    Debug.LogWarning(ex.Message);
                }
            }
            return null;
        }
    }

    internal sealed class NightTarget
    {
        internal Vector3 Position;
        internal string Label;
        internal float Hold;
        internal float Radius=2.15f;
        internal Func<bool> Available;
        internal Action Use;
    }
}
