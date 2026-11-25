using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AfterHours
{
    [Serializable]
    internal sealed class NightState
    {
        public int schema=1;
        public List<string> collected=new();
        public List<string> openDoors=new();
        public List<string> explored=new();
        public int cells=0, stones=3;
        public float charge;
        internal const float FullTorchCharge = 360;
        public bool torch, motorPart, seal, firstFireDoor, power, pump, uplink, contacted, beacon, escaped;
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
                    save.explored ??= new List<string>();
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
