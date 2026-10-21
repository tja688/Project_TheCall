using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheCall.Scoring
{
    public readonly struct ScoringStage
    {
        public ScoringStage(
            ScoringTape tape,
            MonsterSlotView[] extractionSlots,
            RectTransform boardRoot,
            RectTransform dragLayer,
            TMP_Text goldLabel,
            TMP_Text currentNumber,
            Action<string> say,
            Action completed,
            int initialEnergy = 0, int initialGold = 0,
            Action<int, int> moved = null, Action<int> removed = null, Action<int> energyShown = null, FacilitySceneView facility = null,
            int energyDue = 0)
        {
            Tape = tape;
            ExtractionSlots = extractionSlots;
            BoardRoot = boardRoot;
            DragLayer = dragLayer;
            GoldLabel = goldLabel;
            CurrentNumber = currentNumber;
            Say = say;
            Completed = completed;
            InitialEnergy = initialEnergy;
            InitialGold = initialGold;
            Moved = moved;
            Removed = removed;
            EnergyShown = energyShown;
            Facility = facility;
            EnergyDue = energyDue;
        }

        public ScoringTape Tape { get; }
        public MonsterSlotView[] ExtractionSlots { get; }
        public RectTransform BoardRoot { get; }
        public RectTransform DragLayer { get; }
        public TMP_Text GoldLabel { get; }
        public TMP_Text CurrentNumber { get; }
        public Action<string> Say { get; }
        public Action Completed { get; }
        public int InitialEnergy { get; }
        public int InitialGold { get; }
        public Action<int, int> Moved { get; }
        public Action<int> Removed { get; }
        public Action<int> EnergyShown { get; }
        public FacilitySceneView Facility { get; }
        public int EnergyDue { get; }
    }

    public sealed partial class ScoringShow : MonoBehaviour, ICueSink
    {
        const float WalkSeconds = 0.18f;
        const float SwapSeconds = 0.28f;
        const float RemoveSeconds = 0.22f;
        const float ShakeSeconds = 0.28f;
        const float PaceWarmup = 10f;
        const float PacePerStep = 0.08f;
        const float PaceCeiling = 3f;
        const float PayPaceCeiling = 2f;
        const float SkipPace = 4f;
        const float FigureLinger = 1.5f;
        const float FigureInSeconds = 0.12f * FigureLinger;
        const float FigureStaySeconds = 0.1f * FigureLinger;
        const float FigureOutSeconds = 0.1f * FigureLinger;
        const float PunchSeconds = 0.08f * FigureLinger;
        const float SettleSeconds = 0.1f * FigureLinger;
        const float FigureShakeSeconds = 0.16f * FigureLinger;
        const float PaySeconds = 0.9f * FigureLinger;

        readonly object TweenId = new object();
        static ScoringShow _active;

        ScoringStage _stage;
        ScoringTape _tape;
        BoardSlot[] _slots;
        RectTransform _blocker;
        TMP_Text _current;
        Vector3 _currentRestScale;
        Color _currentRestColor;
        Image _playhead;
        Image _flash;
        Material _flashMaterial;
        Sprite _pixel;
        Texture2D _pixelTexture;
        Vector2 _restBoardPosition;
        bool _boardHeld;
        int _shown;
        int _shownGold;
        float _pace = 1f;
        bool _boosted;
        bool _cutToPay;
        bool _finishPay;
        bool _paying;
        bool _finished;
        bool _cancelled;

        struct BoardSlot
        {
            public MonsterSlotView View;
            public MonsterPortrait Portrait;
            public string MonsterId;
            public Vector3 RestScale;
            public Vector2 RestAnchoredPosition;
        }

        public static bool IsPlaying => _active != null;

        public static ScoringShow Play(ScoringStage stage)
        {
            if (stage.Tape == null || stage.BoardRoot == null || stage.DragLayer == null || !stage.DragLayer.gameObject.activeInHierarchy)
            {
                stage.Completed?.Invoke();
                return null;
            }

            if (_active != null)
            {
                stage.Completed?.Invoke();
                return null;
            }

            var host = new GameObject("ScoringShow");
            host.transform.SetParent(stage.DragLayer, false);
            var show = host.AddComponent<ScoringShow>();
            _active = show;
            show.Begin(stage);
            return show;
        }

        public void Cancel()
        {
            _cancelled = true;
            StopAllCoroutines();
            Finish();
        }

        public void Advance() => OnBlocked();

        void OnDisable() => Cancel();

        void Begin(ScoringStage stage)
        {
            _stage = stage;
            _shownGold = stage.InitialGold;
            BeginSfx();
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            try
            {
                var ids = ReadIds(_stage.ExtractionSlots);
                _tape = _stage.Tape;
                Capture(ids);
                _restBoardPosition = _stage.BoardRoot.anchoredPosition;
                _boardHeld = true;
                BindCurrent();
                BuildOverlay();
                BeginEnergy();
                for (var i = 0; i < _tape.Cues.Count; i++)
                {
                    var cue = _tape.Cues[i];
                    if (cue is PayCue)
                    {
                        // Finish receiving energy with the current fast-forward/skip state.
                        yield return FlushEnergy();
                        // Pay presentation is the first actual settlement screen, not Confirm().
                        MusicManager.EnterSettlement();
                        _cutToPay = false;
                        _pace = Mathf.Min(_pace, PayPaceCeiling);
                        _paying = true;
                    }
                    else
                    {
                        var ramp = Mathf.Min(PaceCeiling, 1f + Mathf.Max(0f, i - PaceWarmup) * PacePerStep);
                        _pace = Mathf.Max(_pace, ramp);
                    }

                    yield return cue.Accept(this);
                    if (cue is PayCue)
                        _paying = false;
                }

                yield return FlushEnergy();
                Finish();
            }
            finally
            {
                if (!_finished)
                    Finish();
            }
        }

        void Finish()
        {
            if (_finished)
                return;

            _finished = true;
            if (_active == this)
                _active = null;

            TeardownPresentation();
            NotifyCompleted();
            if (this != null && !gameObject.IsDestroying())
                Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (!_finished)
            {
                _cancelled = true;
                _finished = true;
                if (_active == this)
                    _active = null;
                TeardownPresentation();
                NotifyCompleted();
            }

            if (_active == this)
                _active = null;
        }

        void TeardownPresentation()
        {
            if (_cancelled) StopSfx();
            DOTween.Kill(TweenId);
            EndEnergy();
            RestoreCurrent();
            RestoreSlots();
            if (_boardHeld && _stage.BoardRoot != null)
                _stage.BoardRoot.anchoredPosition = _restBoardPosition;

            if (_flashMaterial != null)
            {
                Destroy(_flashMaterial);
                _flashMaterial = null;
            }

            if (_pixel != null)
            {
                Destroy(_pixel);
                _pixel = null;
            }

            if (_pixelTexture != null)
            {
                Destroy(_pixelTexture);
                _pixelTexture = null;
            }

            if (_blocker != null)
            {
                _blocker.gameObject.SetActive(false);
                Destroy(_blocker.gameObject);
                _blocker = null;
            }
        }

        void NotifyCompleted()
        {
            var completed = _stage.Completed;
            completed?.Invoke();
        }

        static string[] ReadIds(MonsterSlotView[] slots)
        {
            if (slots == null)
                return Array.Empty<string>();

            var ids = new string[slots.Length];
            for (var i = 0; i < slots.Length; i++)
            {
                var id = slots[i] != null ? slots[i].monsterId : null;
                ids[i] = string.IsNullOrEmpty(id) ? null : id;
            }

            return ids;
        }

        IEnumerator ICueSink.Walk(WalkCue cue) => Walk(cue);
        IEnumerator ICueSink.Pop(PopCue cue) => Pop(cue);
        IEnumerator ICueSink.Swap(SwapCue cue) => Swap(cue);
        IEnumerator ICueSink.Remove(RemovalCue cue) => Remove(cue);
        IEnumerator ICueSink.Pay(PayCue cue) => Pay(cue);
        IEnumerator ICueSink.Mark(MarkCue cue) => Mark(cue);

        IEnumerator Walk(WalkCue cue)
        {
            while (_draining) yield return null;
            BeginDrain();
            var point = PointOf(cue.ToIndex);
            if (point == null)
            {
                _playhead.gameObject.SetActive(false);
                yield break;
            }

            var target = HeadOf(cue.ToIndex) + new Vector2(0f, 70);
            _playhead.gameObject.SetActive(true);
            if (cue.FromIndex < 0 || SkipWait())
            {
                _playhead.rectTransform.anchoredPosition = target;
                yield break;
            }

            var tween = Live(_playhead.rectTransform.DOAnchorPos(target, Span(WalkSeconds)));
            yield return Wait(WalkSeconds);
            tween.Kill(false);
            _playhead.rectTransform.anchoredPosition = target;
        }

        IEnumerator Mark(MarkCue cue)
        {
            ActiveSlot = cue.SlotIndex;
            EnergyPhase = cue.Tone == MarkTone.Miss ? "zero" : "mark";
            if (!SkipWait() && cue.Amount > 0 && (cue.Tone == MarkTone.Gold || cue.Tone == MarkTone.Skills))
                PlaySfx(GameplayCue.Obtain);
            if (cue.Tone == MarkTone.Miss) DisplayedSkillEnergy = 0;
            if (cue.Tone == MarkTone.Gold && cue.Amount != 0) yield return RollGold(cue.Amount);
            var head = HeadOf(cue.SlotIndex);
            var label = Spawn(cue.Label, head + new Vector2(0f, -22f));
            label.fontSize = cue.Label.Length > 6 ? 40f : 52f;
            label.color = ToneColor(cue.Tone);
            label.rectTransform.localScale = Vector3.one * 0.4f;
            if (cue.Motion == MarkMotion.Shake)
                yield return ShakeFree(cue.SlotIndex);
            else
                yield return SpringMark(cue.SlotIndex);

            var rise = Live(label.rectTransform.DOAnchorPos(head + new Vector2(0f, 18f), Span(FigureInSeconds)).SetEase(Ease.OutBack));
            var grow = Live(label.rectTransform.DOScale(1f, Span(FigureInSeconds)).SetEase(Ease.OutBack));
            yield return Wait(FigureInSeconds);
            rise.Kill(false);
            grow.Kill(false);
            if (SkipWait())
            {
                Destroy(label.gameObject);
                yield break;
            }

            yield return Wait(FigureStaySeconds);
            var fade = Live(label.DOFade(0f, Span(FigureOutSeconds)));
            yield return Wait(FigureOutSeconds);
            fade.Kill(false);
            Destroy(label.gameObject);
        }

        IEnumerator SpringMark(int index)
        {
            var portrait = PortraitAt(index);
            if (portrait != null)
                portrait.SetSpringMotion(-14f, 9f);

            var root = RootAt(index);
            var rest = root != null && InRange(index) ? _slots[index].RestScale : Vector3.one;
            Tween grow = null;
            if (root != null)
                grow = Live(root.DOScale(rest * 1.12f, Span(PunchSeconds)).SetEase(Ease.OutQuad));

            yield return Wait(PunchSeconds);
            grow?.Kill(false);
            Tween back = null;
            if (root != null)
                back = Live(root.DOScale(rest, Span(SettleSeconds)).SetEase(Ease.OutQuad));

            yield return Wait(SettleSeconds);
            back?.Kill(false);
            if (root != null)
                root.localScale = rest;
            if (portrait != null)
                portrait.RestoreMotion();
        }

        static Color ToneColor(MarkTone tone)
        {
            switch (tone)
            {
                case MarkTone.Miss:
                    return new Color(0.85f, 0.12f, 0.12f, 1f);
                case MarkTone.Gold:
                    return new Color(1f, 0.82f, 0.25f, 1f);
                case MarkTone.Again:
                    return new Color(0.45f, 0.9f, 1f, 1f);
                default:
                    return new Color(1f, 0.93f, 0.55f, 1f);
            }
        }

        IEnumerator ShakeFree(int index)
        {
            var root = RootAt(index);
            var portrait = PortraitAt(index);
            if (portrait != null)
                portrait.SetSpringMotion(-16f, 11f);

            var rest = InRange(index) ? _slots[index].RestAnchoredPosition : Vector2.zero;
            Tween shake = null;
            if (root != null)
                shake = Live(root.DOShakeAnchorPos(Span(FigureShakeSeconds), 13f, 16, 90f, false, true));
            yield return Wait(FigureShakeSeconds);
            shake?.Kill(false);
            if (root != null)
                root.anchoredPosition = rest;
            if (portrait != null)
                portrait.RestoreMotion();
        }

        int ActorOf(int subject, string sourceMonsterId)
        {
            if (string.IsNullOrEmpty(sourceMonsterId) || _slots == null)
                return subject;

            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].MonsterId != sourceMonsterId)
                    continue;

                if (i == subject)
                    return subject;

                var root = Root(_slots[i]);
                if (root != null && root.gameObject.activeInHierarchy)
                    return i;

                return -1;
            }

            return -1;
        }

        void RestoreSlot(int index)
        {
            var portrait = PortraitAt(index);
            if (portrait != null)
                portrait.RestoreMotion();

            var root = RootAt(index);
            if (root != null && InRange(index))
                root.localScale = _slots[index].RestScale;
        }

        IEnumerator Swap(SwapCue cue)
        {
            yield return FlushEnergy();
            if (!cue.Happened)
            {
                if (cue.ActorIndex >= 0)
                    yield return SpringOnce(cue.ActorIndex);
                yield break;
            }

            Lift(cue.ActorIndex);
            Lift(cue.TargetIndex);
            var actorRoot = RootAt(cue.ActorIndex);
            var targetRoot = RootAt(cue.TargetIndex);
            if (actorRoot != null && targetRoot != null)
            {
                var layer = MotionLayer(actorRoot, targetRoot);
                actorRoot.SetParent(layer, true);
                targetRoot.SetParent(layer, true);
                actorRoot.SetAsLastSibling();
                targetRoot.SetAsLastSibling();
                var actorLift = actorRoot.anchoredPosition;
                var targetLift = targetRoot.anchoredPosition;
                var actorTween = Live(actorRoot.DOAnchorPos(targetLift, Span(SwapSeconds)));
                var targetTween = Live(targetRoot.DOAnchorPos(actorLift, Span(SwapSeconds)));
                if (!_cutToPay)
                    yield return Wait(SwapSeconds);
                actorTween.Kill(false);
                targetTween.Kill(false);
            }

            CommitSwap(cue.ActorIndex, cue.TargetIndex);
            PlacePlayhead(cue.TargetIndex);
        }

        IEnumerator Remove(RemovalCue cue)
        {
            yield return FlushEnergy();
            if (cue.Happened)
            {
                if (!SkipWait()) PlaySfx(GameplayCue.SkillKill);
                var root = RootAt(cue.VictimIndex);
                var rest = InRange(cue.VictimIndex) ? _slots[cue.VictimIndex].RestScale : Vector3.one;
                if (root != null)
                {
                    var tween = Live(root.DOScale(Vector3.zero, Span(RemoveSeconds)));
                    yield return Wait(RemoveSeconds);
                    tween.Kill(false);
                    root.gameObject.SetActive(false);
                    root.localScale = rest;
                }

                if (InRange(cue.VictimIndex))
                {
                    var slot = _slots[cue.VictimIndex];
                    HoverRide.Detach(slot.Portrait);
                    slot.MonsterId = null;
                    if (slot.View != null)
                        slot.View.monsterId = null;
                    _slots[cue.VictimIndex] = slot;
                    _stage.Removed?.Invoke(cue.VictimIndex);
                }

                yield break;
            }

            if (cue.VictimIndex >= 0)
            {
                yield return ShakeRoot(cue.VictimIndex, 12f);
                yield break;
            }

            var anchor = PortraitAt(cue.AnchorIndex);
            if (anchor != null)
            {
                anchor.SetSpringMotion(-10f, 7f);
                yield return Wait(0.22f);
                anchor.RestoreMotion();
                yield break;
            }

            yield return ShakeBoard(22f);
        }

        IEnumerator Pay(PayCue cue)
        {
            if (_stage.Say != null)
                _stage.Say(cue.Line);
            if (_shown != cue.Produced)
                throw new InvalidOperationException("合计 " + _shown + " 和产出 " + cue.Produced + " 不一致");

            TextMeshProUGUI label = null;
            if (cue.Kind == PayKind.Paid && cue.Excess)
                label = Spawn("超额", BesideTotal());
            else if (cue.Kind == PayKind.Paid || cue.Kind == PayKind.Short)
                label = Spawn("累计 " + cue.Produced, BesideTotal());

            if (cue.Wage > 0)
            {
                PlaySfx(GameplayCue.Obtain);
                yield return RollGold(cue.Wage);
            }
            else if (cue.Kind == PayKind.Paid && cue.Excess)
                PlaySfx(GameplayCue.Obtain);

            if (cue.Kind == PayKind.Short)
                yield return ShakeBoard(14f);
            else if (cue.Kind == PayKind.Failed)
            {
                Flash(new Color(0.85f, 0.12f, 0.12f, 0.55f));
                yield return ShakeBoard(28f);
            }
            else if (cue.Kind == PayKind.Paid && cue.Excess)
                Flash(new Color(1f, 0.92f, 0.45f, 0.5f));

            yield return Wait(PaySeconds);
            if (label != null)
                Destroy(label.gameObject);
        }

        IEnumerator Wait(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                if (SkipWait())
                    yield break;
                elapsed += Time.unscaledDeltaTime * _pace;
                yield return null;
            }
        }

        void Capture(string[] ids)
        {
            var views = _stage.ExtractionSlots;
            _slots = new BoardSlot[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                var view = views != null && i < views.Length ? views[i] : null;
                var portrait = view != null ? view.portrait : null;
                var root = portrait != null ? portrait.transform as RectTransform : null;
                _slots[i] = new BoardSlot
                {
                    View = view,
                    Portrait = portrait,
                    MonsterId = ids[i],
                    RestScale = root != null ? root.localScale : Vector3.one,
                    RestAnchoredPosition = root != null ? root.anchoredPosition : Vector2.zero,
                };
            }
        }

        void BuildOverlay()
        {
            var blockerObject = new GameObject("ScoringBlocker", typeof(RectTransform), typeof(Image), typeof(BlockerHit));
            blockerObject.transform.SetParent(_stage.BoardRoot, false);
            _blocker = blockerObject.GetComponent<RectTransform>();
            Stretch(_blocker);
            var overlay = blockerObject.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 50;
            blockerObject.AddComponent<GraphicRaycaster>();
            blockerObject.AddComponent<HoverPassthrough>();
            var blockerImage = blockerObject.GetComponent<Image>();
            blockerImage.sprite = Pixel();
            blockerImage.color = new Color(1f, 1f, 1f, 0f);
            blockerImage.raycastTarget = true;
            blockerObject.GetComponent<BlockerHit>().Show = this;
            blockerObject.transform.SetAsLastSibling();

            _flash = CreateImage("ScoringFlash", _blocker);
            Stretch(_flash.rectTransform);
            _flash.color = new Color(1f, 1f, 1f, 0f);
            _flash.raycastTarget = false;
            var shader = Shader.Find("UI/ScoringFlash");
            if (shader != null)
            {
                _flashMaterial = new Material(shader);
                _flash.material = _flashMaterial;
            }

            _playhead = CreateImage("ScoringPlayhead", _blocker);
            _playhead.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _playhead.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _playhead.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _playhead.rectTransform.sizeDelta = new Vector2(36f, 8f);
            _playhead.color = new Color(1f, 0.82f, 0.25f, 1f);
            _playhead.raycastTarget = false;
            _playhead.gameObject.SetActive(false);
            _flash.transform.SetAsFirstSibling();
        }

        void RestoreSlots()
        {
            if (_slots == null)
                return;

            for (var i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot.Portrait == null)
                    continue;

                var root = slot.Portrait.transform as RectTransform;
                if (root == null || root.gameObject.IsDestroying())
                    continue;

                if (slot.View != null && root.parent != slot.View.transform)
                {
                    root.SetParent(slot.View.transform, false);
                    root.anchoredPosition = slot.RestAnchoredPosition;
                    root.localScale = slot.RestScale;
                }
                else
                {
                    root.localScale = slot.RestScale;
                    if (slot.View != null && root.parent == slot.View.transform)
                        root.anchoredPosition = slot.RestAnchoredPosition;
                }

                root.gameObject.SetActive(true);
                slot.Portrait.RestoreMotion();
            }
        }

        void CommitSwap(int actorIndex, int targetIndex)
        {
            var actor = _slots[actorIndex];
            var target = _slots[targetIndex];
            var actorRoot = Root(actor);
            var targetRoot = Root(target);
            if (actorRoot != null && target.View != null)
            {
                actorRoot.DOKill();
                actorRoot.SetParent(target.View.transform, false);
                actorRoot.anchoredPosition = target.RestAnchoredPosition;
                actorRoot.localScale = actor.RestScale;
            }

            if (targetRoot != null && actor.View != null)
            {
                targetRoot.DOKill();
                targetRoot.SetParent(actor.View.transform, false);
                targetRoot.anchoredPosition = actor.RestAnchoredPosition;
                targetRoot.localScale = target.RestScale;
            }

            if (actor.View != null && target.View != null)
            {
                var portrait = actor.View.portrait;
                actor.View.portrait = target.View.portrait;
                target.View.portrait = portrait;
                SwapText(actor.View.title, target.View.title);
                SwapText(actor.View.subtitle, target.View.subtitle);
            }

            var actorPortrait = actor.Portrait;
            var actorId = actor.MonsterId;
            var actorScale = actor.RestScale;
            actor.Portrait = target.Portrait;
            actor.MonsterId = target.MonsterId;
            actor.RestScale = target.RestScale;
            target.Portrait = actorPortrait;
            target.MonsterId = actorId;
            target.RestScale = actorScale;
            if (actor.View != null)
                actor.View.monsterId = actor.MonsterId;
            if (target.View != null)
                target.View.monsterId = target.MonsterId;
            HoverRide.Detach(actor.Portrait);
            HoverRide.Detach(target.Portrait);
            _slots[actorIndex] = actor;
            _slots[targetIndex] = target;
            _stage.Moved?.Invoke(actorIndex, targetIndex);
        }

        void Lift(int index)
        {
            if (!InRange(index))
                return;

            var slot = _slots[index];
            if (slot.Portrait == null || string.IsNullOrEmpty(slot.MonsterId))
                return;

            HoverRide.Attach(slot.Portrait, slot.MonsterId);
            if (slot.View != null)
                slot.View.monsterId = null;
        }

        void AddShown(int amount)
        {
            _shown += amount;
            _stage.EnergyShown?.Invoke(_shown);
            if (_current != null)
                _current.text = _shown.ToString();
        }

        void BindCurrent()
        {
            _current = _stage.CurrentNumber;
            if (_current == null && _stage.BoardRoot != null)
            {
                var found = _stage.BoardRoot.Find("CurrentNumber");
                if (found != null)
                    _current = found.GetComponent<TMP_Text>();
            }

            if (_current == null)
                return;

            var rect = _current.rectTransform;
            _currentRestScale = rect.localScale;
            _currentRestColor = _current.color;
            _shown = _stage.InitialEnergy;
            _current.text = _shown.ToString();
        }

        void RestoreCurrent()
        {
            if (_current == null)
                return;

            var rect = _current.rectTransform;
            rect.localScale = _currentRestScale;
            _current.color = _currentRestColor;
        }

        IEnumerator RollGold(int wage)
        {
            var label = _stage.GoldLabel;
            var gold = _shownGold;
            var end = gold + wage;
            _shownGold = end;
            if (label == null) yield break;
            if (SkipWait())
            {
                label.text = end.ToString();
                yield break;
            }

            var driver = 0f;
            var tween = Live(DOTween.To(
                () => driver,
                value =>
                {
                    driver = value;
                    label.text = Mathf.RoundToInt(Mathf.Lerp(gold, end, value)).ToString();
                },
                1f,
                Span(0.45f)));
            yield return Wait(0.45f);
            tween.Kill(false);
            label.text = end.ToString();
        }

        IEnumerator ShakeBoard(float strength)
        {
            var root = _stage.BoardRoot;
            root.anchoredPosition = _restBoardPosition;
            var tween = Live(root.DOShakeAnchorPos(Span(ShakeSeconds), strength, 18, 90f, false, true));
            yield return Wait(ShakeSeconds);
            tween.Kill(false);
            root.anchoredPosition = _restBoardPosition;
        }

        IEnumerator ShakeRoot(int index, float strength)
        {
            var root = RootAt(index);
            if (root == null || !InRange(index))
                yield break;

            var rest = _slots[index].RestAnchoredPosition;
            var tween = Live(root.DOShakeAnchorPos(Span(ShakeSeconds), strength, 16, 90f, false, true));
            yield return Wait(ShakeSeconds);
            tween.Kill(false);
            root.anchoredPosition = rest;
        }

        IEnumerator SpringOnce(int index)
        {
            var portrait = PortraitAt(index);
            if (portrait == null)
                yield break;

            portrait.SetSpringMotion(-10f, 7f);
            yield return Wait(0.22f);
            portrait.RestoreMotion();
        }

        void Flash(Color color)
        {
            _flash.color = color;
            Live(_flash.DOFade(0f, 0.35f));
        }

        void PlacePlayhead(int index)
        {
            var point = PointOf(index);
            if (point == null)
            {
                _playhead.gameObject.SetActive(false);
                return;
            }

            _playhead.gameObject.SetActive(true);
            _playhead.rectTransform.anchoredPosition = HeadOf(index) + new Vector2(0f, 70);
        }

        void OnBlocked()
        {
            if (_paying)
            {
                _finishPay = true;
                StopSfx();
                return;
            }

            if (!_boosted && !_cutToPay)
            {
                _boosted = true;
                _pace = SkipPace;
            }
            else
            {
                _cutToPay = true;
                StopSfx();
            }
        }

        bool SkipWait() => _cutToPay || _finishPay;

        float Span(float seconds) => seconds / _pace;

        Vector2? PointOf(int index)
        {
            var root = RootAt(index);
            if (root != null)
                return BlockerPoint(root);
            if (!InRange(index) || _slots[index].View == null)
                return null;

            var view = _slots[index].View.transform as RectTransform;
            return view != null ? BlockerPoint(view) : (Vector2?)null;
        }

        Vector2 BesideTotal() => CurrentPoint() + new Vector2(0f, -90f);

        Vector2 CurrentPoint()
        {
            if (_current == null || _blocker == null)
                return Vector2.zero;

            return CanvasMap.LocalPoint(_blocker, GlyphWorld(_current), CanvasMap.EventCamera(_current.transform));
        }

        static Vector3 GlyphWorld(TMP_Text label)
        {
            var rect = label.rectTransform;
            label.ForceMeshUpdate();
            var bounds = label.textBounds;
            if (bounds.size.sqrMagnitude < 0.01f)
                return rect.TransformPoint(rect.rect.center);

            return rect.TransformPoint(bounds.center);
        }

        static string Signed(int amount) => amount > 0 ? "+" + amount : amount.ToString();

        TextMeshProUGUI Spawn(string text, Vector2 anchored)
        {
            var figure = new GameObject("ScoringFigure", typeof(RectTransform));
            figure.transform.SetParent(_blocker, false);
            var rect = figure.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 120f);
            rect.anchoredPosition = anchored;
            var label = figure.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 40f;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.maskable = false;
            var font = Font();
            if (font != null)
                label.font = font;
            label.text = text;
            return label;
        }

        TMP_FontAsset Font()
        {
            if (_current != null && _current.font != null)
                return _current.font;
            if (_stage.GoldLabel != null && _stage.GoldLabel.font != null)
                return _stage.GoldLabel.font;
            return TMP_Settings.defaultFontAsset;
        }

        Image CreateImage(string name, RectTransform parent)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            image.sprite = Pixel();
            image.raycastTarget = false;
            return image;
        }

        Sprite Pixel()
        {
            if (_pixel != null)
                return _pixel;

            _pixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _pixelTexture.SetPixel(0, 0, Color.white);
            _pixelTexture.Apply();
            _pixel = Sprite.Create(_pixelTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return _pixel;
        }

        Vector2 BlockerPoint(RectTransform rect) =>
            CanvasMap.LocalPoint(_blocker, rect.position, CanvasMap.EventCamera(rect));

        /// <summary>
        /// 换位时留在怪物自己的画布上。拖到屏幕覆盖层会把世界坐标当成像素，怪物落到画面左下角。
        /// </summary>
        RectTransform MotionLayer(RectTransform actor, RectTransform target)
        {
            var canvas = actor != null ? actor.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null && target != null)
                canvas = target.GetComponentInParent<Canvas>(true);

            var root = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            return root != null ? root : _stage.DragLayer;
        }

        MonsterPortrait PortraitAt(int index) => InRange(index) ? _slots[index].Portrait : null;

        RectTransform RootAt(int index) => InRange(index) ? Root(_slots[index]) : null;

        static RectTransform Root(BoardSlot slot) =>
            slot.Portrait != null ? slot.Portrait.transform as RectTransform : null;

        bool InRange(int index) => _slots != null && index >= 0 && index < _slots.Length;

        T Live<T>(T tween) where T : Tween
        {
            tween.SetUpdate(true).SetLink(gameObject).SetId(TweenId);
            return tween;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void SwapText(TMP_Text left, TMP_Text right)
        {
            if (left == null || right == null)
                return;

            var text = left.text;
            left.text = right.text;
            right.text = text;
        }

        sealed class BlockerHit : MonoBehaviour, IPointerClickHandler
        {
            public ScoringShow Show;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (Show != null)
                    Show.OnBlocked();
            }
        }
    }
}

