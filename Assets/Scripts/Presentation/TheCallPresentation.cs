using System;
using System.Collections;
using System.Collections.Generic;
using QFramework;
using TheCall.Scoring;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheCall
{
    /// <summary>
    /// 把场景里已经摆好的界面接到核心命令和查询上。
    /// 这里不创建控件，也不改 RectTransform。换图、挪位置都在场景里做。
    /// </summary>
    public sealed class TheCallPresentation : MonoBehaviour, IController
    {
        [SerializeField] OpeningScreenView _opening;
        [SerializeField] LevelStartScreenView _levelStart;
        [SerializeField] OperationScreenView _operation;
        [SerializeField] ResearchScreenView _research;
        [SerializeField] ShopScreenView _shop;
        [SerializeField] ResultScreenView _result;
        [SerializeField] SettingsChromeView _settings;
        [SerializeField] GameObject _toast;
        [SerializeField] TMP_Text _toastText;
        [SerializeField] GameObject _workspace;

        enum BackTarget
        {
            None,
            Operation,
            Shop,
        }

        BackTarget _back;
        bool _sellTab;
        bool _wired;
        bool _busy;
        bool _sessionStarted;
        ProductionLogView _log;
        PresentationSpriteCatalog _spriteCatalog;
        string _selectedTech = "基因实验";
        float _noticeUntil;
        string _lastTrimNotice;

        public IArchitecture GetArchitecture() => TheCallApp.Interface;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BeginPlaySession()
        {
            var presentation = FindAnyObjectByType<TheCallPresentation>();
            if (presentation != null && !presentation._sessionStarted)
                presentation.StartSession();
        }

        void Awake()
        {
            EnsureSpriteCatalog();
            Wire();
            StartSession();
        }

        void EnsureSpriteCatalog()
        {
            if (_spriteCatalog == null)
                _spriteCatalog = Resources.Load<PresentationSpriteCatalog>("PresentationSpriteCatalog");
        }

        public void StartSession()
        {
            _sessionStarted = true;
            // Session resets stop pooled effects and both BGM tracks before reopening the menu.
            GameSfx.StopAll();
            MusicManager.HardStop();
            _back = BackTarget.None;
            _lastTrimNotice = null;
            _sellTab = false;
            _busy = false;
            if (_log != null)
                _log.Hide();
            if (_settings != null)
                _settings.HideAll();
            if (_toast != null)
                _toast.SetActive(false);

            Refresh();
        }

        void Update()
        {
            if (_toast != null && _toast.activeSelf && Time.unscaledTime >= _noticeUntil)
                _toast.SetActive(false);

            if (!EscapePressedThisFrame())
                return;

            if (_settings != null && _settings.LogOpen && _settings.LogOpenedFromSettings)
                _settings.CloseLog();
            else if (_log != null && _log.IsOpen)
                _log.Hide();
            else if (_settings != null && _settings.CodexOpen)
                _settings.CloseCodex();
            else if (_settings != null && _settings.SettingsOpen)
                _settings.HideAll();
            else if (CanQuitFromOpening())
                QuitGame();
        }

#if UNITY_EDITOR
        public void BindScreens(
            OpeningScreenView opening,
            LevelStartScreenView levelStart,
            OperationScreenView operation,
            ResearchScreenView research,
            ShopScreenView shop,
            ResultScreenView result,
            GameObject toast,
            TMP_Text toastText)
        {
            _opening = opening;
            _levelStart = levelStart;
            _operation = operation;
            _research = research;
            _shop = shop;
            _result = result;
            _toast = toast;
            _toastText = toastText;
        }
#endif

        void Wire()
        {
            if (_wired)
                return;

            _wired = true;
            BindOpeningExitButton();
            Listen(_opening != null ? _opening.exitButton : null, QuitGame);
            ListenSlots(_opening != null ? _opening.candidates : null, OnKeep);
            Listen(_levelStart != null ? _levelStart.beginButton : null, OnBegin);
            Listen(_operation != null ? _operation.shopButton : null, OnShopLocked);
            Listen(_operation != null ? _operation.researchButton : null, () => OpenResearch(BackTarget.Operation));
            Listen(_operation != null ? _operation.settingsButton : null, ToggleSettings);
            Listen(_operation != null ? _operation.titleButton : null, OnTitle);
            Listen(_operation != null ? _operation.nextDayButton : null, OnSettle);
            Listen(_operation != null ? _operation.addGeneSlotButton : null, OnAddGeneSlot);
            if (_operation != null && _operation.pointer != null)
                _operation.pointer.Attach(this);
            Listen(_research != null ? _research.researchButton : null, OnResearch);
            Listen(_research != null ? _research.backButton : null, CloseResearch);
            ListenNodes(_research != null ? _research.nodes : null);
            Listen(_shop != null ? _shop.buyTabButton : null, () => SetSellTab(false));
            Listen(_shop != null ? _shop.sellTabButton : null, () => SetSellTab(true));
            Listen(_shop != null ? _shop.shopButton : null, () => SetSellTab(false));
            Listen(_shop != null ? _shop.researchButton : null, () => OpenResearch(BackTarget.Shop));
            Listen(_shop != null ? _shop.saveButton : null, () => Notice("这一版还不能存档。"));
            Listen(_shop != null ? _shop.titleButton : null, OnTitle);
            if (_shop != null)
                _shop.EnsureRefreshControl();
            Listen(_shop != null ? _shop.leaveButton : null, OnLeaveShop);
            Listen(_shop != null ? _shop.refreshButton : null, OnRefreshShelf);
            ListenCards(_shop != null ? _shop.cards : null);
            ListenSlots(_shop != null ? _shop.sellSlots : null, OnSell);
            Listen(_result != null ? _result.restartButton : null, OnTitle);
            BindLog();
            BindSettings();
        }

        void BindSettings()
        {
            if (_settings == null)
                _settings = FindAnyObjectByType<SettingsChromeView>(FindObjectsInactive.Include);
            if (_settings == null)
                return;

            var buttons = _settings.openButtons;
            if (buttons != null)
            {
                for (var i = 0; i < buttons.Length; i++)
                    Listen(buttons[i], ToggleSettings);
            }

            Listen(_settings.quitButton, QuitGame);
            Listen(_settings.codexButton, OpenCodex);
            _settings.Prepare();
            Listen(_settings.logButton, OpenLogFromSettings);
        }

        void ToggleSettings()
        {
            if (_settings == null)
                return;

            if (_settings.SettingsOpen || _settings.CodexOpen || _settings.LogOpenedFromSettings)
                _settings.HideAll();
            else
                _settings.ShowSettings();
        }

        void OpenCodex()
        {
            if (_settings == null)
                return;

            _settings.ShowCodex(this.SendQuery(new SkillIndexQuery()));
        }

        void BindLog()
        {
            _log = FindAnyObjectByType<ProductionLogView>(FindObjectsInactive.Include);
            var buttonObject = _operation != null ? _operation.transform.Find("LogButton") : null;
            Listen(buttonObject != null ? buttonObject.GetComponent<UnityEngine.UI.Button>() : null, ToggleLog);
        }

        void OpenLogFromSettings()
        {
            if (_settings == null)
                return;

            _settings.ShowLog(LogText());
        }

        public void ToggleLog()
        {
            if (_log == null)
                return;

            if (_log.IsOpen)
                _log.Hide();
            else
                _log.Show(LogText());
        }

        string LogText() =>
            ProductionLogText.Format(
                this.SendQuery(new ProductionLogQuery()),
                id =>
                {
                    var monster = this.SendQuery(new MonsterQuery(id));
                    return monster?.DisplayName;
                });

        public void Refresh()
        {
            if (_opening == null)
                return;

            var phase = this.SendQuery(new RunPhaseQuery());
            if (_back != BackTarget.None && (phase == RunPhase.Operation || phase == RunPhase.Shop))
            {
                DrawResearch();
                return;
            }

            _back = BackTarget.None;
            switch (phase)
            {
                case RunPhase.Opening:
                    DrawOpening();
                    break;
                case RunPhase.LevelStart:
                    DrawLevelStart();
                    break;
                case RunPhase.Operation:
                    DrawOperation();
                    break;
                case RunPhase.Shop:
                    DrawShop();
                    break;
                case RunPhase.Victory:
                    DrawResult(true);
                    break;
                default:
                    DrawResult(false);
                    break;
            }
        }

        void DrawOpening()
        {
            Show(_opening);
            var candidates = this.SendQuery(new OpeningCandidatesQuery());
            var slots = _opening.candidates;
            for (var i = 0; i < slots.Length; i++)
            {
                var monster = i < candidates.Count ? candidates[i] : null;
                FillMonster(slots[i], monster, false);
                slots[i].gameObject.SetActive(monster != null);
            }
        }

        void DrawLevelStart()
        {
            Show(_levelStart);
            var target = this.SendQuery(new LevelTargetQuery());
            _levelStart.levelLabel.text = "第 " + target.LevelNumber + " 关";
            _levelStart.dueLabel.text = target.EnergyDue.ToString();
            SetLabel(_levelStart.transform, "DueCaption", "目标额度");
            if (_levelStart.excessLabel != null)
                _levelStart.excessLabel.gameObject.SetActive(false);
            SetShown(_levelStart.transform, "ExcessCaption", false);
        }

        void DrawOperation()
        {
            Show(_operation);
            var run = this.SendQuery(new RunLedgerQuery());
            var target = this.SendQuery(new LevelTargetQuery());
            var energy = this.SendQuery(new LevelEnergyQuery());
            var shortfall = this.SendQuery(new LevelShortfallQuery());
            var goal = target.EnergyDue;
            SetLabel(_operation.transform, "TargetCaption", "目标");
            _operation.targetLabel.text = goal.ToString();
            _operation.energyLabel.text = "累计 " + energy + " · 尚差 " + shortfall + " · 科技 " + run.TechPoints;
            if (_operation.dayLabel != null)
                _operation.dayLabel.text = "第 " + target.LevelNumber + " 天";
            if (_operation.excessLabel != null)
                _operation.excessLabel.text = target.ExcessEnergy + "+";
            if (_operation.overtimeLabel != null)
                _operation.overtimeLabel.text = target.InOvertime ? "加班中" : "正常工时";

            if (!_busy)
            {
                var current = _operation.currentNumber;
                if (current == null)
                {
                    var found = _operation.transform.Find("CurrentNumber");
                    if (found != null)
                        current = found.GetComponent<TMP_Text>();
                }

                if (current != null)
                    current.text = energy.ToString();
            }
            _operation.goldLabel.text = run.Gold.ToString();
            _operation.nextDayLabel.text = target.InOvertime ? "开始加班结算" : "开始结算";

            var cage = this.SendQuery(new MonsterCageQuery());
            _operation.cageSlots = GrowInventory(_operation.cageSlots, cage.Count, false);
            FillList(_operation.cageSlots, cage, ArmedMonsterId());
            FillCells(_operation.extractionSlots, this.SendQuery(new ExtractionSlotsQuery()));
            FillParents(_operation.breedingSlots, this.SendQuery(new BreedingSlotsQuery()));
            FillGeneSlots(_operation.skillChips, run.GeneCapacity);
            if (_operation.addGeneSlotButton != null)
                _operation.addGeneSlotButton.gameObject.SetActive(run.GeneCapacity < 4);
            FillBreedingSkills(_operation.breedingSkills, this.SendQuery(new BreedingPlansQuery()));
            FillOwnedTools(run.Tools);
        }

        void DrawShop()
        {
            Show(_shop);
            var run = this.SendQuery(new RunLedgerQuery());
            var shelf = this.SendQuery(new ShelfQuery());
            _shop.balanceLabel.text = run.Gold.ToString();
            if (_shop.portraitBalanceLabel != null)
                _shop.portraitBalanceLabel.text = run.Gold.ToString();
            if (_shop.refreshPriceLabel != null)
                _shop.refreshPriceLabel.text = "刷新 " + shelf.NextRefreshPrice;
            if (_shop.buyPage != null)
                _shop.buyPage.SetActive(!_sellTab);
            if (_shop.sellPage != null)
                _shop.sellPage.SetActive(_sellTab);

            var leaveText = _shop.leaveButton != null ? _shop.leaveButton.GetComponentInChildren<TMP_Text>() : null;
            if (leaveText != null) leaveText.text = this.SendQuery(new LevelTargetQuery()).LevelNumber >= 7 ? "结束本局" : "下一天";
            var cardIndex = 0;
            var cards = _shop.cards;
            if (cards != null)
            {
                for (var i = 0; i < shelf.Monsters.Count && cardIndex < cards.Length; i++)
                    FillMonsterCard(cards[cardIndex++], shelf.Monsters[i], run.Gold);
                for (var i = 0; i < shelf.Tools.Count && cardIndex < cards.Length; i++)
                    FillToolCard(cards[cardIndex++], shelf.Tools[i], run.Gold);
                for (; cardIndex < cards.Length; cardIndex++)
                    cards[cardIndex].gameObject.SetActive(false);
            }

            var cage = this.SendQuery(new MonsterCageQuery());
            _shop.sellSlots = GrowInventory(_shop.sellSlots, cage.Count, true);
            FillList(_shop.sellSlots, cage, null);
        }

        void DrawResearch()
        {
            Show(_research);
            var run = this.SendQuery(new RunLedgerQuery());
            _research.techPointLabel.text = run.TechPoints.ToString();
            _research.goldLabel.text = run.Gold.ToString();
            TechNodeView selected = null;
            var nodes = _research.nodes;
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                var unlocked = Contains(run.UnlockedTech, node.techName);
                if (node.costLabel != null)
                    node.costLabel.text = unlocked ? "已研究" : "消耗 1 科技点";
                if (node.selection != null)
                    node.selection.SetActive(node.techName == _selectedTech);
                if (node.techName == _selectedTech)
                    selected = node;
            }

            if (selected == null && nodes.Length > 0)
                selected = nodes[0];

            if (selected != null)
            {
                _selectedTech = selected.techName;
                _research.detailTitle.text = selected.title != null ? selected.title.text : selected.techName;
                _research.detailBody.text = selected.summary;
                var unlocked = Contains(run.UnlockedTech, selected.techName);
                _research.detailCost.text = unlocked ? "已经研究" : "科技点消耗：1";
                if (_research.researchLabel != null)
                    _research.researchLabel.text = unlocked ? "已研究" : "研究";
                if (_research.researchButton != null)
                    _research.researchButton.interactable = !unlocked && run.TechPoints >= 1;
            }
        }

        void DrawResult(bool victory)
        {
            Show(_result);
            _result.title.text = victory ? "七关都交上去了" : "账单没有补上";
            _result.body.text = victory
                ? "这一局结束。怪物笼、金币和科技都已清空。"
                : "这一关的目标没有达到。这一局结束。";
            if (_result.portrait != null)
                _result.portrait.Show(MonsterAppearance.FromSeed(victory ? 73 : 29));
        }

        MonsterSlotView[] GrowInventory(MonsterSlotView[] slots, int count, bool selling)
        {
            if (slots == null || slots.Length == 0 || slots[0] == null) return slots;
            var template = slots[0];
            var parent = template.transform.parent as RectTransform;
            var pool = new List<MonsterSlotView>(slots);
            while (pool.Count < count)
            {
                var copy = Instantiate(template, parent);
                copy.name = (selling ? "SellCard_" : "CageCard_") + pool.Count;
                copy.monsterId = null;
                if (selling && copy.button != null)
                {
                    copy.button.onClick.RemoveAllListeners();
                    copy.button.onClick.AddListener(() => OnSell(copy));
                }
                pool.Add(copy);
            }
            var first = template.transform as RectTransform;
            var second = slots.Length > 1 ? slots[1].transform as RectTransform : null;
            var xStep = second != null ? second.anchoredPosition.x - first.anchoredPosition.x : first.rect.width + 8;
            var rowStep = selling ? 156f : 118f;
            for (var i = 0; i < pool.Count; i++)
                ((RectTransform)pool[i].transform).anchoredPosition = first.anchoredPosition + new Vector2((i % 2) * xStep, -(i / 2) * rowStep);
            if (parent != null) parent.sizeDelta = new Vector2(parent.sizeDelta.x, Mathf.Ceil(pool.Count / 2f) * rowStep + 8);
            return pool.ToArray();
        }

        void FillList(MonsterSlotView[] slots, IReadOnlyList<MonsterView> monsters, string selectedId)
        {
            if (slots == null)
                return;

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                    continue;

                var slotObject = slot.gameObject;
                if (!slotObject)
                    continue;

                var monster = monsters != null && i < monsters.Count ? monsters[i] : null;
                slotObject.SetActive(monster != null);
                if (monster != null)
                    FillMonster(slot, monster, monster.Id == selectedId);
            }
        }

        void FillCells(MonsterSlotView[] slots, IReadOnlyList<ExtractionCellView> cells)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                    continue;

                var slotObject = slot.gameObject;
                if (!slotObject)
                    continue;

                var active = cells != null && i < cells.Count;
                slotObject.SetActive(active);
                if (!active)
                    continue;

                slot.index = cells[i].Index;
                FillSeat(slot, cells[i].MonsterId);
            }
        }

        void FillParents(MonsterSlotView[] slots, IReadOnlyList<BreedingParentView> parents)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var active = parents != null && i < parents.Count;
                slots[i].gameObject.SetActive(active);
                if (!active)
                    continue;

                slots[i].index = parents[i].Index;
                FillSeat(slots[i], parents[i].MonsterId);
            }
        }

        void FillGeneSlots(SkillChipView[] chips, int capacity)
        {
            if (chips == null)
                return;

            var geneSlots = this.SendQuery(new GeneSlotsQuery());
            for (var i = 0; i < chips.Length; i++)
            {
                var chip = chips[i];
                if (chip == null)
                    continue;

                var active = i < capacity;
                chip.gameObject.SetActive(active);
                if (!active)
                    continue;

                chip.index = i;
                var hasGene = geneSlots != null && i < geneSlots.Count;
                if (hasGene)
                {
                    var slot = geneSlots[i];
                    chip.label.text = slot.SkillName;
                    if (chip.reservedLabel != null)
                    {
                        chip.reservedLabel.text = slot.IsReserved
                            ? (slot.ReservedBreedingSlot.HasValue ? $"[已预约 培育{slot.ReservedBreedingSlot.Value + 1}]" : "[已预约]")
                            : "";
                    }
                    if (chip.reservedMark != null)
                        chip.reservedMark.SetActive(slot.IsReserved);
                    if (chip.button != null)
                        chip.button.interactable = true;
                    if (chip.selection != null)
                        chip.selection.SetActive(IsArmed(DropPayload.SkillChip(i)));
                }
                else
                {
                    chip.label.text = "空";
                    if (chip.reservedLabel != null)
                        chip.reservedLabel.text = "";
                    if (chip.reservedMark != null)
                        chip.reservedMark.SetActive(false);
                    if (chip.button != null)
                        chip.button.interactable = false;
                    if (chip.selection != null)
                        chip.selection.SetActive(false);
                }
            }
        }

        void FillOwnedTools(IReadOnlyList<string> tools)
        {
            var views = _operation != null ? _operation.ownedToolViews : null;
            if (views == null)
                return;

            EnsureSpriteCatalog();
            for (var i = 0; i < views.Length; i++)
            {
                var view = views[i];
                if (view == null)
                    continue;

                var owned = tools != null && i < tools.Count;
                view.gameObject.SetActive(owned);
                if (!owned)
                    continue;

                var toolName = tools[i];
                view.toolName = toolName;
                var def = _spriteCatalog != null ? _spriteCatalog.FindTool(toolName) : null;
                view.icon = null;
                view.description = "";
                if (view.iconImage != null) view.iconImage.sprite = null;
                if (def != null)
                {
                    view.icon = def.icon;
                    view.description = def.description;
                }
                if (view.iconImage != null && view.icon != null)
                {
                    view.iconImage.sprite = view.icon;
                    view.iconImage.preserveAspect = true;
                }
                if (view.nameLabel != null)
                    view.nameLabel.text = toolName;
            }
        }

        void OnAddGeneSlot()
        {
            var run = this.SendQuery(new RunLedgerQuery());
            if (run.GeneCapacity >= 4)
            {
                Notice("基因仓储已解锁，已达容量上限。");
                return;
            }
            if (run.TechPoints < 1)
            {
                Notice("科技点不足。解锁基因仓储需要 1 科技点。");
                return;
            }
            this.SendCommand(new UnlockTechCommand("基因仓储"));
            Notice("基因仓储解锁成功，基因槽上限扩展为 4。");
            Refresh();
        }

        void FillBreedingSkills(SkillChipView[] chips, IReadOnlyList<BreedingPlanView> plans)
        {
            for (var i = 0; i < chips.Length; i++)
            {
                var active = plans != null && i < plans.Count;
                chips[i].gameObject.SetActive(active);
                if (!active)
                    continue;

                chips[i].index = plans[i].Index;
                chips[i].label.text = string.IsNullOrEmpty(plans[i].SkillName) ? "投入技能" : plans[i].SkillName;
                if (chips[i].selection != null)
                    chips[i].selection.SetActive(
                        !string.IsNullOrEmpty(plans[i].SkillName) && IsArmed(DropPayload.BreedingSkill(plans[i].Index)));
            }
        }

        void FillMonster(MonsterSlotView slot, MonsterView monster, bool selected)
        {
            slot.monsterId = monster != null ? monster.Id : null;
            var occupied = monster != null;
            if (slot.portrait != null)
            {
                slot.portrait.gameObject.SetActive(occupied);
                if (occupied)
                    slot.portrait.Show(monster.Appearance);
                else
                    slot.portrait.Clear();
            }

            if (slot.emptyMark != null)
                slot.emptyMark.SetActive(!occupied);
            if (slot.title != null)
                slot.title.text = occupied ? $"{monster.DisplayName} [{monster.Race}]" : "";
            if (slot.subtitle != null)
                slot.subtitle.text = occupied ? $"{monster.Personality} · {monster.Mood} | {SkillLine(monster)}" : "";
            EnsureSpriteCatalog();
            BindBadge(slot.personalityIcon, occupied ? _spriteCatalog?.GetPersonalityIcon(monster.Personality) : null);
            BindBadge(slot.raceIcon, occupied ? _spriteCatalog?.GetRaceIcon(monster.Race) : null);
            BindBadge(slot.moodIcon, occupied ? _spriteCatalog?.GetMoodIcon(monster.Mood) : null);
            if (slot.selection != null)
                slot.selection.SetActive(selected);
        }

        static void BindBadge(UnityEngine.UI.Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite; image.enabled = sprite != null; image.preserveAspect = true;
        }

        void FillSeat(MonsterSlotView slot, string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                FillMonster(slot, null, false);
                return;
            }

            FillMonster(slot, this.SendQuery(new MonsterQuery(monsterId)), monsterId == ArmedMonsterId());
        }

        void FillMonsterCard(ShopCardView card, ShelfMonster item, int gold)
        {
            card.gameObject.SetActive(true);
            card.monsterId = item.Id;
            card.toolName = null;
            if (card.portrait != null)
            {
                card.portrait.gameObject.SetActive(true);
                card.portrait.Show(item.Appearance);
            }

            if (card.icon != null)
                card.icon.enabled = false;
            card.title.text = item.DisplayName;
            card.body.text = Join(item.SkillNames);
            card.price.text = item.Price + " 金币";
            card.stock.text = "本轮剩余 1 件";
            var affordable = gold >= item.Price;
            card.buyLabel.text = affordable ? "购买" : "金币不足";
            card.buyButton.interactable = affordable;
        }

        void FillToolCard(ShopCardView card, ShelfTool item, int gold)
        {
            card.gameObject.SetActive(true);
            card.monsterId = null;
            card.toolName = item.Name;
            if (card.portrait != null)
                card.portrait.gameObject.SetActive(false);
            EnsureSpriteCatalog();
            var art = _spriteCatalog != null ? _spriteCatalog.FindTool(item.Name) : null;
            if (card.icon != null)
            {
                card.icon.sprite = null;
                card.icon.enabled = true;
                card.icon.type = UnityEngine.UI.Image.Type.Simple;
                card.icon.preserveAspect = true;
                card.icon.color = Color.white;
                if (art != null && art.icon != null)
                    card.icon.sprite = art.icon;
                else
                {
                    var fallback = FindArt(item.Name);
                    if (fallback != null && fallback.icon != null)
                        card.icon.sprite = fallback.icon;
                }
            }

            card.title.text = item.Name;
            card.body.text = art != null ? art.description : (FindArt(item.Name)?.description ?? "");
            card.price.text = item.Price + " 金币";
            card.stock.text = "本轮剩余 1 件";
            var affordable = gold >= item.Price;
            card.buyLabel.text = affordable ? "购买" : "金币不足";
            card.buyButton.interactable = affordable;
        }

        ToolArtView FindArt(string toolName)
        {
            var art = _shop != null ? _shop.toolArt : null;
            if (art == null)
                return null;

            for (var i = 0; i < art.Length; i++)
                if (art[i] != null && art[i].toolName == toolName)
                    return art[i];

            return null;
        }

        void OnKeep(MonsterSlotView slot)
        {
            if (string.IsNullOrEmpty(slot.monsterId))
                return;

            var id = slot.monsterId;
            Run(() => this.SendCommand(new KeepOpeningMonsterCommand(id)));
        }

        void OnBegin()
        {
            Run(() => this.SendCommand(new BeginLevelCommand()));
        }

        string ArmedMonsterId()
        {
            var armed = _operation != null && _operation.pointer != null ? _operation.pointer.Armed : null;
            if (armed == null || armed.Value.Kind != PayloadKind.Monster)
                return null;

            return armed.Value.MonsterId;
        }

        bool IsArmed(DropPayload payload)
        {
            var armed = _operation != null && _operation.pointer != null ? _operation.pointer.Armed : null;
            if (armed == null || armed.Value.Kind != payload.Kind)
                return false;

            if (payload.Kind == PayloadKind.Monster)
                return armed.Value.MonsterId == payload.MonsterId;

            return armed.Value.Index == payload.Index;
        }

        internal bool CommitDrop(OperationDrop drop)
        {
            var applied = this.SendCommand(new CommitOperationDropCommand(drop));
            if (applied)
            {
                Refresh();
                if (drop.Payload.Kind == PayloadKind.SkillChip && !string.IsNullOrEmpty(drop.Landing.MonsterId)) Notice("基因安装成功。");
                else if (drop.Payload.Kind == PayloadKind.SkillChip && drop.Landing.Place == LandingPlace.BreedingSocket) Notice("已预约此格基因用于培育；取回时恢复原格。");
                else if (drop.Payload.Kind == PayloadKind.BreedingSkill) Notice("培育基因已退还原格。");
            }
            else Notice(this.SendQuery(new OperationFeedbackQuery()) ?? "无法放入此位置。");
            return applied;
        }

        public void TriggerSettle() => OnSettle();

        void OnSettle()
        {
            if (_busy)
                return;

            _busy = true;
            var initialEnergy = this.SendQuery(new LevelEnergyQuery());
            var initialGold = this.SendQuery(new RunLedgerQuery()).Gold;
            this.SendCommand(new ConfirmSettlementCommand());
            ScoringShow.Play(new ScoringStage(
                this.GetArchitecture(),
                _operation.extractionSlots,
                _operation.transform as RectTransform,
                _operation.dragLayer,
                _operation.goldLabel,
                _operation.currentNumber,
                Notice,
                FinishSettle, initialEnergy, initialGold));
        }

        void FinishSettle()
        {
            // 演出销毁时仍会通知完成；此时所属界面可能已随场景一起销毁。
            if (this == null)
                return;

            _busy = false;
            var trimmed = this.SendQuery(new SkillTrimFeedbackQuery());
            if (!string.IsNullOrEmpty(trimmed) && trimmed != _lastTrimNotice)
            {
                _lastTrimNotice = trimmed;
                Notice("性格技能上限调整，移除：" + trimmed);
            }
            if (!isActiveAndEnabled)
                return;

            StartCoroutine(RefreshAfterSettle());
        }

        IEnumerator RefreshAfterSettle()
        {
            yield return null;
            if (this != null)
                Refresh();
        }

        void OnShopLocked() => Notice("结算并发工资之后才进入商店。");

        void OpenResearch(BackTarget back)
        {
            _back = back;
            if (string.IsNullOrEmpty(_selectedTech))
                _selectedTech = "基因实验";
            Refresh();
        }

        void CloseResearch()
        {
            _back = BackTarget.None;
            Refresh();
        }

        void OnResearch()
        {
            if (string.IsNullOrEmpty(_selectedTech))
            {
                Notice("先点一项科技。");
                return;
            }

            if (this.SendQuery(new RunPhaseQuery()) != RunPhase.Operation)
            {
                Notice("科技只能在操作阶段研究。");
                return;
            }

            var name = _selectedTech;
            var before = this.SendQuery(new RunLedgerQuery());
            this.SendCommand(new UnlockTechCommand(name));
            var after = this.SendQuery(new RunLedgerQuery());
            if (Contains(after.UnlockedTech, name) && !Contains(before.UnlockedTech, name))
                Notice("研究完成。");
            else if (Contains(before.UnlockedTech, name))
                Notice("这项已经研究过了。");
            else
                Notice("科技点不足。");

            Refresh();
        }

        void OnSelectTech(TechNodeView node)
        {
            _selectedTech = node.techName;
            Refresh();
        }

        void SetSellTab(bool sell)
        {
            _sellTab = sell;
            Refresh();
        }

        void OnBuy(ShopCardView card)
        {
            if (!string.IsNullOrEmpty(card.monsterId))
            {
                var id = card.monsterId;
                Run(() => this.SendCommand(new BuyMonsterCommand(id)));
                return;
            }

            if (string.IsNullOrEmpty(card.toolName))
                return;

            var tool = card.toolName;
            Run(() => this.SendCommand(new BuyToolCommand(tool)));
        }

        void OnSell(MonsterSlotView slot)
        {
            if (string.IsNullOrEmpty(slot.monsterId))
                return;

            var id = slot.monsterId;
            Run(
                () => this.SendCommand(new SellMonsterCommand(id)),
                () => !CageContains(id),
                "这只现在不能出售。锁定中的亲本要等后代出生。");
        }

        void OnRefreshShelf()
        {
            this.SendCommand(new RefreshShelfCommand());
            Refresh();
        }

        void OnLeaveShop() => Run(() => this.SendCommand(new LeaveShopCommand()));

        void OnTitle()
        {
            TheCallApp.Reset();
            StartSession();
        }

        void BindOpeningExitButton()
        {
            if (_opening == null || _opening.exitButton != null)
                return;

            var exit = _opening.transform.Find("ExitButton");
            if (exit != null)
                _opening.exitButton = exit.GetComponent<UnityEngine.UI.Button>();
        }

        static bool EscapePressedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        bool CanQuitFromOpening()
        {
            if (_busy || _opening == null || !_opening.gameObject.activeInHierarchy)
                return false;

            return this.SendQuery(new RunPhaseQuery()) == RunPhase.Opening;
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Run(Action send)
        {
            if (_busy)
                return;

            _busy = true;
            send();
            _busy = false;
            Refresh();
        }

        void Run(Action send, Func<bool> succeeded, string failureNotice)
        {
            if (_busy)
                return;

            _busy = true;
            send();
            _busy = false;
            if (!succeeded() && !string.IsNullOrEmpty(failureNotice))
                Notice(failureNotice);

            Refresh();
        }

        void Notice(string message)
        {
            if (_toastText != null)
                _toastText.text = message;
            if (_toast != null)
                _toast.SetActive(!string.IsNullOrEmpty(message));
            _noticeUntil = Time.unscaledTime + 2.6f;
        }

        void Show(MonoBehaviour screen)
        {
            Activate(_opening, screen == _opening);
            Activate(_levelStart, screen == _levelStart);
            Activate(_operation, screen == _operation);
            Activate(_research, screen == _research);
            Activate(_shop, screen == _shop);
            Activate(_result, screen == _result);
            if (_workspace != null)
                _workspace.SetActive(screen == _operation);

            // Music follows the *visible* screen, never an early core RunPhase mutation.
            if (screen == _opening) MusicManager.Request(MusicState.Menu);
            else if (screen == _shop) MusicManager.Request(MusicState.Shop);
            else if (screen == _operation || screen == _levelStart)
                MusicManager.Request(MusicState.Level);
            else if (screen == _research)
                MusicManager.Request(_back == BackTarget.Shop ? MusicState.Shop : MusicState.Level);
            else if (screen == _result) MusicManager.Request(MusicState.Silent);
        }

        static void Activate(MonoBehaviour view, bool active)
        {
            if (view == null)
                return;

            var screen = view.gameObject;
            if (!screen)
                return;

            screen.SetActive(active);
        }

        bool CageContains(string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId))
                return false;

            var cage = this.SendQuery(new MonsterCageQuery());
            for (var i = 0; i < cage.Count; i++)
                if (cage[i].Id == monsterId)
                    return true;

            return false;
        }

        static bool Contains(IReadOnlyList<string> values, string value)
        {
            if (values == null || value == null)
                return false;

            for (var i = 0; i < values.Count; i++)
                if (values[i] == value)
                    return true;

            return false;
        }

        static void SetLabel(Transform root, string name, string value)
        {
            var found = FindChild(root, name);
            if (found == null)
                return;

            var label = found.GetComponent<TMP_Text>();
            if (label != null)
                label.text = value;
        }

        static void SetShown(Transform root, string name, bool shown)
        {
            var found = FindChild(root, name);
            if (found != null)
                found.gameObject.SetActive(shown);
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null)
                    return found;
            }

            return null;
        }

        static string SkillLine(MonsterView monster)
        {
            if (monster.Skills.Count == 0)
                return "";

            var skill = monster.Skills[0];
            var quote = skill.Quote > 0 ? "报价 " + skill.Quote : "效果";
            var rarity = skill.Rarity == Rarity.Gold ? "金" : skill.Rarity == Rarity.Blue ? "蓝" : "白";
            return monster.Skills.Count == 1 ? quote + " · " + rarity : quote + " · " + monster.Skills.Count + "项";
        }

        static string Join(IReadOnlyList<string> values)
        {
            var text = "";
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    text += "\n";
                text += values[i];
            }

            return text;
        }

        static void Listen(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null && action != null)
                button.onClick.AddListener(action);
        }

        static void ListenSlots(MonsterSlotView[] slots, Action<MonsterSlotView> handler)
        {
            if (slots == null)
                return;

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot != null && slot.button != null)
                    slot.button.onClick.AddListener(() => handler(slot));
            }
        }

        void ListenCards(ShopCardView[] cards)
        {
            if (cards == null)
                return;

            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card != null && card.buyButton != null)
                    card.buyButton.onClick.AddListener(() => OnBuy(card));
            }
        }

        void ListenNodes(TechNodeView[] nodes)
        {
            if (nodes == null)
                return;

            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node != null && node.button != null)
                    node.button.onClick.AddListener(() => OnSelectTech(node));
            }
        }
    }
}
