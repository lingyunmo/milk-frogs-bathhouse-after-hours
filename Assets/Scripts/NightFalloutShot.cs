using UnityEngine;

namespace AfterHours
{
    internal sealed partial class NightGame
    {
        Camera falloutShotCamera;
        Vector3 previousEye;
        Quaternion previousLook;
        float previousFov, previousFar, shotStarted;

        void StartFalloutShot()
        {
            falloutShotCamera = Player.Eyes.GetComponent<Camera>();
            previousEye = Player.Eyes.localPosition;
            previousLook = Player.Eyes.localRotation;
            previousFov = falloutShotCamera.fieldOfView;
            previousFar = falloutShotCamera.farClipPlane;
            falloutShotCamera.farClipPlane = Mathf.Max(165f, previousFar);
            shotStarted = Time.unscaledTime;
            FrameFalloutShot();
        }

        void FrameFalloutShot()
        {
            bool escaped = State.brine.stage == BrineStage.Clear;
            var start = escaped ? new Vector3(276, 7.8f, 26) : new Vector3(271, 2.4f, -33);
            var finish = escaped ? new Vector3(278, 9.9f, 28) : new Vector3(272, 1.85f, -34);
            var focus = escaped ? new Vector3(248, 13, -47) : new Vector3(244, 9, -40);
            float t = Mathf.SmoothStep(0, 1, (Time.unscaledTime - shotStarted) / 5.8f) * Preferences.CameraMotion;
            Player.Eyes.position = Vector3.Lerp(start, finish, t);
            Player.Eyes.rotation = Quaternion.LookRotation(focus - Player.Eyes.position);
            falloutShotCamera.fieldOfView = (escaped ? 59 : 68) - 3 * t;
        }

        void LeaveFalloutShot()
        {
            if (!falloutShotCamera) return;
            Player.Eyes.localPosition = previousEye;
            Player.Eyes.localRotation = previousLook;
            falloutShotCamera.fieldOfView = previousFov;
            falloutShotCamera.farClipPlane = previousFar;
            falloutShotCamera = null;
        }
    }
}
