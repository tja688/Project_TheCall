using System;
using System.Collections;
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
        int _sessionVersion;
        readonly ButtonBindings _bindings = new ButtonBindings();
        SettlementPlayback _playback;
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

        void OnEnable()
        {
            Wire();
            if (_sessionStarted) Refresh();
        }

        void OnDisable()
        {
            RetirePlayback();
            _bindings.Dispose();
            _wired = false;
        }

        void RetirePlayback()
        {
            var panorama = _operation != null ? _operation.pointer?.panorama : null;
            if (panorama != null) panorama.Locked = false;
            _sessionVersion++;
            StopAllCoroutines();
            _playback?.Cancel();
            _playback = null;
            _busy = false;
        }

        void EnsureSpriteCatalog()
        {
            if (_spriteCatalog == null)
                _spriteCatalog = Resources.Load<PresentationSpriteCatalog>("PresentationSpriteCatalog");
        }

        public void StartSession()
        {
            RetirePlayback();
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
            BindAction(_opening != null ? _opening.exitButton : null, PresentationAction.Quit);
            ListenSlots(_opening != null ? _opening.candidates : null, OnKeep);
            BindAction(_levelStart != null ? _levelStart.beginButton : null, PresentationAction.BeginLevel);
            Listen(_operation != null ? _operation.shopButton : null, OnShopLocked);
            BindAction(_operation != null ? _operation.researchButton : null, PresentationAction.OpenResearch);
            BindAction(_operation != null ? _operation.titleButton : null, PresentationAction.ReturnToTitle);
            BindAction(_operation != null ? _operation.nextDayButton : null, PresentationAction.Settle);
            BindAction(_operation != null ? _operation.addGeneSlotButton : null, PresentationAction.AddGeneSlot);
            if (_operation != null && _operation.pointer != null)
                _operation.pointer.Attach(this);
            BindAction(_research != null ? _research.researchButton : null, PresentationAction.Research);
            BindAction(_research != null ? _research.backButton : null, PresentationAction.CloseResearch);
            ListenNodes(_research != null ? _research.nodes : null);
            BindAction(_shop != null ? _shop.buyTabButton : null, PresentationAction.BuyTab);
            BindAction(_shop != null ? _shop.sellTabButton : null, PresentationAction.SellTab);
            BindAction(_shop != null ? _shop.shopButton : null, PresentationAction.BuyTab);
            BindAction(_shop != null ? _shop.researchButton : null, PresentationAction.OpenResearch);
            Listen(_shop != null ? _shop.saveButton : null, () => Notice("这一版还不能存档。"));
            BindAction(_shop != null ? _shop.titleButton : null, PresentationAction.ReturnToTitle);
            if (_shop != null)
                _shop.EnsureRefreshControl();
            BindAction(_shop != null ? _shop.leaveButton : null, PresentationAction.LeaveShop);
            BindAction(_shop != null ? _shop.refreshButton : null, PresentationAction.RefreshShelf);
            ListenCards(_shop != null ? _shop.cards : null);
            ListenSlots(_shop != null ? _shop.sellSlots : null, OnSell);
            BindAction(_result != null ? _result.restartButton : null, PresentationAction.ReturnToTitle);
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
                    BindAction(buttons[i], PresentationAction.ToggleSettings);
            }

            BindAction(_settings.quitButton, PresentationAction.Quit);
            BindAction(_settings.codexButton, PresentationAction.OpenHandbook);
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

            _settings.ShowCodex(this.SendQuery(new HandbookIndexQuery()));
        }

        void BindLog()
        {
            _log = FindAnyObjectByType<ProductionLogView>(FindObjectsInactive.Include);
            var buttonObject = _operation != null ? _operation.transform.Find("LogButton") : null;
            BindAction(buttonObject != null ? buttonObject.GetComponent<UnityEngine.UI.Button>() : null, PresentationAction.ToggleLog);
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
            if (_playback != null && _playback.IsPlaying)
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
            _opening?.Render(GetArchitecture(), _spriteCatalog);
        }

        void DrawLevelStart()
        {
            Show(_levelStart);
            _levelStart?.Render(GetArchitecture());
        }

        void DrawOperation()
        {
            Show(_operation);
            _operation?.Render(GetArchitecture(), _spriteCatalog);
        }

        void DrawShop()
        {
            Show(_shop);
            _shop?.Render(GetArchitecture(), _spriteCatalog, _sellTab, BindSellSlot);
        }

        void DrawResearch()
        {
            Show(_research);
            if (_research != null) _selectedTech = _research.Render(GetArchitecture(), _selectedTech);
        }

        void DrawResult(bool victory)
        {
            Show(_result);
            _result?.Render(victory);
        }

        void BindSellSlot(MonsterSlotView slot) => Listen(slot != null ? slot.button : null, () => OnSell(slot));

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

        internal bool CommitDrop(OperationDrop drop)
        {
            if (_busy || !isActiveAndEnabled) return false;
            var applied = this.SendCommand(new CommitOperationDropCommand(drop));
            if (applied)
            {
                if (drop.Landing.Place == LandingPlace.Discard && _operation != null && _operation.lab != null)
                    _operation.lab.recycler?.Consume();
                Refresh();
                if (drop.Payload.Kind == PayloadKind.SkillChip && !string.IsNullOrEmpty(drop.Landing.MonsterId)) Notice("基因安装成功。");
                else if (drop.Payload.Kind == PayloadKind.SkillChip && drop.Landing.Place == LandingPlace.BreedingSocket) Notice("已预约此格基因用于培育；取回时恢复原格。");
                else if (drop.Payload.Kind == PayloadKind.BreedingSkill) Notice("培育基因已退还原格。");
            }
            else Notice(this.SendQuery(new OperationFeedbackQuery()) ?? "无法放入此位置。");
            return applied;
        }

        public void TriggerSettle() => OnSettle();

        public void Perform(PresentationAction action)
        {
            if (!isActiveAndEnabled) return;
            // Menu and reset controls remain available while gameplay is blocked by playback.
            if (_busy && action != PresentationAction.ReturnToTitle && action != PresentationAction.ToggleSettings
                && action != PresentationAction.OpenHandbook && action != PresentationAction.ToggleLog
                && action != PresentationAction.Quit && action != PresentationAction.OpenSettingsLog) return;
            switch (action)
            {
                case PresentationAction.None: break;
                case PresentationAction.BeginLevel: OnBegin(); break;
                case PresentationAction.Settle: OnSettle(); break;
                case PresentationAction.ReturnToTitle: OnTitle(); break;
                case PresentationAction.OpenResearch:
                    var phase = this.SendQuery(new RunPhaseQuery());
                    if (phase == RunPhase.Operation || phase == RunPhase.Shop)
                        OpenResearch(phase == RunPhase.Shop ? BackTarget.Shop : BackTarget.Operation);
                    break;
                case PresentationAction.CloseResearch: CloseResearch(); break;
                case PresentationAction.Research: OnResearch(); break;
                case PresentationAction.BuyTab: SetSellTab(false); break;
                case PresentationAction.SellTab: SetSellTab(true); break;
                case PresentationAction.RefreshShelf: OnRefreshShelf(); break;
                case PresentationAction.LeaveShop: OnLeaveShop(); break;
                case PresentationAction.AddGeneSlot: OnAddGeneSlot(); break;
                case PresentationAction.ToggleSettings: ToggleSettings(); break;
                case PresentationAction.OpenHandbook: OpenCodex(); break;
                case PresentationAction.ToggleLog: ToggleLog(); break;
                case PresentationAction.OpenSettingsLog: OpenLogFromSettings(); break;
                case PresentationAction.Quit: QuitGame(); break;
                default: throw new ArgumentOutOfRangeException(nameof(action), action, null);
            }
        }

        void OnSettle()
        {
            if (_busy)
                return;

            _busy = true;
            var panorama = _operation != null ? _operation.pointer?.panorama : null;
            if (panorama != null && (!panorama.Production || panorama.Moving))
            {
                StartCoroutine(SettleAfterSlide(panorama, _sessionVersion));
                return;
            }
            StartSettlement();
        }

        IEnumerator SettleAfterSlide(FacilityPanorama panorama, int version)
        {
            panorama.Locked = true;
            panorama.Focus(true);
            while (panorama != null && panorama.Moving) yield return null;
            if (panorama != null) panorama.Locked = false;
            if (this != null && isActiveAndEnabled && version == _sessionVersion && _operation.gameObject.activeInHierarchy)
                StartSettlement();
            else _busy = false;
        }

        void StartSettlement()
        {
            _playback = new SettlementPlayback(GetArchitecture());
            var version = _sessionVersion;
            try
            {
                if (!_playback.TryStart(_operation, Notice, () =>
                    {
                        if (this != null && version == _sessionVersion) FinishSettle();
                    })) _busy = false;
            }
            catch
            {
                _busy = false;
                throw;
            }
        }

        void FinishSettle()
        {
            // 演出销毁时仍会通知完成；此时所属界面可能已随场景一起销毁。
            if (this == null)
                return;

            _busy = false;
            if (!isActiveAndEnabled) return;
            var trimmed = this.SendQuery(new SkillTrimFeedbackQuery());
            if (!string.IsNullOrEmpty(trimmed) && trimmed != _lastTrimNotice)
            {
                _lastTrimNotice = trimmed;
                Notice("性格技能上限调整，移除：" + trimmed);
            }
            StartCoroutine(RefreshAfterSettle(_sessionVersion));
        }

        IEnumerator RefreshAfterSettle(int version)
        {
            yield return null;
            if (this != null && isActiveAndEnabled && version == _sessionVersion)
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
            RetirePlayback();
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
            try { send(); }
            finally { _busy = false; }
            Refresh();
        }

        void Run(Action send, Func<bool> succeeded, string failureNotice)
        {
            if (_busy)
                return;

            _busy = true;
            try { send(); }
            finally { _busy = false; }
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

        static bool Contains(System.Collections.Generic.IReadOnlyList<string> values, string value)
        {
            if (values == null || value == null)
                return false;

            for (var i = 0; i < values.Count; i++)
                if (values[i] == value)
                    return true;

            return false;
        }

        void Listen(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            _bindings.Listen(button, () =>
            {
                var declared = button != null ? button.GetComponent<PresentationActionButton>() : null;
                if (declared == null || !declared.isActiveAndEnabled || declared.Action == PresentationAction.None)
                    action?.Invoke();
            });
        }

        void BindAction(UnityEngine.UI.Button button, PresentationAction action) => Listen(button, () => Perform(action));

        void ListenSlots(MonsterSlotView[] slots, Action<MonsterSlotView> handler)
        {
            if (slots == null)
                return;

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot != null && slot.button != null)
                    Listen(slot.button, () => handler(slot));
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
                    Listen(card.buyButton, () => OnBuy(card));
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
                    Listen(node.button, () => OnSelectTech(node));
            }
        }
    }
}
