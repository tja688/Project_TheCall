using UnityEngine;
using UnityEngine.Audio;

namespace TheCall
{
    /// <summary>
    /// Serialized references. Music stays in Assets/Sound/BGM, UI effects in Assets/Sound/SFX/UI.
    /// Clips are not copied into Resources.
    /// </summary>
    public sealed class GameAudioCatalog : ScriptableObject
    {
        public AudioClip menu;
        public AudioClip tutorial;
        public AudioClip level;
        public AudioClip shop;
        public AudioClip[] uiClicks;
        public AudioClip purchase;
        public AudioClip error;
        public AudioClip clockOut;
        public AudioMixer mixer;
        public AudioMixerGroup bgmGroup;
        public AudioMixerGroup sfxGroup;

        public AudioClip Find(MusicState state)
        {
            switch (state)
            {
                case MusicState.Menu: return menu;
                case MusicState.Tutorial: return tutorial;
                case MusicState.Level: return level;
                case MusicState.Shop: return shop;
                default: return null;
            }
        }
    }
}
