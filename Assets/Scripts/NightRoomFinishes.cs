using System.Collections.Generic;
using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightWorld
    {
        // Keep trim batches room-sized so walls across the map can still be culled.
        readonly Dictionary<(int X,int Y,int Z,Material Surface),DetailMesh> roomTrim = new();
        readonly HashSet<(Vector3 A, Vector3 B)> dressedFaces = new();
        Material coping, paintedIron, linen;

        DetailMesh Trim(Vector3 center, Material surface)
        {
            var key = (Mathf.FloorToInt(center.x/12), Mathf.FloorToInt((center.y+.2f)/3.6f),
                Mathf.FloorToInt(center.z/12), surface);
            if (!roomTrim.TryGetValue(key,out var mesh))
                roomTrim[key] = mesh = new DetailMesh(.84f);
            return mesh;
        }

        void FinishTrim()
        {
            foreach (var batch in roomTrim) batch.Value.Place(root,"墙面收口 · "+batch.Key.Surface.name,batch.Key.Surface);
            roomTrim.Clear();
            dressedFaces.Clear();
        }

        enum WallUse { Plant, Wash, Rest, Staff }

        static WallUse StoreyFinish(float y) => y < -1 ? WallUse.Plant : y < 2 ? WallUse.Wash :
            y < 6 ? WallUse.Rest : WallUse.Staff;

        // Use the wall segment endpoints, so a rail or pipe always stops before a door or window.
        void WallFacing(Vector3 middle, Vector3 along, Vector3 normal, float length, WallUse use)
        {
            if (length < .76f) return;
            Vector3 left = middle - along*(length*.5f-.055f), right = middle + along*(length*.5f-.055f);
            if (!dressedFaces.Add((left,right))) return;
            Material material = use == WallUse.Wash ? coping : use == WallUse.Rest ? wood : paintedIron;
            var trim = Trim(middle,material);
            // A low plinth projects far enough to read against the floor in a passing beam.
            trim.Moulding(left+Vector3.up*.14f,right+Vector3.up*.14f,normal,.15f,.025f);
            float railY = use == WallUse.Wash ? 1.27f : use == WallUse.Staff ? .75f : .91f;
            Vector3 a=left+Vector3.up*railY,b=right+Vector3.up*railY;
            if (use == WallUse.Wash || use == WallUse.Staff)
            {
                trim.Moulding(a,b,normal,use == WallUse.Wash ? .076f : .115f,.031f);
                trim.Moulding(a+Vector3.up*.052f,b+Vector3.up*.052f,normal,.017f,.017f);
                return;
            }
            // Lift the rail off the wall; bracket shadows show its distance from the plaster.
            float gap = use == WallUse.Plant ? .068f : .081f;
            a+=normal*gap; b+=normal*gap;
            trim.Pipe(a,b,use == WallUse.Plant ? .036f : .042f);
            var fittings = Trim(middle,steel);
            int spans = Mathf.Max(1,Mathf.CeilToInt((length-.34f)/2.15f));
            for (int i=0; i<=spans; i++)
            {
                Vector3 joint=Vector3.Lerp(a+along*.10f,b-along*.10f,(float)i/spans);
                fittings.Pipe(joint-along*.025f,joint+along*.025f,.049f);
                fittings.Pipe(joint-normal*(gap+.002f),joint,.025f);
            }
        }

        void WallSegment(Vector3 center, Vector3 size, WallUse use, int side=0)
        {
            if (size.y<1.6f) return;
            bool alongX = size.x > size.z;
            Vector3 along=alongX?Vector3.right:Vector3.forward;
            Vector3 face=alongX?Vector3.forward:Vector3.right;
            float half=(alongX?size.z:size.x)*.5f;
            Vector3 basePoint=center-Vector3.up*(size.y*.5f);
            for(int sign=-1;sign<=1;sign+=2)
                if (side==0 || side==sign)
                    WallFacing(basePoint+face*(sign*(half+.002f)),along,face*sign,alongX?size.x:size.z,use);
        }

        void DressDoorPanels(GameObject leaf, Vector3 foot, Quaternion rotation, bool fire)
        {
            float halfWidth=leaf.transform.localScale.x*.5f;
            Vector3 across=rotation*Vector3.right;
            var panels=new DetailMesh(.69f);
            foreach (float sign in new[]{-1f,1f})
            {
                Vector3 face=rotation*Vector3.forward*sign;
                Vector3 origin=foot+face*.052f;
                if (fire)
                {
                    // The wear plate and seam catch light below the panic hardware.
                    Vector3 c=origin+Vector3.up*.29f;
                    panels.Box(c, new Vector3(2*halfWidth-.10f,.48f,.008f));
                    continue;
                }
                foreach (var limits in new[]{(.17f,.88f),(1.12f,2.25f)})
                {
                    Vector3 edge=across*(halfWidth-.14f);
                    Vector3 bottom=origin+Vector3.up*limits.Item1, top=origin+Vector3.up*limits.Item2;
                    panels.Moulding(bottom-edge,top-edge,face,.055f,.019f);
                    panels.Moulding(bottom+edge,top+edge,face,.055f,.019f);
                    panels.Moulding(bottom-edge,bottom+edge,face,.055f,.019f);
                    panels.Moulding(top-edge,top+edge,face,.055f,.019f);
                }
            }
            panels.Place(leaf.transform,fire?"防火门踢板":"实木门芯压线",fire?steel:wood);
        }

        void DoorSurround(Vector3 foot, bool alongX, float halfWidth, float height, float halfWall,
            Material finish)
        {
            Vector3 along=alongX?Vector3.right:Vector3.forward;
            Vector3 face=alongX?Vector3.forward:Vector3.right;
            var trim=Trim(foot,finish);
            foreach (float side in new[]{-1f,1f})
            {
                Vector3 basePoint=foot+face*(side*(halfWall+.002f));
                var left=basePoint-along*(halfWidth+.046f);
                var right=basePoint+along*(halfWidth+.046f);
                Vector3 rise=Vector3.up*(height+.044f);
                trim.Moulding(left,left+rise,face*side,.092f,.032f);
                trim.Moulding(right,right+rise,face*side,.092f,.032f);
                trim.Moulding(left+rise-along*.046f,right+rise+along*.046f,face*side,.096f,.037f);
            }
        }
    }
}
