using System.Collections.Generic;

namespace TheCall.Scoring
{
    public sealed partial class ScoringShow
    {
        readonly List<SfxVoice> _voices = new List<SfxVoice>();
        readonly Dictionary<GameplayCue, float> _lastCue = new Dictionary<GameplayCue, float>();
        bool _requirementReached;

        void BeginSfx()
        {
            // Overtime starts with the previous total. An already reached target is silent.
            _requirementReached = _stage.EnergyDue <= 0 || _stage.InitialEnergy >= _stage.EnergyDue;
            PlaySfx(GameplayCue.Settle);
        }

        void PlaySfx(GameplayCue cue)
        {
            var now = UnityEngine.Time.unscaledTime;
            // Fast-forward can consume many cues per frame. Do not stack their transients.
            if (_lastCue.TryGetValue(cue, out var last) && now - last < 0.09f) return;
            _lastCue[cue] = now;
            _voices.RemoveAll(voice => !voice.IsPlaying);
            var playing = GameplaySfx.Play(cue);
            if (playing != null) _voices.Add(playing);
        }

        void ObserveRequirement(float total)
        {
            if (_requirementReached || total < _stage.EnergyDue) return;
            _requirementReached = true;
            PlaySfx(GameplayCue.ReachRequirement);
        }

        void StopSfx()
        {
            foreach (var voice in _voices) voice.Stop();
            _voices.Clear();
        }
    }
}
