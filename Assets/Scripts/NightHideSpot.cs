using UnityEngine;

namespace AfterHours
{
    internal sealed class NightHideSpot
    {
        internal readonly Vector3 Mouth, Seat;
        internal readonly float EyeHeight, Yaw;
        internal readonly Transform Door;
        internal readonly NightTarget Leave;

        internal NightHideSpot(Vector3 mouth, Vector3 seat, float eyeHeight, Transform door, string exit)
        {
            Mouth = mouth;
            Seat = seat;
            EyeHeight = eyeHeight;
            var outward = mouth - seat;
            Yaw = Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
            Door = door;
            Leave = new NightTarget { Label = exit };
        }

        internal void Swing(float t)
        {
            if (!Door) return;
            float opening = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .28f, t));
            float closing = Mathf.SmoothStep(1, 0, Mathf.InverseLerp(.73f, 1, t));
            Door.localRotation = Quaternion.Euler(0, 99f * opening * closing, 0);
        }
    }

    internal sealed partial class NightWorld
    {
        void DressHideSpots()
        {
            void Spot(string enter, string leave, Vector3 mouth, Vector3 seat, float eyes, Vector3 touch, Transform door = null)
            {
                var spot = new NightHideSpot(mouth, seat, eyes, door, leave);
                spot.Leave.Use = () => game.Player.LeaveHideSpot(game);
                var entry = game.Target(touch, enter, 0, () => game.Player.EnterHideSpot(game,spot),
                    () => !game.State.detained && !game.Player.InHideSpot && !game.Player.CarryingLoad &&
                        (game.Player.Feet-mouth).sqrMagnitude < 2.4f*2.4f &&
                        Vector3.Dot(game.Player.Feet-seat, (mouth-seat).normalized) > .32f, 2.55f);
                entry.AimHeight = mouth.y + 1.50f - touch.y;
            }

            Spot("钻进沙发底", "爬出沙发底", new(-15,3.65f,6.47f), new(-15,3.65f,5.43f), .23f, new(-15,4.13f,5.73f));
            Spot("钻进高脚沙发底", "爬出沙发底", new(36.58f,3.65f,19.48f), new(37.73f,3.65f,19.48f), .24f, new(37.31f,4.12f,19.48f));
            Spot("掀开布帘钻入台下", "掀帘出来", new(26.22f,3.65f,19.01f), new(26.22f,3.65f,20.30f), .34f, new(26.22f,4.16f,19.77f));
            Spot("钻进宿舍床底", "爬出床底", new(-3,7.25f,18.78f), new(-3,7.25f,19.80f), .20f, new(-3,7.57f,19.55f));

            Spot("钻进检修台底", "爬出检修台", new(-14,-3.55f,6.42f), new(-14,-3.55f,5.25f), .38f, new(-14,-2.94f,5.80f));
            var sheet = Box("晾衣架内侧的旧床单",new(-12.86f,1.22f,18.93f),new(3.16f,2.12f,.075f),linen);
            FoldedScreen(sheet);
            Spot("挤进晾衣架", "从床单间出来", new(-10.55f,.05f,18.46f), new(-12.08f,.05f,18.46f), 1.01f,
                new(-11.24f,1.10f,18.46f));
            Spot("钻进行军床底", "爬出行军床", new(284,.08f,-.42f), new(284,.08f,.60f), .15f, new(284,.38f,.35f));

            var hinge = LockerWithVent(new(1.76f,3.60f,23.41f));
            Spot("躲进寄存柜", "推门出来", new(1.76f,3.65f,22.12f), new(1.76f,3.65f,23.43f), 1.34f,
                new(1.98f,4.64f,22.94f), hinge);

            // Low service lights silhouette feet across the doorway even before the mains return.
            void GuideLight(string label, Vector3 wall, float yaw, Vector3 outward, Color tint)
            {
                var sconce = Prop("industrial_wall_sconce",label,wall,new(.20f,.34f,.18f),yaw,false);
                LightSconce(sconce,.30f);
                Fixture(wall + outward*.20f + Vector3.up*.16f, outward + Vector3.down*.24f,
                    tint, 1.65f, 6.8f, 94, .003f);
            }
            GuideLight("沙发厅夜灯",new(-19.83f,4.49f,7.33f),90,Vector3.right,new(.99f,.70f,.38f));
            GuideLight("宿舍门边地脚灯",new(-4.2f,8.10f,14.18f),0,Vector3.forward,new(.74f,.85f,.88f));
            GuideLight("更衣室巡夜灯",new(3.82f,4.51f,18.94f),-90,Vector3.left,new(.90f,.75f,.48f));
        }

        void BedWithClearance(Transform parent, string label, Vector3 feet, Vector3 size)
        {
            var bed = Prop("vintage_day_bed",label,feet,size,0,false).transform;
            bed.SetParent(parent,true);
            CollisionBox(bed,"床垫支架",feet + Vector3.up*(size.y*.49f),new(size.x*.98f,size.y*.18f,size.z*.98f));
            foreach (int side in new[]{ -1,1 })
                CollisionBox(bed,"铁床端架",feet + new Vector3(side*(size.x*.5f-.07f),size.y*.48f,0),
                    new(.09f,size.y*.96f,size.z*.97f));
        }

        Transform LockerWithVent(Vector3 feet)
        {
            var shell = new GameObject("更衣室 · 单人寄存柜").transform;
            shell.SetParent(root,false);
            shell.position = feet;
            const float halfWidth = .44f, height = 1.99f, face = -.44f;
            void Part(string label, Transform parent, Vector3 local, Vector3 size, Material finish, float tilt = 0)
            {
                var panel = Box(label, parent.TransformPoint(local), size, finish, parent);
                Surface(panel, finish, .84f);
                panel.transform.localRotation = Quaternion.Euler(tilt,0,0);
            }
            Part("背板",shell,new(0,height/2,.40f),new(.88f,height,.045f),paintedIron);
            foreach(float side in new[]{ -halfWidth,halfWidth })
                Part("折边侧板",shell,new(side,height/2,-.02f),new(.038f,height,.88f),paintedIron);
            Part("顶板",shell,new(0,height,0),new(.92f,.045f,.90f),steel);
            Part("底槛",shell,new(0,.035f,0),new(.86f,.07f,.87f),steel);
            var hinge = new GameObject("寄存柜门轴").transform;
            hinge.SetParent(shell,false);
            hinge.localPosition = new(-halfWidth,0,face);
            // Vent bars, jambs and lower door surround a real aperture at eye level.
            Part("柜门下片",hinge,new(halfWidth,.595f,0),new(.85f,1.09f,.038f),paintedIron);
            Part("柜门上片",hinge,new(halfWidth,1.775f,0),new(.85f,.40f,.038f),paintedIron);
            foreach(float x in new[]{ .095f,.785f })
                Part("百叶侧框",hinge,new(x,1.3575f,0),new(.16f,.435f,.038f),paintedIron);
            for(int i=0;i<5;i++)
                Part("冲压通风叶片",hinge,new(halfWidth,1.16f+i*.101f,0),new(.55f,.031f,.052f),steel,-15);
            Part("门扣",hinge,new(.72f,1.02f,-.040f),new(.064f,.14f,.042f),brass);
            Part("拉手",hinge,new(.69f,1.01f,-.071f),new(.12f,.031f,.049f),steel);
            return hinge;
        }
    }

    internal sealed partial class NightPlayer
    {
        internal bool InHideSpot => hideSpot != null;
        internal bool Concealed => InHideSpot && hideProgress >= .84f;
        internal NightTarget HideExit => InHideSpot && hideProgress >= 1 && !leaving ? hideSpot.Leave : null;
        NightHideSpot hideSpot;
        float hideProgress, outsideYaw, outsideEye, hidePan, outsidePitch;
        Vector3 outsideFeet;
        bool leaving;

        internal void EnterHideSpot(NightGame game, NightHideSpot spot)
        {
            if (InHideSpot || Vaulting || CarryingLoad || game.State.detained) return;
            if (!FloorRoutes.CanMove(Feet, spot.Mouth))
            {
                game.Notify("靠近入口，再钻进去。");
                return;
            }
            hideSpot = spot;
            hideProgress = hidePan = 0;
            leaving = false;
            outsideFeet = Feet;
            outsideYaw = transform.eulerAngles.y;
            outsidePitch = pitch;
            outsideEye = eyeHeight;
            Sprinting = false;
            planarVelocity = Vector3.zero;
            vertical = footDistance = 0;
            Body.enabled = false;
            SetTorch(false);
            SetTorchVisible(false);
            game.Emit(spot.Mouth, spot.Door ? .23f : .12f, spot.Door ? "tool" : "step");
        }

        internal void LeaveHideSpot(NightGame game)
        {
            if (HideExit == null) return;
            if (!CanStand(hideSpot.Mouth))
            {
                game.Notify("出口被挡住了。");
                return;
            }
            leaving = true;
            outsideFeet = hideSpot.Mouth;
            outsideYaw = hideSpot.Yaw + hidePan;
            outsidePitch = pitch;
            outsideEye = 1.60f;
        }

        internal void AdvanceHiding(float dt, float lookX, float lookY)
        {
            if (hideProgress >= 1 && !leaving)
            {
                hidePan = Mathf.Clamp(hidePan + lookX, -58, 58);
                pitch = Mathf.Clamp(pitch - lookY, -34, 29);
                transform.rotation = Quaternion.Euler(0, hideSpot.Yaw + hidePan, 0);
                Eyes.localRotation = Quaternion.Euler(pitch, 0, 0);
                UpdateStamina(false,false,dt);
                return;
            }
            hideProgress = Mathf.MoveTowards(hideProgress, leaving ? 0 : 1, dt / .90f);
            float align = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0,.36f,hideProgress));
            float slide = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.34f,.73f,hideProgress));
            var foot = Vector3.Lerp(Vector3.Lerp(outsideFeet, hideSpot.Mouth, align), hideSpot.Seat, slide);
            transform.SetPositionAndRotation(foot, Quaternion.Euler(0, Mathf.LerpAngle(outsideYaw,hideSpot.Yaw+hidePan,align),0));
            eyeHeight = Mathf.Lerp(outsideEye,hideSpot.EyeHeight,align);
            Eyes.localPosition = Vector3.up * eyeHeight;
            pitch = leaving ? outsidePitch : Mathf.Lerp(outsidePitch,0,align);
            Eyes.localRotation = Quaternion.Euler(pitch,0,0);
            hideSpot.Swing(hideProgress);
            if (leaving && hideProgress <= 0)
            {
                float exitPitch = outsidePitch;
                SetFeet(outsideFeet,outsideYaw);
                LookDown(exitPitch);
                lastGround = Feet;
            }
        }

        internal void InterruptHiding()
        {
            if (!InHideSpot) return;
            var mouth = hideSpot.Mouth;
            SetFeet(mouth,hideSpot.Yaw);
            lastGround = mouth;
        }

        void ResetHiding()
        {
            hideSpot?.Swing(0);
            hideSpot = null;
            hideProgress = 0;
            leaving = false;
        }
    }
}
