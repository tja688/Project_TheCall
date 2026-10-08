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
        [SerializeField] GameObject _toast;
        [SerializeField] TMP_Text _toastText;

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
        ProductionLogView _log;
        string _selectedTech = "基因实验";
        float _noticeUntil;

        public IArchitecture GetArchitecture() => TheCallApp.Interface;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BeginPlaySession()
        {
            var presentation = FindAnyObjectByType<TheCallPresentation>();
            if (presentation != null)
                presentation.StartSession();
        }

        void Awake()
        {
            Wire();
            StartSession();
        }

        void StartSession()
        {
            _back = BackTarget.None;
            _sellTab = false;
            _busy = false;
            if (_log != null)
                _log.Hide();
            if (_toast != null)
                _toast.SetActive(false);

            Refresh();
        }

        void Update()
        {
            if (_toast != null && _toast.activeSelf && Time.unscaledTime >= _noticeUntil)
                _toast.SetActive(false);

            if (EscapePressedThisFrame() && CanQuitFromOpening())
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
            Listen(_operation != null ? _operation.titleButton : null, OnTitle);
            Listen(_operation != null ? _operation.nextDayButton : null, OnSettle);
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
        }

        void BindLog()
        {
            _log = FindAnyObjectByType<ProductionLogView>(FindObjectsInactive.Include);
            var buttonObject = _operation != null ? _operation.transform.Find("LogButton") : null;
            Listen(buttonObject != null ? buttonObject.GetComponent<UnityEngine.UI.Button>() : null, ToggleLog);
        }

        void ToggleLog()
        {
            if (_log == null)
                return;

            if (_log.IsOpen)
                _log.Hide();
            else
                _log.Show(ProductionLogText.Format(this.SendQuery(new ProductionLogQuery())));
        }

        internal void Refresh()
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
            _levelStart.excessLabel.text = target.ExcessEnergy + "+";
        }

        void DrawOperation()
        {
            Show(_operation);
            var run = this.SendQuery(new RunLedgerQuery());
            var target = this.SendQuery(new LevelTargetQuery());
            var energy = this.SendQuery(new LevelEnergyQuery());
            var shortfall = this.SendQuery(new LevelShortfallQuery());
            _operation.targetLabel.text = target.EnergyDue.ToString();
            _operation.energyLabel.text = shortfall > 0 ? "欠额 " + shortfall : "当前 " + energy;
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
                    current.text = "0";
            }
            _operation.goldLabel.text = run.Gold.ToString();
            _operation.nextDayLabel.text = shortfall > 0
                ? "补上欠额"
                : target.LevelNumber >= 7 ? "结束第7天" : "进入第" + (target.LevelNumber + 1) + "天";

            var cage = this.SendQuery(new MonsterCageQuery());
            FillList(_operation.cageSlots, cage, ArmedMonsterId());
            FillCells(_operation.extractionSlots, this.SendQuery(new ExtractionSlotsQuery()));
            FillParents(_operation.breedingSlots, this.SendQuery(new BreedingSlotsQuery()));
            FillSkills(_operation.skillChips, run.SkillSlots);
            FillBreedingSkills(_operation.breedingSkills, this.SendQuery(new BreedingPlansQuery()));
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

            var monsterCursor = 0;
            var toolCursor = 0;
            var cards = _shop.cards;
            for (var i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                var toolSlot = i == 2 || i == 5;
                if (!toolSlot && monsterCursor < shelf.Monsters.Count)
                    FillMonsterCard(card, shelf.Monsters[monsterCursor++], run.Gold);
                else if (toolSlot && toolCursor < shelf.Tools.Count)
                    FillToolCard(card, shelf.Tools[toolCursor++], run.Gold);
                else
                    card.gameObject.SetActive(false);
            }

            var cage = this.SendQuery(new MonsterCageQuery());
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
                    _research.researchButton.interactable = !unlocked;
            }
        }

        void DrawResult(bool victory)
        {
            Show(_result);
            _result.title.text = victory ? "七关都交上去了" : "账单没有补上";
            _result.body.text = victory
                ? "这一局结束。怪物笼、金币和科技都已清空。"
                : "加班后仍未补足欠额。这一局结束。";
            if (_result.portrait != null)
                _result.portrait.Show(MonsterAppearance.FromSeed(victory ? 73 : 29));
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

        void FillSkills(SkillChipView[] chips, IReadOnlyList<string> skills)
        {
            for (var i = 0; i < chips.Length; i++)
            {
                var has = skills != null && i < skills.Count;
                chips[i].index = i;
                chips[i].label.text = has ? skills[i] : "空";
                if (chips[i].selection != null)
                    chips[i].selection.SetActive(has && IsArmed(DropPayload.SkillChip(i)));
                if (chips[i].button != null)
                    chips[i].button.interactable = has;
            }
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
                slot.title.text = occupied ? monster.DisplayName : "";
            if (slot.subtitle != null)
                slot.subtitle.text = occupied ? SkillLine(monster) : "";
            if (slot.selection != null)
                slot.selection.SetActive(selected);
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
            if (card.icon != null)
            {
                card.icon.enabled = true;
                card.icon.type = UnityEngine.UI.Image.Type.Simple;
                card.icon.preserveAspect = true;
                card.icon.color = Color.white;
                var art = FindArt(item.Name);
                if (art != null && art.icon != null)
                    card.icon.sprite = art.icon;
            }

            var artEntry = FindArt(item.Name);
            card.title.text = item.Name;
            card.body.text = artEntry != null ? artEntry.description : "";
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
            Run(
                () => this.SendCommand(new KeepOpeningMonsterCommand(id)),
                () => this.SendQuery(new RunPhaseQuery()) == RunPhase.Operation,
                "已留下。",
                "没能留下这只。");
        }

        void OnBegin()
        {
            Run(
                () => this.SendCommand(new BeginLevelCommand()),
                () => this.SendQuery(new RunPhaseQuery()) == RunPhase.Operation,
                "进入操作。",
                "这一关还不能开始。");
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
                Refresh();

            return applied;
        }

        void OnSettle()
        {
            if (_busy)
                return;

            _busy = true;
            this.SendCommand(new ConfirmSettlementCommand());
            ScoringShow.Play(new ScoringStage(
                this.GetArchitecture(),
                _operation.extractionSlots,
                _operation.transform as RectTransform,
                _operation.dragLayer,
                _operation.goldLabel,
                _operation.currentNumber,
                Notice,
                FinishSettle));
        }

        void FinishSettle()
        {
            _busy = false;
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
                Run(
                    () => this.SendCommand(new BuyMonsterCommand(id)),
                    () => CageContains(id),
                    "已买下。",
                    "没有买成。");
                return;
            }

            if (string.IsNullOrEmpty(card.toolName))
                return;

            var tool = card.toolName;
            Run(
                () => this.SendCommand(new BuyToolCommand(tool)),
                () => Contains(this.SendQuery(new RunLedgerQuery()).Tools, tool),
                "工具已买下。",
                "没有买成。");
        }

        void OnSell(MonsterSlotView slot)
        {
            if (string.IsNullOrEmpty(slot.monsterId))
                return;

            var id = slot.monsterId;
            Run(
                () => this.SendCommand(new SellMonsterCommand(id)),
                () => !CageContains(id),
                "已出售。",
                "这只现在不能出售。锁定中的亲本要等后代出生。");
        }

        void OnRefreshShelf()
        {
            this.SendCommand(new RefreshShelfCommand());
            Refresh();
        }

        void OnLeaveShop()
        {
            var before = this.SendQuery(new RunPhaseQuery());
            Run(
                () => this.SendCommand(new LeaveShopCommand()),
                () => this.SendQuery(new RunPhaseQuery()) != before,
                "离开商店。",
                "现在还不能离开。");
        }

        void OnTitle()
        {
            TheCallApp.Reset();
            StartSession();
            Notice("回到开局。");
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

        void Run(Action send, Func<bool> succeeded, string success, string failure)
        {
            if (_busy)
                return;

            _busy = true;
            send();
            _busy = false;
            Notice(succeeded() ? success : failure);

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
