using QFramework;
using UnityEngine;
using UnityEngine.Audio;

namespace TheCall
{
    /// <summary>
    /// Single volume authority. Mixer's exposed parameters work in dB, UI values in [0,1].
    /// SFX source gain in AudioKit is held at 1 to avoid applying SFX volume twice.
    /// </summary>
    public static class GameAudioSettings
    {
        const string MasterKey = "TheCall.Audio.Master";
        const string MusicKey = "TheCall.Audio.Music";
        const string SfxKey = "TheCall.Audio.SFX";

        public static float MasterVolume => PlayerPrefs.GetFloat(MasterKey, 1f);
        public static float MusicVolume => PlayerPrefs.GetFloat(MusicKey, 1f);
        public static float SfxVolume => PlayerPrefs.GetFloat(SfxKey, 1f);

        public static void SetMasterVolume(float value) => PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp01(value));
        public static void SetMusicVolume(float value) => PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
        public static void SetSfxVolume(float value) => PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
        public static void Save() => PlayerPrefs.Save();

        public static void SetMusicEnabled(bool value) => AudioKit.Settings.IsMusicOn.Value = value;
        public static void SetSfxEnabled(bool value) => AudioKit.Settings.IsSoundOn.Value = value;

        internal static float EffectiveMaster => MasterVolume;
        internal static float EffectiveMusic =>
            AudioKit.Settings.IsMusicOn.Value ? MusicVolume : 0f;
        internal static float EffectiveSfx =>
            AudioKit.Settings.IsSoundOn.Value ? SfxVolume : 0f;

        internal static void ApplyMixer(AudioMixer mixer)
        {
            if (mixer == null) return;
            mixer.SetFloat("MasterVolume", ToDecibels(EffectiveMaster));
            mixer.SetFloat("MusicVolume", ToDecibels(EffectiveMusic));
            mixer.SetFloat("SFXVolume", ToDecibels(EffectiveSfx));
        }

        static float ToDecibels(float linear) =>
            linear <= 0.0001f ? -80f : 20f * Mathf.Log10(linear);
    }
}
