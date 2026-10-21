using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QFramework;
using TheCall.Scoring;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    // Local starting-state fixtures. Every observed sound comes from production input/playback.
    public static class GameplayAudioAcceptance
    {
        [Serializable] sealed class Sound
        {
            public string scenario, cue, clip;
            public int frame;
            public float time, shownEnergy;
            public bool sourcePlaying, routed, loop;
        }
        [Serializable] sealed class Report
        {
            public string kind = "LOCAL_AUDIO_FIXTURES_NOT_NATURAL_RUN";
            public string unity;
            public bool passed;
            public List<string> checks = new List<string>();
            public List<Sound> sounds = new List<Sound>();
        }
        static Report _report;
        static string _scenario, _directory;
        static bool _previousSound;
        public static string Status { get; private set; } = "idle";
        static TheCallPresentation Presentation => Object.FindAnyObjectByType<TheCallPresentation>();
        static OperationScreenView Op => Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
        static IArchitecture App => TheCallApp.Interface;

        public static string Begin(string directory)
        {
            if (!Application.isPlaying || ScoringShow.IsPlaying || Status == "running")
                throw new InvalidOperationException("Requires idle Play Mode");
            _directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(_directory);
            _report = new Report { unity = Application.unityVersion };
            _previousSound = AudioKit.Settings.IsSoundOn.Value;
            AudioKit.Settings.IsSoundOn.Value = true;
            GameplaySfx.Played += Record;
            Status = "running";
            var host = new GameObject("GameplayAudioAcceptanceRunner").AddComponent<AudioAcceptanceRunner>();
            host.StartCoroutine(Exercise(host));
            return Status;
        }

        static void Record(GameplayCue cue, AudioClip clip)
        {
            var source = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include)
                .FirstOrDefault(s => s.clip == clip && s.isPlaying);
            var catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
            _report.sounds.Add(new Sound { scenario = _scenario, cue = cue.ToString(), clip = clip.name,
                frame = Time.frameCount, time = Time.unscaledTime, shownEnergy = Op.lab.facility.PresentedEnergy,
                sourcePlaying = source != null, routed = source != null && source.outputAudioMixerGroup == catalog.sfxGroup,
                loop = source != null && source.loop });
        }

        static IEnumerator Exercise(AudioAcceptanceRunner host)
        {
            try
            {
                _scenario = "drag";
                PresentationFixtureAcceptance.Begin("audio", 2, _directory);
                Op.lab.facility.panorama.Focus(false, true);
                yield return null;
                var source = Op.lab.facility.dormSlots.First(s => !string.IsNullOrEmpty(s.monsterId));
                var id = source.monsterId;
                var drag = source.GetComponent<OperationPayloadDrag>();
                var gesture = Gesture(source.transform);
                drag.OnPointerDown(gesture);
                drag.OnBeginDrag(gesture);
                Check(Count(GameplayCue.MonsterPickUp) == 1, "drag picks up once");
                Op.lab.facility.panorama.Focus(true, true);
                yield return null;
                Canvas.ForceUpdateCanvases();
                gesture.position = Center(Op.extractionSlots[0].transform);
                drag.OnDrag(gesture); drag.OnEndDrag(gesture); drag.OnPointerUp(gesture);
                Check(Op.extractionSlots[0].monsterId == id && Count(GameplayCue.MonsterPlace) == 1, "successful actual drop places once");
                var placed = Op.extractionSlots[0].GetComponent<OperationPayloadDrag>();
                gesture = Gesture(placed.transform);
                placed.OnPointerDown(gesture); placed.OnBeginDrag(gesture);
                gesture.position = new Vector2(-20, -20);
                placed.OnEndDrag(gesture);
                Check(Count(GameplayCue.MonsterPickUp) == 2 && Count(GameplayCue.MonsterPlace) == 1, "rejected drop has no successful-place sound");
                Check(Count(GameplayCue.MonsterDiscard) == 0 && Count(GameplayCue.SkillKill) == 0, "rejected drop has no death sound");

                _scenario = "discard";
                PresentationFixtureAcceptance.Begin("audio", 1, _directory);
                var discardId = App.SendQuery(new MonsterCageQuery())[0].Id;
                var applied = Commit(new OperationDrop(DropPayload.Monster(discardId), new DropLanding(LandingPlace.Discard, 0, null)));
                Check(applied && Count(GameplayCue.MonsterDiscard) == 1 && Count(GameplayCue.SkillKill) == 0, "active discard uses its own clip after successful command");

                var states = new List<string>();
                var logs = new List<string>();
                foreach (var mode in new[] { 0, 1, 2 })
                {
                    _scenario = "settlement-" + mode;
                    SettlementVisualAcceptance.Prepare("complex", 5);
                    ResetTarget(10, 20);
                    Presentation.Refresh();
                    Op.lab.facility.panorama.Focus(true, true);
                    yield return null;
                    Op.nextDayButton.onClick.Invoke();
                    if (mode > 0)
                    {
                        var show = Object.FindAnyObjectByType<ScoringShow>();
                        show.Advance();
                        if (mode == 2) show.Advance();
                    }
                    yield return AwaitPlayback();
                    states.Add(M5PlayModeAutomation.State());
                    logs.Add(ProductionLogText.Format(App.GetModel<RunModel>().ProductionLog));
                    File.WriteAllText(Path.Combine(_directory, "settlement-" + mode + "-state.txt"), states[mode]);
                    File.WriteAllText(Path.Combine(_directory, "settlement-" + mode + "-log.txt"), logs[mode]);
                    Check(Count(GameplayCue.Settle) == 1, _scenario + " starts once");
                    Check(Count(GameplayCue.ReachRequirement) == 1, _scenario + " crosses target once");
                    if (mode == 0)
                    {
                        Check(Count(GameplayCue.SkillEnergy) > 0 && Count(GameplayCue.MonsterEnergyGain) > 0, "productive skills and local columns use distinct clips");
                        Check(Count(GameplayCue.SkillMultiply) > 0, "actual factor merge plays multiply");
                    }
                    if (mode == 2) Check(Count(GameplayCue.MonsterEnergyGain) == 0 && Count(GameplayCue.SkillMultiply) == 0, "skip suppresses intermediate charge and factor bursts");
                    Check(_report.sounds.Where(s => s.scenario == _scenario && s.cue == GameplayCue.ReachRequirement.ToString()).All(s => s.shownEnergy >= 10), "target cue follows displayed energy");
                    yield return null;
                }
                Check(states.Distinct().Count() == 1 && logs.Distinct().Count() == 1, "normal, fast and skip preserve identical rule state and production records");

                _scenario = "zero";
                SettlementVisualAcceptance.Prepare("zero", 2);
                yield return null;
                Op.nextDayButton.onClick.Invoke();
                yield return AwaitPlayback();
                Check(Count(GameplayCue.SkillEnergy) == 0 && Count(GameplayCue.MonsterEnergyGain) == 0 && Count(GameplayCue.SkillMultiply) == 0 && Count(GameplayCue.ReachRequirement) == 0, "zero and failed effects do not produce success sounds");

                _scenario = "kill";
                PresentationFixtureAcceptance.Begin("audio", 1, _directory);
                var run = App.GetModel<RunModel>();
                var bomber = run.CreateMonster(new[] { "自爆体" }, personality: "稳定", mood: "普通");
                run.AddToCage(bomber);
                App.SendCommand(new PlaceMonsterCommand(bomber.Id, OperationArea.Extraction, 0));
                Presentation.Refresh();
                Op.lab.facility.panorama.Focus(true, true);
                yield return null;
                Op.nextDayButton.onClick.Invoke();
                yield return AwaitPlayback();
                Check(Count(GameplayCue.SkillKill) == 1 && Count(GameplayCue.MonsterDiscard) == 0, "real self-destruction plays skill kill once");

                _scenario = "no-column";
                SettlementVisualAcceptance.Prepare("basic", 1);
                var columns = Op.lab.facility.energyColumns;
                try
                {
                    Op.lab.facility.energyColumns = Array.Empty<FacilityEnergyColumn>();
                    yield return null;
                    Op.nextDayButton.onClick.Invoke();
                    yield return AwaitPlayback();
                    Check(Count(GameplayCue.SkillEnergy) > 0 && Count(GameplayCue.MonsterEnergyGain) == 0, "missing local column does not play monster-bar growth sound");
                }
                finally { Op.lab.facility.energyColumns = columns; }

                _scenario = "cancel";
                SettlementVisualAcceptance.Prepare("basic", 2);
                yield return null;
                Op.nextDayButton.onClick.Invoke();
                yield return null;
                Presentation.enabled = false;
                Check(!ScoringShow.IsPlaying, "owner disable cancels playback");
                var catalog = Resources.Load<GameAudioCatalog>("GameAudioCatalog");
                Check(!Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Any(s => s.isPlaying && (s.clip == catalog.settle || catalog.skillEnergy.Contains(s.clip))), "canceled settlement voices stop before pooled reuse");
                Presentation.enabled = true;

                _scenario = "mute";
                AudioKit.Settings.IsSoundOn.Value = false;
                Check(GameplaySfx.Play(GameplayCue.MonsterPlace) == null && Count(GameplayCue.MonsterPlace) == 0, "SFX mute prevents playback");
                AudioKit.Settings.IsSoundOn.Value = true;
                var voice = GameplaySfx.Play(GameplayCue.MonsterPlace);
                yield return new WaitForSecondsRealtime(1);
                Check(!voice.IsPlaying, "one-shot handle retires on natural completion");
                var later = GameplaySfx.Play(GameplayCue.MonsterDiscard);
                voice.Stop();
                Check(later.IsPlaying, "retired handle cannot stop a reused pooled player");
                later.Stop();
                Check(_report.sounds.All(s => s.sourcePlaying && s.routed && !s.loop), "every traced clip has an actual playing one-shot AudioSource on SFX mixer");
                Check(!_report.sounds.Any(s => s.clip.Contains("ExperiencePickup")), "experience sequence is not misused for gameplay rewards");
                _report.passed = true;
                Status = "completed";
            }
            finally
            {
                if (Status != "completed") Status = "failed";
                GameplaySfx.Played -= Record;
                AudioKit.Settings.IsSoundOn.Value = _previousSound;
                File.WriteAllText(Path.Combine(_directory, "runtime.json"), JsonUtility.ToJson(_report, true));
                Object.Destroy(host.gameObject);
            }
        }

        static IEnumerator AwaitPlayback()
        {
            var deadline = Time.realtimeSinceStartup + 100;
            while (ScoringShow.IsPlaying && Time.realtimeSinceStartup < deadline) yield return null;
            Check(!ScoringShow.IsPlaying, _scenario + " completes playback");
            // Destroy() and the controller's deferred Refresh complete on the following frame.
            yield return null;
        }
        static void ResetTarget(int due, int excess)
        {
            var ids = App.SendQuery(new ExtractionSlotsQuery()).Select(cell => cell.MonsterId).ToArray();
            var run = App.GetModel<RunModel>();
            App.GetModel<LevelModel>().BeginLevel(due, excess);
            for (var i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i])) continue;
                run.AddToCage(run.Find(ids[i]));
                App.SendCommand(new PlaceMonsterCommand(ids[i], OperationArea.Extraction, i));
            }
        }
        static int Count(GameplayCue cue) => _report.sounds.Count(s => s.scenario == _scenario && s.cue == cue.ToString());
        static void Check(bool condition, string check)
        {
            if (!condition) throw new InvalidOperationException("AUDIO_ACCEPTANCE_FAIL: " + check);
            _report.checks.Add(check);
        }
        static bool Commit(OperationDrop drop) => (bool)typeof(TheCallPresentation).GetMethod("CommitDrop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(Presentation, new object[] { drop });
        static PointerEventData Gesture(Transform target) => new PointerEventData(EventSystem.current) { position = Center(target), pressPosition = Center(target), button = PointerEventData.InputButton.Left };
        static Vector2 Center(Transform target) => RectTransformUtility.WorldToScreenPoint(CanvasMap.EventCamera(target), ((RectTransform)target).TransformPoint(((RectTransform)target).rect.center));
    }

    public sealed class AudioAcceptanceRunner : MonoBehaviour { }
}
