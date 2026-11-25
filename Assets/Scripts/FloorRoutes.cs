using System;
using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed class FloorRoutes
    {
        internal const int DoorLayer = 2;
        const int GeometryMask = ~((1 << DoorLayer) | (1 << NightPlayer.PlayerLayer));
        readonly List<Vector3> points = new();
        readonly List<List<int>> edges = new();
        readonly Dictionary<(int x, int z, int floor), int> cells = new();

        internal FloorRoutes()
        {
            for (int level = -1; level <= 2; level++)
            {
                int minX = level == 0 ? -31 : -19, maxX = level == 0 ? 171 : 15;
                int minZ = level == 0 ? -35 : 1, maxZ = level == 0 ? 43 : 23;
                for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    var p = new Vector3(x, level * 3.6f, z);
                    if (!Physics.Raycast(p + Vector3.up * .32f, Vector3.down, out var hit, .56f, GeometryMask) ||
                        Physics.CheckCapsule(hit.point + Vector3.up * .52f, hit.point + Vector3.up * 1.24f, .30f, GeometryMask))
                        continue;
                    cells.Add((x,z,level), Add(hit.point));
                }
            }
            foreach (var item in cells)
            {
                var (x,z,level) = item.Key;
                LinkCell(item.Value, x+1,z,level);
                LinkCell(item.Value, x,z+1,level);
                LinkCell(item.Value, x+1,z+1,level);
                LinkCell(item.Value, x+1,z-1,level);
            }
            for (int level = -1; level < 2; level++)
            {
                AddStairs(-18,14,1,level);
                AddStairs(14,10,-1,level);
            }
        }

        int Add(Vector3 point)
        {
            points.Add(point);
            edges.Add(new List<int>(6));
            return points.Count-1;
        }

        void Link(int a, int b)
        {
            if (a < 0 || b < 0 || a == b) return;
            edges[a].Add(b);
            edges[b].Add(a);
        }

        void LinkCell(int a, int x, int z, int level)
        {
            if (cells.TryGetValue((x,z,level), out int b) && Clear(points[a],points[b])) Link(a,b);
        }

        static bool Clear(Vector3 a, Vector3 b)
        {
            Vector3 d=b-a;
            return !Physics.SphereCast(a+Vector3.up*.8f,.30f,d.normalized,out _,d.magnitude,GeometryMask);
        }

        void AddStairs(float x, float mouth, int sign, int level)
        {
            float y = level*3.6f;
            var a = new Vector3(x-.93f,y,mouth+sign*1.3f);
            var b = new Vector3(x-.93f,y+1.8f,mouth+sign*7.3f);
            var c = new Vector3(x+.93f,y+1.8f,mouth+sign*7.3f);
            var d = new Vector3(x+.93f,y+3.6f,mouth+sign*1.3f);
            int start=Nearest(a,true), end=Nearest(d,true);
            int i=Add(a), j=Add(b), k=Add(c), l=Add(d);
            Link(start,i); Link(i,j); Link(j,k); Link(k,l); Link(l,end);
        }

        int Nearest(Vector3 desired, bool gridOnly=false)
        {
            int closest=-1;
            float best=float.PositiveInfinity;
            for (int i=0; i<(gridOnly?cells.Count:points.Count); i++)
            {
                var delta=points[i]-desired;
                float dist=delta.x*delta.x+delta.z*delta.z+5*delta.y*delta.y;
                if (dist>=best) continue;
                if (Mathf.Abs(delta.y)<.45f && !Clear(desired,points[i])) continue;
                best=dist;
                closest=i;
            }
            return closest;
        }

        internal bool Find(Vector3 from, Vector3 target, List<Vector3> result)
        {
            result.Clear();
            if (Mathf.Abs(from.y-target.y)<.18f && Vector3.Distance(from,target)<8 && Clear(from,target))
            {
                result.Add(target);
                return true;
            }
            int start=Nearest(from), goal=Nearest(target);
            if (start<0 || goal<0) return false;
            var incoming=new Dictionary<int,int>();
            var distances=new Dictionary<int,float>{{start,0}};
            var queue=new SortedSet<(float score,int node)> { (Vector3.Distance(points[start],points[goal]),start) };
            while (queue.Count>0)
            {
                var top=queue.Min;
                queue.Remove(top);
                int node=top.node;
                if (node==goal)
                {
                    for (int i=goal; ; i=incoming[i])
                    {
                        result.Add(points[i]);
                        if (i==start) break;
                    }
                    result.Reverse();
                    if (Mathf.Abs(points[goal].y-target.y)<.35f && Clear(points[goal],target)) result.Add(target);
                    return true;
                }
                foreach (int next in edges[node])
                {
                    float cost=distances[node]+Vector3.Distance(points[node],points[next]);
                    float heuristic=Vector3.Distance(points[next],points[goal]);
                    if (distances.TryGetValue(next,out float previous))
                    {
                        if (cost>=previous) continue;
                        queue.Remove((previous+heuristic,next));
                    }
                    distances[next]=cost;
                    incoming[next]=node;
                    queue.Add((cost+heuristic,next));
                }
            }
            return false;
        }
    }
}
