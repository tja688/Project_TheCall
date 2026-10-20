using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QFramework;
using TheCall.Scoring;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    /// <summary>Injected starting resources followed by real drag, settlement, breeding and daily-mood flows.</summary>
    public static class MonsterCultivationAcceptance
    {
        const string DefaultEvidence = "docs/audit/2026-10-20/templates/cultivation";
        [Serializable] sealed class Reading
        {
            public string step, monsterId, phase, personality, mood, appearance, visual, assembly, note;
            public int day, frame, parts, workDays, restDays;
            public string[] parents, skills, skillSources, actualParts;
        }
        [Serializable] sealed class Report
        {
            public string kind = "LOCAL_CULTIVATION_UI_FIXTURE_NOT_NATURAL_RUN";
            public string scenario, unity, injectedStartingResources, failure;
            public bool completed;
            public List<Reading> steps = new List<Reading>();
        }

        static IArchitecture App => TheCallApp.Interface;
        static RunModel Run => App.GetModel<RunModel>();
        static TheCallPresentation Presentation => Object.FindAnyObjectByType<TheCallPresentation>();
        static OperationScreenView Operation => Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
        static ShopScreenView Shop => Object.FindAnyObjectByType<ShopScreenView>(FindObjectsInactive.Include);
        static string _evidence = DefaultEvidence, _stage = "idle";
        static Report _active, _install, _breeding, _mood;
        static string _installId, _installedGeneId, _installAssembly, _installAppearance;
        static int _installSkills, _installGenes;
        static string[] _parents;
        static MonsterAppearance[] _parentAppearances;
        static string _workerId, _childId, _moodAppearance, _moodInitialVisual;
        static MonsterEyeFamily _moodEyeFamily;
        static int _workedChecks, _restedChecks, _expectedWork, _expectedRest;
        static bool _pendingMoodWork;

        public static string Stage => _stage;
        // Playback completion refreshes the screen one frame later. Wait for both conditions.
        public static bool ReadyForCheck => Application.isPlaying && !ScoringShow.IsPlaying && Presentation != null && !Presentation.Transitioning && Shop != null && Shop.gameObject.activeInHierarchy && Shop.leaveButton != null && Shop.leaveButton.interactable;
        public static bool ReadyForDayStart
        {
            get { var screen=Object.FindAnyObjectByType<LevelStartScreenView>(FindObjectsInactive.Include); return Application.isPlaying&&Presentation!=null&&!Presentation.Transitioning&&screen!=null&&screen.gameObject.activeInHierarchy&&screen.beginButton!=null&&screen.beginButton.interactable; }
        }
        public static bool ReadyForOperation => Application.isPlaying && Presentation!=null&&!Presentation.Transitioning&&Operation!=null&&Operation.gameObject.activeInHierarchy&&App.SendQuery(new RunPhaseQuery())==RunPhase.Operation;

        public static string InstallBegin(string evidenceDirectory = DefaultEvidence)
        {
            BeginReport("install", evidenceDirectory,
                "PresentationFixtureAcceptance install fixture: one monster with 能量体, 迷恋死亡 personality, one 奇异香 gene. No skill is installed by the fixture.");
            _install = _active;
            PresentationFixtureAcceptance.Begin("install", 1, Path.Combine(_evidence, "install-fixture"));
            FocusProduction();
            var monster = Run.Cage.Single();
            _installId = monster.Id; _installSkills = monster.Skills.Count; _installGenes = Run.GeneSlots.Count;
            _installedGeneId = Run.GeneSlots.Single().Id;
            _installAppearance = Genes(monster.Appearance);
            var portrait = CagePortrait(_installId);
            CheckPortrait(_installId, portrait);
            _installAssembly = portrait.AssemblySignature;
            Read(_install, "before-gene-install", monster, portrait);
            _stage = "install-ready"; Save(_install);
            return "PASS install-ready: actual cage slot and 奇异香 gene prepared; capture before calling InstallGene";
        }

        public static string InstallGene()
        {
            _active = _install;
            Need(_install != null && _stage == "install-ready", "Call InstallBegin first");
            Need(PresentationFixtureAcceptance.DragGene(0, "monster", 0, true).Contains("PASS"), "Real gene drag did not pass");
            var monster = Run.Find(_installId);
            Need(monster != null && monster.Skills.Count == _installSkills + 1 && Run.GeneSlots.Count == _installGenes - 1,
                "Installation did not add one skill and consume one gene");
            var added = monster.Skills.Single(skill => skill.Name == "奇异香");
            Need(added.SourceKind == SkillSourceKind.Gene && added.SourceId == _installedGeneId, "Installed skill lost the consumed gene provenance");
            Need(Genes(monster.Appearance) == _installAppearance, "Skill installation changed inherited structure genes");
            var portrait = CagePortrait(_installId);
            CheckPortrait(_installId, portrait);
            Need(portrait.AssemblySignature == _installAssembly, "Skill installation changed the fixed template, colors or accessories");
            Read(_install, "after-real-gene-drag", monster, portrait, "Consumed gene " + _installedGeneId + "; birth appearance unchanged");
            _install.completed = true; _stage = "install-completed"; Save(_install);
            return "PASS real gene drag consumes 奇异香, preserves birth genes, preserves template, colors, eye positions and accessories";
        }

        public static string BreedingBegin(string evidenceDirectory = DefaultEvidence, bool startSettlement = true)
        {
            BeginReport("breeding", evidenceDirectory,
                "Local fixture resets two starting monsters, then initializes two parents with existing skills, modifier 120, zero work/rest history, different fixed templates and accessories, 独立/自律 personalities; adds one 能量体 worker with modifier 500. No child or daily mood is injected.");
            _breeding = _active;
            PresentationFixtureAcceptance.Begin("breeding", 2, Path.Combine(_evidence, "breeding-fixture"));
            foreach (var old in Run.Cage.ToArray()) Run.TryDestroy(old.Id);
            var first = Run.CreateMonster(new[] { "优秀能量体", "双头能量体" }, 120,
                new MonsterAppearance(0, 0, 3, 0, 2), personality: "独立", race: "森林");
            var second = Run.CreateMonster(new[] { "生长能量体", "奇异香" }, 120,
                new MonsterAppearance(7, 3, 1, 0, 23), personality: "自律", race: "海生");
            var worker = Run.CreateMonster(new[] { "能量体" }, 500, personality: "稳定", race: "森林");
            Run.AddToCage(first); Run.AddToCage(second); Run.AddToCage(worker);
            _parents = new[] { first.Id, second.Id }; _parentAppearances = new[] { first.Appearance, second.Appearance };
            _workerId = worker.Id; _childId = null; _workedChecks = _restedChecks = 0;
            Presentation.Refresh(); FocusProduction();
            Read(_breeding, "parent-a-before-placement", first, CagePortrait(first.Id));
            Read(_breeding, "parent-b-before-placement", second, CagePortrait(second.Id));
            Need(PresentationFixtureAcceptance.DragParent(0, 0).Contains("PASS"), "First parent drag failed");
            Need(PresentationFixtureAcceptance.DragParent(0, 1).Contains("PASS"), "Second parent drag failed");
            for (var i = 0; i < _parents.Length; i++)
            {
                var portrait = Operation.breedingSlots[i].portrait; CheckPortrait(_parents[i], portrait);
                Read(_breeding, "parent-in-real-breeding-seat-" + i, Run.Find(_parents[i]), portrait);
            }
            PlaceFromCage(_workerId, 0);
            Read(_breeding, "worker-in-real-extraction-seat", worker, Operation.extractionSlots[0].portrait);
            _stage = "breeding-prepared"; Save(_breeding);
            if (startSettlement) return BreedingSettle();
            return "PASS breeding-prepared: two real parent drags and one real production drag; capture the layout before calling BreedingSettle";
        }

        public static string BreedingSettle()
        {
            _active = _breeding;
            Need(_breeding != null && _stage == "breeding-prepared", "Call BreedingBegin before the real settlement click");
            _stage = "breeding-playing"; StartSettlement(); Save(_breeding);
            return "PASS breeding-playing: two real parent drags, one real production drag and settlement click; wait for ReadyForCheck then call BreedingCheck";
        }

        public static string BreedingCheck()
        {
            _active = _breeding;
            Need(_breeding != null && _stage == "breeding-playing" && ReadyForCheck, "Wait for completed settlement and the actual shop screen");
            CheckShopPortraits(_breeding);
            LeaveShop();
            var children = Run.LivingMonsters.Where(monster => monster.ParentIds.Count == 2 && monster.ParentIds.SequenceEqual(_parents)).ToArray();
            Need(children.Length == 1, "Production LeaveShop did not create exactly one child with both real parent IDs");
            var child = children[0]; _childId = child.Id;
            Need(MonsterAppearance.Compatible(child.Appearance.Recipe,child.Appearance.HeadAccessory,child.Appearance.BackAccessory), "Child inherited incompatible accessories");
            Need(child.Appearance.Palette == _parentAppearances[1].Palette, "Child palette did not follow the second parent");
            foreach (var skill in child.Skills)
                Need(skill.SourceKind == SkillSourceKind.Parent && _parents.Contains(skill.SourceMonsterId) &&
                    Run.Find(skill.SourceMonsterId).Skills.Any(source => source.Id == skill.SourceId && source.Name == skill.Name),
                    "Child skill lost its real inherited skill-instance source");
            Need(child.Skills.Select(skill => skill.SourceMonsterId).Distinct().Count() == 2, "Child did not inherit a contribution from each distinct-skill parent");
            // Capture birth provenance before BeginLevel: 好奇 can legitimately rewrite skills at the next start phase.
            Read(_breeding, "child-born-by-production-leave-shop", child, null, "Whole authored template selected by birth rules; palette follows parent B; both parent skill-instance sources retained");
            _stage="breeding-born";Save(_breeding);
            return "PASS child born by real LeaveShop; wait for ReadyForDayStart before BeginPendingDay";
        }
        public static string BeginPendingDay()
        {
            Need(ReadyForDayStart&&(_stage=="breeding-born"||_stage=="mood-next-day"),"Wait for completed level-start transition");
            var breeding=_stage=="breeding-born";
            var screen=Object.FindAnyObjectByType<LevelStartScreenView>(FindObjectsInactive.Include);
            Click(screen.beginButton);Need(App.SendQuery(new RunPhaseQuery())==RunPhase.Operation,"Begin-day click did not enter operation");
            _stage=breeding?"breeding-entering-operation":"mood-entering-operation";
            return "PASS begin-day clicked; wait for ReadyForOperation before checking portraits";
        }
        public static string BreedingNextDay()
        {
            _active=_breeding;Need(_stage=="breeding-entering-operation"&&ReadyForOperation,"Wait for operation transition before child portrait check");
            FocusProduction();var child=Run.Find(_childId);
            var portrait = CagePortrait(child.Id); CheckPortrait(child.Id, portrait);
            Read(_breeding, "child-on-next-day-cage", child, portrait, "Current start-phase skills and mood, including any legal personality effect, drive the actual portrait");
            _breeding.completed = true;
            _mood = new Report { scenario = "mood", unity = Application.unityVersion,
                injectedStartingResources = "Continues the real breeding fixture. Tracks parent A through two actual work days, optionally followed by two actual rest days. Does not set mood, work counters, skills or structure genes after initialization." };
            var tracked = Run.Find(_parents[0]); var trackedPortrait = CagePortrait(tracked.Id);
            _moodAppearance = Genes(tracked.Appearance); _moodInitialVisual = trackedPortrait.VisualSignature; _moodEyeFamily = trackedPortrait.Traits.EyeFamily;
            Read(_mood, "before-real-daily-mood-flow", tracked, trackedPortrait);
            _stage = "breeding-completed"; Save(_breeding); Save(_mood);
            return "PASS production settlement and LeaveShop create one inherited child; actual next-day cage portrait matches current rules. MoodDayBegin/Check can now run twice for work, then optionally twice with false for rest";
        }

        public static string MoodDayBegin(bool working = true)
        {
            _active = _mood;
            Need(_mood != null && !ScoringShow.IsPlaying && App.SendQuery(new RunPhaseQuery()) == RunPhase.Operation,
                "Finish BreedingCheck or the previous MoodDayCheck first");
            Need(working ? _workedChecks < 2 && _restedChecks == 0 : _workedChecks == 2 && _restedChecks < 2,
                "Mood fixture expects two work days, followed by up to two rest days");
            var tracked = Run.Find(_parents[0]);
            _expectedWork = working ? tracked.ConsecutiveWorkDays + 1 : 0;
            _expectedRest = working ? 0 : tracked.ConsecutiveRestDays + 1;
            _pendingMoodWork = working;
            FocusProduction();
            if (working) PlaceFromCage(tracked.Id, 0);
            PlaceFromCage(_workerId, working ? 1 : 0);
            var portrait = working ? Operation.extractionSlots[0].portrait : CagePortrait(tracked.Id);
            CheckPortrait(tracked.Id, portrait);
            Read(_mood, working ? "before-real-work-settlement" : "before-real-rest-settlement", tracked, portrait);
            _stage = "mood-playing"; StartSettlement(); Save(_mood);
            return "PASS mood-playing: tracked parent " + (working ? "works" : "rests") + "; wait for ReadyForCheck then call MoodDayCheck";
        }

        public static string MoodLeaveShop()
        {
            Need(_stage=="mood-playing"&&ReadyForCheck,"Wait for settled and interactive shop");
            LeaveShop();_stage="mood-next-day";return "PASS mood-day LeaveShop clicked; wait for ReadyForDayStart";
        }
        public static string MoodDayCheck()
        {
            _active = _mood;
            Need(_mood != null && _stage == "mood-entering-operation" && ReadyForOperation, "Wait for operation transition after the begin-day click");
            FocusProduction();
            var tracked = Run.Find(_parents[0]); var portrait = CagePortrait(tracked.Id);
            CheckPortrait(tracked.Id, portrait);
            Need(tracked.ConsecutiveWorkDays == _expectedWork && tracked.ConsecutiveRestDays == _expectedRest,
                "Production day transition did not advance/reset the actual parent history");
            var expectedMood = _pendingMoodWork ? (_expectedWork == 2 ? "疲倦" : "普通") : (_expectedRest == 2 ? "热情" : "普通");
            Need(tracked.Mood == expectedMood, "Real daily mood differs from the expected two-day work/rest threshold");
            Need(Genes(tracked.Appearance) == _moodAppearance, "Daily mood changed inherited structure genes");
            Need(portrait.Traits.EyeFamily == _moodEyeFamily, "Mood changed personality eye family");
            if (_pendingMoodWork) _workedChecks++; else _restedChecks++;
            if (_workedChecks == 2 && _restedChecks == 0 || _restedChecks == 2)
                Need(portrait.VisualSignature != _moodInitialVisual, "A real changed mood did not refresh portrait identity");
            Read(_mood, "after-real-" + (_pendingMoodWork ? "work-" + _workedChecks : "rest-" + _restedChecks), tracked, portrait,
                "Expected " + expectedMood + "; eye family and all birth genes retained");
            _mood.completed = _workedChecks == 2; _stage = _restedChecks == 2 ? "mood-completed" : "mood-day-completed"; Save(_mood);
            return "PASS actual parent mood " + tracked.Mood + "; work=" + tracked.ConsecutiveWorkDays + "; rest=" + tracked.ConsecutiveRestDays +
                "; current portrait refreshed with unchanged genes and eye family";
        }

        static void BeginReport(string scenario, string evidenceDirectory, string injected)
        {
            Need(Application.isPlaying && !ScoringShow.IsPlaying, "Requires idle Play Mode");
            _evidence = Path.GetFullPath(string.IsNullOrEmpty(evidenceDirectory) ? DefaultEvidence : evidenceDirectory);
            Directory.CreateDirectory(_evidence);
            _active = new Report { scenario = scenario, unity = Application.unityVersion, injectedStartingResources = injected };
        }

        static void FocusProduction()
        {
            Operation.lab.facility.panorama.Focus(true, true);
            Presentation.Refresh(); Canvas.ForceUpdateCanvases();
        }

        static MonsterPortrait CagePortrait(string id)
        {
            var slot = Operation.cageSlots.SingleOrDefault(candidate => candidate != null && candidate.gameObject.activeInHierarchy && candidate.monsterId == id);
            Need(slot != null && slot.portrait != null, "Current cage does not expose monster " + id);
            return slot.portrait;
        }

        static void PlaceFromCage(string id, int extractionIndex)
        {
            var index = Run.Cage.ToList().FindIndex(monster => monster.Id == id);
            Need(index >= 0, "Expected cage source " + id);
            Need(PresentationFixtureAcceptance.DragInventory(index, extractionIndex).Contains("PASS"), "Production monster drag failed");
            CheckPortrait(id, Operation.extractionSlots[extractionIndex].portrait);
        }

        static void CheckPortrait(string id, MonsterPortrait portrait)
        {
            var current = App.SendQuery(new MonsterQuery(id));
            Need(current != null && portrait != null && portrait.HasCreature && portrait.PartCount >= 8 &&
                portrait.VisualSignature == MonsterVisualTraits.From(current).Signature, "Actual portrait differs from current monster projection: " + id);
        }

        static void CheckShopPortraits(Report report)
        {
            if (Shop.facility != null) Shop.facility.ShowMonsters();
            Canvas.ForceUpdateCanvases();
            var count = 0;
            foreach (var card in Shop.cards.Where(card => card != null && card.gameObject.activeInHierarchy && !string.IsNullOrEmpty(card.monsterId)))
            {
                CheckPortrait(card.monsterId, card.portrait);
                Read(report, "actual-shop-stock", Run.Find(card.monsterId), card.portrait); count++;
            }
            Need(count > 0, "Actual shop has no visible monster offers to verify");
        }

        static void StartSettlement()
        {
            Click(Operation.nextDayButton);
            Need(ScoringShow.IsPlaying, "Actual next-day button did not begin settlement playback");
            var show = Object.FindAnyObjectByType<ScoringShow>();
            show.Advance(); show.Advance();
        }

        static void LeaveShop()
        {
            Need(ReadyForCheck && App.SendQuery(new RunPhaseQuery()) == RunPhase.Shop, "Shop not ready for the real leave button");
            Click(Shop.leaveButton);
            Need(App.SendQuery(new RunPhaseQuery()) == RunPhase.LevelStart, "Real LeaveShop did not advance to next-day start");
        }

        static void Click(Button button)
        {
            Need(button != null && button.gameObject.activeInHierarchy && button.interactable, "Actual UI button unavailable: " + (button == null ? "missing reference" : button.name + " active=" + button.gameObject.activeInHierarchy + " interactable=" + button.interactable + " transitioning=" + Presentation.Transitioning));
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var point = RectTransformUtility.WorldToScreenPoint(CanvasMap.EventCamera(rect), rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Need(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "Real pointer ray cannot reach button " + button.name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Canvas.ForceUpdateCanvases();
        }

        static void Read(Report report, string step, Monster monster, MonsterPortrait portrait, string note = null)
        {
            report.steps.Add(new Reading { step = step, monsterId = monster.Id, frame = Time.frameCount,
                phase = Run.Phase.ToString(), day = Run.LevelNumber, personality = monster.Personality, mood = monster.Mood,
                appearance = Genes(monster.Appearance), workDays = monster.ConsecutiveWorkDays, restDays = monster.ConsecutiveRestDays,
                parents = monster.ParentIds.ToArray(), skills = monster.Skills.Select(skill => skill.Name).ToArray(),
                skillSources = monster.Skills.Select(skill => skill.Id + "|" + skill.SourceKind + "|" + skill.SourceId + "|" + skill.SourceMonsterId).ToArray(),
                visual = portrait != null ? portrait.VisualSignature : "", assembly = portrait != null ? portrait.AssemblySignature : "",
                parts = portrait != null ? portrait.PartCount : 0, actualParts = portrait != null ? ActualParts(portrait) : Array.Empty<string>(), note = note });
        }

        static string[] ActualParts(MonsterPortrait portrait) => portrait.GetComponentsInChildren<Image>(false)
            .Where(image => image.enabled && image.sprite != null).Select(image => image.transform.parent.name + "|" + image.sprite.name).ToArray();

        static int[] GeneValues(MonsterAppearance value) => new[] { value.Recipe,value.Palette,value.HeadAccessory,value.BackAccessory,value.Variation };
        static string Genes(MonsterAppearance value) => string.Join(",", GeneValues(value));

        static void Need(bool value, string message)
        {
            if (value) return;
            if (_active != null) { _active.failure = message; Save(_active); }
            throw new InvalidOperationException("MONSTER_CULTIVATION_FAIL: " + message);
        }

        static void Save(Report report)
        {
            Directory.CreateDirectory(_evidence);
            File.WriteAllText(Path.Combine(_evidence, report.scenario + "-report.json"), JsonUtility.ToJson(report, true));
        }
    }
}
