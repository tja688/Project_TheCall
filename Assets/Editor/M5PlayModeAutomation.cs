using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using QFramework;
using TheCall;
using TheCall.Scoring;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    public static class M5PlayModeAutomation
    {
        public sealed class SeededDraw : IDraw
        {
            readonly System.Random _random;

            public SeededDraw(int seed)
            {
                _random = new System.Random(seed);
            }

            public T Choose<T>(IReadOnlyList<T> options)
            {
                if (options == null || options.Count == 0) return default;
                return options[_random.Next(options.Count)];
            }

            public bool Chance(int percent)
            {
                return _random.Next(100) < percent;
            }
        }

        [MenuItem("TheCall/Testing/Run M5 Play Mode Automation", false, 300)]
        public static void RunAllMenu()
        {
            var log = RunAll();
            Debug.Log(log);
        }

        public static string RunAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine("           M5 Play Mode Full-Loop Verification & Screenshot Suite               ");
            sb.AppendLine("================================================================================");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            var screenshotDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screenshots/M5"));
            if (!Directory.Exists(screenshotDir))
            {
                Directory.CreateDirectory(screenshotDir);
            }

            var bookPath = Path.Combine(Application.streamingAssetsPath, "call-book.json");
            if (!File.Exists(bookPath))
            {
                throw new FileNotFoundException("StreamingAssets/call-book.json not found", bookPath);
            }

            var presentation = Object.FindAnyObjectByType<TheCallPresentation>();
            if (presentation == null)
            {
                throw new InvalidOperationException("TheCallPresentation not found in active scene.");
            }

            var screenshotsTaken = new List<string>();

            // -------------------------------------------------------------------------
            // SCENARIO 1: Full 7-Day Victory Run
            // -------------------------------------------------------------------------
            sb.AppendLine("\n--- [Scenario 1] Full 7-Day Victory Run (Deterministic Seed) ---");
            ResetAndInitSession(bookPath, 12345, presentation);

            var app = TheCallApp.Interface;
            var runModel = app.GetModel<RunModel>();

            var metricsHeader = string.Format("{0,-6} | {1,-6} | {2,-6} | {3,-8} | {4,-6} | {5,-6} | {6,-6} | {7,-6}",
                "Day", "Due", "Excess", "Achieved", "Gold", "Tech", "Tools", "Cage");
            sb.AppendLine(metricsHeader);
            sb.AppendLine(new string('-', metricsHeader.Length));

            for (int day = 1; day <= 7; day++)
            {
                if (day == 1)
                {
                    var phase = app.SendQuery(new RunPhaseQuery());
                    if (phase != RunPhase.Opening)
                        throw new InvalidOperationException($"Expected Opening phase on Day 1, got {phase}");

                    // Screenshot 1: Opening Screen
                    var shot1 = CaptureScreenshot("01_Opening_Screen.png");
                    screenshotsTaken.Add(shot1);

                    var candidates = app.SendQuery(new OpeningCandidatesQuery());
                    if (candidates.Count == 0)
                        throw new InvalidOperationException("No opening candidates found.");

                    app.SendCommand(new KeepOpeningMonsterCommand(candidates[0].Id));
                    presentation.Refresh();

                    var curCage = app.SendQuery(new MonsterCageQuery());
                    // Place 3 monsters
                    for (int i = 0; i < 3 && i < curCage.Count; i++)
                    {
                        app.SendCommand(new PlaceMonsterCommand(curCage[i].Id, OperationArea.Extraction, i));
                    }
                    presentation.Refresh();

                    // Screenshot 2: Operation Screen with Monster Details Hover
                    var hover = Object.FindAnyObjectByType<MonsterHover>(FindObjectsInactive.Include);
                    if (hover != null)
                    {
                        hover.enabled = false;
                        var details = app.SendQuery(new MonsterDetailsQuery(curCage[0].Id));
                        hover.ShowManual(details);
                        presentation.Refresh();
                    }

                    var shot2 = CaptureScreenshot("02_Operation_Monster_Details.png");
                    screenshotsTaken.Add(shot2);

                    if (hover != null)
                    {
                        hover.HideManual();
                        hover.enabled = true;
                        presentation.Refresh();
                    }
                }
                else
                {
                    // Day >= 2 begins in LevelStart
                    var phase = app.SendQuery(new RunPhaseQuery());
                    if (phase != RunPhase.LevelStart)
                        throw new InvalidOperationException($"Expected LevelStart phase on Day {day}, got {phase}");

                    app.SendCommand(new BeginLevelCommand());
                    presentation.Refresh();

                    var phaseAfterBegin = app.SendQuery(new RunPhaseQuery());
                    if (phaseAfterBegin != RunPhase.Operation)
                        throw new InvalidOperationException($"Expected Operation phase on Day {day}, got {phaseAfterBegin}");

                    // On Day 2: Test Tech expansion and Gene reservation
                    if (day == 2)
                    {
                        // Ensure 2 tech points for unlocking "基因仓储" and "基因实验"
                        while (runModel.TechPoints < 2)
                        {
                            runModel.AddTechPoint();
                        }

                        app.SendCommand(new UnlockTechCommand("基因仓储"));
                        app.SendCommand(new UnlockTechCommand("基因实验"));

                        // Discard one extra monster to generate a gene chip if cage has enough
                        var cageForGene = app.SendQuery(new MonsterCageQuery());
                        if (cageForGene.Count > 3)
                        {
                            app.SendCommand(new DiscardMonsterCommand(cageForGene[cageForGene.Count - 1].Id));
                        }

                        // Reserve gene slot 0 into breeding slot 0
                        app.SendCommand(new PlaceBreedingSkillCommand(0, 0));
                        presentation.Refresh();

                        // Screenshot 3: Expansion & Gene Reservation
                        var shot3 = CaptureScreenshot("03_Expansion_And_Gene_Reservation.png");
                        screenshotsTaken.Add(shot3);
                    }

                    // Place extraction slots
                    var curCage = app.SendQuery(new MonsterCageQuery());
                    var extraction = app.SendQuery(new ExtractionSlotsQuery());
                    for (int i = 0; i < 3 && i < curCage.Count; i++)
                    {
                        if (string.IsNullOrEmpty(extraction[i].MonsterId))
                        {
                            app.SendCommand(new PlaceMonsterCommand(curCage[i].Id, OperationArea.Extraction, i));
                        }
                    }
                    presentation.Refresh();
                }

                // Ensure all monsters have an active energy-producing skill and scaled modifiers to exceed Due and Excess
                foreach (var monster in runModel.LivingMonsters)
                {
                    if (!monster.HasSkill("能量体"))
                    {
                        runModel.TryGainSkill(monster.Id, "能量体");
                    }
                    monster.AddModifier(20 + day * 15);
                }

                var target = app.SendQuery(new LevelTargetQuery());
                int due = target.EnergyDue;
                int excess = target.ExcessEnergy;

                // Settle
                presentation.TriggerSettle();
                FastForwardScoringShow(presentation);

                var prodLog = app.SendQuery(new ProductionLogQuery());
                int achieved = prodLog.Count > 0 ? prodLog[prodLog.Count - 1].Produced : 0;
                var ledger = app.SendQuery(new RunLedgerQuery());
                var cageNow = app.SendQuery(new MonsterCageQuery());

                sb.AppendLine(string.Format("{0,-6} | {1,-6} | {2,-6} | {3,-8} | {4,-6} | {5,-6} | {6,-6} | {7,-6}",
                    day, due, excess, achieved, ledger.Gold, ledger.TechPoints, ledger.Tools.Count, cageNow.Count));

                if (achieved < due)
                {
                    throw new InvalidOperationException($"Day {day} failed to achieve Due energy {due}; produced {achieved}");
                }
                if (achieved < excess)
                {
                    throw new InvalidOperationException($"Day {day} failed to achieve Excess energy {excess}; produced {achieved}");
                }

                if (day == 1)
                {
                    // Screenshot 4: Settlement Breakdown
                    presentation.ToggleLog();
                    presentation.Refresh();
                    var shot4 = CaptureScreenshot("04_Settlement_Breakdown.png");
                    screenshotsTaken.Add(shot4);
                    presentation.ToggleLog();
                    presentation.Refresh();

                    // Screenshot 5: Shop Tools & Cards
                    var shot5 = CaptureScreenshot("05_Shop_Tools_Cards.png");
                    screenshotsTaken.Add(shot5);
                }

                // In Shop: purchase if available
                var shelf = app.SendQuery(new ShelfQuery());
                if (shelf.Tools.Count > 0 && ledger.Gold >= shelf.Tools[0].Price)
                {
                    app.SendCommand(new BuyToolCommand(shelf.Tools[0].Name));
                }

                // Advance to next day / finish
                app.SendCommand(new LeaveShopCommand());
                presentation.Refresh();
            }

            // Verify Victory
            var finalPhase = app.SendQuery(new RunPhaseQuery());
            if (finalPhase != RunPhase.Victory)
            {
                throw new InvalidOperationException($"Expected Victory phase after Day 7, got {finalPhase}");
            }

            // Screenshot 7: Victory Screen
            var shot7 = CaptureScreenshot("07_Victory_Screen.png");
            screenshotsTaken.Add(shot7);
            sb.AppendLine("=> Scenario 1 PASS: Full 7-Day Victory Run completed successfully with RunPhase.Victory!");

            // -------------------------------------------------------------------------
            // SCENARIO 2: Overtime Pass
            // -------------------------------------------------------------------------
            sb.AppendLine("\n--- [Scenario 2] Overtime Pass ---");
            ResetAndInitSession(bookPath, 54321, presentation);

            app = TheCallApp.Interface;
            runModel = app.GetModel<RunModel>();

            var s2Candidates = app.SendQuery(new OpeningCandidatesQuery());
            app.SendCommand(new KeepOpeningMonsterCommand(s2Candidates[0].Id));
            presentation.Refresh();

            // Normal worktime: place 0 monsters -> energy is 0 < Due (50)
            presentation.TriggerSettle();
            FastForwardScoringShow(presentation);

            var s2Target = app.SendQuery(new LevelTargetQuery());
            var s2Shortfall = app.SendQuery(new LevelShortfallQuery());
            if (!s2Target.InOvertime)
                throw new InvalidOperationException("Expected InOvertime == true after normal work shortfall.");
            if (s2Shortfall <= 0)
                throw new InvalidOperationException($"Expected shortfall > 0, got {s2Shortfall}.");

            sb.AppendLine($"[Overtime Entered] Shortfall={s2Shortfall}, InOvertime={s2Target.InOvertime}");

            // Screenshot 6: Overtime State
            var shot6 = CaptureScreenshot("06_Overtime_State.png");
            screenshotsTaken.Add(shot6);

            // Overtime worktime: place monsters and boost to pass
            var s2Cage = app.SendQuery(new MonsterCageQuery());
            foreach (var m in runModel.LivingMonsters)
            {
                if (!m.HasSkill("能量体"))
                {
                    runModel.TryGainSkill(m.Id, "能量体");
                }
                m.AddModifier(40);
            }
            for (int i = 0; i < 3 && i < s2Cage.Count; i++)
            {
                app.SendCommand(new PlaceMonsterCommand(s2Cage[i].Id, OperationArea.Extraction, i));
            }
            presentation.Refresh();

            // Settle in overtime
            presentation.TriggerSettle();
            FastForwardScoringShow(presentation);

            var s2TargetAfter = app.SendQuery(new LevelTargetQuery());
            var s2ShortfallAfter = app.SendQuery(new LevelShortfallQuery());
            var s2PhaseAfter = app.SendQuery(new RunPhaseQuery());

            if (s2TargetAfter.InOvertime)
                throw new InvalidOperationException("Expected InOvertime == false after passing overtime work.");
            if (s2ShortfallAfter != 0)
                throw new InvalidOperationException($"Expected shortfall == 0, got {s2ShortfallAfter}.");
            if (s2PhaseAfter != RunPhase.Shop)
                throw new InvalidOperationException($"Expected Shop phase after clearing overtime, got {s2PhaseAfter}.");

            sb.AppendLine("=> Scenario 2 PASS: Overtime passed and advanced to Shop!");

            // -------------------------------------------------------------------------
            // SCENARIO 3: Two-Round Defeat
            // -------------------------------------------------------------------------
            sb.AppendLine("\n--- [Scenario 3] Two-Round Defeat ---");
            ResetAndInitSession(bookPath, 99999, presentation);

            app = TheCallApp.Interface;
            var s3Candidates = app.SendQuery(new OpeningCandidatesQuery());
            app.SendCommand(new KeepOpeningMonsterCommand(s3Candidates[0].Id));
            presentation.Refresh();

            // Round 1: normal worktime fails
            presentation.TriggerSettle();
            FastForwardScoringShow(presentation);

            var s3Target1 = app.SendQuery(new LevelTargetQuery());
            if (!s3Target1.InOvertime)
                throw new InvalidOperationException("Round 1 failed to enter overtime.");

            // Round 2: overtime fails again
            presentation.TriggerSettle();
            FastForwardScoringShow(presentation);

            var s3Phase = app.SendQuery(new RunPhaseQuery());
            if (s3Phase != RunPhase.Failed)
                throw new InvalidOperationException($"Expected Failed phase after two failed rounds, got {s3Phase}.");

            // Screenshot 8: Defeat Screen
            var shot8 = CaptureScreenshot("08_Defeat_Screen.png");
            screenshotsTaken.Add(shot8);
            sb.AppendLine("=> Scenario 3 PASS: Two-round failure correctly triggered RunPhase.Failed!");

            // -------------------------------------------------------------------------
            // SCENARIO 4: New Run Reset
            // -------------------------------------------------------------------------
            sb.AppendLine("\n--- [Scenario 4] New Run Reset ---");
            ResetAndInitSession(bookPath, 77777, presentation);

            app = TheCallApp.Interface;
            var s4Phase = app.SendQuery(new RunPhaseQuery());
            var s4Ledger = app.SendQuery(new RunLedgerQuery());
            var s4Candidates = app.SendQuery(new OpeningCandidatesQuery());

            if (s4Phase != RunPhase.Opening)
                throw new InvalidOperationException($"Expected Opening phase after reset, got {s4Phase}.");
            if (s4Ledger.Gold != 0)
                throw new InvalidOperationException($"Expected Gold == 0 after reset, got {s4Ledger.Gold}.");
            if (s4Ledger.TechPoints != 0)
                throw new InvalidOperationException($"Expected TechPoints == 0 after reset, got {s4Ledger.TechPoints}.");
            if (s4Ledger.Tools.Count != 0)
                throw new InvalidOperationException($"Expected 0 tools after reset, got {s4Ledger.Tools.Count}.");
            if (s4Ledger.UnlockedTech.Count != 0)
                throw new InvalidOperationException($"Expected 0 unlocked techs after reset, got {s4Ledger.UnlockedTech.Count}.");
            if (s4Candidates.Count == 0)
                throw new InvalidOperationException("Expected candidate monsters after reset.");

            // Screenshot 9: New Run Reset
            var shot9 = CaptureScreenshot("09_New_Run_Reset.png");
            screenshotsTaken.Add(shot9);
            sb.AppendLine("=> Scenario 4 PASS: New run successfully reset to clean initial state!");

            // -------------------------------------------------------------------------
            // Artifacts Summary
            // -------------------------------------------------------------------------
            sb.AppendLine("\n================================================================================");
            sb.AppendLine("                             Captured Screenshots                               ");
            sb.AppendLine("================================================================================");
            foreach (var path in screenshotsTaken)
            {
                var fileInfo = new FileInfo(path);
                sb.AppendLine(string.Format("{0,-35} | {1,10} bytes | {2}",
                    fileInfo.Name, fileInfo.Length, fileInfo.FullName));
            }

            sb.AppendLine("\n================================================================================");
            sb.AppendLine("             ALL 4 SCENARIOS & 9 SCREENSHOTS VERIFIED SUCCESSFULLY!             ");
            sb.AppendLine("================================================================================");

            return sb.ToString();
        }

        static void ResetAndInitSession(string bookPath, int seed, TheCallPresentation presentation)
        {
            TheCallApp.Reset();
            ContentGate.Use(ContentBook.Parse(File.ReadAllText(bookPath)));
            TheCallApp.OnRegisterPatch = a => a.RegisterUtility<IDraw>(new SeededDraw(seed));
            presentation.StartSession();
            presentation.Refresh();
        }

        static void FastForwardScoringShow(TheCallPresentation presentation)
        {
            var scoringShow = Object.FindAnyObjectByType<ScoringShow>();
            if (scoringShow != null)
            {
                scoringShow.StopAllCoroutines();
                Object.DestroyImmediate(scoringShow.gameObject);
            }
            if (presentation != null)
            {
                presentation.Refresh();
            }
        }

        public static string CaptureScreenshot(string filename)
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Screenshots/M5"));
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var path = Path.GetFullPath(Path.Combine(dir, filename));

            var cam = Camera.main;
            if (cam == null)
            {
                cam = Object.FindAnyObjectByType<Camera>();
            }

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas != null && canvas.worldCamera == null && cam != null)
            {
                canvas.worldCamera = cam;
            }

            Canvas.ForceUpdateCanvases();

            var rt = new RenderTexture(1920, 1080, 24);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            Object.DestroyImmediate(rt);

            var bytes = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            File.WriteAllBytes(path, bytes);
            return path;
        }
    }
}
