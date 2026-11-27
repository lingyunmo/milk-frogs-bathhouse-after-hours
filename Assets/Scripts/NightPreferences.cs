
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
        internal float Brightness { get; private set; } = Read("brightness", 1, .7f, 1.45f);
        internal bool PracticalGlow { get; private set; } = Flag("practical-glow");
        internal bool AmbientOcclusion { get; private set; } = Flag("ambient-occlusion");
        internal bool FilmGrain { get; private set; } = Flag("film-grain");

        internal void SetPracticalGlow(bool value) => PracticalGlow = Write("practical-glow", value);
        internal void SetAmbientOcclusion(bool value) => AmbientOcclusion = Write("ambient-occlusion", value);
        internal void SetFilmGrain(bool value) => FilmGrain = Write("film-grain", value);

        static float Read(string name, float fallback, float min, float max) =>
            Mathf.Clamp(PlayerPrefs.GetFloat(Prefix + name, fallback), min, max);

        static bool Flag(string name) => PlayerPrefs.GetInt(Prefix + name, 1) != 0;

        internal void SetSensitivity(float value) => Sensitivity = Write("sensitivity", value);
        internal void SetMotion(float value) => CameraMotion = Write("camera-motion", value);
        internal void SetFov(float value) => FieldOfView = Write("field-of-view", value);
        internal void SetVolume(float value) => Volume = Write("volume", value);
        internal void SetBrightness(float value) => Brightness = Write("brightness", value);

        static float Write(string name, float value)
        {
            PlayerPrefs.SetFloat(Prefix + name, value);
            return value;
        }

        static bool Write(string name, bool value)
        {
            PlayerPrefs.SetInt(Prefix + name, value ? 1 : 0);
            return value;
        }
    }
}
