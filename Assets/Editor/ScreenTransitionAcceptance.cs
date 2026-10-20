using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QFramework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    /// <summary>真实按钮、逐帧区块采样和截图。商店使用局部资源 fixture，不冒充自然通关。</summary>
    public static class ScreenTransitionAcceptance
    {
        static TheCallPresentation P => Object.FindAnyObjectByType<TheCallPresentation>(FindObjectsInactive.Include);
        static T View<T>() where T : Component => Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        static readonly List<object> Checks = new List<object>();
        static readonly List<object> Samples = new List<object>();
        static string _directory;
        static string _failure;
        static int _step;
        static float _since;
        static int _frame;
        static int _shopCategoryFrame;
        static int _pictures;
        static float _nextPicture;
        static string _recording;
        static string _state;
        static readonly HashSet<int> MotionFrames = new HashSet<int>();
        static bool _running;
        static float _originalTimeScale;
        static List<RestPose> _operationPoses;
        static List<RestPose> _shopPoses;
        sealed class RestPose
        {
            public RectTransform target;
            public Vector3 position;
            public Vector3 scale;
        }
        public static string Status => _failure ?? (_running ? "running " + _step : "completed " + Checks.Count);
        static void Need(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
        static void Pass(string message) => Checks.Add(new { result = "PASS", check = message });
        static void Next() { _step++; _since = Time.unscaledTime; }

        public static string Saved()
        {
            Need(!Application.isPlaying, "Saved verification needs Edit Mode");
            var assemblies = Object.FindObjectsByType<ScreenAssembly>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Need(assemblies.Length == 6, "Expected six configured screens");
            foreach (var assembly in assemblies)
            {
                Need(assembly.parts.All(p => p.target != null && p.target.IsChildOf(assembly.transform)), "Lost or foreign part reference");
                Need(assembly.parts.Select(p => p.target).Distinct().Count() == assembly.parts.Length, "Duplicated animation target");
                Need(!assembly.Prepared && assembly.GetComponent<CanvasGroup>().alpha == 1, "Saved transient pose");
                foreach (var surface in assembly.surfaces)
                {
                    Need(surface != null, "Lost surface");
                    var tiles = surface.regions;
                    Need(Mathf.Abs(tiles.Sum(r => r.area.width * r.area.height) - 1920 * 1080) < 1, "Surface coverage differs from source");
                    for (var i = 0; i < tiles.Length; i++)
                    {
                        Need(tiles[i].area.xMin >= 0 && tiles[i].area.yMin >= 0 && tiles[i].area.xMax <= 1920 &&
                            tiles[i].area.yMax <= 1080 && tiles[i].area.width > 0 && tiles[i].area.height > 0, "Crop outside source");
                        for (var j = i+1; j < tiles.Length; j++)
                            Need(!tiles[i].area.Overlaps(tiles[j].area), "Surface crops overlap");
                    }
                }
            }
            return "PASS six saved screens; references, unique targets, source coverage, no crop overlap or transient poses";
        }

        public static string Begin(string directory)
        {
            Need(Application.isPlaying && !_running, "Enter Play Mode; do not run two acceptance sessions");
            _directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(_directory);
            Checks.Clear(); Samples.Clear();
            _failure = null; _running = true; _step = 0; _since = Time.unscaledTime;
            _shopCategoryFrame = -1;
            _originalTimeScale = Time.timeScale;
            TheCallApp.Reset();
            ContentGate.Use(ContentBook.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "call-book.json"))));
            TheCallApp.OnRegisterPatch = app => app.RegisterUtility<IDraw>(new M5PlayModeAutomation.SeededDraw(90));
            P.StartSession();
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            return Status;
        }

        static void StartRecording(string name)
        {
            Need(P.Transitioning, "Expected animated screen change: " + name);
            _recording = name; _pictures = 0; _nextPicture = Time.unscaledTime;
            MotionFrames.Clear(); _state = M5PlayModeAutomation.State();
        }

        static void Observe()
        {
            if (_recording == null) return;
            Need(M5PlayModeAutomation.State() == _state, "Screen animation changed authoritative rule state");
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            var active = Object.FindObjectsByType<ScreenAssembly>(FindObjectsSortMode.None).FirstOrDefault(a => a.Prepared);
            if (active != null)
            {
                var values = active.parts.Where(p => p.target != null && p.target.gameObject.activeInHierarchy)
                    .Select(p => new { target = p.target.name, x = p.target.anchoredPosition.x, y = p.target.anchoredPosition.y,
                        alpha = p.target.GetComponent<CanvasGroup>().alpha }).ToArray();
                Samples.Add(new { transition = _recording, frame = Time.frameCount, time = Time.unscaledTime, screen = active.name,
                    alpha = active.GetComponent<CanvasGroup>().alpha, parts = active.LivePartCount, values });
                MotionFrames.Add(Time.frameCount);
            }
            if (Time.unscaledTime >= _nextPicture && _pictures < 14)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(_directory, _recording + "-" + _pictures.ToString("00") + ".png"));
                _pictures++; _nextPicture = Time.unscaledTime + 0.085f;
            }
            if (!P.Transitioning)
            {
                Need(MotionFrames.Count >= 5, "Too few advancing animation frames: " + _recording);
                Idle();
                Pass(_recording + ": " + MotionFrames.Count + " advancing frames, rule state stable, final poses and input restored");
                _recording = null;
            }
        }

        static void Idle()
        {
            Need(!P.Transitioning, "Transition did not finish");
            var assemblies = Object.FindObjectsByType<ScreenAssembly>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Need(assemblies.Count(a => a.gameObject.activeInHierarchy) == 1, "More than one player screen remains active");
            foreach (var a in assemblies)
            {
                Need(!a.Prepared && a.GetComponent<CanvasGroup>().alpha == 1, "Unrestored assembly " + a.name);
                Need(a.parts.All(p => p.target.GetComponent<CanvasGroup>().alpha == 1), "Part remained faded " + a.name);
            }
            var shield = View<OpeningScreenView>().transform.parent.Find("ScreenTransitionShield");
            Need(shield == null || !shield.gameObject.activeSelf, "Input shield still active");
            Need(!View<FacilityPanorama>().Locked, "Panorama left locked");
        }

        static void Click(string name) => HandbookAcceptance.ClickVisible(name);
        static List<RestPose> Remember(Component screen) => screen.GetComponent<ScreenAssembly>().parts
            .Where(p => p.target.gameObject.activeInHierarchy && (p.offset != Vector2.zero || p.scale != 1))
            .Select(p => new RestPose { target = p.target, position = p.target.localPosition, scale = p.target.localScale }).ToList();
        static void CheckPoses(List<RestPose> poses)
        {
            foreach (var pose in poses)
                Need(Vector3.Distance(pose.target.localPosition, pose.position) < 0.001f &&
                    Vector3.Distance(pose.target.localScale, pose.scale) < 0.001f, "Repeated transition drifted: " + pose.target.name);
        }
        static void OpenResearch(string recording)
        {
            Click("ResearchButton"); StartRecording(recording); Next();
        }

        static void Tick()
        {
            if (!_running) return;
            try
            {
                Need(Application.isPlaying, "Play Mode ended during acceptance");
                Observe();
                var elapsed = Time.unscaledTime - _since;
                Need(elapsed < 60, "Acceptance step timed out: " + _step);
                switch (_step)
                {
                    case 0:
                        if (elapsed < 0.3f) return;
                        Click(View<OpeningScreenView>().candidates[0].button.name);
                        StartRecording("01-opening-production");
                        var before = M5PlayModeAutomation.State();
                        P.Perform(PresentationAction.OpenResearch); P.Perform(PresentationAction.Settle);
                        P.Refresh(); P.Refresh();
                        Need(before == M5PlayModeAutomation.State(), "Input during transition altered state");
                        Pass("Repeated Refresh and blocked gameplay actions do not restart or mutate transition");
                        Next(); break;
                    case 1:
                        if (P.Transitioning) return;
                        Need(View<OperationScreenView>().gameObject.activeInHierarchy, "Opening did not reach production");
                        _operationPoses = Remember(View<OperationScreenView>());
                        OpenResearch("02-research-open"); break;
                    case 2:
                        if (P.Transitioning) return;
                        Click(View<ResearchScreenView>().nodes[2].button.name);
                        Need(!P.Transitioning, "Selecting a tech replayed screen entrance");
                        Click("BackButton"); StartRecording("03-research-close"); Next(); break;
                    case 3:
                        if (P.Transitioning) return;
                        CheckPoses(_operationPoses);
                        Time.timeScale = 0;
                        OpenResearch("04-research-unscaled"); break;
                    case 4:
                        if (P.Transitioning) return;
                        Need(Time.timeScale == 0, "Pause fixture changed");
                        Click("BackButton"); Next(); break;
                    case 5:
                        if (P.Transitioning) return;
                        Time.timeScale = _originalTimeScale;
                        CheckPoses(_operationPoses);
                        Idle(); Pass("Research opens/closes at timeScale zero");
                        Click("FacilityDormTab"); Next(); break;
                    case 6:
                        Need(!P.Transitioning, "Dorm pan incorrectly started a scene transition");
                        if (View<FacilityPanorama>().Moving) return;
                        Need(!View<FacilityPanorama>().Production, "Dorm did not dock");
                        Click("FacilityProductionTab"); Next(); break;
                    case 7:
                        Need(!P.Transitioning, "Production pan incorrectly started a scene transition");
                        if (View<FacilityPanorama>().Moving) return;
                        Pass("Both facility areas keep their continuous panorama and have no assembly transition");
                        FacilityArtAcceptance.PrepareShop(Path.Combine(_directory, "fixtures")); Next(); break;
                    case 8:
                        if (TheCall.Scoring.ScoringShow.IsPlaying)
                        {
                            Need(!P.Transitioning, "Shop entered before settlement completed");
                            View<TheCall.Scoring.ScoringShow>().Advance(); return;
                        }
                        if (!P.Transitioning) return;
                        StartRecording("05-settlement-shop"); Next(); break;
                    case 9:
                        if (P.Transitioning) return;
                        Need(View<ShopScreenView>().gameObject.activeInHierarchy, "Shop did not appear");
                        if (_shopCategoryFrame < 0)
                        {
                            Click("FacilityToolCategory");
                            _shopCategoryFrame = Time.frameCount;
                            return;
                        }
                        if (Time.frameCount <= _shopCategoryFrame) return;
                        Canvas.ForceUpdateCanvases();
                        Need(!P.Transitioning, "Category selection replayed entrance");
                        FacilityArtAcceptance.BuyTool();
                        Click("RefreshShelf");
                        Need(!P.Transitioning, "Purchase/refresh replayed entrance");
                        _shopPoses = Remember(View<ShopScreenView>());
                        Pass("Shop category, purchase and paid refresh remain usable without scene animation");
                        OpenResearch("06-shop-research"); break;
                    case 10:
                        if (P.Transitioning) return;
                        Click("BackButton"); StartRecording("07-research-shop"); Next(); break;
                    case 11:
                        if (P.Transitioning) return;
                        CheckPoses(_shopPoses);
                        Click("LeaveButton"); StartRecording("08-shop-next-day"); Next(); break;
                    case 12:
                        if (P.Transitioning) return;
                        if (View<LevelStartScreenView>().gameObject.activeInHierarchy)
                        {
                            Click(View<LevelStartScreenView>().beginButton.name);
                            StartRecording("09-level-production");
                            return;
                        }
                        Click("ResearchButton"); Next(); break;
                    case 13:
                        if (elapsed < 0.05f) return;
                        P.Perform(PresentationAction.ReturnToTitle);
                        Idle();
                        Need(View<OpeningScreenView>().gameObject.activeInHierarchy, "Reset during departure did not reopen title");
                        Pass("Return to title during departure restores poses and retires old callbacks");
                        Next(); break;
                    case 14:
                        if (elapsed < 0.05f) return;
                        Click(View<OpeningScreenView>().candidates[0].button.name); Next(); break;
                    case 15:
                        if (!View<OperationScreenView>().gameObject.activeInHierarchy || !P.Transitioning) return;
                        P.enabled = false;
                        Idle();
                        P.enabled = true;
                        Need(!P.Transitioning, "Re-enable restarted an already visible screen");
                        Next(); break;
                    case 16:
                        if (elapsed < 0.05f) return;
                        Click("ResearchButton"); Next(); break;
                    case 17:
                        if (!View<ResearchScreenView>().gameObject.activeInHierarchy || !P.Transitioning) return;
                        P.Perform(PresentationAction.ReturnToTitle);
                        Idle(); Pass("Disable/re-enable and reset during arrival release all chunks and input");
                        Complete(); break;
                }
            }
            catch (Exception e)
            {
                _failure = "FAILED step " + _step + ": " + e;
                Complete();
            }
        }

        static void Complete()
        {
            _running = false;
            Time.timeScale = _originalTimeScale;
            EditorApplication.update -= Tick;
            File.WriteAllText(Path.Combine(_directory, "acceptance.json"), Newtonsoft.Json.JsonConvert.SerializeObject(
                new { status = _failure ?? "PASS", kind = "TRANSITION_UI_ACCEPTANCE_WITH_SHOP_FIXTURE", checks = Checks, samples = Samples },
                Newtonsoft.Json.Formatting.Indented));
        }
    }
}
