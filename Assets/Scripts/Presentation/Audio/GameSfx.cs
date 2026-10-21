using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace TheCall
{
    /// <summary>
    /// AudioKit entry point for all new effects; use AudioClip references, never Resources paths.
    /// Looping effects must be attached to a GameObject so disable/destroy cleans them up.
    /// </summary>
    public static class GameSfx
    {
        static readonly HashSet<LoopSfxOwner> ActiveLoops = new HashSet<LoopSfxOwner>();
        static readonly HashSet<SfxVoice> ActiveShots = new HashSet<SfxVoice>();

        public static SfxVoice PlayOneShot(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || !AudioKit.Settings.IsSoundOn.Value) return null;
            var voice = new SfxVoice();
            var player = AudioKit.PlaySound(clip, false, _ => voice.Finished(), Mathf.Clamp01(volume), pitch);
            Route(player);
            voice.Attach(player);
            ActiveShots.Add(voice);
            return voice;
        }

        public static void PlayLoop(GameObject owner, AudioClip clip, float volume = 1f)
        {
            if (owner == null || !owner.activeInHierarchy || clip == null ||
                !AudioKit.Settings.IsSoundOn.Value) return;
            var component = owner.GetComponent<LoopSfxOwner>();
            if (component == null) component = owner.AddComponent<LoopSfxOwner>();
            component.Play(clip, Mathf.Clamp01(volume));
        }

        public static void StopLoop(GameObject owner)
        {
            if (owner == null) return;
            var component = owner.GetComponent<LoopSfxOwner>();
            if (component != null) component.Stop();
        }

        public static void StopAll()
        {
            foreach (var voice in new List<SfxVoice>(ActiveShots)) voice.Stop();
            // Stop owner handles before the AudioKit pool recycles them.
            var handles = new List<LoopSfxOwner>(ActiveLoops);
            foreach (var handle in handles)
                if (handle != null) handle.Stop();
            ActiveLoops.Clear();
            AudioKit.StopAllSound();
        }

        internal static void Track(LoopSfxOwner owner) { ActiveLoops.Add(owner); }
        internal static void Untrack(LoopSfxOwner owner) { ActiveLoops.Remove(owner); }
        internal static void Untrack(SfxVoice voice) { ActiveShots.Remove(voice); }

        internal static AudioPlayer StartLoop(AudioClip clip, float volume)
        {
            var player = AudioKit.PlaySound(clip, true, null, volume);
            Route(player);
            return player;
        }

        static void Route(AudioPlayer player)
        {
            if (player == null) return;
            var catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
            if (catalog != null && catalog.sfxGroup != null)
                player.OutputMixerGroup = catalog.sfxGroup;
        }
    }

    // The handle retires when playback ends, before AudioKit reuses its pooled player.
    public sealed class SfxVoice
    {
        AudioPlayer _player;
        public bool IsPlaying => _player != null;
        internal void Attach(AudioPlayer player) => _player = player;
        internal void Finished()
        {
            _player = null;
            GameSfx.Untrack(this);
        }
        public void Stop()
        {
            var player = _player;
            Finished();
            if (player != null) player.Stop();
        }
    }

    // Public because AddComponent<T> requires a concrete component with a public class.
    public sealed class LoopSfxOwner : MonoBehaviour
    {
        AudioPlayer _player;
        AudioClip _clip;

        internal void Play(AudioClip clip, float volume)
        {
            if (_player != null && _clip == clip) return;
            Stop();
            _clip = clip;
            _player = GameSfx.StartLoop(clip, volume);
            if (_player != null) GameSfx.Track(this);
        }

        public void Stop()
        {
            var player = _player;
            _player = null;
            _clip = null;
            GameSfx.Untrack(this);
            if (player != null) player.Stop();
        }

        void OnDisable() { Stop(); }
        void OnDestroy() { Stop(); }
    }
}
