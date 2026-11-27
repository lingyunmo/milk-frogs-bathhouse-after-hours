using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        internal const float WestEdge = -32;
        internal const float EastEdge = 172;
        internal const float SouthEdge = -36;
        internal const float NorthEdge = 44;

        internal static bool ContainsPlayable(Vector3 point)
        {
            if (!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z)) return false;
            if (point.x < WestEdge || point.x > EastEdge || point.z < SouthEdge || point.z > NorthEdge ||
                point.y < -4.5f || point.y > 11.6f) return false;
            if (point.y >= -1.25f) return true;
            var map = new Vector2(point.x, point.z);
            return (point.x >= -20.1f && point.x <= 16.1f && point.z >= -.1f && point.z <= 24.1f) ||
                NightMainRing.Footprint.Contains(map) || NightMainRing.BasementConnector.Contains(map);
        }

        void BoundTheStreet()
        {
            var grounds = new GameObject("连续街道碰撞").transform;
            grounds.SetParent(root, false);
            void Backing(float x1, float z1, float x2, float z2, float top)
            {
                CollisionBox(grounds, "备用承重地面", new Vector3((x1 + x2) / 2, top - .18f, (z1 + z2) / 2),
                    new Vector3(x2 - x1, .36f, z2 - z1));
            }
            Backing(WestEdge, SouthEdge, EastEdge, .02f, -.046f);
            Backing(WestEdge, 23.98f, EastEdge, NorthEdge, -.046f);
            Backing(WestEdge, -.02f, -20, 24.01f, -.046f);
            Backing(16, -.02f, 52.01f, 24.01f, -.046f);
            Backing(53.99f, -.02f, EastEdge, 24.01f, -.046f);
            Backing(51.99f, 5.98f, 54.01f, 24.01f, -.046f);
            PitRamps(grounds);

            var barriers = new GameObject("街区封闭边界").transform;
            barriers.SetParent(root, false);
            var west = Box("浴场西侧围墙", new Vector3(WestEdge + .18f, .89f, 4),
                new Vector3(.36f, 1.80f, 80), brick, barriers);
            var north = Box("后巷围墙", new Vector3(70, .98f, NorthEdge - .18f),
                new Vector3(204, 1.98f, .36f), brick, barriers);
            var south = Box("街对面街沿围栏", new Vector3(70, .66f, SouthEdge + .13f),
                new Vector3(204, 1.34f, .26f), brick, barriers);
            var east = Box("街尾工地围墙", new Vector3(EastEdge - .18f, .87f, 4),
                new Vector3(.36f, 1.76f, 80), brick, barriers);
            foreach (var section in new[] { west, north, south, east }) Surface(section, brick, 2.2f);

            CollisionBox(barriers, "西侧碰撞护栏", new Vector3(WestEdge + .10f, 1.2f, 4), new Vector3(.32f, 7.2f, 80));
            CollisionBox(barriers, "东侧碰撞护栏", new Vector3(EastEdge - .10f, 1.2f, 4), new Vector3(.32f, 7.2f, 80));
            CollisionBox(barriers, "北侧碰撞护栏", new Vector3(70, 1.2f, NorthEdge - .10f), new Vector3(204, 7.2f, .32f));
            CollisionBox(barriers, "南侧碰撞护栏", new Vector3(70, 1.2f, SouthEdge + .10f), new Vector3(204, 7.2f, .32f));
        }

        static Transform CollisionBox(Transform parent, string label, Vector3 center, Vector3 size)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(parent, false);
            obj.transform.position = center;
            obj.AddComponent<BoxCollider>().size = size;
            return obj.transform;
        }

        void PitRamps(Transform grounds)
        {
            var street = GameObject.Find("街区").transform;
            foreach (Transform piece in street)
                if (piece.name == "沟口台阶") piece.GetComponent<Collider>().enabled = false;
            foreach (int sign in new[] { -1, 1 })
            {
                var top = new Vector3(53, -.017f, 3 + sign * 3f);
                var bottom = new Vector3(53, -.86f, 3 + sign * 1.22f);
                Vector3 delta = bottom - top;
                var rotation = Quaternion.LookRotation((delta.z > 0 ? delta : -delta).normalized, Vector3.up);
                var ramp = CollisionBox(grounds, "检修沟通行斜面", (top + bottom) * .5f - rotation * Vector3.up * .06f,
                    new Vector3(1.99f, .12f, delta.magnitude + .018f));
                ramp.rotation = rotation;
            }
        }
    }
}
