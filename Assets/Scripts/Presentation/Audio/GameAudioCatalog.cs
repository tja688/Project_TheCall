using UnityEngine;
using UnityEngine.Audio;

namespace TheCall
{
    /// <summary>
    /// Serialized references. Music and effects stay in Assets/Sound.
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
        [Header("Gameplay effects")]
        public AudioClip monsterPickUp;
        public AudioClip monsterPlace;
        public AudioClip monsterDiscard;
        public AudioClip[] monsterEnergyGain;
        public AudioClip[] obtain;
        public AudioClip[] skillEnergy;
        public AudioClip skillMultiply;
        public AudioClip[] skillKill;
        public AudioClip settle;
        public AudioClip reachRequirement;
        [Header("Ordered settlement assets, awaiting result-stage design")]
        public AudioClip[] experiencePickup;
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
