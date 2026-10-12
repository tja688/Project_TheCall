using UnityEngine;
using UnityEngine.Audio;

namespace TheCall
{
    /// <summary>Serialized references: clips remain in Assets/Sound/BGM, not Resources.</summary>
    public sealed class GameAudioCatalog : ScriptableObject
    {
        public AudioClip menu;
        public AudioClip tutorial;
        public AudioClip level;
        public AudioClip shop;
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
