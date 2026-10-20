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
    /// 这里只协调输入、界面交接和演出生命周期。素材布局在场景中，临时姿态归 ScreenAssembly。
    /// </summary>
    public sealed partial class TheCallPresentation : MonoBehaviour, IController
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
            CancelTransition();
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
            _visibleScreen = null;
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
            Listen(_operation != null ? _operation.shopButton : null, OnShopLocked, false);
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
            Listen(_shop != null ? _shop.saveButton : null, () =>
            {
                UiSfx.PlayError();
                Notice("这一版还不能存档。");
            }, false);
            BindAction(_shop != null ? _shop.titleButton : null, PresentationAction.ReturnToTitle);
            if (_shop != null)
                _shop.EnsureRefreshControl();
            BindAction(_shop != null ? _shop.leaveButton : null, PresentationAction.LeaveShop);
            BindAction(_shop != null ? _shop.refreshButton : null, PresentationAction.RefreshShelf);
            ListenCards(_shop != null ? _shop.cards : null);
            ListenSlots(_shop != null ? _shop.sellSlots : null, OnSell, false);
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
            if (Transitioning)
            {
                _refreshAfterTransition = true;
                return;
            }
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
            Show(_opening, () => _opening?.Render(GetArchitecture(), _spriteCatalog));
        }

        void DrawLevelStart()
        {
            Show(_levelStart, () => _levelStart?.Render(GetArchitecture()));
        }

        void DrawOperation()
        {
            Show(_operation, () => _operation?.Render(GetArchitecture(), _spriteCatalog));
        }

        void DrawShop()
        {
            Show(_shop, () => _shop?.Render(GetArchitecture(), _spriteCatalog, _sellTab, BindSellSlot));
        }

        void DrawResearch()
        {
            Show(_research, () => { if (_research != null) _selectedTech = _research.Render(GetArchitecture(), _selectedTech); });
        }

        void DrawResult(bool victory)
        {
            Show(_result, () => _result?.Render(victory));
        }

        void BindSellSlot(MonsterSlotView slot) => Listen(slot != null ? slot.button : null, () => OnSell(slot), false);

        void OnAddGeneSlot()
        {
            var run = this.SendQuery(new RunLedgerQuery());
            if (run.GeneCapacity >= 4)
            {
                UiSfx.PlayError();
                Notice("基因仓储已解锁，已达容量上限。");
                return;
            }
            if (run.TechPoints < 1)
            {
                UiSfx.PlayError();
                Notice("科技点不足。解锁基因仓储需要 1 科技点。");
                return;
            }
            this.SendCommand(new UnlockTechCommand("基因仓储"));
            UiSfx.PlayClick();
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
            if (_busy || Transitioning || !isActiveAndEnabled) return false;
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
            else
            {
                UiSfx.PlayError();
                Notice(this.SendQuery(new OperationFeedbackQuery()) ?? "无法放入此位置。");
            }
            return applied;
        }

        public void TriggerSettle() => OnSettle();

    }
}
