using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using QFramework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    // Explicit local UI fixtures. Never count these injected resources as natural seven-day evidence.
    public static class PresentationFixtureAcceptance
    {
        [Serializable] sealed class Item { public string name; public bool passed; public string actual; public int frame; }
        [Serializable] sealed class Report { public string kind = "LOCAL_UI_FIXTURE_NOT_NATURAL_RUN"; public string unity; public List<Item> checks = new List<Item>(); }
        static Report report = new Report();
        static readonly List<string> ids = new List<string>();
        static string scenario;
        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/audit/2026-10-12/remediation/evidence/ui-fixtures"));
        static TheCallPresentation Presentation => Object.FindAnyObjectByType<TheCallPresentation>();
        static OperationScreenView Operation => Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
        static RunModel Run => TheCallApp.Interface.GetModel<RunModel>();
        static LevelModel Level => TheCallApp.Interface.GetModel<LevelModel>();
        static ContentBook Book => ContentGate.Current;

        public static string Begin(string kind, int count = 30)
        {
            Need(Application.isPlaying && Object.FindAnyObjectByType<TheCall.Scoring.ScoringShow>() == null, "Fixture requires idle Play Mode.");
            foreach (var hover in Object.FindObjectsByType<MonsterHover>(FindObjectsInactive.Include, FindObjectsSortMode.None)) { hover.HideManual(); hover.enabled = true; }
            TheCallApp.Reset();
            ContentGate.Use(ContentBook.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "call-book.json"))));
            TheCallApp.OnRegisterPatch = app => app.RegisterUtility<IDraw>(new M5PlayModeAutomation.SeededDraw(412));
            scenario = kind + "-" + count;
            report = new Report { unity = Application.unityVersion };
            ids.Clear();
            var run = Run;
            run.EnterOperation(1); Level.BeginLevel(50, 60); Level.FitExtraction(5); Level.ApplyShape(1, 2);
            if (kind == "tools")
            {
                Need(count >= 1 && count <= 18, "Tool count out of range");
                foreach (var definition in Book.Tools.Take(count)) run.AddTool(definition.Name);
                Level.FitExtraction(TheCallApp.Interface.GetUtility<IToolCatalog>().ExtractionCells(run.Tools));
            }
            if (kind == "capacity" || kind == "atomic")
            {
                run.AddTool("上级员工证"); Level.FitExtraction(6);
                foreach (var definition in Book.Techs) run.AddTechPoint();
            }
            var monsterCount = kind == "tools" ? 1 : Math.Max(1, count);
            for (var i = 0; i < monsterCount; i++)
            {
                var skills = kind == "details" ? new[] { "能量体", "左能量体", "右能量体", "双头能量体" } : new[] { "能量体" };
                var personality = kind == "install" || kind == "duplicate" || kind == "full" ? "迷恋死亡" : "稳定";
                if (kind == "full") skills = new[] { "能量体", "左能量体", "右能量体", "双头能量体" };
                var monster = run.CreateMonster(skills, personality: personality, consecutiveWorkDays: 2, consecutiveRestDays: 3);
                run.AddToCage(monster); ids.Add(monster.Id);
            }
            if (kind == "install" || kind == "duplicate" || kind == "full" || kind == "reject") run.PutSkillInSlot(kind == "duplicate" ? "能量体" : "奇异香");
            Presentation.StartSession(); Presentation.Refresh(); Canvas.ForceUpdateCanvases();
            return Save("begin " + kind, "count=" + count + " IDs=" + string.Join(",", ids));
        }

        public static string CheckTools(int expected)
        {
            Canvas.ForceUpdateCanvases();
            var catalog = Resources.Load<PresentationSpriteCatalog>("PresentationSpriteCatalog");
            var views = Operation.ownedToolViews.Where(view => view.gameObject.activeInHierarchy).ToArray();
            Need(views.Length == expected && Run.Tools.Count == expected, "Tool count mismatch");
            var rects = new List<Rect>();
            for (var i = 0; i < views.Length; i++)
            {
                var tool = views[i]; var definition = catalog.FindTool(Run.Tools[i]);
                Need(tool.toolName == Run.Tools[i] && tool.nameLabel.text == Run.Tools[i], "Tool name consumer mismatch");
                Need(tool.iconImage.enabled && tool.iconImage.sprite == definition.icon && tool.description == definition.description, "Tool Sprite/description consumer mismatch");
                Need(tool.iconImage.transform != tool.transform, "Tool image still owns layout root");
                var rect = ScreenRect((RectTransform)tool.transform);
                Need(rect.width > 0 && rect.height > 0 && rect.xMin >= 0 && rect.xMax <= Screen.width + 1, "Tool outside screen");
                foreach (var prior in rects) Need(!rect.Overlaps(prior), "Tools overlap");
                rects.Add(rect);
            }
            return Save("tools " + expected, string.Join(";", views.Select(view => view.toolName + "=" + view.iconImage.sprite.name)));
        }
        public static string HoverTool(int index)
        {
            var tool = Operation.ownedToolViews[index]; Need(tool.gameObject.activeInHierarchy, "Tool hidden");
            tool.OnPointerEnter(new PointerEventData(EventSystem.current));
            var panel = tool.GetComponentInParent<Canvas>().transform.Find("ToolDetails");
            Need(panel != null && panel.GetComponentInChildren<TMP_Text>().text.Contains(tool.description), "Actual tooltip missing description");
            return Save("tool-hover " + index, tool.toolName + ":" + tool.description);
        }

        public static string CheckInventory(int expected, bool selling = false)
        {
            if (selling && Run.Phase != RunPhase.Shop) { Run.EnterShop(); Run.MarkShopStocked(); Presentation.Refresh(); Click(Object.FindAnyObjectByType<ShopScreenView>(FindObjectsInactive.Include).sellTabButton); }
            var slots = selling ? Object.FindAnyObjectByType<ShopScreenView>(FindObjectsInactive.Include).sellSlots : Operation.cageSlots;
            var active = slots.Where(slot => slot.gameObject.activeInHierarchy && !string.IsNullOrEmpty(slot.monsterId)).ToArray();
            Need(active.Length == expected && active.Select(slot => slot.monsterId).Distinct().Count() == expected, "Inventory truncated or IDs duplicated");
            Need(active.Select(slot => slot.monsterId).SequenceEqual(Run.Cage.Select(monster => monster.Id)), "Inventory mapping order mismatch");
            Need(active.Last().GetComponent<OperationPayloadDrag>() != null || selling, "Final card lacks player drag component");
            return Save((selling ? "sell" : "cage") + "-inventory " + expected, "first=" + active.First().monsterId + " last=" + active.Last().monsterId + " contentHeight=" + ((RectTransform)active.First().transform.parent).rect.height);
        }
        public static string DragInventory(int index, int extractionIndex)
        {
            var id = Run.Cage[index].Id;
            var slot = Operation.cageSlots.Single(view => view.monsterId == id && view.gameObject.activeInHierarchy);
            ScrollTo(slot);
            Need(VisibleInScroll(slot), "Requested inventory source remains clipped");
            Drag(slot.GetComponent<OperationPayloadDrag>(), Operation.extractionSlots[extractionIndex].gameObject);
            Need(Level.Extraction[extractionIndex] == id && Operation.extractionSlots[extractionIndex].monsterId == id, "Actual first/last card drag failed");
            return Save("inventory-drag " + index, id + " -> extraction " + extractionIndex);
        }
        public static string DragParent(int cageIndex, int flatIndex)
        {
            var id = Run.Cage[cageIndex].Id; var source = Operation.cageSlots.Single(slot => slot.monsterId == id && slot.gameObject.activeInHierarchy);
            ScrollTo(source); Need(VisibleInScroll(source), "Parent source clipped");
            var destination = Operation.breedingSlots[flatIndex];
            Drag(source.GetComponent<OperationPayloadDrag>(), destination.gameObject);
            Need(Level.Breeding[flatIndex] == id && destination.monsterId == id, "Real parent placement failed");
            return Save("parent-drag", id + " -> flat parent seat " + flatIndex);
        }

        public static string ReturnExtraction(int index)
        {
            var source = Operation.extractionSlots[index]; var id = source.monsterId;
            var scroll = Operation.cageSlots[0].GetComponentInParent<ScrollRect>();
            Drag(source.GetComponent<OperationPayloadDrag>(), scroll.viewport.gameObject);
            Need(Run.Cage.Any(monster => monster.Id == id), "Actual return drag failed");
            return Save("inventory-return", id);
        }
        public static string SellInventory(int index)
        {
            Need(Run.Phase == RunPhase.Shop, "Open fixture sell inventory first");
            var shop = Object.FindAnyObjectByType<ShopScreenView>(FindObjectsInactive.Include);
            var id = Run.Cage[index].Id; var beforeGold = Run.Gold; var beforeCount = Run.Cage.Count;
            var slot = shop.sellSlots.Single(view => view.monsterId == id && view.gameObject.activeInHierarchy);
            ScrollTo(slot); Need(VisibleInScroll(slot), "Sell card clipped"); Click(slot.button);
            Need(Run.Cage.Count == beforeCount - 1 && !Run.Cage.Any(monster => monster.Id == id) && Run.Gold > beforeGold, "Actual sell button failed or sold wrong ID");
            return Save("sell " + index, id + " gold " + beforeGold + " -> " + Run.Gold);
        }

        public static string Research(string name, bool expectedSuccess = true)
        {
            var research = Object.FindAnyObjectByType<ResearchScreenView>(FindObjectsInactive.Include);
            if (!research.gameObject.activeInHierarchy) Click(Operation.researchButton);
            var node = research.nodes.Single(item => item.techName == name); Click(node.button);
            var before = State(); var points = Run.TechPoints;
            if (research.researchButton.interactable) Click(research.researchButton);
            if (expectedSuccess) Need(Run.HasTech(name) && Run.TechPoints == points - 1, "Actual research failed");
            else Need(State() == before, "Rejected research changed domain state");
            Need(research.detailBody.text.Contains(Book.FindTech(name).Description), "Research description consumer mismatch");
            return Save("research " + name, "success=" + expectedSuccess + " points=" + Run.TechPoints + " description=" + research.detailBody.text);
        }
        public static string BackResearch()
        { Click(Object.FindAnyObjectByType<ResearchScreenView>(FindObjectsInactive.Include).backButton); return Save("research-back", "operation=" + Operation.gameObject.activeInHierarchy); }
        public static string CheckCapacity()
        {
            Need(Level.Extraction.Count == 6 && Level.BreedingSlotCount == 2 && Level.ParentCount(0) == 3 && Run.GeneCapacity == 4, "Domain capacity mismatch");
            Need(Operation.extractionSlots.Count(slot => slot.gameObject.activeInHierarchy) == 6 && Operation.breedingSlots.Count(slot => slot.gameObject.activeInHierarchy) == 6 && Operation.skillChips.Count(chip => chip.gameObject.activeInHierarchy) == 4, "Actual capacity consumer mismatch");
            return Save("capacity", "6 extraction / 2 breeding / 3 parents / 4 genes");
        }
        public static string PrepareGenes(int count)
        {
            // Explicit fixture injection before real reservation/discard input.
            Need(Run.GeneSlots.Count == 0, "PrepareGenes expects empty storage");
            foreach (var name in new[] { "奇异香", "左能量体", "右能量体", "自爆" }.Take(count)) Run.PutSkillInSlot(name);
            Presentation.Refresh(); return Save("fixture-genes", "count=" + count);
        }
        public static string DragGene(int index, string target, int targetIndex = 0, bool expectedSuccess = true)
        {
            var before = State(); var geneCount = Run.GeneSlots.Count;
            var source = Operation.skillChips[index].GetComponent<OperationPayloadDrag>();
            var destination = target == "breeding" ? Operation.breedingSkills[targetIndex].gameObject : Operation.cageSlots[targetIndex].gameObject;
            Drag(source, destination);
            if (!expectedSuccess)
            {
                Need(State() == before, "Rejected install/reservation changed monster/gene/gold/reservation state");
                Need(!string.IsNullOrEmpty(TheCallApp.Interface.SendQuery(new OperationFeedbackQuery())), "Rejected input lacks domain feedback");
            }
            else if (target == "breeding") Need(Run.GeneSlots[index].ReservedBreedingSlot == targetIndex && Level.SkillAt(targetIndex) == Run.GeneSlots[index].SkillName, "Reservation failed or source index lost");
            else Need(Run.GeneSlots.Count == geneCount - 1, "Successful install did not consume exact gene");
            return Save("gene-drag " + index + " -> " + target, "success=" + expectedSuccess + " before=" + before + " after=" + State());
        }
        public static string ReturnGene(int breedingIndex)
        {
            var reserved = Run.GeneSlots.Single(gene => gene.ReservedBreedingSlot == breedingIndex); var id = reserved.Id; var index = Run.GeneSlots.ToList().IndexOf(reserved);
            Drag(Operation.breedingSkills[breedingIndex].GetComponent<OperationPayloadDrag>(), Operation.skillChips[index].gameObject);
            Need(Run.GeneSlots[index].Id == id && !Run.GeneSlots[index].IsReserved && Level.SkillAt(breedingIndex) == null, "Reservation did not return to original gene slot");
            return Save("gene-return", "id=" + id + " originalIndex=" + index);
        }
        public static string Discard(int cageIndex, bool expectedSuccess)
        {
            var before = State(); var id = Run.Cage[cageIndex].Id; var count = Run.GeneSlots.Count;
            var source = Operation.cageSlots.Single(slot => slot.monsterId == id && slot.gameObject.activeInHierarchy);
            ScrollTo(source); Drag(source.GetComponent<OperationPayloadDrag>(), Operation.discardButton.gameObject);
            if (expectedSuccess) Need(Run.Find(id) == null && Run.GeneSlots.Count == count + 1, "Fourth-slot discard failed");
            else Need(before == State() && Run.Find(id) != null && !string.IsNullOrEmpty(TheCallApp.Interface.SendQuery(new OperationFeedbackQuery())), "Full/reserved storage rejection is not atomic or explained");
            return Save("discard", "success=" + expectedSuccess + " before=" + before + " after=" + State());
        }

        public static string EnableDuplicateFixture()
        { Run.AddTool("复读腺"); Presentation.Refresh(); return Save("fixture-duplicate-permission", "复读腺 injected only for local UI validation"); }

        public static string Consumer(string category, int index)
        {
            Begin("consumer", 1); scenario = "consumer-" + category + "-" + index;
            var old = Run.Cage[0].Id; Run.TryRemoveFromCage(old, out _); Run.TryDestroy(old);
            var personality = category == "personality" ? Book.Personalities[index].Name : "稳定";
            var race = category == "race" ? Book.Races[index].Name : "森林";
            var mood = category == "mood" ? Book.Moods[index].Name : "普通";
            var creature = Run.CreateMonster(new[] { "能量体" }, personality: personality, race: race, mood: mood, consecutiveWorkDays: 2, consecutiveRestDays: 3);
            if (category == "mood") creature.SetMood(mood); // fixed personalities may override generation; fixture explicitly selects neutral stable personality.
            mood = creature.Mood;
            Run.AddToCage(creature); ids.Clear(); ids.Add(creature.Id); Presentation.Refresh(); Canvas.ForceUpdateCanvases();
            var slot = Operation.cageSlots[0]; var sprites = Resources.Load<PresentationSpriteCatalog>("PresentationSpriteCatalog");
            Need(slot.monsterId == creature.Id && slot.subtitle.text.Contains(personality) && slot.subtitle.text.Contains(mood) && slot.title.text.Contains(race), "Actual card text consumer missing formal content");
            Need(slot.personalityIcon.enabled && slot.personalityIcon.sprite == sprites.GetPersonalityIcon(personality), "Actual personality Sprite mismatch");
            Need(slot.raceIcon.enabled && slot.raceIcon.sprite == sprites.GetRaceIcon(race), "Actual race Sprite mismatch");
            Need(slot.moodIcon.enabled && slot.moodIcon.sprite == sprites.GetMoodIcon(mood), "Actual mood Sprite mismatch");
            var details = TheCallApp.Interface.SendQuery(new MonsterDetailsQuery(creature.Id));
            var hover = Object.FindAnyObjectByType<MonsterHover>(FindObjectsInactive.Include); hover.enabled = false; hover.ShowManual(details);
            var window = (RectTransform)typeof(MonsterHover).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hover);
            var header = window.Find("Header").GetComponentInChildren<TMP_Text>();
            Need(header.text.Contains(details.PersonalityDescription) && header.text.Contains(details.Mood) && header.text.Contains("休息 3天"), "Actual detail header omits formal mechanism/history");
            return Save("actual-consumer " + category + " " + index, "personality=" + personality + ";race=" + race + ";mood=" + details.Mood + ";icons=" + slot.personalityIcon.sprite.name + "," + slot.raceIcon.sprite.name + "," + slot.moodIcon.sprite.name + ";header=" + header.text);
        }
        public static string SkillConsumer(int index)
        {
            Begin("skill-consumer", 1); scenario = "consumer-skill-" + index;
            var old = Run.Cage[0].Id; Run.TryRemoveFromCage(old, out _); Run.TryDestroy(old);
            var definition = Book.Skills[index]; var monster = Run.CreateMonster(new[] { definition.Name }, personality: "稳定");
            Run.AddToCage(monster); ids.Clear(); ids.Add(monster.Id); Presentation.Refresh();
            var details = TheCallApp.Interface.SendQuery(new MonsterDetailsQuery(monster.Id));
            Need(details != null && details.Skills.Count == 1 && details.Skills[0].Name == definition.Name, "Formal skill detail missing");
            var hover = Object.FindAnyObjectByType<MonsterHover>(FindObjectsInactive.Include); hover.enabled = false; hover.ShowManual(details);
            foreach (var pair in new[] { new KeyValuePair<string,string>("_subNames", definition.Name), new KeyValuePair<string,string>("_subFunctions", details.Skills[0].Function), new KeyValuePair<string,string>("_subSentences", details.Skills[0].DisplaySentence) })
            {
                var text = ((TMP_Text[])typeof(MonsterHover).GetField(pair.Key, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hover))[0];
                Need(text.gameObject.activeInHierarchy && !string.IsNullOrEmpty(pair.Value) && text.text.Contains(pair.Value), "Actual skill consumer missing: " + pair.Key);
                text.ForceMeshUpdate(true); Need(!text.isTextOverflowing, "Actual single skill text clipped: " + definition.Name);
            }
            return Save("actual-skill-consumer", definition.Name + ";rarity=" + details.Skills[0].Rarity + ";use=" + details.Skills[0].Kind + ";function=" + details.Skills[0].Function + ";description=" + details.Skills[0].DisplaySentence);
        }

        public static string DetailsAt(string corner)
        {
            Need(ids.Count > 0, "Begin details fixture first");
            var source = TheCallApp.Interface.SendQuery(new MonsterDetailsQuery(ids[0]));
            Need(source.Skills.Count == 4, "Details fixture must have four skills");
            var details = new MonsterDetails(source.Id, "非常非常长的四技能怪物名称：组件化多来源验证", source.Skills, source.Modifier, source.Immovable, source.Capacity, source.Appearance, source.Personality, source.Race, source.Mood, source.MoodEnergyDelta, source.ConsecutiveWorkDays, source.ConsecutiveRestDays, source.PersonalityDescription);
            var hover = Object.FindAnyObjectByType<MonsterHover>(FindObjectsInactive.Include); hover.enabled = false;
            var canvas = (RectTransform)hover.transform.parent; var bounds = canvas.rect;
            var left = corner.Contains("left"); var top = corner.Contains("top");
            var anchor = new Rect(left ? bounds.xMin + 4 : bounds.xMax - 164, top ? bounds.yMax - 164 : bounds.yMin + 4, 160, 160);
            var metrics = new HoverMetrics(new Vector2(420, 176), 12, 8, bounds);
            var placement = HoverLayout.Place(anchor, left ? 1 : Screen.width - 1, Screen.width, 4, metrics);
            typeof(MonsterHover).GetMethod("Show", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hover, new object[] { details, placement });
            Canvas.ForceUpdateCanvases();
            var window = (RectTransform)typeof(MonsterHover).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hover);
            var fields = new[] { "_subNames", "_subSentences" };
            foreach (var field in fields)
            {
                var texts = (TMP_Text[])typeof(MonsterHover).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hover);
                for (var i = 0; i < 4; i++)
                {
                    texts[i].ForceMeshUpdate(true);
                    Need(!texts[i].isTextOverflowing, "Actual four-skill detail text clipped: " + texts[i].text);
                    var rect = ScreenRect(texts[i].rectTransform); Need(rect.xMin >= -1 && rect.yMin >= -1 && rect.xMax <= Screen.width + 1 && rect.yMax <= Screen.height + 1, "Actual detail text outside screen");
                }
            }
            var header = window.Find("Header").GetComponentInChildren<TMP_Text>(); header.ForceMeshUpdate(true);
            Need(header.text.Contains(details.DisplayName) && !header.isTextOverflowing, "Long name header clipped");
            var headerBounds = ScreenRect(header.rectTransform); Need(headerBounds.xMin >= -1 && headerBounds.yMin >= -1 && headerBounds.xMax <= Screen.width + 1 && headerBounds.yMax <= Screen.height + 1, "Header outside screen");
            return Save("details " + corner, "longName=" + details.DisplayName + ";fourSkillNames=" + string.Join(",", details.Skills.Select(skill => skill.Name)) + ";headerBounds=" + headerBounds);
        }
        public static string HideDetails()
        { var hover = Object.FindAnyObjectByType<MonsterHover>(FindObjectsInactive.Include); hover.HideManual(); hover.enabled = true; return Save("details-hide", "restored"); }

        public static string Assets()
        {
            Need(!Application.isPlaying, "Asset reload audit requires non-Play Mode");
            scenario = "assets"; report = new Report { unity = Application.unityVersion };
            PresentationRemediation.ValidateSaved();
            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI", "Assets/Prefabs/MonsterSlotCard" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true)) Need(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Prefab missing script: " + path);
                    foreach (var slot in root.GetComponentsInChildren<MonsterSlotView>(true)) Need(slot.portrait != null && slot.title != null && slot.subtitle != null && slot.personalityIcon != null && slot.raceIcon != null && slot.moodIcon != null, "Prefab actual slot references incomplete: " + path);
                    foreach (var hover in root.GetComponentsInChildren<MonsterHover>(true))
                    {
                        var so = new SerializedObject(hover); Need(so.FindProperty("_window").objectReferenceValue != null, "Prefab hover window null");
                        foreach (var field in new[] { "_subpanels", "_subNames", "_subUses", "_subRarities", "_subFunctions", "_subSentences" })
                        { var array = so.FindProperty(field); Need(array.arraySize == 4, "Hover must have four actual consumers"); for (var i = 0; i < 4; i++) Need(array.GetArrayElementAtIndex(i).objectReferenceValue != null, "Hover consumer reference null"); }
                    }
                    Save("prefab-reload", path); count++;
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Need(count >= 9, "Fewer than nine relevant prefab loads");
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterAssemblyCatalog>("Assets/Resources/MonsterAssemblyCatalog.asset");
            Need(catalog.parts.Count == 39 && catalog.templates.Count == 6 && catalog.parts.All(part => part.sprite != null), "Saved assembly content incomplete");
            foreach (var template in catalog.templates)
                foreach (MonsterPartKind kind in Enum.GetValues(typeof(MonsterPartKind)))
                { var slot = template.GetSlot(kind); if (slot.enabled) Need(catalog.GetPartOrNull(slot.partId)?.sprite != null, "Saved template reference missing: " + slot.partId); }
            return Save("asset-audit", "prefabs=" + count + ";39 saved parts / 6 templates complete");
        }
        public static string Idempotency()
        {
            Need(!Application.isPlaying, "Migration audit requires non-Play Mode");
            scenario = "idempotency"; report = new Report { unity = Application.unityVersion };
            var presentation = Object.FindAnyObjectByType<TheCallPresentation>(FindObjectsInactive.Include);
            var identity = presentation;
            var portrait = Object.FindAnyObjectByType<MonsterPortrait>(FindObjectsInactive.Include); var rect = (RectTransform)portrait.transform;
            var original = rect.anchoredPosition; var sentinel = original + new Vector2(3.25f, 2.5f);
            rect.anchoredPosition = sentinel;
            try
            {
                PresentationRemediation.Apply();
                var count = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                PresentationRemediation.Apply();
                Need(Object.FindAnyObjectByType<TheCallPresentation>(FindObjectsInactive.Include) == identity, "Migration replaced presentation root");
                Need(rect.anchoredPosition == sentinel, "Migration overwrote unrelated portrait adjustment");
                Need(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == count, "Second migration added duplicate objects");
                PresentationRemediation.ValidateSaved();
                return Save("migration-idempotency", "stableRootReference=" + identity.name + ";objects=" + count + ";portrait authoring sentinel retained");
            }
            finally { rect.anchoredPosition = original; UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(presentation.gameObject.scene); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(presentation.gameObject.scene); }
        }

        static void Drag(OperationPayloadDrag source, GameObject target)
        {
            Need(source != null && source.gameObject.activeInHierarchy && target.activeInHierarchy, "Player drag endpoint unavailable");
            Canvas.ForceUpdateCanvases();
            var sourcePoint = Center((RectTransform)source.transform); var targetPoint = Center((RectTransform)target.transform);
            var data = new PointerEventData(EventSystem.current) { position = sourcePoint, button = PointerEventData.InputButton.Left };
            source.OnPointerDown(data); source.OnBeginDrag(data); data.position = targetPoint; source.OnDrag(data); source.OnEndDrag(data);
            Canvas.ForceUpdateCanvases();
        }
        static void Click(Button button) { Need(button != null && button.gameObject.activeInHierarchy && button.interactable, "Player button unavailable"); button.onClick.Invoke(); Canvas.ForceUpdateCanvases(); }
        static void ScrollTo(MonsterSlotView slot)
        {
            var scroll = slot.GetComponentInParent<ScrollRect>(); if (scroll == null) return;
            scroll.StopMovement(); Canvas.ForceUpdateCanvases();
            var content = scroll.content; var target = (RectTransform)slot.transform;
            var max = Mathf.Max(0, content.rect.height - scroll.viewport.rect.height);
            var distance = Mathf.Clamp(-target.anchoredPosition.y, 0, max);
            scroll.verticalNormalizedPosition = max <= 0 ? 1 : 1 - distance / max;
            Canvas.ForceUpdateCanvases();
        }
        static bool VisibleInScroll(MonsterSlotView slot)
        {
            var scroll = slot.GetComponentInParent<ScrollRect>(); return scroll == null || RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, Center((RectTransform)slot.transform), null);
        }
        static Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        static Rect ScreenRect(RectTransform rect)
        { var corners = new Vector3[4]; rect.GetWorldCorners(corners); var min = RectTransformUtility.WorldToScreenPoint(null, corners[0]); var max = RectTransformUtility.WorldToScreenPoint(null, corners[2]); return Rect.MinMaxRect(min.x,min.y,max.x,max.y); }
        static string State()
        {
            return "gold=" + Run.Gold + ";tech=" + Run.TechPoints + ";tools=" + string.Join(",", Run.Tools) + ";unlocked=" + string.Join(",", Run.UnlockedTech) + ";cage=" + string.Join(",", Run.Cage.Select(monster => monster.Id))
                + ";monsters=" + string.Join(";", Run.LivingMonsters.Select(monster => monster.Id + "[" + string.Join(",", monster.Skills.Select(skill => skill.Id + ":" + skill.Name + ":" + skill.Quote + ":" + skill.IsTemporary + ":" + skill.SourceKind + ":" + skill.SourceId + ":" + skill.SourceMonsterId)) + "]"))
                + ";genes=" + string.Join(",", Run.GeneSlots.Select(gene => gene.Id + ":" + gene.SkillName + ":" + gene.ReservedBreedingSlot))
                + ";extraction=" + string.Join(",", Level.Extraction) + ";breeding=" + string.Join(",", Level.Breeding);
        }
        static void Need(bool value, string message) { if (!value) { Save("FAIL", message, false); throw new InvalidOperationException(message); } }
        static string Save(string name, string actual, bool passed = true)
        {
            report.checks.Add(new Item { name = name, passed = passed, actual = actual, frame = Time.frameCount });
            Directory.CreateDirectory(Root); File.WriteAllText(Path.Combine(Root, (scenario ?? "assets") + "-report.json"), JsonUtility.ToJson(report, true));
            return name + " " + (passed ? "PASS " : "FAIL ") + actual;
        }
        public static string Capture(string name)
        { Directory.CreateDirectory(Root); var path = Path.Combine(Root, Path.GetFileName(name)); ScreenCapture.CaptureScreenshot(path); return path; }
    }
}
