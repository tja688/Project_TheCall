using System.Collections;
using QFramework;
using UnityEngine;
using UnityEngine.Audio;

namespace TheCall
{
    public enum MusicState { Silent, Menu, Tutorial, Level, Shop }

    /// <summary>
    /// Sole BGM owner. AudioKit's one MusicPlayer cannot crossfade; it is reserved for SFX here.
    /// Two persistent sources, never more; interrupted transitions cancel and reclaim both.
    /// </summary>
    public sealed class MusicManager : MonoBehaviour
    {
        const string CatalogKey = "GameAudioCatalog";
        static MusicManager _instance;
        AudioSource _first;
        AudioSource _second;
        GameAudioCatalog _catalog;
        Coroutine _transition;
        MusicState _target = MusicState.Silent;
        bool _quitting;
        bool _reportedMissing;

        public static MusicState CurrentState => _instance != null ? _instance._target : MusicState.Silent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearStatic() { _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap() { EnsureInstance(); }

        static MusicManager EnsureInstance()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("[TheCall] MusicManager");
            _instance = go.AddComponent<MusicManager>();
            DontDestroyOnLoad(go);
            return _instance;
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            _catalog = Resources.Load<GameAudioCatalog>(CatalogKey);
            _first = CreateTrack("BGM A");
            _second = CreateTrack("BGM B");
            if (_catalog == null)
                Debug.LogError("[MusicManager] Missing Resources/GameAudioCatalog.asset. BGM stays silent.");
            // Old AudioKit MusicPlayer must not coexist with the sole BGM owner.
            AudioKit.StopMusic();
        }

        AudioSource CreateTrack(string label)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            if (_catalog != null) source.outputAudioMixerGroup = _catalog.bgmGroup;
            return source;
        }

        void Start()
        {
            // AudioMixer.SetFloat must be called after Awake / OnEnable, not during them.
            if (_catalog != null && _catalog.mixer != null)
                AudioKit.Settings.SoundVolume.Value = 1f;
            ApplyVolume();
        }

        void Update() { ApplyVolume(); }

        void ApplyVolume()
        {
            if (_catalog != null && _catalog.mixer != null)
            {
                // Bus controls live here; only fade envelopes change individual source volumes.
                GameAudioSettings.ApplyMixer(_catalog.mixer);
                if (!Mathf.Approximately(AudioKit.Settings.SoundVolume.Value, 1f))
                    AudioKit.Settings.SoundVolume.Value = 1f;
            }
            else
            {
                // Safe fallback if a mixer reference is accidentally removed.
                var scalar = GameAudioSettings.EffectiveMaster * GameAudioSettings.EffectiveMusic;
                if (_first != null) _first.mute = scalar <= 0f;
                if (_second != null) _second.mute = scalar <= 0f;
                // No mixer: other volume levels are applied to source gain.
                AudioKit.Settings.SoundVolume.Value =
                    GameAudioSettings.EffectiveMaster * GameAudioSettings.EffectiveSfx;
            }
        }

        public static void Request(MusicState state)
        {
            EnsureInstance().Switch(state, DurationFor(state));
        }

        public static void EnterSettlement() { EnsureInstance().Switch(MusicState.Silent, 0.65f); }

        /// <summary>For future tutorial UI: invoke only when tutorial has become visible.</summary>
        public static void EnterTutorial() { Request(MusicState.Tutorial); }

        public static void HardStop()
        {
            if (_instance != null) _instance.StopEverything();
        }

        public static void ReturnToTitle()
        {
            HardStop();
            Request(MusicState.Menu);
        }

        static float DurationFor(MusicState state)
        {
            switch (state)
            {
                case MusicState.Shop: return 0.5f;
                case MusicState.Tutorial: return 0.9f;
                case MusicState.Menu: return 0.9f;
                case MusicState.Level: return 0.8f;
                default: return 0.65f;
            }
        }

        void Switch(MusicState next, float seconds)
        {
            if (_quitting) return;
            if (_target == next) return;
            _target = next;

            // Cancelling a Coroutine also cancels any queued fade completion callback.
            if (_transition != null)
            {
                StopCoroutine(_transition);
                _transition = null;
            }

            AudioSource from = null;
            if (_first != null && _first.isPlaying) from = _first;
            if (_second != null && _second.isPlaying &&
                (from == null || _second.volume > from.volume)) from = _second;

            var other = from == _first ? _second : _first;
            // A cancelled crossfade may leave two sources alive. Always kill the loser.
            if (other != null) Clear(other);

            var clip = next == MusicState.Silent || _catalog == null ? null : _catalog.Find(next);
            if (next != MusicState.Silent && clip == null)
            {
                if (!_reportedMissing)
                {
                    Debug.LogError("[MusicManager] Unassigned BGM: " + next);
                    _reportedMissing = true;
                }
                if (from != null) _transition = StartCoroutine(Fade(from, null, seconds));
                return;
            }

            if (clip == null)
            {
                if (from != null) _transition = StartCoroutine(Fade(from, null, seconds));
                return;
            }

            // Already on the correct clip after an interrupted transition: only fade it to full.
            if (from != null && from.clip == clip)
            {
                _transition = StartCoroutine(Fade(from, from, seconds));
                return;
            }

            var incoming = other;
            incoming.clip = clip;
            incoming.loop = true;
            incoming.volume = 0f;
            incoming.Play();
            _transition = StartCoroutine(Fade(from, incoming, seconds));
        }

        IEnumerator Fade(AudioSource outgoing, AudioSource incoming, float seconds)
        {
            var outgoingStart = outgoing != null ? outgoing.volume : 0f;
            var incomingStart = incoming != null ? incoming.volume : 0f;
            var elapsed = 0f;
            seconds = Mathf.Max(0.01f, seconds);
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / seconds);
                if (outgoing != null && outgoing != incoming)
                    outgoing.volume = outgoingStart * (1f - t);
                if (incoming != null)
                    incoming.volume = Mathf.Lerp(incomingStart, 1f, t);
                yield return null;
            }

            if (outgoing != null && outgoing != incoming) Clear(outgoing);
            if (incoming != null) incoming.volume = 1f;
            _transition = null;
        }

        static void Clear(AudioSource source)
        {
            source.Stop();
            source.clip = null;
            source.volume = 0f;
        }

        void StopEverything()
        {
            StopAllCoroutines();
            _transition = null;
            _target = MusicState.Silent;
            if (_first != null) Clear(_first);
            if (_second != null) Clear(_second);
            AudioKit.StopMusic();
        }

        void OnApplicationQuit()
        {
            _quitting = true;
            StopEverything();
            GameSfx.StopAll();
            GameAudioSettings.Save();
        }

        void OnDestroy()
        {
            if (_instance != this) return;
            StopEverything();
            _instance = null;
        }
    }
}
