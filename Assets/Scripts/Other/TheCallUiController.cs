using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TheCall;

/// <summary>
/// Runtime presentation layer for the prototype scene. The scene builder supplies the visual skin;
/// this controller owns only screen state, command routing and lightweight feedback.
/// </summary>
public sealed class TheCallUiController : MonoBehaviour
{
    [Serializable]
    public sealed class Skin
    {
        public Sprite panel;
        public Sprite paper;
        public Sprite title;
        public Sprite slot;
        public Sprite slotSelected;
        public Sprite button;
        public Sprite buttonHover;
        public Sprite buttonPressed;
        public Sprite buttonDisabled;
        public Sprite close;
        public Sprite tooltip;
        public Sprite coin;
        public Sprite tech;
        public Sprite monster;
        public TMP_FontAsset font;
        public Texture2D background;
    }

    [SerializeField] Skin skin;
    [SerializeField] RectTransform root;
    [SerializeField] float refreshInterval = 0.15f;
    [SerializeField] bool buildOnAwake = true;

    readonly Color32 ink = new Color32(0x55, 0x41, 0x4A, 0xFF);
    readonly Color32 cream = new Color32(0xF4, 0xDF, 0xB3, 0xFF);
    readonly Color32 muted = new Color32(0x8C, 0x7F, 0x76, 0xFF);
    readonly Color32 warning = new Color32(0xD8, 0x88, 0x66, 0xFF);

    GameObject opening;
    GameObject operation;
    GameObject research;
    GameObject shop;
    GameObject result;
    GameObject toast;
    TMP_Text phaseLabel;
    TMP_Text energyLabel;
    TMP_Text goldLabel;
    TMP_Text techLabel;
    TMP_Text targetLabel;
    TMP_Text toastLabel;
    RectTransform operationWindow;
    RectTransform cageGrid;
    RectTransform extractionGrid;
    RectTransform breedingGrid;
    RectTransform skillGrid;
    RectTransform researchGrid;
    RectTransform shopGrid;
    RectTransform resultList;
    Button confirmButton;
    Button shopLeaveButton;
    Button openingButton;
    int selectedOpening;
    string selectedMonster;
    RunPhase lastPhase = (RunPhase)(-1);
    float nextRefresh;

    public void Configure(Skin value, RectTransform parent)
    {
        skin = value;
        root = parent;
        if (isActiveAndEnabled && buildOnAwake)
            Build();
    }

    void Awake()
    {
        if (root == null)
            root = transform as RectTransform;
        if (buildOnAwake)
            Build();
    }

    void Update()
    {
        if (Time.unscaledTime < nextRefresh)
            return;

        nextRefresh = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    public void Build()
    {
        if (root == null || skin == null || skin.font == null)
            return;

        ClearGenerated();
        BuildChrome();
        BuildOpening();
        BuildOperation();
        BuildResearch();
        BuildShop();
        BuildResult();
        BuildToast();
        Refresh();
    }

    void BuildChrome()
    {
        var bar = Node("RunStatus", root);
        Stretch(bar);
        bar.offsetMin = new Vector2(36f, -86f);
        bar.offsetMax = new Vector2(-36f, 0f);

        var barPaper = ImageNode("Paper", bar.transform, skin.paper, false);
        Stretch(barPaper.GetComponent<RectTransform>());
        barPaper.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.92f);

        phaseLabel = Label("Phase", bar, "OPENING", 28f, TextAlignmentOptions.MidlineLeft, ink);
        Anchor(phaseLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), 18f, 0f, 250f, 0f);
        targetLabel = Label("Target", bar, "", 22f, TextAlignmentOptions.MidlineLeft, muted);
        Anchor(targetLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), 270f, 0f, 390f, 0f);

        phaseLabel = phaseLabel;
        energyLabel = Counter("Energy", bar, "ENERGY", 520f, skin.coin);
        goldLabel = Counter("Gold", bar, "GOLD", 760f, skin.coin);
        techLabel = Counter("Tech", bar, "TECH", 1000f, skin.tech);
    }

    TMP_Text Counter(string name, Transform parent, string caption, float x, Sprite icon)
    {
        var holder = Node(name, parent);
        Anchor(holder, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), x, 0f, 190f, 0f);
        var iconGo = ImageNode("Icon", holder, icon, false);
        Anchor(iconGo.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), 0f, 0f, 30f, 30f);
        var value = Label("Value", holder, "0", 28f, TextAlignmentOptions.MidlineLeft, ink);
        Anchor(value.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), 38f, 0f, -4f, 0f);
        var cap = Label("Caption", holder, caption, 14f, TextAlignmentOptions.MidlineLeft, muted);
        Anchor(cap.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), 38f, 22f, -4f, 2f);
        return value;
    }

    void BuildOpening()
    {
        opening = Window("Opening", root, "THE CALL", 760f, 560f);
        var prompt = Label("Prompt", opening.transform, "选择一只怪物，开始今天的实验", 32f, TextAlignmentOptions.Center, ink);
        Anchor(prompt.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 26f, -82f, -26f, -42f);
        var cards = Grid("Candidates", opening.transform, new Vector2Int(3, 1), 176f, 142f, 18f);
        Anchor(cards, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), 48f, -90f, -48f, 70f);
        for (var i = 0; i < 3; i++)
        {
            var card = Card("Candidate" + i, cards, "怪物", skin.monster, 150f, 116f);
            var button = card.GetComponent<Button>();
            var index = i;
            button.onClick.AddListener(() => SelectOpening(index));
        }
        var action = ButtonNode("Keep", opening.transform, "KEEP", 180f, 44f);
        Anchor(action.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), -90f, 42f, 180f, 44f);
        openingButton = action;
        action.onClick.AddListener(KeepOpening);
    }

    void BuildOperation()
    {
        operation = Window("Operation", root, "OPERATION", 1120f, 610f);
        operationWindow = operation.GetComponent<RectTransform>();
        var instruction = Label("Instruction", operation.transform, "拖动或点击怪物安排提取与培育", 22f, TextAlignmentOptions.MidlineLeft, muted);
        Anchor(instruction.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 28f, -80f, -28f, -48f);

        var cage = Panel("Cage", operation.transform, "MONSTER CAGE", 274f, 374f);
        Anchor(cage.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), 30f, -8f, 274f, 374f);
        cageGrid = Grid("Grid", cage.transform, new Vector2Int(4, 3), 50f, 50f, 10f);
        Anchor(cageGrid, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), 18f, 22f, -18f, -44f);

        var board = Panel("Board", operation.transform, "EXTRACTION", 770f, 180f);
        Anchor(board.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -15f, 86f, 770f, 180f);
        extractionGrid = Grid("Grid", board.transform, new Vector2Int(5, 1), 110f, 92f, 10f);
        Anchor(extractionGrid, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), 26f, 20f, -26f, -46f);

        var breeding = Panel("Breeding", operation.transform, "BREEDING", 770f, 156f);
        Anchor(breeding.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -15f, -136f, 770f, 156f);
        breedingGrid = Grid("Grid", breeding.transform, new Vector2Int(4, 1), 110f, 76f, 10f);
        Anchor(breedingGrid, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), 26f, 20f, -26f, -42f);

        var skill = Panel("Skills", operation.transform, "SKILL SLOT", 274f, 140f);
        Anchor(skill.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), 30f, -220f, 274f, 140f);
        skillGrid = Grid("Grid", skill.transform, new Vector2Int(3, 1), 58f, 58f, 8f);
        Anchor(skillGrid, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), 14f, 16f, -14f, -42f);

        var researchButton = ButtonNode("ResearchButton", operation.transform, "RESEARCH", 150f, 40f);
        Anchor(researchButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), -200f, 38f, 150f, 40f);
        researchButton.onClick.AddListener(() => SetModal(research, true));
        confirmButton = ButtonNode("Confirm", operation.transform, "SETTLE", 150f, 40f);
        Anchor(confirmButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), -30f, 38f, 150f, 40f);
        confirmButton.onClick.AddListener(ConfirmSettlement);
    }

    void BuildResearch()
    {
        research = Window("Research", root, "RESEARCH", 850f, 520f);
        var points = Label("Points", research.transform, "TECH POINTS  0", 24f, TextAlignmentOptions.MidlineLeft, ink);
        Anchor(points.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 28f, -78f, -28f, -42f);
        researchGrid = Grid("Grid", research.transform, new Vector2Int(1, 5), 760f, 64f, 8f);
        Anchor(researchGrid, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), 38f, 54f, -38f, -104f);
        for (var i = 0; i < TechCatalog.Names.Length; i++)
        {
            var row = Card("Tech" + i, researchGrid, TechCatalog.Names[i], skin.tech, 760f, 56f);
            var index = i;
            row.GetComponent<Button>().onClick.AddListener(() => UnlockTech(index));
        }
        var close = ButtonNode("Close", research.transform, "CLOSE", 140f, 38f);
        Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 0f, 16f, 140f, 38f);
        close.onClick.AddListener(() => SetModal(research, false));
    }

    void BuildShop()
    {
        shop = Window("Shop", root, "SHOP", 980f, 520f);
        var shelf = Panel("Shelf", shop.transform, "SHELF", 900f, 260f);
        Anchor(shelf.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, 56f, 900f, 260f);
        shopGrid = Grid("Grid", shelf.transform, new Vector2Int(4, 1), 180f, 156f, 12f);
        Anchor(shopGrid, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), 26f, 34f, -26f, -56f);
        for (var i = 0; i < 4; i++)
        {
            var item = Card("Shelf" + i, shopGrid, "待补货", skin.monster, 180f, 156f);
            var index = i;
            item.GetComponent<Button>().onClick.AddListener(() => BuyMonster(index));
        }
        var footer = Label("Footer", shop.transform, "不买也可以离开。货架离店后刷新。", 20f, TextAlignmentOptions.Center, muted);
        Anchor(footer.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), 30f, 70f, -30f, 28f);
        shopLeaveButton = ButtonNode("Leave", shop.transform, "LEAVE SHOP", 180f, 44f);
        Anchor(shopLeaveButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 0f, 18f, 180f, 44f);
        shopLeaveButton.onClick.AddListener(LeaveShop);
    }

    void BuildResult()
    {
        result = Window("Result", root, "RESULT", 720f, 470f);
        var headline = Label("Headline", result.transform, "", 38f, TextAlignmentOptions.Center, ink);
        Anchor(headline.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), 26f, -88f, -26f, -44f);
        resultList = Grid("List", result.transform, new Vector2Int(1, 5), 600f, 42f, 5f);
        Anchor(resultList, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), 50f, -54f, -50f, 76f);
    }

    void BuildToast()
    {
        toast = Node("Toast", root).gameObject;
        var paper = ImageNode("Paper", toast.transform, skin.tooltip != null ? skin.tooltip : skin.paper, false);
        Stretch(paper.GetComponent<RectTransform>());
        toastLabel = Label("Message", toast.transform, "", 22f, TextAlignmentOptions.Center, cream);
        Stretch(toastLabel.rectTransform);
        var rt = toast.GetComponent<RectTransform>();
        Anchor(rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), 0f, 34f, 420f, 56f);
        toast.SetActive(false);
    }

    void Refresh()
    {
        if (!Application.isPlaying || root == null)
            return;

        var phase = TheCallApp.Interface.SendQuery(new RunPhaseQuery());
        var ledger = TheCallApp.Interface.SendQuery(new RunLedgerQuery());
        var level = TheCallApp.Interface.SendQuery(new LevelTargetQuery());
        phaseLabel.text = PhaseName(phase);
        goldLabel.text = ledger.Gold.ToString();
        techLabel.text = ledger.TechPoints.ToString();
        energyLabel.text = TheCallApp.Interface.SendQuery(new LevelEnergyQuery()).ToString();
        targetLabel.text = level == null ? "" : "TARGET " + level.EnergyDue;

        if (phase != lastPhase)
        {
            lastPhase = phase;
            RefreshPhase(phase);
            ShowMessage(PhaseMessage(phase));
        }

        if (phase == RunPhase.Opening)
            RefreshOpening();
        if (phase == RunPhase.Operation || phase == RunPhase.LevelStart)
            RefreshOperation();
        if (phase == RunPhase.Shop)
            RefreshShop();
        if (phase == RunPhase.Victory || phase == RunPhase.Failed)
            RefreshResult(phase);
    }

    void RefreshPhase(RunPhase phase)
    {
        SetModal(opening, phase == RunPhase.Opening);
        SetModal(operation, phase == RunPhase.Operation || phase == RunPhase.LevelStart);
        SetModal(shop, phase == RunPhase.Shop);
        SetModal(result, phase == RunPhase.Victory || phase == RunPhase.Failed);
        if (phase != RunPhase.Operation && phase != RunPhase.LevelStart)
            SetModal(research, false);
        if (phase == RunPhase.LevelStart)
            TheCallApp.Interface.SendCommand(new BeginLevelCommand());
    }

    void RefreshOpening()
    {
        var candidates = TheCallApp.Interface.SendQuery(new OpeningCandidatesQuery());
        if (candidates == null)
            return;
        for (var i = 0; i < candidates.Count && i < opening.transform.Find("Candidates/Grid").childCount; i++)
            FillCard(opening.transform.Find("Candidates/Grid").GetChild(i), candidates[i].Skills.FirstOrDefault()?.Name ?? "未知");
    }

    void RefreshOperation()
    {
        FillGrid(cageGrid, TheCallApp.Interface.SendQuery(new MonsterCageQuery()), true);
        FillExtraction();
        FillBreeding();
        FillSkills();
    }

    void RefreshShop()
    {
        var shelf = TheCallApp.Interface.SendQuery(new ShelfQuery());
        if (shelf == null)
            return;
        for (var i = 0; i < shopGrid.childCount; i++)
        {
            var child = shopGrid.GetChild(i);
            if (i < shelf.Monsters.Count)
            {
                var monster = shelf.Monsters[i];
                FillCard(child, string.Join(" · ", monster.SkillNames) + "\n" + monster.Price + " G");
                child.gameObject.SetActive(true);
            }
            else
                child.gameObject.SetActive(false);
        }
    }

    void RefreshResult(RunPhase phase)
    {
        var headline = result.transform.Find("Headline").GetComponent<TMP_Text>();
        headline.text = phase == RunPhase.Victory ? "SEVEN DAYS COMPLETE" : "THE CALL ENDS";
        var entries = TheCallApp.Interface.SendQuery(new SettlementRecordQuery());
        for (var i = 0; i < resultList.childCount; i++)
        {
            var label = resultList.GetChild(i).GetComponentInChildren<TMP_Text>();
            label.text = i < entries.Count ? EntryText(entries[i]) : "";
        }
    }

    void FillExtraction()
    {
        var slots = TheCallApp.Interface.SendQuery(new ExtractionSlotsQuery());
        for (var i = 0; i < extractionGrid.childCount; i++)
        {
            var id = i < slots.Count ? slots[i].MonsterId : null;
            FillSlot(extractionGrid.GetChild(i), id, () => ReturnMonster(id));
        }
    }

    void FillBreeding()
    {
        var slots = TheCallApp.Interface.SendQuery(new BreedingSlotsQuery());
        for (var i = 0; i < breedingGrid.childCount; i++)
        {
            var id = i < slots.Count ? slots[i].MonsterId : null;
            FillSlot(breedingGrid.GetChild(i), id, () => ReturnMonster(id));
        }
    }

    void FillSkills()
    {
        var ledger = TheCallApp.Interface.SendQuery(new RunLedgerQuery());
        for (var i = 0; i < skillGrid.childCount; i++)
        {
            var label = skillGrid.GetChild(i).GetComponentInChildren<TMP_Text>();
            label.text = i < ledger.SkillSlots.Count ? ledger.SkillSlots[i] : "空";
            skillGrid.GetChild(i).gameObject.SetActive(true);
        }
    }

    void FillGrid(RectTransform grid, IReadOnlyList<MonsterView> monsters, bool selectable)
    {
        for (var i = 0; i < grid.childCount; i++)
        {
            if (i < monsters.Count)
            {
                FillCard(grid.GetChild(i), monsters[i].Skills.Count == 0 ? "未知" : string.Join(" · ", monsters[i].SkillNames));
                var id = monsters[i].Id;
                var button = grid.GetChild(i).GetComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectMonster(id));
                grid.GetChild(i).gameObject.SetActive(true);
            }
            else
                grid.GetChild(i).gameObject.SetActive(false);
        }
    }

    void SelectOpening(int index)
    {
        selectedOpening = index;
        if (openingButton != null)
            openingButton.interactable = true;
        ShowMessage("候选 " + (index + 1) + " 已选中");
    }

    void KeepOpening()
    {
        var candidates = TheCallApp.Interface.SendQuery(new OpeningCandidatesQuery());
        if (candidates == null || selectedOpening >= candidates.Count)
            return;
        TheCallApp.Interface.SendCommand(new KeepOpeningMonsterCommand(candidates[selectedOpening].Id));
    }

    void SelectMonster(string id)
    {
        selectedMonster = id;
        ShowMessage("已选择怪物，点击提取槽或培育槽放置");
    }

    void ConfirmSettlement() => TheCallApp.Interface.SendCommand(new ConfirmSettlementCommand());

    void LeaveShop() => TheCallApp.Interface.SendCommand(new LeaveShopCommand());

    void ReturnMonster(string id)
    {
        if (!string.IsNullOrEmpty(id))
            TheCallApp.Interface.SendCommand(new ReturnMonsterCommand(id));
    }

    void UnlockTech(int index)
    {
        if (index >= 0 && index < TechCatalog.Names.Length)
            TheCallApp.Interface.SendCommand(new UnlockTechCommand(TechCatalog.Names[index]));
    }

    void BuyMonster(int index)
    {
        var shelf = TheCallApp.Interface.SendQuery(new ShelfQuery());
        if (shelf != null && index < shelf.Monsters.Count)
            TheCallApp.Interface.SendCommand(new BuyMonsterCommand(shelf.Monsters[index].Id));
    }

    void ShowMessage(string message)
    {
        if (toast == null || toastLabel == null || string.IsNullOrEmpty(message))
            return;
        StopAllCoroutines();
        StartCoroutine(Toast(message));
    }

    IEnumerator Toast(string message)
    {
        toastLabel.text = message;
        toast.SetActive(true);
        var rt = toast.GetComponent<RectTransform>();
        rt.localScale = Vector3.zero;
        var time = 0f;
        while (time < 0.16f)
        {
            time += Time.unscaledDeltaTime;
            rt.localScale = Vector3.one * EaseOutBack(Mathf.Clamp01(time / 0.16f));
            yield return null;
        }
        yield return new WaitForSecondsRealtime(1.3f);
        time = 0f;
        while (time < 0.12f)
        {
            time += Time.unscaledDeltaTime;
            rt.localScale = Vector3.one * (1f - Mathf.Clamp01(time / 0.12f));
            yield return null;
        }
        toast.SetActive(false);
    }

    void SetModal(GameObject target, bool visible)
    {
        if (target == null)
            return;
        if (target.activeSelf == visible)
            return;
        target.SetActive(visible);
        if (visible)
        {
            var rt = target.GetComponent<RectTransform>();
            rt.localScale = Vector3.one * 0.92f;
            StartCoroutine(PopIn(rt));
        }
    }

    IEnumerator PopIn(RectTransform rt)
    {
        var start = rt.localScale;
        var time = 0f;
        while (time < 0.18f)
        {
            time += Time.unscaledDeltaTime;
            rt.localScale = Vector3.LerpUnclamped(start, Vector3.one, EaseOutBack(Mathf.Clamp01(time / 0.18f)));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    GameObject Window(string name, Transform parent, string title, float width, float height)
    {
        var window = Node(name, parent).gameObject;
        var rt = window.GetComponent<RectTransform>();
        Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, 0f, width, height);
        ImageNode("Frame", window.transform, skin.panel, false).GetComponent<Image>().pixelsPerUnitMultiplier = 1f / 14f;
        var titleGo = ImageNode("Title", window.transform, skin.title, false);
        Anchor(titleGo.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), 0f, -34f, 280f, 48f);
        var label = Label("Label", titleGo.transform, title, 28f, TextAlignmentOptions.Center, cream);
        Stretch(label.rectTransform);
        return window;
    }

    GameObject Panel(string name, Transform parent, string title, float width, float height)
    {
        var panel = Node(name, parent).gameObject;
        ImageNode("Paper", panel.transform, skin.paper, false).GetComponent<Image>().pixelsPerUnitMultiplier = 1f / 14f;
        var label = Label("Title", panel.transform, title, 16f, TextAlignmentOptions.MidlineLeft, muted);
        Anchor(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), 18f, -34f, -18f, -14f);
        Anchor(panel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, 0f, width, height);
        return panel;
    }

    GameObject Card(string name, Transform parent, string text, Sprite icon, float width, float height)
    {
        var card = Node(name, parent).gameObject;
        var image = ImageNode("Frame", card.transform, skin.slot, true).GetComponent<Image>();
        image.pixelsPerUnitMultiplier = 1f / 6f;
        var button = card.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock { normalColor = Color.white, highlightedColor = new Color(1f, 0.92f, 0.68f), pressedColor = new Color(0.82f, 0.72f, 0.56f), selectedColor = Color.white, disabledColor = new Color(1f, 1f, 1f, 0.5f), colorMultiplier = 1f, fadeDuration = 0.06f };
        var iconGo = ImageNode("Icon", card.transform, icon, false);
        Anchor(iconGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), 0f, 6f, Mathf.Min(54f, width * 0.45f), Mathf.Min(54f, height * 0.45f));
        var label = Label("Label", card.transform, text, 16f, TextAlignmentOptions.Center, ink);
        Anchor(label.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), 4f, 6f, -4f, 28f);
        return card;
    }

    Button ButtonNode(string name, Transform parent, string text, float width, float height)
    {
        var go = Node(name, parent).gameObject;
        var image = ImageNode("Frame", go.transform, skin.button, true).GetComponent<Image>();
        image.pixelsPerUnitMultiplier = 1f / 6f;
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var label = Label("Label", go.transform, text, 18f, TextAlignmentOptions.Center, cream);
        Stretch(label.rectTransform);
        return button;
    }

    GameObject ImageNode(string name, Transform parent, Sprite sprite, bool raycast)
    {
        var go = Node(name, parent).gameObject;
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.raycastTarget = raycast;
        image.color = Color.white;
        return go;
    }

    TMP_Text Label(string name, Transform parent, string text, float size, TextAlignmentOptions alignment, Color color)
    {
        var go = Node(name, parent).gameObject;
        var label = go.AddComponent<TextMeshProUGUI>();
        label.font = skin.font;
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.extraPadding = true;
        return label;
    }

    RectTransform Grid(string name, Transform parent, Vector2Int constraint, float cellWidth, float cellHeight, float spacing)
    {
        var go = Node(name, parent);
        var grid = go.GetComponent<GridLayoutGroup>() ?? go.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = constraint.y == 1 ? GridLayoutGroup.Constraint.FixedRowCount : GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = constraint.y == 1 ? constraint.y : constraint.x;
        grid.cellSize = new Vector2(cellWidth, cellHeight);
        grid.spacing = new Vector2(spacing, spacing);
        grid.childAlignment = TextAnchor.MiddleCenter;
        return go;
    }

    RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    void FillCard(Transform card, string text)
    {
        var label = card.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = text;
    }

    void FillSlot(Transform slot, string id, UnityEngine.Events.UnityAction onClick)
    {
        slot.gameObject.SetActive(true);
        var label = slot.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.text = id ?? "空位";
        var button = slot.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        if (id != null)
            button.onClick.AddListener(onClick);
    }

    void ClearGenerated()
    {
        for (var i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (child.name == "Background" || child.name == "Demo" || child.name == "TheCallUI")
                continue;
            Destroy(child.gameObject);
        }
    }

    string PhaseName(RunPhase phase)
    {
        switch (phase)
        {
            case RunPhase.Opening: return "OPENING";
            case RunPhase.LevelStart: return "DAY START";
            case RunPhase.Operation: return "OPERATION";
            case RunPhase.Shop: return "SHOP";
            case RunPhase.Victory: return "VICTORY";
            case RunPhase.Failed: return "FAILED";
            default: return "";
        }
    }

    string PhaseMessage(RunPhase phase)
    {
        switch (phase)
        {
            case RunPhase.Opening: return "新的实验日开始了";
            case RunPhase.LevelStart: return "培育结果已进入怪物笼";
            case RunPhase.Operation: return "安排怪物，准备结算";
            case RunPhase.Shop: return "工资已发放，商店开门";
            case RunPhase.Victory: return "七日实验完成";
            case RunPhase.Failed: return "欠额未补足";
            default: return "";
        }
    }

    string EntryText(SettlementEntry entry)
    {
        var landing = entry as SettlementLanding;
        if (landing != null)
            return landing.SkillName + "   " + landing.Base + " x " + landing.Multiplier + " = " + landing.Energy;
        var payment = entry as SettlementPayment;
        if (payment != null)
            return payment.Failed ? "欠额未补足" : "交款 " + payment.Deducted + "   工资 +" + payment.Wage;
        var removal = entry as SettlementRemoval;
        if (removal != null)
            return removal.Happened ? "消灭 " + removal.MonsterId : "消灭未发生";
        var swap = entry as SettlementSwap;
        return swap == null ? "" : "换位 " + (swap.Happened ? "完成" : "未发生");
    }

    void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, float left, float bottom, float right, float top)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static float EaseOutBack(float t)
    {
        var c1 = 1.70158f;
        var c3 = c1 + 1f;
        var x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
