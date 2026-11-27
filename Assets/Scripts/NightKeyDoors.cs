using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal static class NightKeyDoors
    {
        internal enum KeySlot { Linen, Staff, Uplink }

        internal readonly struct LockSpec
        {
            internal readonly KeySlot Slot;
            internal readonly string Room, KeyId, KeyName, Hint, UnlockId;
            internal readonly float Floor, Yaw;
            internal readonly Vector3 Position;
            internal readonly Vector3? SideDoor;

            internal LockSpec(KeySlot slot, string room, string keyId, string keyName, string hint,
                float floor, Vector3 position, float yaw, Vector3? sideDoor = null)
            {
                Slot = slot;
                Room = room;
                KeyId = keyId;
                KeyName = keyName;
                Hint = hint;
                UnlockId = keyId + "-used";
                Floor = floor;
                Position = position;
                Yaw = yaw;
                SideDoor = sideDoor;
            }
        }

        internal static readonly LockSpec[] Locks =
        {
            new(KeySlot.Linen, "毛巾库", "key-linen", "毛巾库钥匙", "备用钥匙留在更衣室的长凳上。",
                3.6f, new Vector3(-3.22f, 4.085f, 21.10f), -18, new Vector3(5, 3.6f, 5)),
            new(KeySlot.Staff, "值班室", "key-staff", "值班室钥匙", "交班的人把钥匙留在了前台。",
                7.2f, new Vector3(8.62f, .944f, 3.765f), 18),
            new(KeySlot.Uplink, "弱电间", "key-uplink", "弱电间钥匙", "检修员带钥匙去了二楼放映室。",
                7.2f, new Vector3(-6.75f, 4.344f, 2.13f), 32, new Vector3(4, 7.2f, 19))
        };

        internal static LockSpec? Find(string room, float floor)
        {
            foreach (var spec in Locks)
                if (spec.Room == room && Mathf.Abs(spec.Floor - floor) < .20f)
                    return spec;
            return null;
        }

        internal static bool IsUnlocked(LockSpec spec, NightState state)
        {
            if (state.collected.Contains(spec.UnlockId) || state.openDoors.Contains(spec.Room)) return true;
            // Existing saves can retain access to rooms whose resources or work they already reached.
            return spec.Slot switch
            {
                KeySlot.Linen => state.seal || state.sinkStage >= 2 || state.collected.Contains("seal"),
                KeySlot.Staff => state.collected.Contains("cell-staff") || state.collected.Contains(NightEvents.DutyCaseKey),
                KeySlot.Uplink => state.networkStage > 0 || state.uplink,
                _ => false
            };
        }
    }

    internal sealed partial class NightGame
    {
        internal bool KeyDoorLocked(NightWorld.Door door) =>
            door.Key is { } spec && !NightKeyDoors.IsUnlocked(spec, State);

        internal bool HasDoorKey(NightWorld.Door door) =>
            door.Key is { } spec && State.collected.Contains(spec.KeyId);

        internal bool CanUseDoorLatch(NightWorld.Door door)
        {
            Vector3 offset = Player.Feet - door.Position;
            return Vector3.Dot(offset, door.Inward) > .21f && Mathf.Abs(offset.y) < .65f;
        }

        bool TryUnlockDoor(NightWorld.Door door, out bool saveUnlock)
        {
            saveUnlock = false;
            if (door.Key is not { } spec) return true;
            if (KeyDoorLocked(door) && !HasDoorKey(door) && !CanUseDoorLatch(door))
            {
                Notify(spec.Hint, 4.5f);
                return false;
            }

            saveUnlock = !State.collected.Contains(spec.UnlockId);
            if (saveUnlock) State.collected.Add(spec.UnlockId);
            return true;
        }

        internal void TakeDoorKey(NightKeyDoors.LockSpec spec)
        {
            if (State.collected.Contains(spec.KeyId)) return;
            State.collected.Add(spec.KeyId);
            world.Refresh();
            PickupFeedback(spec.Position, "拾到" + spec.KeyName, "pickup");
            SaveProgress();
        }

        internal bool HasLockedKeyDoors => world.KeyDoors.Exists(KeyDoorLocked);

        internal bool CanCrossKeyDoors(Vector3 from, Vector3 to)
        {
            foreach (var door in world.KeyDoors)
            {
                const float edge = .34f;
                Vector3 a = from - door.Position, b = to - door.Position;
                if (Mathf.Min(a.y, b.y) > .70f || Mathf.Max(a.y, b.y) < -.70f) continue;

                Vector3 normal = door.Inward;
                float near = Vector3.Dot(a, normal), far = Vector3.Dot(b, normal);
                if (Mathf.Min(near, far) > edge || Mathf.Max(near, far) < -edge ||
                    !door.Leaf.activeSelf || !KeyDoorLocked(door)) continue;

                float depth = far - near;
                float first = Mathf.Abs(depth) < .0001f ? 0f : Mathf.Clamp01((-edge - near) / depth);
                float last = Mathf.Abs(depth) < .0001f ? 1f : Mathf.Clamp01((edge - near) / depth);
                Vector3 across = new(normal.z, 0, -normal.x);
                float xA = Vector3.Dot(Vector3.Lerp(a, b, first), across);
                float xB = Vector3.Dot(Vector3.Lerp(a, b, last), across);
                float width = door.Leaf.transform.lossyScale.x * .5f + .28f;
                if (Mathf.Min(xA, xB) <= width && Mathf.Max(xA, xB) >= -width)
                    return false;
            }
            return true;
        }
    }

    internal sealed partial class NightWorld
    {
        internal readonly List<Door> KeyDoors = new();

        void DressKeyDoor(Door door, NightKeyDoors.LockSpec spec, Vector3 target, Quaternion rotation)
        {
            KeyDoors.Add(door);
            foreach (float side in new[] { -1f, 1f })
            {
                var center = door.Position + rotation * new Vector3(.44f, .97f, side * .062f);
                var plate = Box("锁孔铜片", center, new Vector3(.063f, .082f, .015f), brass);
                plate.transform.rotation = rotation;
                plate.transform.SetParent(door.Leaf.transform, true);
                plate.GetComponent<Collider>().enabled = false;
                var keyhole = Box("锁孔", center + rotation * new Vector3(0, 0, side * .010f),
                    new Vector3(.013f, .025f, .008f), steel);
                keyhole.transform.rotation = rotation;
                keyhole.transform.SetParent(plate.transform, true);
                keyhole.GetComponent<Collider>().enabled = false;
            }

            bool Locked() => !NightDetention.Locks(door.Name, game.State) && game.KeyDoorLocked(door);
            game.Target(target, "用" + spec.KeyName + "开门", 0, () => game.ToggleDoor(door),
                () => Locked() && game.HasDoorKey(door) && !game.CanUseDoorLatch(door));
            game.Target(target, "拉开内闩", 0, () => game.ToggleDoor(door),
                () => Locked() && game.CanUseDoorLatch(door));
            game.Target(target, "查看门锁 · " + door.Name, 0, () => game.ToggleDoor(door),
                () => Locked() && !game.HasDoorKey(door) && !game.CanUseDoorLatch(door));
        }

        void DressRoomKey(NightKeyDoors.LockSpec spec)
        {
            game.Target(spec.Position + Vector3.up * .045f, "拿起" + spec.KeyName, 0, () => game.TakeDoorKey(spec),
                () => !game.State.collected.Contains(spec.KeyId), 1.85f);

            var key = new GameObject(spec.KeyName);
            key.transform.SetParent(root, false);
            var mesh = new DetailMesh(.22f);
            Vector3 Ring(float angle) => new(Mathf.Cos(angle) * .027f, .009f, Mathf.Sin(angle) * .027f);
            const int count = 12;
            for (int i = 0; i < count; i++)
                mesh.Pipe(Ring(i * Mathf.PI * 2 / count), Ring((i + 1) * Mathf.PI * 2 / count), .0065f, 6);
            mesh.Box(new Vector3(.065f, .009f, 0), new Vector3(.095f, .010f, .019f));
            mesh.Box(new Vector3(.085f, .009f, -.014f), new Vector3(.022f, .010f, .018f));
            mesh.Box(new Vector3(.108f, .009f, -.011f), new Vector3(.020f, .010f, .017f));
            var metal = Material(new Color(.67f, .47f, .19f));
            metal.SetFloat("_Metallic", .63f);
            metal.SetFloat("_Glossiness", .62f);
            mesh.Place(key.transform, "钥匙", metal);
            key.transform.SetPositionAndRotation(spec.Position, Quaternion.Euler(0, spec.Yaw, 0));
            pickups.Add((spec.KeyId, key));
        }
    }
}
