using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed class FloorRoutes
    {
        internal const int DoorLayer = 2;
        const int GeometryMask = ~((1 << DoorLayer) | (1 << NightPlayer.PlayerLayer));
        const float FloorHeight = 3.6f;
        const float Radius = .30f;
        const float SnapRadius = 2.6f;
        const float SupportInterval = .48f;
        static readonly Vector3 LowerBody = Vector3.up * .52f;
        static readonly Vector3 UpperBody = Vector3.up * 1.24f;
        static readonly (float X, float Mouth, int Direction)[] Stairwells = { (-18, 14, 1), (14, 10, -1) };

        readonly List<Vector3> points = new();
        readonly List<List<int>> edges = new();
        readonly Dictionary<(int x, int z, int floor), int> cells = new();
        readonly List<int> stairNodes = new();
        readonly SortedSet<(float Score, int Node)> open = new();
        readonly int[] previous;
        readonly float[] distance;
        readonly int[] components;

        internal FloorRoutes()
        {
            for (int level = -1; level <= 2; level++)
            {
                int minX = level == 0 ? -31 : -19;
                int maxX = level == 0 ? 171 : level > 0 ? Mathf.CeilToInt(NightAnnexLayout.Ramp.xMax) - 1 : 15;
                int minZ = level == 0 ? -35 : 1, maxZ = level == 0 ? 43 : 23;
                for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    var feet = new Vector3(x, level * FloorHeight, z);
                    // The parking ditch is lower than its surrounding floor but has a ramp at each end.
                    float down = level == 0 && x >= 52 && x <= 54 && z >= 0 && z <= 6 ? 1.35f : .56f;
                    if (Ground(feet, down, out Vector3 surface) && BodyFits(surface))
                        cells.Add((x, z, level), Add(surface));
                }
            }
            foreach (var item in cells)
            {
                var (x, z, level) = item.Key;
                LinkCell(item.Value, x + 1, z, level);
                LinkCell(item.Value, x, z + 1, level);
                LinkCell(item.Value, x + 1, z + 1, level);
                LinkCell(item.Value, x + 1, z - 1, level);
            }
            for (int level = -1; level < 2; level++)
            foreach (var stairwell in Stairwells)
                AddStairs(stairwell.X, stairwell.Mouth, stairwell.Direction, level);
            AddAnnexRamp();

            previous = new int[points.Count];
            distance = new float[points.Count];
            components = new int[points.Count];
            Array.Fill(components, -1);
            var frontier = new Queue<int>();
            for (int start = 0; start < points.Count; start++)
            {
                if (components[start] >= 0) continue;
                components[start] = start;
                frontier.Enqueue(start);
                while (frontier.Count > 0)
                {
                    foreach (int next in edges[frontier.Dequeue()])
                    {
                        if (components[next] >= 0) continue;
                        components[next] = start;
                        frontier.Enqueue(next);
                    }
                }
            }
        }

        internal static int LevelAt(float height) => Mathf.Clamp(Mathf.RoundToInt(height / FloorHeight), -1, 2);
        internal static float LevelHeight(int level) => level * FloorHeight;

        internal static bool IsInsideBathhouse(Vector3 feet) =>
            LevelAt(feet.y) != 0 || NightAtlas.Building.Contains(new Vector2(feet.x, feet.z));

        internal Vector3 PointAt(int node) => points[node];

        // Sample existing supported floor cells on the hunter's connected graph.
        internal void CollectPatrolNodes(Vector3 from, Vector3 center, bool inside, int radius, int spacing, List<int> result)
        {
            result.Clear();
            int start = Nearest(from, true);
            if (start < 0) return;
            int component = components[start];
            int level = LevelAt(center.y);
            int cx = Mathf.RoundToInt(center.x), cz = Mathf.RoundToInt(center.z);
            for (int dx = -radius; dx <= radius; dx += spacing)
            for (int dz = -radius; dz <= radius; dz += spacing)
            {
                int distanceSquared = dx * dx + dz * dz;
                if (distanceSquared < 9 || distanceSquared > radius * radius ||
                    !cells.TryGetValue((cx + dx, cz + dz, level), out int node) || components[node] != component)
                    continue;
                Vector3 point = points[node];
                if (IsInsideBathhouse(point) == inside &&
                    (Mathf.Abs(point.y - from.y) > 1.5f || (point - from).sqrMagnitude >= 16))
                    result.Add(node);
            }
        }

        int Add(Vector3 point)
        {
            points.Add(point);
            edges.Add(new List<int>(6));
            return points.Count - 1;
        }

        void Link(int a, int b)
        {
            if (a < 0 || b < 0 || a == b) return;
            edges[a].Add(b);
            edges[b].Add(a);
        }

        void LinkCell(int index, int x, int z, int level)
        {
            if (!cells.TryGetValue((x, z, level), out int next)) return;
            Vector3 start = points[index], end = points[next];
            if (Mathf.Abs(start.y - end.y) <= .57f && Supported((start + end) * .5f) && Clear(start, end))
                Link(index, next);
        }

        static bool Ground(Vector3 feet, float depth, out Vector3 surface)
        {
            surface = default;
            if (!Physics.Raycast(feet + Vector3.up * .32f, Vector3.down, out var hit, depth,
                GeometryMask, QueryTriggerInteraction.Ignore) || hit.normal.y < .66f)
                return false;
            surface = hit.point;
            return NightWorld.ContainsPlayable(surface);
        }

        static bool Supported(Vector3 feet) => Ground(feet, .70f, out _);

        static bool BodyFits(Vector3 feet) =>
            !Physics.CheckCapsule(feet + LowerBody, feet + UpperBody, Radius, GeometryMask, QueryTriggerInteraction.Ignore);

        static bool Clear(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            return length < .001f || !Physics.CapsuleCast(from + LowerBody, from + UpperBody,
                Radius, delta / length, length, GeometryMask, QueryTriggerInteraction.Ignore);
        }

        // Used on short movement steps as well as route shortcuts. A clear sightline by itself
        // does not ensure there is a floor under the creature along that line.
        internal static bool CanMove(Vector3 from, Vector3 to)
        {
            if (!NightWorld.ContainsPlayable(to) || !BodyFits(to) || !Clear(from, to)) return false;

            int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / SupportInterval));
            for (int step = 1; step <= samples; step++)
                if (!Supported(Vector3.Lerp(from, to, (float)step / samples))) return false;
            return true;
        }

        static bool InStairwell(Vector3 feet)
        {
            if (feet.y > NightAnnexLayout.Lower - .25f && feet.y < NightAnnexLayout.Upper + .25f &&
                NightAnnexLayout.Ramp.Contains(new Vector2(feet.x, feet.z)))
                return true;
            foreach (var stairwell in Stairwells)
            {
                float depth = (feet.z - stairwell.Mouth) * stairwell.Direction;
                if (Mathf.Abs(feet.x - stairwell.X) < 2 && depth >= 1 && depth < 10) return true;
            }
            return false;
        }

        internal static bool IsOnStair(Vector3 feet) =>
            Mathf.Abs(feet.y - Mathf.Round(feet.y / FloorHeight) * FloorHeight) > .16f && InStairwell(feet);

        void AddAnnexRamp()
        {
            var low = new Vector3(NightAnnexLayout.RampX, NightAnnexLayout.Lower, NightAnnexLayout.LowerRampEntry);
            var high = new Vector3(NightAnnexLayout.RampX, NightAnnexLayout.Upper, NightAnnexLayout.UpperRampEntry);
            int entry = Nearest(low, false), exit = Nearest(high, false);
            if (entry < 0 || exit < 0) return;

            var lane = new List<Vector3>();
            Vector3 previousPoint = points[entry];
            for (float z = low.z; z <= high.z; z += 1)
            {
                float height = NightAnnexLayout.RampHeight(z);
                if (!Ground(new Vector3(low.x, height, z), .70f, out Vector3 point) || !CanMove(previousPoint, point))
                    return;
                lane.Add(point);
                previousPoint = point;
            }
            if (!CanMove(previousPoint, points[exit])) return;

            int node = entry;
            foreach (Vector3 point in lane)
            {
                int next = Add(point);
                stairNodes.Add(next);
                Link(node, next);
                node = next;
            }
            Link(node, exit);
        }

        void AddStairs(float x, float mouth, int direction, int level)
        {
            float y = level * FloorHeight;
            const float lane = .98f, near = 1.3f, rampStart = 1.94f, rampEnd = 6.48f, turn = 7.3f;
            const float rise = FloorHeight * .5f;
            Vector3 At(float offset, float height, float depth) => new(x + offset, y + height, mouth + direction * depth);
            Vector3 lower = At(-lane, 0, near);
            Vector3 upper = At(lane, FloorHeight, near);
            int entry = Nearest(lower, false);
            int exit = Nearest(upper, false);
            if (entry < 0 || exit < 0) return;

            // Match the physical ramps in NightStairwells. The midpoint nodes also let
            // the hunter resume a route if a decision arrives halfway up a stair.
            int node = entry;
            Append(lower);
            Append(At(-lane, 0, rampStart));
            Append(At(-lane, rise * .5f, (rampStart + rampEnd) * .5f));
            Append(At(-lane, rise, rampEnd));
            Append(At(-lane, rise, turn));
            Append(At(lane, rise, turn));
            Append(At(lane, rise, rampEnd));
            Append(At(lane, rise * 1.5f, (rampStart + rampEnd) * .5f));
            Append(At(lane, FloorHeight, rampStart));
            Append(upper);
            Link(node, exit);

            void Append(Vector3 point)
            {
                int next = Add(point);
                stairNodes.Add(next);
                Link(node, next);
                node = next;
            }
        }

        int Nearest(Vector3 desired, bool includeStairs, Func<Vector3, Vector3, bool> allowed = null, bool outgoing = true)
        {
            int closest = -1;
            float best = float.PositiveInfinity;
            int level = LevelAt(desired.y);
            int x = Mathf.RoundToInt(desired.x), z = Mathf.RoundToInt(desired.z);
            for (int dx = -3; dx <= 3; dx++)
            for (int dz = -3; dz <= 3; dz++)
                if (cells.TryGetValue((x + dx, z + dz, level), out int cell)) Consider(cell);

            if (includeStairs && InStairwell(desired))
                foreach (int node in stairNodes) Consider(node);
            return closest;

            void Consider(int node)
            {
                Vector3 point = points[node], delta = point - desired;
                float horizontal = delta.x * delta.x + delta.z * delta.z;
                if (horizontal > SnapRadius * SnapRadius || Mathf.Abs(delta.y) > .64f) return;
                float score = horizontal + 5 * delta.y * delta.y;
                if (score >= best || !CanMove(desired, point)) return;
                if (allowed != null && !(outgoing ? allowed(desired, point) : allowed(point, desired))) return;
                best = score;
                closest = node;
            }
        }

        internal bool Find(Vector3 from, Vector3 target, List<Vector3> result, Func<Vector3, Vector3, bool> allowed = null)
        {
            result.Clear();
            if (!NightWorld.ContainsPlayable(from) || !NightWorld.ContainsPlayable(target)) return false;

            if (Mathf.Abs(from.y - target.y) < .18f && (from - target).sqrMagnitude < 64 &&
                CanMove(from, target) && (allowed == null || allowed(from, target)))
            {
                result.Add(target);
                return true;
            }

            int start = Nearest(from, true, allowed), goal = Nearest(target, true, allowed, outgoing: false);
            if (start < 0 || goal < 0) return false;
            if (components[start] != components[goal]) return false;
            open.Clear();
            Array.Fill(distance, float.PositiveInfinity);
            distance[start] = 0;
            open.Add((Estimate(start), start));
            while (open.Count > 0)
            {
                var candidate = open.Min;
                open.Remove(candidate);
                int node = candidate.Node;
                if (node == goal)
                {
                    for (int cursor = goal; ; cursor = previous[cursor])
                    {
                        result.Add(points[cursor]);
                        if (cursor == start) break;
                    }
                    result.Reverse();
                    if ((points[goal] - target).sqrMagnitude > .01f && CanMove(points[goal], target))
                        result.Add(target);
                    return true;
                }

                foreach (int next in edges[node])
                {
                    float cost = distance[node] + Vector3.Distance(points[node], points[next]);
                    if (cost >= distance[next] || allowed != null && !allowed(points[node], points[next])) continue;
                    float heuristic = Estimate(next);
                    if (float.IsFinite(distance[next])) open.Remove((distance[next] + heuristic, next));
                    distance[next] = cost;
                    previous[next] = node;
                    open.Add((cost + heuristic, next));
                }
            }
            return false;

            float Estimate(int node) => Vector3.Distance(points[node], points[goal]);
        }
    }
}
