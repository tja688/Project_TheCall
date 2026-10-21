using System;
using UnityEngine;

namespace TheCall
{
    public enum GameplayCue
    {
        MonsterPickUp, MonsterPlace, MonsterDiscard, MonsterEnergyGain,
        Obtain, SkillEnergy, SkillMultiply, SkillKill, Settle, ReachRequirement
    }

    public static class GameplaySfx
    {
        static GameAudioCatalog _catalog;
        static readonly int[] Variants = new int[Enum.GetValues(typeof(GameplayCue)).Length];

#if UNITY_EDITOR
        // Records actual requested clips for the repeatable in-Editor gameplay acceptance.
        public static event Action<GameplayCue, AudioClip> Played;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _catalog = null;
            Array.Clear(Variants, 0, Variants.Length);
#if UNITY_EDITOR
            Played = null;
#endif
        }

        public static SfxVoice Play(GameplayCue cue)
        {
            if (!Application.isPlaying) return null;
            if (_catalog == null) _catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
            if (_catalog == null) return null;
            AudioClip clip;
            var volume = 0.8f;
            switch (cue)
            {
                case GameplayCue.MonsterPickUp: clip = _catalog.monsterPickUp; break;
                case GameplayCue.MonsterPlace: clip = _catalog.monsterPlace; break;
                case GameplayCue.MonsterDiscard: clip = _catalog.monsterDiscard; break;
                case GameplayCue.MonsterEnergyGain: clip = Next(_catalog.monsterEnergyGain, cue); volume = 0.48f; break;
                case GameplayCue.Obtain: clip = Next(_catalog.obtain, cue); break;
                case GameplayCue.SkillEnergy: clip = Next(_catalog.skillEnergy, cue); volume = 0.64f; break;
                case GameplayCue.SkillMultiply: clip = _catalog.skillMultiply; volume = 0.65f; break;
                case GameplayCue.SkillKill: clip = Next(_catalog.skillKill, cue); break;
                case GameplayCue.Settle: clip = _catalog.settle; break;
                case GameplayCue.ReachRequirement: clip = _catalog.reachRequirement; break;
                default: throw new ArgumentOutOfRangeException(nameof(cue), cue, null);
            }
            var voice = GameSfx.PlayOneShot(clip, volume);
#if UNITY_EDITOR
            if (voice != null) Played?.Invoke(cue, clip);
#endif
            return voice;
        }

        static AudioClip Next(AudioClip[] clips, GameplayCue cue)
        {
            if (clips == null || clips.Length == 0) return null;
            // Presentation variation must not consume the gameplay random draw stream.
            var index = (int)cue;
            var clip = clips[Variants[index]];
            Variants[index] = (Variants[index] + 1) % clips.Length;
            return clip;
        }
    }
}
