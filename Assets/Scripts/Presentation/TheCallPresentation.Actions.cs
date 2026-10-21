using System;
using System.Collections;
using QFramework;
using TheCall.Scoring;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheCall
{
    public sealed partial class TheCallPresentation
    {
        public void Perform(PresentationAction action)
        {
            if (!isActiveAndEnabled) return;
            if (Transitioning && action != PresentationAction.ReturnToTitle && action != PresentationAction.Quit) return;
            // Menu and reset controls remain available while gameplay is blocked by playback.
            if (_busy && action != PresentationAction.ReturnToTitle && action != PresentationAction.ToggleSettings
                && action != PresentationAction.OpenHandbook && action != PresentationAction.ToggleLog
                && action != PresentationAction.Quit && action != PresentationAction.OpenSettingsLog) return;
            if (action != PresentationAction.None && !UsesDedicatedCue(action))
                UiSfx.PlayClick();
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
            if (_busy || Transitioning)
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

        void OnShopLocked()
        {
            UiSfx.PlayError();
            Notice("结算并发工资之后才进入商店。");
        }

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
                UiSfx.PlayError();
                Notice("先点一项科技。");
                return;
            }

            if (this.SendQuery(new RunPhaseQuery()) != RunPhase.Operation)
            {
                UiSfx.PlayError();
                Notice("科技只能在操作阶段研究。");
                return;
            }

            var name = _selectedTech;
            var before = this.SendQuery(new RunLedgerQuery());
            this.SendCommand(new UnlockTechCommand(name));
            var after = this.SendQuery(new RunLedgerQuery());
            if (Contains(after.UnlockedTech, name) && !Contains(before.UnlockedTech, name))
            {
                GameplaySfx.Play(GameplayCue.Obtain);
                Notice("研究完成。");
            }
            else if (Contains(before.UnlockedTech, name))
            {
                UiSfx.PlayError();
                Notice("这项已经研究过了。");
            }
            else
            {
                UiSfx.PlayError();
                Notice("科技点不足。");
            }

            Refresh();
        }

        void OnSelectTech(TechNodeView node)
        {
            if (Transitioning) return;
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
            if (card == null)
                return;

            if (!string.IsNullOrEmpty(card.monsterId))
            {
                var id = card.monsterId;
                var listed = ShelfListsMonster(id);
                Run(() => this.SendCommand(new BuyMonsterCommand(id)));
                if (listed && !ShelfListsMonster(id)) UiSfx.PlayPurchase();
                else UiSfx.PlayError();
                return;
            }

            if (string.IsNullOrEmpty(card.toolName))
            {
                UiSfx.PlayClick();
                return;
            }

            var tool = card.toolName;
            var owned = Contains(this.SendQuery(new RunLedgerQuery()).Tools, tool);
            Run(() => this.SendCommand(new BuyToolCommand(tool)));
            if (!owned && Contains(this.SendQuery(new RunLedgerQuery()).Tools, tool)) UiSfx.PlayPurchase();
            else UiSfx.PlayError();
        }

        void OnSell(MonsterSlotView slot)
        {
            if (slot == null || string.IsNullOrEmpty(slot.monsterId))
            {
                UiSfx.PlayClick();
                return;
            }

            var id = slot.monsterId;
            if (!CageContains(id))
            {
                UiSfx.PlayError();
                return;
            }

            Run(
                () => this.SendCommand(new SellMonsterCommand(id)),
                () => !CageContains(id),
                "这只现在不能出售。锁定中的亲本要等后代出生。");
            if (!CageContains(id)) UiSfx.PlayClick();
            else UiSfx.PlayError();
        }

        void OnRefreshShelf()
        {
            var before = this.SendQuery(new RunLedgerQuery()).Gold;
            this.SendCommand(new RefreshShelfCommand());
            if (this.SendQuery(new RunLedgerQuery()).Gold < before) UiSfx.PlayClick();
            else UiSfx.PlayError();
            Refresh();
        }

        void OnLeaveShop()
        {
            var before = this.SendQuery(new RunPhaseQuery());
            Run(() => this.SendCommand(new LeaveShopCommand()));
            if (before == RunPhase.Shop && this.SendQuery(new RunPhaseQuery()) != RunPhase.Shop)
                UiSfx.PlayClockOut();
            else
                UiSfx.PlayError();
        }

        static bool UsesDedicatedCue(PresentationAction action) =>
            action == PresentationAction.Settle
            || action == PresentationAction.LeaveShop
            || action == PresentationAction.RefreshShelf
            || action == PresentationAction.Research
            || action == PresentationAction.AddGeneSlot;

        bool ShelfListsMonster(string id)
        {
            var monsters = this.SendQuery(new ShelfQuery()).Monsters;
            for (var i = 0; i < monsters.Count; i++)
                if (monsters[i].Id == id)
                    return true;
            return false;
        }

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
            if (_busy || Transitioning || _opening == null || !_opening.gameObject.activeInHierarchy)
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
            if (_busy || Transitioning)
                return;

            _busy = true;
            try { send(); }
            finally { _busy = false; }
            Refresh();
        }

        void Run(Action send, Func<bool> succeeded, string failureNotice)
        {
            if (_busy || Transitioning)
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

        void ShowImmediate(MonoBehaviour screen)
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

        void Listen(UnityEngine.UI.Button button, UnityEngine.Events.UnityAction action, bool cue = true)
        {
            if (button == null) return;
            _bindings.Listen(button, () =>
            {
                var declared = button != null ? button.GetComponent<PresentationActionButton>() : null;
                if (declared != null && declared.isActiveAndEnabled && declared.Action != PresentationAction.None)
                    return;
                if (cue) UiSfx.PlayClick();
                action?.Invoke();
            });
        }

        void BindAction(UnityEngine.UI.Button button, PresentationAction action) =>
            Listen(button, () => Perform(action), false);

        void ListenSlots(MonsterSlotView[] slots, Action<MonsterSlotView> handler, bool cue = true)
        {
            if (slots == null)
                return;

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot != null && slot.button != null)
                    Listen(slot.button, () => handler(slot), cue);
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
                    Listen(card.buyButton, () => OnBuy(card), false);
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
