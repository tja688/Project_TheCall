using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TheCall.Editor
{
    public static class PresentationSceneBuilder
    {
        const string PortraitPath = "Assets/Prefabs/UI/MonsterPortrait.prefab";

        static float PortraitViewportWidth => MonsterPortrait.RecommendedViewportSize.x;
        static float PortraitViewportHeight => MonsterPortrait.RecommendedViewportSize.y;

        static readonly Color Stage = new Color32(16, 20, 28, 255);
        static readonly Color Panel = new Color32(27, 38, 52, 245);
        static readonly Color PanelDeep = new Color32(18, 28, 40, 250);
        static readonly Color Mint = new Color32(72, 214, 168, 255);
        static readonly Color MintInk = new Color32(10, 32, 30, 255);
        static readonly Color Ink = new Color32(232, 244, 240, 255);
        static readonly Color Muted = new Color32(154, 176, 186, 255);
        static readonly Color Gold = new Color32(240, 196, 96, 255);
        static readonly Color Danger = new Color32(214, 92, 112, 255);
        static readonly Color ButtonFace = new Color32(36, 52, 66, 255);

        const string ArtDir = "Assets/Art/Ui";

        static Sprite _ui;
        static TMP_FontAsset _font;
        static GameObject _portraitPrefab;
        static Sprite _stage;
        static Sprite _breed;
        static Sprite _production;
        static Sprite _panel;
        static Sprite _frame;
        static Sprite _soft;
        static Sprite _btnMint;
        static Sprite _btnDark;
        static Sprite _btnDanger;
        static Sprite _pipe;
        static Sprite _doctor;
        static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();

        [MenuItem("The Call/重建表现层界面")]
        public static void RebuildFromMenu()
        {
            // -automated 下没有人点确认，对话框会把这次菜单调用挂住。
            if (!LaunchedAutomated() && !EditorUtility.DisplayDialog(
                    "重建表现层",
                    "会覆盖场景里的 UICanvas 和怪物画像预制体。已经在这些物体上改过的位置和图片会丢掉。",
                    "重建",
                    "取消"))
                return;

            Build();
        }

        static bool LaunchedAutomated()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "-automated")
                    return true;
            }

            return false;
        }

        public static string Build()
        {
            if (EditorApplication.isPlaying)
                return "请先退出播放模式。";

            EditorApplication.LockReloadAssemblies();
            try
            {
                return BuildInternal();
            }
            finally
            {
                EditorApplication.UnlockReloadAssemblies();
            }
        }

        public static void BuildHoverWindow()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Exit play mode before building the hover window.");

            const string path = "Assets/Prefabs/UI/MonsterHoverPanel.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new InvalidOperationException("Missing " + path);

            var canvas = FindOpenCanvas();
            for (var i = canvas.childCount - 1; i >= 0; i--)
            {
                var child = canvas.GetChild(i);
                if (child.name == "MonsterHover" || child.name == "MonsterHoverPanel")
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            var hoverObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
            hoverObject.name = "MonsterHover";
            Stretch(hoverObject.GetComponent<RectTransform>());
            var window = hoverObject.transform.Find("Window");
            if (window == null)
                throw new InvalidOperationException(path + " has no Window.");

            window.gameObject.SetActive(false);
            hoverObject.SetActive(true);
            hoverObject.transform.SetAsLastSibling();

            var scene = canvas.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Transform FindOpenCanvas()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == "UICanvas")
                    return roots[i].transform;
            }

            throw new InvalidOperationException("The open scene has no UICanvas.");
        }

        static void Assign(SerializedProperty property, UnityEngine.Object[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static string BuildInternal()
        {
            ConfigureParts();
            ConfigureUi();
            LoadUi();
            EnsureFolder("Assets/Prefabs", "UI");
            _ui = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            _font = Resources.Load<TMP_FontAsset>("SmileySans-Oblique-3 SDF");
            _portraitPrefab = CreatePortraitPrefab();

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Main.unity")
                scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            DestroyNamed("UICanvas");
            DestroyNamed("EventSystem");

            var camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Stage;
            }

            var eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventObject.GetComponent<InputSystemUIInputModule>();

            var canvasObject = new GameObject("UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var opening = BuildOpening(canvasObject.transform);
            var levelStart = BuildLevelStart(canvasObject.transform);
            var operation = BuildOperation(canvasObject.transform);
            var research = BuildResearch(canvasObject.transform);
            var shop = BuildShop(canvasObject.transform);
            var result = BuildResult(canvasObject.transform);
            var toast = BuildToast(canvasObject.transform, out var toastText);

            var presentation = UnityEngine.Object.FindAnyObjectByType<TheCallPresentation>();
            if (presentation == null)
            {
                var host = new GameObject("TheCallPresentation");
                presentation = host.AddComponent<TheCallPresentation>();
            }

            presentation.BindScreens(opening, levelStart, operation, research, shop, result, toast, toastText);
            opening.gameObject.SetActive(true);
            levelStart.gameObject.SetActive(false);
            operation.gameObject.SetActive(false);
            research.gameObject.SetActive(false);
            shop.gameObject.SetActive(false);
            result.gameObject.SetActive(false);
            toast.SetActive(false);
            BuildHoverWindow();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return "built";
        }

        static OpeningScreenView BuildOpening(Transform parent)
        {
            var screen = Screen(parent, "OpeningScreen");
            Ambience(screen.transform, 0.4f);
            var title = Label(screen.transform, "Title", "怪物育成公司", 42, Ink, TextAlignmentOptions.Center);
            At(title, 360, 70, 1200, 64);
            var subtitle = Label(screen.transform, "Subtitle", "留下第一只怪物", 22, Muted, TextAlignmentOptions.Center);
            At(subtitle, 360, 140, 1200, 36);
            var view = screen.AddComponent<OpeningScreenView>();
            view.candidates = new[]
            {
                MonsterCard(screen.transform, "Candidate_0", 320, 210, 400, 520, 1.85f, "留下它"),
                MonsterCard(screen.transform, "Candidate_1", 760, 210, 400, 520, 1.85f, "留下它"),
                MonsterCard(screen.transform, "Candidate_2", 1200, 210, 400, 520, 1.85f, "留下它"),
            };
            return view;
        }

        static LevelStartScreenView BuildLevelStart(Transform parent)
        {
            var screen = Screen(parent, "LevelStartScreen");
            Ambience(screen.transform, 0.28f);
            var card = Box(screen.transform, "PanelImage", Panel);
            At(card, 520, 220, 880, 620);
            var level = Label(card.transform, "Level", "第 1 关", 40, Ink, TextAlignmentOptions.Center);
            At(level, 40, 48, 800, 64);
            var dueCaption = Label(card.transform, "DueCaption", "本关交款", 20, Muted, TextAlignmentOptions.Center);
            At(dueCaption, 80, 180, 320, 32);
            var due = Label(card.transform, "Due", "0", 54, Ink, TextAlignmentOptions.Center);
            At(due, 80, 220, 320, 80);
            var excessCaption = Label(card.transform, "ExcessCaption", "超额科技", 20, Muted, TextAlignmentOptions.Center);
            At(excessCaption, 480, 180, 320, 32);
            var excess = Label(card.transform, "Excess", "0+", 54, Gold, TextAlignmentOptions.Center);
            At(excess, 480, 220, 320, 80);
            var begin = Click(card.transform, "BeginButton", "进入操作台", Mint, MintInk, 24, out _);
            At(begin, 250, 470, 380, 72);
            var view = screen.AddComponent<LevelStartScreenView>();
            view.levelLabel = level;
            view.dueLabel = due;
            view.excessLabel = excess;
            view.beginButton = begin;
            return view;
        }

        static OperationScreenView BuildOperation(Transform parent)
        {
            var screen = Screen(parent, "OperationScreen");
            var background = screen.transform.Find("BackgroundImage").GetComponent<Image>();
            background.raycastTarget = true;
            Zone(background.gameObject, LandingPlace.EmptySpace, null, null);
            var pointer = screen.AddComponent<OperationPointer>();
            var dragLayerObject = new GameObject("DragLayer", typeof(RectTransform));
            dragLayerObject.transform.SetParent(screen.transform, false);
            Stretch(dragLayerObject.GetComponent<RectTransform>());
            pointer.dragLayer = dragLayerObject.GetComponent<RectTransform>();
            var title = Label(screen.transform, "Title", "怪物育成公司", 28, Ink, TextAlignmentOptions.Left);
            At(title, 64, 18, 360, 40);
            var targetCaption = Label(screen.transform, "TargetCaption", "次日目标", 18, Muted, TextAlignmentOptions.Left);
            At(targetCaption, 64, 58, 110, 28);
            var target = Label(screen.transform, "TargetNumber", "0", 20, Ink, TextAlignmentOptions.Left);
            At(target, 176, 56, 80, 30);
            var energyWord = Label(screen.transform, "EnergyWord", "能量", 18, Muted, TextAlignmentOptions.Left);
            At(energyWord, 250, 58, 60, 28);
            var energy = Label(screen.transform, "CurrentEnergy", "当前 0", 18, Mint, TextAlignmentOptions.Left);
            At(energy, 320, 58, 180, 28);
            var currentNumber = Label(screen.transform, "CurrentNumber", "0", 51.5f, Ink, TextAlignmentOptions.Left);
            At(currentNumber, 1265.6f, 538f, 240.4f, 113.35f);
            var goldCaption = Label(screen.transform, "GoldCaption", "金币", 18, Gold, TextAlignmentOptions.Right);
            At(goldCaption, 1120, 28, 70, 32);
            var gold = Label(screen.transform, "GoldNumber", "0", 24, Gold, TextAlignmentOptions.Left);
            At(gold, 1196, 24, 120, 36);

            var shop = Click(screen.transform, "ShopButton", "商店", ButtonFace, Ink, 18, out _);
            At(shop, 1340, 20, 150, 48);
            var research = Click(screen.transform, "ResearchButton", "科研", ButtonFace, Ink, 18, out _);
            At(research, 1506, 20, 150, 48);
            var back = Click(screen.transform, "TitleButton", "返回标题", ButtonFace, Ink, 18, out _);
            At(back, 1672, 20, 180, 48);

            var rail = Box(screen.transform, "FloorRail", PanelDeep);
            At(rail, 16, 108, 40, 944);
            var floorTwo = Label(rail.transform, "Floor2", "2F", 16, Mint, TextAlignmentOptions.Center);
            At(floorTwo, 0, 24, 40, 28);
            var floorOne = Label(rail.transform, "Floor1", "1F", 16, Mint, TextAlignmentOptions.Center);
            At(floorOne, 0, 470, 40, 28);

            var breeding = Box(screen.transform, "培育室", Panel);
            At(breeding, 64, 108, 1360, 430);
            PaintRoom(breeding, _breed);
            CaptionShade(breeding.transform);
            var breedingTitle = Label(breeding.transform, "Caption", "培育室", 24, Ink, TextAlignmentOptions.Left);
            At(breedingTitle, 24, 16, 200, 36);
            var seats = new MonsterSlotView[6];
            seats[0] = MonsterCard(breeding.transform, "BreedingSeat_0", 48, 70, 300, 280, 1.7f, null, true);
            seats[1] = MonsterCard(breeding.transform, "BreedingSeat_1", 380, 70, 300, 280, 1.7f, null, true);
            seats[2] = MonsterCard(breeding.transform, "BreedingSeat_2", 712, 70, 300, 280, 1.7f, null, true);
            seats[3] = MonsterCard(breeding.transform, "BreedingSeat_3", 48, 358, 200, 64, 0.4f, null, true);
            seats[4] = MonsterCard(breeding.transform, "BreedingSeat_4", 260, 358, 200, 64, 0.4f, null, true);
            seats[5] = MonsterCard(breeding.transform, "BreedingSeat_5", 472, 358, 200, 64, 0.4f, null, true);
            for (var i = 0; i < seats.Length; i++)
            {
                Zone(seats[i].gameObject, LandingPlace.BreedingSeat, seats[i], null);
                Drag(seats[i].gameObject, PayloadKind.Monster, pointer);
            }
            var breedSkills = new SkillChipView[2];
            breedSkills[0] = Chip(breeding.transform, "BreedingSkill_0", 1048, 110, 270, 96);
            breedSkills[1] = Chip(breeding.transform, "BreedingSkill_1", 1048, 230, 270, 96);
            breedSkills[0].label.text = "投入技能";
            breedSkills[1].label.text = "投入技能";
            for (var i = 0; i < breedSkills.Length; i++)
            {
                Zone(breedSkills[i].gameObject, LandingPlace.BreedingSocket, null, breedSkills[i]);
                Drag(breedSkills[i].gameObject, PayloadKind.BreedingSkill, pointer);
            }

            var production = Box(screen.transform, "生产区", Panel);
            At(production, 64, 554, 1360, 498);
            PaintRoom(production, _production);
            CaptionShade(production.transform);
            var productionTitle = Label(production.transform, "Caption", "生产区", 24, Ink, TextAlignmentOptions.Left);
            At(productionTitle, 24, 16, 200, 36);
            var belt = Box(production.transform, "BeltImage", PanelDeep);
            At(belt, 36, 400, 1288, 72);
            belt.enabled = false;
            var extracts = new MonsterSlotView[6];
            for (var i = 0; i < extracts.Length; i++)
            {
                extracts[i] = MonsterCard(production.transform, "ExtractionSlot_" + i, 40 + i * 218, 168, 200, 230, 1.15f, null, true);
                Zone(extracts[i].gameObject, LandingPlace.Extraction, extracts[i], null);
                Drag(extracts[i].gameObject, PayloadKind.Monster, pointer);
            }

            var cagePanel = Box(screen.transform, "收容笼", Panel);
            At(cagePanel, 1440, 108, 456, 430);
            var cageTitle = Label(cagePanel.transform, "Caption", "收容笼", 24, Ink, TextAlignmentOptions.Left);
            At(cageTitle, 20, 12, 200, 36);
            var cageScroll = Scroll(cagePanel.transform, "CageScroll", out var cageContent, true);
            At(cageScroll, 16, 56, 440, 358);
            cageContent.sizeDelta = new Vector2(0f, 9 * 118f + 8f);
            var cage = new MonsterSlotView[18];
            for (var i = 0; i < cage.Length; i++)
            {
                var column = i % 2;
                var row = i / 2;
                cage[i] = MonsterCard(cageContent, "CageCard_" + i, 8 + column * 204, 4 + row * 118, 196, 112, 0.48f, null);
                Zone(cage[i].gameObject, LandingPlace.Cage, cage[i], null);
                Drag(cage[i].gameObject, PayloadKind.Monster, pointer);
            }

            Zone(cageScroll.GetComponent<ScrollRect>().viewport.gameObject, LandingPlace.Cage, null, null);

            var discard = Click(screen.transform, "废弃回收", "废弃回收", Danger, Danger, 22, out var discardLabel);
            At(discard, 1440, 554, 456, 148);
            At(discardLabel, 16, 12, 424, 36);
            var recycle = Icon(discard.transform, "RecycleIcon", "recycle", 150, 52, 72, 72);
            var trash = Icon(discard.transform, "TrashIcon", "trash", 250, 52, 72, 72);
            Zone(discard.gameObject, LandingPlace.Discard, null, null);

            var skills = Box(screen.transform, "技能槽", Panel);
            At(skills, 1440, 718, 456, 230);
            skills.raycastTarget = true;
            Zone(skills.gameObject, LandingPlace.SkillSlot, null, null);
            var skillTitle = Label(skills.transform, "Caption", "技能槽", 22, Ink, TextAlignmentOptions.Left);
            At(skillTitle, 16, 12, 160, 32);
            var chips = new[]
            {
                Chip(skills.transform, "SkillChip_0", 16, 56, 136, 72),
                Chip(skills.transform, "SkillChip_1", 160, 56, 136, 72),
                Chip(skills.transform, "SkillChip_2", 304, 56, 136, 72),
            };
            for (var i = 0; i < chips.Length; i++)
            {
                Zone(chips[i].gameObject, LandingPlace.SkillSlot, null, chips[i]);
                Drag(chips[i].gameObject, PayloadKind.SkillChip, pointer);
            }
            var equip = Click(skills.transform, "EquipButton", "装入", Mint, MintInk, 18, out _);
            At(equip, 16, 148, 424, 64);
            equip.gameObject.SetActive(false);

            var next = Click(screen.transform, "NextDayButton", "进入下一天", Mint, MintInk, 22, out var nextLabel);
            At(next, 1440, 968, 456, 72);

            var view = screen.AddComponent<OperationScreenView>();
            view.targetLabel = target;
            view.energyLabel = energy;
            view.currentNumber = currentNumber;
            view.goldLabel = gold;
            view.nextDayLabel = nextLabel;
            view.shopButton = shop;
            view.researchButton = research;
            view.titleButton = back;
            view.nextDayButton = next;
            view.discardButton = discard;
            view.equipButton = equip;
            view.cageSlots = cage;
            view.extractionSlots = extracts;
            view.breedingSlots = seats;
            view.breedingSkills = breedSkills;
            view.skillChips = chips;
            view.dragLayer = pointer.dragLayer;
            view.pointer = pointer;
            dragLayerObject.transform.SetAsLastSibling();
            return view;
        }

        static ResearchScreenView BuildResearch(Transform parent)
        {
            var screen = Screen(parent, "ResearchScreen");
            Ambience(screen.transform, 0.22f);
            var title = Label(screen.transform, "Title", "怪物育成公司", 26, Ink, TextAlignmentOptions.Left);
            At(title, 36, 22, 240, 40);
            var section = Label(screen.transform, "Section", "科学研究", 26, Ink, TextAlignmentOptions.Left);
            At(section, 290, 22, 200, 40);
            var techCaption = Label(screen.transform, "TechCaption", "科技点", 18, Muted, TextAlignmentOptions.Right);
            At(techCaption, 1180, 28, 90, 32);
            var tech = Label(screen.transform, "TechNumber", "0", 24, Ink, TextAlignmentOptions.Left);
            At(tech, 1280, 24, 80, 36);
            var goldCaption = Label(screen.transform, "GoldCaption", "金币", 18, Gold, TextAlignmentOptions.Right);
            At(goldCaption, 1360, 28, 70, 32);
            var gold = Label(screen.transform, "GoldNumber", "0", 24, Gold, TextAlignmentOptions.Left);
            At(gold, 1440, 24, 90, 36);
            var researchTab = Click(screen.transform, "ResearchTab", "科研", Mint, MintInk, 18, out _);
            At(researchTab, 1560, 20, 140, 48);
            var back = Click(screen.transform, "BackButton", "返回", ButtonFace, Ink, 18, out _);
            At(back, 1716, 20, 160, 48);

            var tree = new GameObject("科研树", typeof(RectTransform));
            tree.transform.SetParent(screen.transform, false);
            At(tree, 0, 0, 1920, 1080);
            Pipe(tree.transform, "PipeFromGene", 400, 507, 86, 16);
            Pipe(tree.transform, "PipeVertical", 470, 330, 16, 400);
            Pipe(tree.transform, "PipeToSlot", 486, 337, 214, 16);
            Pipe(tree.transform, "PipeToMutation", 940, 337, 140, 16);
            Pipe(tree.transform, "PipeToStew", 486, 707, 214, 16);
            Pipe(tree.transform, "PipeToScience", 940, 707, 140, 16);

            var nodes = new[]
            {
                Tech(tree.transform, "基因实验", "基因实验", "培育方案可以放入一条技能。", 160, 420),
                Tech(tree.transform, "槽位扩容", "槽位扩容", "培育槽从 1 个变成 2 个。", 700, 250),
                Tech(tree.transform, "变异学说", "变异学说", "后代有机会多获得一条技能。", 1080, 250),
                Tech(tree.transform, "大乱炖", "大乱炖", "每个培育槽可以放 3 只亲本。", 700, 620),
                Tech(tree.transform, "科学培育", "科学培育", "培育后代获得产能修正。", 1080, 620),
            };

            var detail = Box(screen.transform, "详情", Panel);
            At(detail, 1468, 120, 420, 900);
            var detailTitle = Label(detail.transform, "DetailTitle", "基因实验", 28, Ink, TextAlignmentOptions.Center);
            At(detailTitle, 24, 48, 372, 48);
            var detailBody = Label(detail.transform, "DetailBody", "培育方案可以放入一条技能。", 20, Gold, TextAlignmentOptions.TopLeft);
            At(detailBody, 36, 180, 348, 220);
            var detailCost = Label(detail.transform, "DetailCost", "科技点消耗：1", 18, Muted, TextAlignmentOptions.Center);
            At(detailCost, 36, 680, 348, 32);
            var research = Click(detail.transform, "ResearchButton", "研究", Mint, MintInk, 24, out var researchLabel);
            At(research, 48, 760, 324, 72);

            var view = screen.AddComponent<ResearchScreenView>();
            view.techPointLabel = tech;
            view.goldLabel = gold;
            view.detailTitle = detailTitle;
            view.detailBody = detailBody;
            view.detailCost = detailCost;
            view.researchButton = research;
            view.researchLabel = researchLabel;
            view.backButton = back;
            view.nodes = nodes;
            return view;
        }

        static ShopScreenView BuildShop(Transform parent)
        {
            var screen = Screen(parent, "ShopScreen");
            Ambience(screen.transform, 0.18f);
            var title = Label(screen.transform, "Title", "物资交易", 30, Ink, TextAlignmentOptions.Left);
            At(title, 36, 20, 280, 44);
            var shop = Click(screen.transform, "ShopButton", "商店", Mint, MintInk, 18, out _);
            At(shop, 1188, 18, 130, 46);
            var research = Click(screen.transform, "ResearchButton", "科研", ButtonFace, Ink, 18, out _);
            At(research, 1330, 18, 130, 46);
            var save = Click(screen.transform, "SaveButton", "保存", ButtonFace, Ink, 18, out _);
            At(save, 1472, 18, 130, 46);
            var back = Click(screen.transform, "TitleButton", "返回标题", ButtonFace, Ink, 18, out _);
            At(back, 1614, 18, 170, 46);

            var sellTab = Click(screen.transform, "SellTab", "出售库存", ButtonFace, Ink, 18, out _);
            At(sellTab, 36, 100, 180, 48);
            var buyTab = Click(screen.transform, "BuyTab", "购入商品", Mint, MintInk, 18, out _);
            At(buyTab, 232, 100, 180, 48);
            string[] filters = { "全部", "生产 / 设施", "培育", "科研", "怪物" };
            for (var i = 0; i < filters.Length; i++)
            {
                var chip = Label(screen.transform, "Filter_" + i, filters[i], 16, i == 0 ? Mint : Muted, TextAlignmentOptions.Center);
                At(chip, 36 + i * 150, 164, 140, 32);
            }

            var balanceCaption = Label(screen.transform, "BalanceCaption", "余额", 18, Gold, TextAlignmentOptions.Right);
            At(balanceCaption, 1180, 164, 70, 28);
            var balance = Label(screen.transform, "BalanceNumber", "0", 24, Gold, TextAlignmentOptions.Left);
            At(balance, 1260, 160, 120, 32);

            var buyPage = new GameObject("购入商品页", typeof(RectTransform));
            buyPage.transform.SetParent(screen.transform, false);
            At(buyPage, 0, 0, 1920, 1080);
            var cards = new ShopCardView[6];
            for (var i = 0; i < cards.Length; i++)
            {
                var column = i % 3;
                var row = i / 3;
                cards[i] = ShopCard(buyPage.transform, "ShelfCard_" + i, 32 + column * 466, 220 + row * 300, 450, 284);
            }

            var sellPage = new GameObject("出售库存页", typeof(RectTransform));
            sellPage.transform.SetParent(screen.transform, false);
            At(sellPage, 0, 0, 1920, 1080);
            var sellScroll = Scroll(sellPage.transform, "SellScroll", out var sellContent);
            At(sellScroll, 32, 220, 1380, 800);
            sellContent.sizeDelta = new Vector2(0f, 9 * 156f + 12f);
            var sellSlots = new MonsterSlotView[18];
            for (var i = 0; i < sellSlots.Length; i++)
            {
                var column = i % 2;
                var row = i / 2;
                sellSlots[i] = MonsterCard(sellContent, "SellCard_" + i, 12 + column * 680, 8 + row * 156, 660, 146, 0.85f, "出售");
            }

            var doctor = Box(screen.transform, "博士看板", Panel);
            At(doctor, 1456, 100, 432, 940);
            var doctorBalanceCaption = Label(doctor.transform, "BalanceCaption", "余额", 16, Muted, TextAlignmentOptions.Right);
            At(doctorBalanceCaption, 220, 16, 70, 24);
            var doctorBalance = Label(doctor.transform, "BalanceNumber", "0", 20, Gold, TextAlignmentOptions.Left);
            At(doctorBalance, 300, 12, 100, 28);
            var portrait = Box(doctor.transform, "PortraitImage", PanelDeep);
            At(portrait, 36, 70, 360, 560);
            portrait.raycastTarget = false;
            if (_doctor != null)
            {
                portrait.sprite = _doctor;
                portrait.type = Image.Type.Simple;
                portrait.preserveAspect = true;
                portrait.color = Color.white;
            }
            var doctorName = Label(doctor.transform, "DoctorName", "疯狂博士", 26, Ink, TextAlignmentOptions.Center);
            At(doctorName, 36, 650, 360, 40);
            var flavor = Label(doctor.transform, "DoctorCaption", "古怪博士", 18, Muted, TextAlignmentOptions.Center);
            At(flavor, 36, 740, 360, 32);
            var leave = Click(doctor.transform, "LeaveButton", "返回", ButtonFace, Ink, 22, out _);
            At(leave, 48, 830, 336, 72);

            var artRoot = new GameObject("工具图标", typeof(RectTransform));
            artRoot.transform.SetParent(screen.transform, false);
            var art = new[]
            {
                ToolArt(artRoot.transform, "急急装置", "最先触发的产能技能再执行一次。", Icons.GetValueOrDefault("急急装置")),
                ToolArt(artRoot.transform, "上级员工证", "提取轨增加一格。", Icons.GetValueOrDefault("上级员工证")),
                ToolArt(artRoot.transform, "劣胜装置", "单词条怪物的能量数值加倍。", Icons.GetValueOrDefault("劣胜装置")),
            };
            artRoot.SetActive(false);

            var view = screen.AddComponent<ShopScreenView>();
            view.balanceLabel = balance;
            view.portraitBalanceLabel = doctorBalance;
            view.buyTabButton = buyTab;
            view.sellTabButton = sellTab;
            view.shopButton = shop;
            view.researchButton = research;
            view.saveButton = save;
            view.titleButton = back;
            view.leaveButton = leave;
            view.buyPage = buyPage;
            view.sellPage = sellPage;
            view.cards = cards;
            view.sellSlots = sellSlots;
            view.toolArt = art;
            sellPage.SetActive(false);
            return view;
        }

        static ResultScreenView BuildResult(Transform parent)
        {
            var screen = Screen(parent, "ResultScreen");
            Ambience(screen.transform, 0.28f);
            var card = Box(screen.transform, "PanelImage", Panel);
            At(card, 460, 160, 1000, 760);
            var title = Label(card.transform, "Title", "这一局结束", 40, Ink, TextAlignmentOptions.Center);
            At(title, 60, 48, 880, 64);
            var portrait = SpawnPortrait(card.transform, "Portrait");
            var portraitRect = portrait.GetComponent<RectTransform>();
            At(portraitRect, 358, 160, PortraitViewportWidth, PortraitViewportHeight);
            portraitRect.localScale = new Vector3(2.4f, 2.4f, 1f);
            var body = Label(card.transform, "Body", "", 22, Muted, TextAlignmentOptions.Center);
            At(body, 80, 460, 840, 80);
            var restart = Click(card.transform, "RestartButton", "再来一局", Mint, MintInk, 24, out _);
            At(restart, 310, 600, 380, 72);
            var view = screen.AddComponent<ResultScreenView>();
            view.title = title;
            view.body = body;
            view.portrait = portrait;
            view.restartButton = restart;
            return view;
        }

        static GameObject BuildToast(Transform parent, out TextMeshProUGUI text)
        {
            var toast = Box(parent, "Toast", PanelDeep).gameObject;
            At(toast, 460, 980, 1000, 64);
            text = Label(toast.transform, "Text", "", 20, Ink, TextAlignmentOptions.Center);
            At(text, 16, 12, 968, 40);
            return toast;
        }

        static GameObject CreatePortraitPrefab()
        {
            var root = new GameObject("MonsterPortrait", typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = MonsterPortrait.RecommendedViewportSize;
            var portrait = root.AddComponent<MonsterPortrait>();
            var tailGroup = PivotGroup(root.transform, "TailMotion", new Vector2(0.5f, 0.5f));
            var tail = Layer(tailGroup, "Tail", "Tail");
            var feet = PivotGroup(root.transform, "FeetMotion", new Vector2(0.5f, 0.22f));
            var foot = Layer(feet, "Foot", "Foot");
            var bodyGroup = PivotGroup(root.transform, "BodyMotion", new Vector2(0.5f, 0.46f));
            var body = Layer(bodyGroup, "Body", "Body");
            var hand = Layer(root.transform, "Hand", "Hand");
            var headGroup = PivotGroup(root.transform, "HeadMotion", new Vector2(0.5f, 0.67f));
            var head = Layer(headGroup, "Head", "Head");
            var eye = Layer(headGroup, "Eye", "Eye");
            var mouth = Layer(headGroup, "Mouth", "Mouth");
            var accessory = Layer(root.transform, "Accessory", "Accessory");
            var hat = Layer(root.transform, "Hat", "Hat");
            var serialized = new SerializedObject(portrait);
            serialized.FindProperty("_tail").objectReferenceValue = tail;
            serialized.FindProperty("_foot").objectReferenceValue = foot;
            serialized.FindProperty("_body").objectReferenceValue = body;
            serialized.FindProperty("_hand").objectReferenceValue = hand;
            serialized.FindProperty("_head").objectReferenceValue = head;
            serialized.FindProperty("_eye").objectReferenceValue = eye;
            serialized.FindProperty("_mouth").objectReferenceValue = mouth;
            serialized.FindProperty("_hat").objectReferenceValue = hat;
            serialized.FindProperty("_accessory").objectReferenceValue = accessory;
            serialized.FindProperty("_headGroup").objectReferenceValue = headGroup;
            serialized.FindProperty("_feetGroup").objectReferenceValue = feet;
            serialized.FindProperty("_bodyGroup").objectReferenceValue = bodyGroup;
            serialized.FindProperty("_tailGroup").objectReferenceValue = tailGroup;
            serialized.FindProperty("_motionProfile").objectReferenceValue = EnsureMotionProfile();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PortraitPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static MonsterMotionProfile EnsureMotionProfile()
        {
            const string path = "Assets/Resources/MonsterMotionProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<MonsterMotionProfile>(path);
            if (profile != null)
            {
                profile.SetDefaultsIfUninitialized();
                EditorUtility.SetDirty(profile);
                return profile;
            }

            profile = ScriptableObject.CreateInstance<MonsterMotionProfile>();
            profile.SetDefaultsIfUninitialized();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static Transform PivotGroup(Transform parent, string name, Vector2 pivot)
        {
            var group = new GameObject(name, typeof(RectTransform));
            group.transform.SetParent(parent, false);
            var rect = group.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = pivot;
            return group.transform;
        }

        static Image Layer(Transform parent, string name, string folder)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image.sprite = FirstSprite("Assets/Resources/MonsterParts/" + folder);
            image.color = Color.white;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.gameObject.SetActive(false);
            return image;
        }

        static MonsterPortrait SpawnPortrait(Transform parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(_portraitPrefab, parent);
            instance.name = name;
            return instance.GetComponent<MonsterPortrait>();
        }

        static MonsterSlotView MonsterCard(Transform parent, string name, float x, float y, float w, float h, float scale, string action, bool glassy = false)
        {
            if (MonsterSlotPrefabLibrary.TryLoadPrefab(w, h, action, glassy, out var slotPrefab))
                return PlaceMonsterSlotPrefab(parent, name, x, y, w, h, slotPrefab);

            var button = Click(parent, name, "", Panel, Ink, 18, out var unusedLabel);
            unusedLabel.gameObject.SetActive(false);
            At(button, x, y, w, h);
            var background = button.GetComponent<Image>();
            var plate = glassy ? _soft : _panel;
            if (plate != null)
            {
                background.sprite = plate;
                background.type = Image.Type.Sliced;
                background.color = Color.white;
                background.pixelsPerUnitMultiplier = glassy ? 1f : 1.1f;
            }

            background.raycastTarget = true;
            var selection = StretchChild(button.transform, "Selection", new Color(1f, 0.82f, 0.35f, 0.28f));
            selection.raycastTarget = false;
            selection.gameObject.SetActive(false);
            var portrait = SpawnPortrait(button.transform, "Portrait");
            var portraitRect = portrait.GetComponent<RectTransform>();
            var visualW = PortraitViewportWidth * scale;
            var wide = w > h * 2.2f;
            var portraitX = wide ? 12f : Mathf.Max(8f, (w - visualW) * 0.5f);
            At(portraitRect, portraitX, 8f, PortraitViewportWidth, PortraitViewportHeight);
            portraitRect.localScale = new Vector3(scale, scale, 1f);
            var empty = Label(button.transform, "EmptyMark", "?", 36, Muted, TextAlignmentOptions.Center);
            At(empty, 0, h * 0.28f, w, 48);
            empty.gameObject.SetActive(false);
            var title = Label(button.transform, "Title", "技能", 16, Ink, TextAlignmentOptions.Center);
            var subtitle = Label(button.transform, "Subtitle", "", 13, Muted, TextAlignmentOptions.Center);
            if (string.IsNullOrEmpty(action))
            {
                At(title, 8, h - 52, w - 16, 24);
                At(subtitle, 8, h - 28, w - 16, 20);
            }
            else if (wide)
            {
                At(title, portraitX + visualW + 12f, 28, w - visualW - 180f, 28);
                At(subtitle, portraitX + visualW + 12f, 58, w - visualW - 180f, 22);
                var bar = Box(button.transform, "ActionBar", Mint);
                At(bar, w - 140f, (h - 44f) * 0.5f, 124f, 44f);
                bar.raycastTarget = false;
                var actionLabel = Label(bar.transform, "Label", action, 18, MintInk, TextAlignmentOptions.Center);
                At(actionLabel, 0, 6, 124, 32);
            }
            else
            {
                var portraitBottom = 8f + PortraitViewportHeight * scale;
                At(title, 8, portraitBottom + 12f, w - 16, 28);
                At(subtitle, 8, portraitBottom + 42f, w - 16, 22);
                var bar = Box(button.transform, "ActionBar", Mint);
                At(bar, 16, h - 64, w - 32, 48);
                bar.raycastTarget = false;
                var actionLabel = Label(bar.transform, "Label", action, 18, MintInk, TextAlignmentOptions.Center);
                At(actionLabel, 0, 8, w - 32, 32);
            }

            var view = button.gameObject.AddComponent<MonsterSlotView>();
            view.button = button;
            view.portrait = portrait;
            view.title = title;
            view.subtitle = subtitle;
            view.emptyMark = empty.gameObject;
            view.selection = selection.gameObject;
            return view;
        }

        static MonsterSlotView PlaceMonsterSlotPrefab(
            Transform parent,
            string name,
            float x,
            float y,
            float w,
            float h,
            GameObject prefab)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
                At(rect, x, y, w, h);

            var view = instance.GetComponent<MonsterSlotView>();
            if (view == null)
                throw new InvalidOperationException("Monster slot prefab is missing MonsterSlotView: " + prefab.name);

            return view;
        }

        static SkillChipView Chip(Transform parent, string name, float x, float y, float w, float h)
        {
            var button = Click(parent, name, "空", ButtonFace, Ink, 16, out var label);
            At(button, x, y, w, h);
            var selection = StretchChild(button.transform, "Selection", new Color(1f, 0.82f, 0.35f, 0.35f));
            selection.raycastTarget = false;
            selection.transform.SetAsFirstSibling();
            selection.gameObject.SetActive(false);
            var view = button.gameObject.AddComponent<SkillChipView>();
            view.button = button;
            view.label = label;
            view.selection = selection.gameObject;
            return view;
        }

        static ShopCardView ShopCard(Transform parent, string name, float x, float y, float w, float h)
        {
            var panel = Box(parent, name, Panel);
            At(panel, x, y, w, h);
            var title = Label(panel.transform, "Title", "商品", 22, Ink, TextAlignmentOptions.Left);
            At(title, 20, 18, 250, 32);
            var price = Label(panel.transform, "Price", "0 金币", 20, Gold, TextAlignmentOptions.Right);
            At(price, 250, 18, 180, 32);
            var body = Label(panel.transform, "Body", "", 16, Muted, TextAlignmentOptions.TopLeft);
            At(body, 20, 64, 240, 110);
            var stock = Label(panel.transform, "Stock", "本轮剩余 1 件", 15, Muted, TextAlignmentOptions.Left);
            At(stock, 20, 190, 200, 24);
            var portrait = SpawnPortrait(panel.transform, "Portrait");
            var portraitRect = portrait.GetComponent<RectTransform>();
            At(portraitRect, 300, 70, PortraitViewportWidth, PortraitViewportHeight);
            portraitRect.localScale = new Vector3(1.15f, 1.15f, 1f);
            var icon = Box(panel.transform, "IconImage", Color.white);
            At(icon, 300, 70, 128, 128);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.type = Image.Type.Simple;
            icon.enabled = false;
            var buy = Click(panel.transform, "BuyButton", "购买", ButtonFace, Ink, 16, out var buyLabel);
            At(buy, 280, 214, 146, 48);
            var view = panel.gameObject.AddComponent<ShopCardView>();
            view.buyButton = buy;
            view.buyLabel = buyLabel;
            view.title = title;
            view.body = body;
            view.price = price;
            view.stock = stock;
            view.icon = icon;
            view.portrait = portrait;
            return view;
        }

        static TechNodeView Tech(Transform parent, string objectName, string techName, string summary, float x, float y)
        {
            var button = Click(parent, objectName, "", Panel, Ink, 22, out var title);
            title.text = techName;
            At(button, x, y, 240, 190);
            At(title, 16, 96, 208, 36);
            var cost = Label(button.transform, "Cost", "消耗 1 科技点", 15, Muted, TextAlignmentOptions.Center);
            At(cost, 16, 138, 208, 28);
            var icon = Box(button.transform, "IconImage", Color.white);
            At(icon, 70, 8, 100, 82);
            icon.raycastTarget = false;
            icon.enabled = false;
            if (Icons.TryGetValue(techName, out var iconSprite))
            {
                icon.sprite = iconSprite;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
                icon.color = Color.white;
                icon.enabled = true;
            }
            var selection = StretchChild(button.transform, "Selection", new Color(1f, 0.86f, 0.45f, 0.22f));
            selection.raycastTarget = false;
            selection.transform.SetAsFirstSibling();
            selection.gameObject.SetActive(false);
            var view = button.gameObject.AddComponent<TechNodeView>();
            view.techName = techName;
            view.summary = summary;
            view.button = button;
            view.title = title;
            view.costLabel = cost;
            view.selection = selection.gameObject;
            return view;
        }

        static ToolArtView ToolArt(Transform parent, string toolName, string description, Sprite icon)
        {
            var go = new GameObject(toolName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ToolArtView>();
            view.toolName = toolName;
            view.description = description;
            view.icon = icon;
            return view;
        }

        static GameObject Screen(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var background = StretchChild(go.transform, "BackgroundImage", Stage);
            if (_stage != null)
            {
                background.sprite = _stage;
                background.type = Image.Type.Simple;
                background.color = Color.white;
            }

            background.raycastTarget = false;
            return go;
        }

        static RectTransform Scroll(Transform parent, string name, out RectTransform content, bool bar = false)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var scroll = root.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            var viewportObject = new GameObject("Viewport", typeof(RectTransform));
            viewportObject.transform.SetParent(root.transform, false);
            Stretch(viewportObject.GetComponent<RectTransform>());
            var viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.02f);
            viewportImage.raycastTarget = true;
            viewportObject.AddComponent<Mask>().showMaskGraphic = false;
            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportObject.transform, false);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 800f);
            scroll.viewport = viewportObject.GetComponent<RectTransform>();
            scroll.content = content;
            if (bar)
                AddVerticalBar(scroll);

            return root.GetComponent<RectTransform>();
        }

        static void AddVerticalBar(ScrollRect scroll)
        {
            var viewport = scroll.viewport;
            viewport.offsetMax = new Vector2(-16f, viewport.offsetMax.y);
            var barObject = new GameObject("Scrollbar", typeof(RectTransform));
            barObject.transform.SetParent(scroll.transform, false);
            var barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 1f);
            barRect.sizeDelta = new Vector2(14f, 0f);
            barRect.anchoredPosition = Vector2.zero;
            var barImage = barObject.AddComponent<Image>();
            barImage.color = PanelDeep;
            var scrollbar = barObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var sliding = new GameObject("Sliding Area", typeof(RectTransform));
            sliding.transform.SetParent(barObject.transform, false);
            Stretch(sliding.GetComponent<RectTransform>());
            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(sliding.transform, false);
            var handleRect = handle.GetComponent<RectTransform>();
            Stretch(handleRect);
            handleRect.offsetMin = new Vector2(2f, 2f);
            handleRect.offsetMax = new Vector2(-2f, -2f);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = Muted;
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        static void Zone(GameObject go, LandingPlace place, MonsterSlotView monster, SkillChipView chip)
        {
            var zone = go.GetComponent<OperationZone>();
            if (zone == null)
                zone = go.AddComponent<OperationZone>();
            zone.Place = place;
            zone.monsterView = monster;
            zone.chipView = chip;
        }

        static void Drag(GameObject go, PayloadKind kind, OperationPointer session)
        {
            var drag = go.GetComponent<OperationPayloadDrag>();
            if (drag == null)
                drag = go.AddComponent<OperationPayloadDrag>();
            drag.payloadKind = kind;
            drag.session = session;
        }

        static void Pipe(Transform parent, string name, float x, float y, float w, float h)
        {
            var image = Box(parent, name, new Color(0.93f, 0.72f, 0.86f, 0.9f));
            At(image, x, y, w, h);
            image.raycastTarget = false;
            if (_pipe != null)
            {
                image.sprite = _pipe;
                image.type = Image.Type.Sliced;
                image.color = new Color(0.96f, 0.74f, 0.86f, 1f);
                image.pixelsPerUnitMultiplier = 1f;
            }
        }

        static Button Click(Transform parent, string name, string text, Color background, Color foreground, float fontSize, out TextMeshProUGUI label)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = background;
            image.raycastTarget = true;
            var face = Face(background);
            if (face != null)
            {
                image.sprite = face;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
                image.pixelsPerUnitMultiplier = 1f;
            }
            else if (_ui != null)
            {
                image.sprite = _ui;
                image.type = Image.Type.Sliced;
            }

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.fadeDuration = 0.08f;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);
            button.colors = colors;
            label = Label(go.transform, "Label", text, fontSize, foreground, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(8f, 4f);
            label.rectTransform.offsetMax = new Vector2(-8f, -4f);
            return button;
        }

        static Image Box(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (_panel != null && Same(color, Panel))
            {
                image.sprite = _panel;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
                image.pixelsPerUnitMultiplier = 1.15f;
            }
            else if (_panel != null && Same(color, PanelDeep))
            {
                image.sprite = _panel;
                image.type = Image.Type.Sliced;
                image.color = new Color(0.78f, 0.86f, 0.9f, 1f);
                image.pixelsPerUnitMultiplier = 1.15f;
            }
            else if (_ui != null)
            {
                image.sprite = _ui;
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        static Image StretchChild(Transform parent, string name, Color color)
        {
            var image = Box(parent, name, color);
            Stretch(image.rectTransform);
            return image;
        }

        static TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            if (_font != null)
                label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        static void At(GameObject gameObject, float x, float y, float w, float h) => At(gameObject.transform, x, y, w, h);

        static void At(Component component, float x, float y, float w, float h)
        {
            var rect = component.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        static void Center(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        static void Ambience(Transform parent, float alpha)
        {
            if (_breed == null)
                return;

            var image = StretchChild(parent, "Ambience", Color.white);
            image.sprite = _breed;
            image.type = Image.Type.Simple;
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
        }

        static void PaintRoom(Image panel, Sprite art)
        {
            if (art == null)
                return;

            panel.sprite = art;
            panel.type = Image.Type.Simple;
            panel.color = Color.white;
            panel.pixelsPerUnitMultiplier = 1f;
            if (_frame == null)
                return;

            var frame = new GameObject("Frame", typeof(RectTransform)).AddComponent<Image>();
            frame.transform.SetParent(panel.transform, false);
            Stretch(frame.rectTransform);
            frame.sprite = _frame;
            frame.type = Image.Type.Sliced;
            frame.color = Color.white;
            frame.pixelsPerUnitMultiplier = 1.2f;
            frame.raycastTarget = false;
        }

        static void CaptionShade(Transform parent)
        {
            var shade = Box(parent, "CaptionShade", new Color(0f, 0f, 0f, 0.45f));
            At(shade, 16, 10, 168, 42);
            shade.raycastTarget = false;
        }

        static Image Icon(Transform parent, string name, string key, float x, float y, float w, float h)
        {
            var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            At(image, x, y, w, h);
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = Color.white;
            if (Icons.TryGetValue(key, out var sprite) && sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
            }

            return image;
        }


        static Sprite Face(Color background)
        {
            if (_btnMint != null && Same(background, Mint))
                return _btnMint;
            if (_btnDanger != null && Same(background, Danger))
                return _btnDanger;
            return _btnDark;
        }


        static bool Same(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.03f
                   && Mathf.Abs(a.g - b.g) < 0.03f
                   && Mathf.Abs(a.b - b.b) < 0.03f;
        }

        static void ConfigureUi()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art"))
                AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder(ArtDir))
                return;

            AssetDatabase.Refresh();
            var borders = new Dictionary<string, Vector4>
            {
                ["Panel.png"] = new Vector4(16, 16, 16, 16),
                ["PanelSoft.png"] = new Vector4(16, 16, 16, 16),
                ["ButtonMint.png"] = new Vector4(16, 16, 16, 16),
                ["ButtonDark.png"] = new Vector4(16, 16, 16, 16),
                ["ButtonDanger.png"] = new Vector4(16, 16, 16, 16),
                ["Frame.png"] = new Vector4(18, 18, 18, 18),
                ["Pipe.png"] = new Vector4(12, 6, 12, 6),
            };
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtDir }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                        continue;

                    var file = System.IO.Path.GetFileName(path);
                    borders.TryGetValue(file, out var border);
                    if (UiImportMatches(importer, border))
                        continue;

                    ApplyUiImport(importer, border);
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        static bool UiImportMatches(TextureImporter importer, Vector4 border)
        {
            return importer.textureType == TextureImporterType.Sprite
                   && importer.spriteImportMode == SpriteImportMode.Single
                   && importer.alphaIsTransparency
                   && !importer.mipmapEnabled
                   && importer.filterMode == FilterMode.Bilinear
                   && Mathf.Approximately(importer.spritePixelsPerUnit, 100f)
                   && importer.spriteBorder == border;
        }

        static void ApplyUiImport(TextureImporter importer, Vector4 border)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.alphaIsTransparency = true;
            settings.mipmapEnabled = false;
            settings.filterMode = FilterMode.Bilinear;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePixelsPerUnit = 100f;
            settings.spriteBorder = border;
            importer.SetTextureSettings(settings);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        static void LoadUi()
        {
            _stage = LoadSprite("BgStage.png");
            _breed = LoadSprite("BgBreed.png");
            _production = LoadSprite("BgProduction.png");
            _panel = LoadSprite("Panel.png");
            _frame = LoadSprite("Frame.png");
            _soft = LoadSprite("PanelSoft.png");
            _btnMint = LoadSprite("ButtonMint.png");
            _btnDark = LoadSprite("ButtonDark.png");
            _btnDanger = LoadSprite("ButtonDanger.png");
            _pipe = LoadSprite("Pipe.png");
            _doctor = LoadSprite("Doctor.png");
            Icons.Clear();
            Icons["急急装置"] = LoadSprite("IconRocket.png");
            Icons["上级员工证"] = LoadSprite("IconBadge.png");
            Icons["劣胜装置"] = LoadSprite("IconBolt.png");
            Icons["基因实验"] = LoadSprite("IconGene.png");
            Icons["槽位扩容"] = LoadSprite("IconSlot.png");
            Icons["变异学说"] = LoadSprite("IconMutant.png");
            Icons["大乱炖"] = LoadSprite("IconStew.png");
            Icons["科学培育"] = LoadSprite("IconFlask.png");
            Icons["recycle"] = LoadSprite("IconRecycle.png");
            Icons["trash"] = LoadSprite("IconTrash.png");
        }

        static Sprite LoadSprite(string file)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ArtDir + "/" + file))
            {
                if (asset is Sprite sprite)
                    return sprite;
            }

            return null;
        }

        static void ConfigureParts()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/MonsterParts" });
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                        continue;

                    if (PartImportSettingsMatch(importer))
                        continue;

                    ApplyPartImportSettings(importer);
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        static bool PartImportSettingsMatch(TextureImporter importer)
        {
            return importer.textureType == TextureImporterType.Sprite
                   && importer.spriteImportMode == SpriteImportMode.Single
                   && importer.alphaIsTransparency
                   && !importer.mipmapEnabled
                   && importer.filterMode == FilterMode.Point
                   && Mathf.Approximately(importer.spritePixelsPerUnit, 100f)
                   && importer.textureCompression == TextureImporterCompression.Uncompressed;
        }

        static void ApplyPartImportSettings(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.alphaIsTransparency = true;
            settings.mipmapEnabled = false;
            settings.filterMode = FilterMode.Point;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spritePixelsPerUnit = 100f;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.spritePixelsPerUnit = 100f;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        static Sprite FirstSprite(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                return null;

            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            paths.Sort(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite sprite)
                        return sprite;
                }
            }

            return null;
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(parent))
            {
                var slash = parent.LastIndexOf('/');
                EnsureFolder(parent.Substring(0, slash), parent.Substring(slash + 1));
            }

            if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                AssetDatabase.CreateFolder(parent, child);
        }

        static void DestroyNamed(string name)
        {
            var objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
            foreach (var candidate in objects)
            {
                if (candidate != null && candidate.name == name && candidate.transform.parent == null)
                    UnityEngine.Object.DestroyImmediate(candidate);
            }
        }
    }

    [CustomEditor(typeof(TheCallPresentation))]
    public sealed class TheCallPresentationEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "界面已经放在场景里。点下面的按钮只显示其中一个。怪物立绘看 Portrait 上的矩形：里面是运行时那套拼接怪物，改矩形的位置和大小，游戏里和拖拽松手后的位置会一起变。重建界面只用于重新生成这套演示，不要覆盖已经人工调整的场景内容。", 
                MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("开局"))
                    ShowOnly("_opening");
                if (GUILayout.Button("关卡"))
                    ShowOnly("_levelStart");
                if (GUILayout.Button("主界面"))
                    ShowOnly("_operation");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("科研"))
                    ShowOnly("_research");
                if (GUILayout.Button("商店"))
                    ShowOnly("_shop");
                if (GUILayout.Button("结算"))
                    ShowOnly("_result");
            }
        }

        void ShowOnly(string propertyName)
        {
            var serialized = new SerializedObject(target);
            string[] names = { "_opening", "_levelStart", "_operation", "_research", "_shop", "_result" };
            foreach (var name in names)
            {
                var property = serialized.FindProperty(name);
                if (property?.objectReferenceValue is not MonoBehaviour view)
                    continue;

                Undo.RecordObject(view.gameObject, "显示界面");
                view.gameObject.SetActive(name == propertyName);
            }

            var toast = serialized.FindProperty("_toast");
            if (toast?.objectReferenceValue is GameObject toastObject)
                toastObject.SetActive(false);
        }
    }
}
