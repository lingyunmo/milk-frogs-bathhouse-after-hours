
using UnityEngine;

namespace AfterHours
{
    internal sealed class NightPreferences
    {
        const string Prefix = "after-hours.";
        internal float Sensitivity { get; private set; } = Read("sensitivity", 2.2f, .5f, 5);
        internal float CameraMotion { get; private set; } = Read("camera-motion", .72f, 0, 1);
        internal float FieldOfView { get; private set; } = Read("field-of-view", 61, 50, 82);
        internal float Volume { get; private set; } = Read("volume", .85f, 0, 1);
        internal bool PracticalGlow { get; private set; } = PlayerPrefs.GetInt(Prefix + "practical-glow", 1) != 0;

        internal void SetPracticalGlow(bool value)
        {
            PracticalGlow = value;
            PlayerPrefs.SetInt(Prefix + "practical-glow", value ? 1 : 0);
        }

        static float Read(string name, float fallback, float min, float max) =>
            Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + name, fallback), min, max);

        internal void SetSensitivity(float value) => Sensitivity = Write("sensitivity", value);
        internal void SetMotion(float value) => CameraMotion = Write("camera-motion", value);
        internal void SetFov(float value) => FieldOfView = Write("field-of-view", value);
        internal void SetVolume(float value) => Volume = Write("volume", value);

        static float Write(string name, float value)
        {
            PlayerPrefs.SetFloat(Prefix + name, value);
            return value;
        }
    }
}
