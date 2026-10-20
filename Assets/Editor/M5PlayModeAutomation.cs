using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using QFramework;
using TheCall.Scoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    // Acceptance driver: each call performs one real player input and returns.
    // The CLI waits for advancing frames / completed presentation between calls.
    // Local stress fixtures are deliberately separate from this natural-run driver.
    public static class M5PlayModeAutomation
    {
        public sealed class SeededDraw : IDraw
        {
            readonly System.Random _random;
            public int Cursor { get; private set; }
            public SeededDraw(int seed) { _random = new System.Random(seed); }
            public T Choose<T>(IReadOnlyList<T> options)
            {
                if (options == null || options.Count == 0) throw new InvalidOperationException("Empty random pool");
                Cursor++;
                return options[_random.Next(options.Count)];
            }
            public bool Chance(int percent) { Cursor++; return _random.Next(100) < percent; }
        }

        static readonly StringBuilder Trace = new StringBuilder();
        static SeededDraw _draw;
        public static bool Playing => Object.FindAnyObjectByType<ScoringShow>() != null;
        static string _evidenceName = "natural-normal";
        static string _evidenceDirectory;
        public static string EvidenceRoot => _evidenceDirectory ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/audit/2026-10-12/remediation/evidence", _evidenceName));

        public static string Begin(int seed)
        {
            if (!Application.isPlaying || Playing) throw new InvalidOperationException("Enter Play Mode and wait for the prior presentation first.");
            Application.runInBackground = true;
            var gameType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameType != null) EditorWindow.GetWindow(gameType).Focus();
            TheCallApp.Reset();
            ContentGate.Use(ContentBook.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "call-book.json"))));
            _draw = new SeededDraw(seed);
            TheCallApp.OnRegisterPatch = app => app.RegisterUtility<IDraw>(_draw);
            Trace.Clear();
            Trace.AppendLine("natural-player-run seed=" + seed + " unity=" + Application.unityVersion);
            Presentation().StartSession();
            Presentation().Refresh();
            return Snapshot("begin");
        }

        public static string Click(string action, string value = "")
        {
            if (Playing && action != "playback") throw new InvalidOperationException("Wait for presentation to finish.");
            Button button = null;
            switch (action)
            {
                case "opening":
                    var opening = Object.FindAnyObjectByType<OpeningScreenView>(FindObjectsInactive.Include);
                    foreach (var slot in opening.candidates) if (slot.monsterId == value) button = slot.button;
                    break;
                case "begin": button = Object.FindAnyObjectByType<LevelStartScreenView>(FindObjectsInactive.Include).beginButton; break;
                case "settle": button = Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include).nextDayButton; break;
                case "leave": button = Object.FindAnyObjectByType<ShopScreenView>(FindObjectsInactive.Include).leaveButton; break;
                case "refresh": button = Object.FindAnyObjectByType<ShopScreenView>(FindObjectsInactive.Include).refreshButton; break;
                case "buy":
                    foreach (var card in Object.FindAnyObjectByType<ShopScreenView>(FindObjectsInactive.Include).cards)
                        if (card.monsterId == value || card.toolName == value) button = card.buyButton;
                    break;
                case "restart": button = Object.FindAnyObjectByType<ResultScreenView>(FindObjectsInactive.Include).restartButton; break;
                case "research-open":
                    button = TheCallApp.Interface.SendQuery(new RunPhaseQuery()) == RunPhase.Shop
                        ? Object.FindAnyObjectByType<ShopScreenView>(FindObjectsInactive.Include).researchButton
                        : Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include).researchButton;
                    break;
                case "research-back": button = Object.FindAnyObjectByType<ResearchScreenView>(FindObjectsInactive.Include).backButton; break;
                default: throw new ArgumentException("Unknown input: " + action);
            }
            if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
                throw new InvalidOperationException("Unavailable player button: " + action + " " + value);
            Canvas.ForceUpdateCanvases();
            var clickPoint = CenterOf((RectTransform)button.transform);
            var clickData = new PointerEventData(EventSystem.current) { position = clickPoint, button = PointerEventData.InputButton.Left };
            var clickHits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(clickData, clickHits);
            if (clickHits.Count == 0 || clickHits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>() != button)
                throw new InvalidOperationException("Player button is occluded: " + action + " " + value);
            ExecuteEvents.Execute(button.gameObject, clickData, ExecuteEvents.pointerClickHandler);
            return Snapshot(action + " " + value);
        }

        public static string Drag(string monsterId, string area, int index)
        {
            if (Playing) throw new InvalidOperationException("Presentation still playing");
            var operation = Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
            MonsterSlotView source = null;
            foreach (var slot in operation.cageSlots.Concat(operation.extractionSlots).Concat(operation.breedingSlots))
                if (slot.monsterId == monsterId && slot.GetComponent<OperationPayloadDrag>() != null) { source = slot; break; }
            if (source == null) throw new InvalidOperationException("Visible drag source missing: " + monsterId);
            var targets = area == "extraction" ? operation.extractionSlots : area == "breeding" ? operation.breedingSlots : operation.cageSlots;
            var target = targets[index];
            if (area != "cage" && !target.gameObject.activeInHierarchy) throw new InvalidOperationException("Drop target hidden");
            Canvas.ForceUpdateCanvases();
            var canvas = target.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var destination = area == "cage" && operation.lab != null ? operation.lab.cageScroll.viewport : target.GetComponent<RectTransform>();
            var position = RectTransformUtility.WorldToScreenPoint(camera, destination.TransformPoint(destination.rect.center));
            var start = CenterOf((RectTransform)source.transform);
            var data = new PointerEventData(EventSystem.current) { position = start, pressPosition = start, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<MonsterSlotView>() != source)
                throw new InvalidOperationException("Drag source occluded: " + monsterId);
            var payload = source.GetComponent<OperationPayloadDrag>();
            payload.OnPointerDown(data);
            payload.OnBeginDrag(data);
            data.position = position;
            payload.OnDrag(data);
            payload.OnEndDrag(data);
            if (area != "cage" && target.monsterId != monsterId) throw new InvalidOperationException("Actual raycast drop failed: " + monsterId + " -> " + area + " " + index);
            if (area == "cage" && !System.Linq.Enumerable.Any(TheCallApp.Interface.GetModel<RunModel>().Cage, m => m.Id == monsterId)) throw new InvalidOperationException("Actual return drop failed");
            return Snapshot("drag " + monsterId + " " + area + " " + index);
        }

        public static string Snapshot(string input = "sample")
        {
            var app = TheCallApp.Interface;
            var run = app.GetModel<RunModel>();
            var level = app.GetModel<LevelModel>();
            var text = "frame=" + Time.frameCount + " input=" + input + " phase=" + run.Phase + " day=" + run.LevelNumber
                + " energy=" + level.Energy + " gold=" + run.Gold + " tech=" + run.TechPoints + " rng=" + (_draw?.Cursor ?? -1) + " playback=" + Playing;
            Trace.AppendLine(text);
            foreach (var monster in run.LivingMonsters)
            {
                Trace.Append("  monster=" + monster.Id + " personality=" + monster.Personality + " race=" + monster.Race + " mood=" + monster.Mood + " skills=");
                foreach (var skill in monster.Skills) Trace.Append(skill.Name + ":" + skill.Quote + ":temporary=" + skill.IsTemporary + ";");
                Trace.AppendLine();
            }
            Directory.CreateDirectory(EvidenceRoot);
            File.WriteAllText(Path.Combine(EvidenceRoot, "player-trace.txt"), Trace.ToString());
            File.WriteAllText(Path.Combine(EvidenceRoot, "production-log.txt"), ProductionLogText.Format(run.ProductionLog));
            File.AppendAllText(Path.Combine(EvidenceRoot, "state-trace.txt"), input + "\n" + State() + "\n");
            return text;
        }

        public static string State()
        {
            var app = TheCallApp.Interface; var run = app.GetModel<RunModel>(); var level = app.GetModel<LevelModel>();
            var b = new StringBuilder();
            b.AppendLine("phase=" + run.Phase + ";day=" + run.LevelNumber + ";energy=" + level.Energy + ";due=" + level.EnergyDue + ";overtime=" + level.InOvertime + ";gold=" + run.Gold + ";tech=" + run.TechPoints + ";retained=" + run.RetainedEnergy + ";rng=" + (_draw?.Cursor ?? -1));
            b.AppendLine("tools=" + string.Join(",", run.Tools) + ";research=" + string.Join(",", run.UnlockedTech));
            b.AppendLine("cage=" + string.Join(",", run.Cage.Select(m => m.Id)) + ";extraction=" + string.Join(",", level.Extraction) + ";breeding=" + string.Join(",", level.Breeding));
            foreach (var gene in run.GeneSlots) b.AppendLine("gene=" + gene.Id + ";skill=" + gene.SkillName + ";reservation=" + gene.ReservedBreedingSlot);
            foreach (var m in run.LivingMonsters.OrderBy(m => m.Id))
            {
                b.AppendLine("monster=" + m.Id + ";personality=" + m.Personality + ";race=" + m.Race + ";mood=" + m.Mood + ";baseMood=" + m.DayBaseMood + ";boost=" + m.DayMoodBoost + ";work=" + m.ConsecutiveWorkDays + ";rest=" + m.ConsecutiveRestDays + ";clocked=" + m.HasClockedInToday + ";worked=" + run.DidWorkToday(m.Id) + ";modifier=" + m.Modifier + ";immovable=" + m.Immovable + ";capacity=" + m.Capacity + ";parents=" + string.Join(",", m.ParentIds));
                foreach (var skill in m.Skills) b.AppendLine("skill=" + skill.Id + ";name=" + skill.Name + ";quote=" + skill.Quote + ";permanent=" + skill.PermanentQuote + ";temporary=" + skill.IsTemporary + ";source=" + skill.SourceKind + ";sourceId=" + skill.SourceId + ";sourceMonster=" + skill.SourceMonsterId);
            }
            return b.ToString();
        }

        // Capture includes Overlay canvases. Its file is written after end-of-frame;
        // the caller must wait for a later frame and visually inspect the result.
        public static string CaptureScreenshot(string filename)
        {
            Directory.CreateDirectory(EvidenceRoot);
            var path = Path.Combine(EvidenceRoot, Path.GetFileName(filename));
            ScreenCapture.CaptureScreenshot(path);
            Snapshot("capture " + filename);
            return path;
        }

        [Serializable] sealed class PlannedInput { public string Action; public string Value; public int Index; }
        [Serializable] sealed class PlayerPlan { public int seed; public PlannedInput[] actions; }
        static PlayerPlan _plan;
        static int _step;
        static double _nextInput;
        static bool _running;
        static string _failure;
        static int _playbackMode;
        static ScoringShow _lastPlayback;
        static int _clickCount;
        public static string PlanStatus => _failure ?? (_running ? "running " + _step + "/" + _plan.actions.Length : "completed " + _step);

        public static string BeginPlan(string path, int playbackMode = 0, string evidenceDirectory = null)
        {
            if (_running) throw new InvalidOperationException("Player replay already running.");
            _evidenceDirectory = string.IsNullOrEmpty(evidenceDirectory) ? null : Path.GetFullPath(evidenceDirectory);
            _plan = JsonUtility.FromJson<PlayerPlan>(File.ReadAllText(path));
            _playbackMode = playbackMode; _lastPlayback = null; _clickCount = 0;
            _evidenceName = playbackMode == 0 ? "natural-normal" : playbackMode == 1 ? "natural-fast" : "natural-skip";
            Directory.CreateDirectory(EvidenceRoot);
            File.WriteAllText(Path.Combine(EvidenceRoot, "state-trace.txt"), "");
            Begin(_plan.seed);
            _step = 0; _failure = null; _running = true;
            CaptureScreenshot("01-opening.png");
            _nextInput = EditorApplication.timeSinceStartup + 1;
            EditorApplication.update -= AdvancePlan;
            EditorApplication.update += AdvancePlan;
            return PlanStatus;
        }

        static void AdvancePlan()
        {
            if (!_running || EditorApplication.timeSinceStartup < _nextInput) return;
            if (Presentation().Transitioning) return;
            if (Playing)
            {
                if (_playbackMode != 0)
                {
                    var show = Object.FindAnyObjectByType<ScoringShow>();
                    if (_lastPlayback != show) { _lastPlayback = show; _clickCount = 0; }
                    var blocker = Object.FindAnyObjectByType<OperationScreenView>().transform.Find("ScoringBlocker");
                    if (blocker != null && (_clickCount < (_playbackMode == 1 ? 1 : 2) || (_playbackMode == 2 && _clickCount == 2)))
                    {
                        ExecuteEvents.Execute(blocker.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
                        _clickCount++;
                        _nextInput = EditorApplication.timeSinceStartup + 0.5;
                    }
                }
                return;
            }
            try
            {
                if (_step >= _plan.actions.Length)
                {
                    _running = false;
                    EditorApplication.update -= AdvancePlan;
                    Snapshot("natural-plan-completed");
                    if (TheCallApp.Interface.SendQuery(new RunPhaseQuery()) != RunPhase.Victory)
                        throw new InvalidOperationException("Natural player replay did not end in Victory.");
                    CaptureScreenshot("07-victory.png");
                    File.WriteAllText(Path.Combine(EvidenceRoot, "final-state.txt"), State());
                    return;
                }
                var input = _plan.actions[_step];
                // Completion refresh is deferred by one frame. Wait for the actual
                // player screen, not only the destruction of the playback host.
                if ((input.Action == "buy" || input.Action == "leave" || input.Action == "refresh") && !Object.FindAnyObjectByType<ShopScreenView>(FindObjectsInactive.Include).gameObject.activeInHierarchy) return;
                if (input.Action == "begin" && !Object.FindAnyObjectByType<LevelStartScreenView>(FindObjectsInactive.Include).gameObject.activeInHierarchy) return;
                if (input.Action == "buy" && !ExposeShopCategory(input.Value)) return;
                if (input.Action == "extraction" || input.Action == "breeding")
                {
                    if (!ExposeCageSource(input.Value)) return;
                    Drag(input.Value, input.Action, input.Index);
                }
                else if (input.Action == "cage")
                {
                    var operation = Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
                    Drag(input.Value, "cage", 0);
                }
                else Click(input.Action, input.Value);
                _step++;
                _nextInput = EditorApplication.timeSinceStartup + 0.65;
                if (input.Action == "settle") CaptureScreenshot("settle-" + _step + ".png");
                else if (input.Action == "leave") CaptureScreenshot("day-end-" + _step + ".png");
                else if (input.Action == "buy") CaptureScreenshot("shop-" + _step + ".png");
            }
            catch (Exception error)
            {
                _running = false; _failure = error.ToString();
                EditorApplication.update -= AdvancePlan;
                Directory.CreateDirectory(EvidenceRoot);
                File.WriteAllText(Path.Combine(EvidenceRoot, "player-replay-failure.txt"), _failure);
                Debug.LogError(_failure);
            }
        }

        static TheCallPresentation Presentation() => Object.FindAnyObjectByType<TheCallPresentation>() ?? throw new InvalidOperationException("Missing presentation");

        static Vector2 CenterOf(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

        static bool ExposeShopCategory(string value)
        {
            var shop = Object.FindAnyObjectByType<ShopScreenView>();
            if (shop.facility == null) return true;
            var card = shop.cards.FirstOrDefault(c => c.monsterId == value || c.toolName == value);
            if (card == null || card.gameObject.activeInHierarchy) return true;
            if (string.IsNullOrEmpty(card.toolName)) shop.facility.ShowMonsters(); else shop.facility.ShowTools();
            _nextInput = EditorApplication.timeSinceStartup + 0.25;
            return false;
        }

        static bool ExposeCageSource(string monsterId)
        {
            var operation = Object.FindAnyObjectByType<OperationScreenView>();
            if (operation == null || !(operation.lab?.cageScroll is LabCageLift lift)) return true;
            var slot = operation.cageSlots.FirstOrDefault(s => s.gameObject.activeInHierarchy && s.monsterId == monsterId);
            if (slot == null) return true;
            if (lift.IsMoving) { _nextInput = EditorApplication.timeSinceStartup + 0.25; return false; }
            var point = CenterOf((RectTransform)slot.transform);
            if (RectTransformUtility.RectangleContainsScreenPoint(lift.viewport, point)) return true;
            var center = CenterOf(lift.viewport);
            var button = point.y > center.y ? lift.previousButton : lift.nextButton;
            if (button == null || !button.interactable) throw new InvalidOperationException("Cannot expose cage source " + monsterId);
            button.onClick.Invoke();
            _nextInput = EditorApplication.timeSinceStartup + 0.25;
            return false;
        }
    }
}
