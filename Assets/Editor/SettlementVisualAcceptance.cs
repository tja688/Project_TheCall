using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QFramework;
using TheCall.Scoring;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    // Injects only starting monsters/resources. The real button, rules and playback do the settlement.
    public sealed class SettlementVisualAcceptance
    {
        [Serializable] public sealed class Frame
        {
            public int frame, slot, number, shards;
            public string phase;
            public float time, energy, dial, tank, transfer, pace;
            public float[] fills, lights;
            public bool captionsOverlap;
        }
        [Serializable] public sealed class Report
        {
            public string kind = "LOCAL_SETTLEMENT_FIXTURE_NOT_NATURAL_RUN";
            public string scenario;
            public int expected, shown;
            public bool completed, allColumnsEmpty;
            public List<Frame> frames = new List<Frame>();
        }
        static OperationScreenView Op => Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
        static TheCallPresentation Presentation => Object.FindAnyObjectByType<TheCallPresentation>();
        static IArchitecture App => TheCallApp.Interface;
        public static string Status { get; private set; } = "idle";
        string _directory;
        int _mode;
        Report _report;
        readonly HashSet<string> _captures = new HashSet<string>();
        string _phase;
        float _phaseSince;
        float _lastSample;

        public static string Prepare(string scenario = "basic", int count = 5)
        {
            if (!Application.isPlaying || ScoringShow.IsPlaying) throw new InvalidOperationException("Requires idle Play Mode");
            PresentationFixtureAcceptance.Begin("settlement", 1, "Temp/SettlementFixture");
            var run = App.GetModel<RunModel>();
            foreach (var old in run.Cage.ToArray()) run.TryDestroy(old.Id);
            if (scenario == "tools") run.AddTool("小喇叭");
            if (count == 6) { run.AddTool("上级员工证"); App.GetModel<LevelModel>().FitExtraction(6); }
            if (scenario == "retained") App.GetModel<LevelModel>().BeginLevel(50, 60, 35);
            for (var i = 0; i < count; i++)
            {
                var skills = i == 1 ? new[] { "蜜能量体" } : i == 3 ? new[] { "奇异香" } : new[] { "能量体" };
                if (scenario == "zero") skills = new[] { "奇异香" };
                if (scenario == "complex" && i == 2) skills = new[] { "左能量体", "双头能量体", "右能量体", "能量体" };
                var monster = run.CreateMonster(skills, personality: scenario == "complex" && (i == 1 || i == 4) ? "自私" : "稳定", mood: "普通");
                run.AddToCage(monster);
                App.SendCommand(new PlaceMonsterCommand(monster.Id, OperationArea.Extraction, i));
            }
            Presentation.Refresh();
            Op.lab.facility.panorama.Focus(true, true);
            Canvas.ForceUpdateCanvases();
            return "PASS prepared " + scenario + " with " + count + " real extraction bindings";
        }

        public static string Begin(string directory, int mode = 0, string scenario = "basic")
        {
            if (ScoringShow.IsPlaying) throw new InvalidOperationException("Prior playback still running");
            var recorder = new SettlementVisualAcceptance();
            recorder._directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(recorder._directory);
            recorder._mode = mode;
            recorder._report = new Report { scenario = scenario };
            Status = "running";
            Presentation.StartCoroutine(recorder.Record());
            return "started";
        }

        IEnumerator Record()
        {
            yield return new WaitForEndOfFrame();
            Capture("00-layout");
            Op.nextDayButton.onClick.Invoke();
            var initial = App.SendQuery(new SettlementRecordQuery()).OfType<SettlementLanding>().Sum(l => l.Energy);
            _report.expected = initial + (int)Op.lab.facility.PresentedEnergy;
            var started = Time.realtimeSinceStartup;
            var clicked = false;
            while (ScoringShow.IsPlaying && Time.realtimeSinceStartup - started < 120)
            {
                yield return new WaitForEndOfFrame();
                var show = Object.FindAnyObjectByType<ScoringShow>();
                if (show == null) break;
                var view = Op.lab.facility;
                var sample = new Frame
                {
                    frame = Time.frameCount, time = Time.realtimeSinceStartup - started,
                    slot = show.ActiveSlot, number = show.DisplayedSkillEnergy, phase = show.EnergyPhase,
                    shards = show.ShardCount, transfer = show.TransferProgress, pace = show.PlayPace,
                    energy = view.PresentedEnergy, dial = view.DialProgress, tank = view.collectorFluid.localScale.y,
                    fills = view.energyColumns.Select(c => c.Fill).ToArray(),
                    lights = view.energyColumns.Select(c => c.lamp.lightSource.intensity).ToArray(),
                    captionsOverlap = CaptionsOverlap()
                };
                if (sample.time - _lastSample >= 1f / 60)
                {
                    _report.frames.Add(sample);
                    _lastSample = sample.time;
                }
                var phase = show.ActiveSlot + ":" + show.EnergyPhase;
                if (_phase != phase) { _phase = phase; _phaseSince = sample.time; }
                var phaseAge = sample.time - _phaseSince;
                if (_mode == 0)
                {
                    var toolCaption = Op.GetComponentsInChildren<CanvasGroup>().FirstOrDefault(c => c.name == "ScoringEffectCaption" && c.alpha > 0.98f && c.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "小喇叭"));
                    if (toolCaption != null && _captures.Add("05-tool")) Capture("05-tool");
                    if (show.ActiveSlot == 1 && (show.EnergyPhase == "skill" || show.EnergyPhase == "support" || show.EnergyPhase == "merge" || show.EnergyPhase == "number" || show.EnergyPhase == "charged"))
                    {
                        var key = "02-" + show.EnergyPhase;
                        // Capture settled readable captions and visibly separated fragments.
                        if (phaseAge > (show.EnergyPhase == "number" ? 0.1f : show.EnergyPhase == "charged" ? 0.025f : 0.2f) && _captures.Add(key)) Capture(key);
                    }
                    if (show.TransferProgress > 0.42f && show.TransferProgress < 0.72f && show.ActiveSlot == 2 && _captures.Add("03-transfer")) Capture("03-transfer");
                    if (show.EnergyPhase == "zero" && _captures.Add("04-zero")) Capture("04-zero");
                }
                else if (!clicked)
                {
                    show.Advance();
                    if (_mode == 2) show.Advance();
                    clicked = true;
                }
            }
            _report.shown = Mathf.RoundToInt(Op.lab.facility.PresentedEnergy);
            _report.completed = !ScoringShow.IsPlaying;
            _report.allColumnsEmpty = Op.lab.facility.energyColumns.All(c => c.Fill == 0 && c.Glow == 0);
            File.WriteAllText(Path.Combine(_directory, "trace.json"), JsonUtility.ToJson(_report, true));
            Status = _report.completed && _report.shown == _report.expected && _report.allColumnsEmpty ? "completed" : "failed";
            Capture("99-completed");
        }

        void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(_directory, name + ".png"));

        static bool CaptionsOverlap()
        {
            var captions = Op.GetComponentsInChildren<CanvasGroup>().Where(c =>
                (c.name == "ScoringEnergyCaption" || c.name == "ScoringEffectCaption") && c.alpha > 0.2f).ToArray();
            var rects = new List<Rect>();
            var scale = Op.GetComponentInParent<Canvas>().scaleFactor;
            var gaugeCorners = new Vector3[4];
            ((RectTransform)Op.lab.facility.needle.parent).GetWorldCorners(gaugeCorners);
            var gaugeMin = RectTransformUtility.WorldToScreenPoint(null, gaugeCorners[0]);
            var gaugeMax = RectTransformUtility.WorldToScreenPoint(null, gaugeCorners[2]);
            var gauge = Rect.MinMaxRect(gaugeMin.x, gaugeMin.y, gaugeMax.x, gaugeMax.y);
            foreach (var caption in captions)
            {
                var corners = new Vector3[4];
                ((RectTransform)caption.transform).GetWorldCorners(corners);
                var min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                var max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                var rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                if (rect.xMin < 0 || rect.xMax > Screen.width || rect.yMin < 80 * scale || rect.yMax > Screen.height - 128 * scale) return true;
                if (rect.Overlaps(gauge)) return true;
                if (rects.Any(prior => prior.Overlaps(rect))) return true;
                rects.Add(rect);
            }
            return false;
        }
    }
}
