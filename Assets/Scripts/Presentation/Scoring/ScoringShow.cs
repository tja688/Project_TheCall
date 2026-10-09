using System;
using System.Collections;
using DG.Tweening;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheCall.Scoring
{
    public readonly struct ScoringStage
    {
        public ScoringStage(
            IArchitecture architecture,
            MonsterSlotView[] extractionSlots,
            RectTransform boardRoot,
            RectTransform dragLayer,
            TMP_Text goldLabel,
            TMP_Text currentNumber,
            Action<string> say,
            Action completed)
        {
            Architecture = architecture;
            ExtractionSlots = extractionSlots;
            BoardRoot = boardRoot;
            DragLayer = dragLayer;
            GoldLabel = goldLabel;
            CurrentNumber = currentNumber;
            Say = say;
            Completed = completed;
        }

        public IArchitecture Architecture { get; }
        public MonsterSlotView[] ExtractionSlots { get; }
        public RectTransform BoardRoot { get; }
        public RectTransform DragLayer { get; }
        public TMP_Text GoldLabel { get; }
        public TMP_Text CurrentNumber { get; }
        public Action<string> Say { get; }
        public Action Completed { get; }
    }

    public sealed class ScoringShow : MonoBehaviour, ICueSink
    {
        const float PlayheadLift = 70f;
        const float FigureLift = 64f;
        const float WalkSeconds = 0.18f;
        const float SwapSeconds = 0.28f;
        const float RemoveSeconds = 0.22f;
        const float ShakeSeconds = 0.28f;
        const float EatSquashSeconds = 0.07f;
        const float EatSettleSeconds = 0.14f;
        const float PaceWarmup = 10f;
        const float PacePerStep = 0.08f;
        const float PaceCeiling = 3f;
        const float PayPaceCeiling = 2f;
        const float SkipPace = 4f;
        const float FigureLinger = 1.5f;
        const float RiseSeconds = 0.12f * FigureLinger;
        const float HoldSeconds = 0.14f * FigureLinger;
        const float FlightSeconds = 0.36f * FigureLinger;
        const float FigureInSeconds = 0.12f * FigureLinger;
        const float FigureStaySeconds = 0.1f * FigureLinger;
        const float FigureOutSeconds = 0.1f * FigureLinger;
        const float BeatGrowSeconds = 0.1f * FigureLinger;
        const float BeatFlySeconds = 0.26f * FigureLinger;
        const float PunchSeconds = 0.08f * FigureLinger;
        const float SettleSeconds = 0.1f * FigureLinger;
        const float FigureShakeSeconds = 0.16f * FigureLinger;
        const float PaySeconds = 0.9f * FigureLinger;

        static readonly object TweenId = new object();
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
        float _pace = 1f;
        bool _boosted;
        bool _cutToPay;
        bool _finishPay;
        bool _paying;
        bool _finished;

        struct BoardSlot
        {
            public MonsterSlotView View;
            public MonsterPortrait Portrait;
            public string MonsterId;
            public Vector3 RestScale;
            public Vector2 RestAnchoredPosition;
        }

        public static void Play(ScoringStage stage)
        {
            if (stage.Architecture == null || stage.BoardRoot == null || stage.DragLayer == null)
            {
                stage.Completed?.Invoke();
                return;
            }

            if (_active != null)
                return;

            var host = new GameObject("ScoringShow");
            host.transform.SetParent(stage.DragLayer, false);
            var show = host.AddComponent<ScoringShow>();
            _active = show;
            show.Begin(stage);
        }

        void Begin(ScoringStage stage)
        {
            _stage = stage;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            try
            {
                var ids = ReadIds(_stage.ExtractionSlots);
                var entries = _stage.Architecture.SendQuery(new SettlementRecordQuery());
                _tape = ScoringTape.Arrange(entries, ids);
                Capture(ids);
                _stage.Architecture.SendCommand(new ShowSettlementSightCommand());
                _restBoardPosition = _stage.BoardRoot.anchoredPosition;
                _boardHeld = true;
                BindCurrent();
                BuildOverlay();
                for (var i = 0; i < _tape.Cues.Count; i++)
                {
                    var cue = _tape.Cues[i];
                    if (cue is PayCue)
                    {
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
            if (this != null)
                Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (!_finished)
            {
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
            if (_stage.Architecture != null)
                _stage.Architecture.SendCommand(new HideSettlementSightCommand());

            DOTween.Kill(TweenId);
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
            var point = PointOf(cue.ToIndex);
            if (point == null)
            {
                _playhead.gameObject.SetActive(false);
                yield break;
            }

            var target = point.Value + new Vector2(0f, PlayheadLift);
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

        IEnumerator Pop(PopCue cue)
        {
            if (SkipWait())
            {
                AddShown(cue.Energy);
                yield break;
            }

            var banked = false;
            var beats = cue.Beats;
            for (var i = 0; i < beats.Count; i++)
            {
                if (SkipWait())
                    break;

                var beat = beats[i];
                if (beat.Role == BeatRole.Energy)
                {
                    yield return Bank(cue, beat);
                    banked = true;
                    continue;
                }

                yield return PlayBeat(cue.SlotIndex, beat);
            }

            if (!banked)
                AddShown(cue.Energy);

            RestoreSlot(cue.SlotIndex);
        }

        IEnumerator Bank(PopCue cue, ScoreBeat beat)
        {
            if (cue.Energy == 0)
            {
                yield return Bloom(cue.SlotIndex, beat);
                AddShown(0);
                yield break;
            }

            var portrait = PortraitAt(cue.SlotIndex);
            var root = RootAt(cue.SlotIndex);
            var rest = root != null && InRange(cue.SlotIndex) ? _slots[cue.SlotIndex].RestScale : Vector3.one;
            if (portrait != null)
                portrait.SetSpringMotion(-18f, 12f);

            var head = OriginOf(cue.SlotIndex) + new Vector2(0f, FigureLift);
            yield return ThrowScore(cue.Energy, head, root, rest, 78f);
            if (portrait != null)
                portrait.RestoreMotion();
            if (root != null)
                root.localScale = rest;
        }

        IEnumerator PlayBeat(int subject, ScoreBeat beat)
        {
            var actor = ActorOf(subject, beat.SourceName);
            if (actor == subject)
            {
                yield return Bloom(subject, beat);
                yield break;
            }

            if (actor >= 0)
                yield return ShakeFree(actor);

            if (SkipWait())
                yield break;

            var origin = actor >= 0
                ? OriginOf(actor) + new Vector2(0f, FigureLift)
                : OriginOf(subject) + new Vector2(0f, FigureLift + 110f);
            var label = SpawnMark(beat, origin);
            label.rectTransform.localScale = Vector3.one * 0.35f;
            var grow = Live(label.rectTransform.DOScale(1.05f, Span(BeatGrowSeconds)).SetEase(Ease.OutBack));
            yield return Wait(BeatGrowSeconds);
            grow.Kill(false);
            label.rectTransform.localScale = Vector3.one;
            if (SkipWait())
            {
                Destroy(label.gameObject);
                yield break;
            }

            var target = OriginOf(subject) + new Vector2(0f, 36f);
            var fly = Live(label.rectTransform.DOJumpAnchorPos(target, 70f, 1, Span(BeatFlySeconds)));
            yield return Wait(BeatFlySeconds);
            fly.Kill(false);
            label.rectTransform.anchoredPosition = target;
            if (SkipWait())
            {
                Destroy(label.gameObject);
                yield break;
            }

            yield return Absorb(subject, label);
        }

        IEnumerator Bloom(int subject, ScoreBeat beat)
        {
            var portrait = PortraitAt(subject);
            if (portrait != null)
                portrait.SetSpringMotion(-14f, 9f);

            var head = OriginOf(subject) + new Vector2(0f, FigureLift);
            var label = SpawnMark(beat, head + new Vector2(0f, -22f));
            label.rectTransform.localScale = Vector3.one * 0.4f;
            var rise = Live(label.rectTransform.DOAnchorPos(head + new Vector2(0f, 18f), Span(FigureInSeconds)).SetEase(Ease.OutBack));
            var grow = Live(label.rectTransform.DOScale(1f, Span(FigureInSeconds)).SetEase(Ease.OutBack));
            yield return Wait(FigureInSeconds);
            rise.Kill(false);
            grow.Kill(false);
            if (SkipWait())
            {
                Release(label, subject);
                yield break;
            }

            yield return Wait(FigureStaySeconds);
            var fade = Live(label.DOFade(0f, Span(FigureOutSeconds)));
            yield return Wait(FigureOutSeconds);
            fade.Kill(false);
            Release(label, subject);
        }

        IEnumerator Absorb(int subject, TextMeshProUGUI label)
        {
            var root = RootAt(subject);
            var rest = InRange(subject) ? _slots[subject].RestScale : Vector3.one;
            var portrait = PortraitAt(subject);
            if (portrait != null)
                portrait.SetSpringMotion(12f, -8f);

            Tween punch = null;
            if (root != null)
                punch = Live(root.DOScale(rest * 1.12f, Span(PunchSeconds)).SetEase(Ease.OutQuad));
            var pop = Live(label.rectTransform.DOScale(1.22f, Span(PunchSeconds)).SetEase(Ease.OutQuad));
            yield return Wait(PunchSeconds);
            punch?.Kill(false);
            pop.Kill(false);
            Tween back = null;
            if (root != null)
                back = Live(root.DOScale(rest, Span(SettleSeconds)).SetEase(Ease.OutQuad));
            var fade = Live(label.DOFade(0f, Span(FigureOutSeconds)));
            yield return Wait(SettleSeconds);
            back?.Kill(false);
            fade.Kill(false);
            if (root != null)
                root.localScale = rest;
            if (portrait != null)
                portrait.RestoreMotion();
            if (label != null)
                Destroy(label.gameObject);
        }

        IEnumerator Mark(MarkCue cue)
        {
            var head = OriginOf(cue.SlotIndex) + new Vector2(0f, FigureLift);
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

        int ActorOf(int subject, string sourceName)
        {
            if (string.IsNullOrEmpty(sourceName) || _slots == null)
                return subject;

            for (var i = 0; i < _slots.Length; i++)
            {
                var title = _slots[i].View != null ? _slots[i].View.title : null;
                if (title == null || title.text != sourceName)
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

        TextMeshProUGUI SpawnMark(ScoreBeat beat, Vector2 anchored)
        {
            var label = Spawn(MarkText(beat), anchored);
            label.fontSize = SizeOf(beat.Role);
            label.color = ColorOf(beat.Role);
            label.richText = true;
            return label;
        }

        static string MarkText(ScoreBeat beat)
        {
            if (string.IsNullOrEmpty(beat.Caption))
                return beat.Text;

            return beat.Text + "\n<size=46%>" + beat.Caption + "</size>";
        }

        static float SizeOf(BeatRole role)
        {
            switch (role)
            {
                case BeatRole.Energy:
                    return 78f;
                case BeatRole.Factor:
                case BeatRole.Side:
                    return 58f;
                case BeatRole.Add:
                case BeatRole.Again:
                    return 52f;
                case BeatRole.Writeback:
                    return 32f;
                default:
                    return 44f;
            }
        }

        static Color ColorOf(BeatRole role)
        {
            switch (role)
            {
                case BeatRole.Add:
                    return new Color(0.45f, 1f, 0.62f, 1f);
                case BeatRole.Factor:
                case BeatRole.Side:
                    return new Color(1f, 0.45f, 0.72f, 1f);
                case BeatRole.Again:
                    return new Color(0.45f, 0.9f, 1f, 1f);
                case BeatRole.Writeback:
                    return new Color(0.65f, 0.82f, 1f, 1f);
                default:
                    return new Color(1f, 0.93f, 0.55f, 1f);
            }
        }

        void Release(TextMeshProUGUI label, int subject)
        {
            if (label != null)
                Destroy(label.gameObject);

            RestoreSlot(subject);
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
                actorRoot.SetParent(_stage.DragLayer, true);
                targetRoot.SetParent(_stage.DragLayer, true);
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
            if (cue.Happened)
            {
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
                    _stage.Architecture.SendCommand(new ClearSettlementSightCommand(cue.VictimIndex));
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
                label = Spawn("获得 " + cue.Produced, BesideTotal());

            if (cue.Wage > 0)
                yield return RollGold(cue.Wage);

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
            _stage.Architecture.SendCommand(new MoveSettlementSightCommand(actorIndex, targetIndex));
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

        IEnumerator ThrowScore(int amount, Vector2 head, RectTransform monster, Vector3 monsterRest, float fontSize)
        {
            if (SkipWait())
            {
                AddShown(amount);
                yield break;
            }

            var heavy = fontSize >= 70f;
            var label = Spawn(Signed(amount), head + new Vector2(0f, -36f));
            label.fontSize = fontSize;
            label.color = new Color(1f, 0.93f, 0.55f, 1f);
            label.rectTransform.localScale = Vector3.one * 0.4f;
            var rise = Live(label.rectTransform.DOAnchorPos(head, Span(RiseSeconds)).SetEase(Ease.OutQuad));
            var grow = Live(label.rectTransform.DOScale(heavy ? 1.3f : 1.12f, Span(RiseSeconds)).SetEase(Ease.OutBack));
            Tween monsterUp = null;
            if (monster != null)
                monsterUp = Live(monster.DOScale(monsterRest * (heavy ? 1.18f : 1.1f), Span(RiseSeconds)).SetEase(Ease.OutQuad));

            yield return Wait(RiseSeconds);
            rise.Kill(false);
            grow.Kill(false);
            monsterUp?.Kill(false);
            label.rectTransform.anchoredPosition = head;
            label.rectTransform.localScale = Vector3.one;
            if (monster != null)
            {
                var monsterDown = Live(monster.DOScale(monsterRest, Span(HoldSeconds)));
                yield return Wait(HoldSeconds);
                monsterDown.Kill(false);
                monster.localScale = monsterRest;
            }
            else
            {
                yield return Wait(HoldSeconds);
            }

            if (SkipWait() || _current == null)
            {
                Destroy(label.gameObject);
                AddShown(amount);
                if (_current != null)
                    yield return Eat();
                yield break;
            }

            var target = CurrentPoint();
            var fly = Live(label.rectTransform.DOJumpAnchorPos(target, heavy ? 96f : 72f, 1, Span(FlightSeconds)));
            var shrink = Live(label.rectTransform.DOScale(0.35f, Span(FlightSeconds)).SetEase(Ease.InQuad));
            yield return Wait(FlightSeconds);
            fly.Kill(false);
            shrink.Kill(false);
            Destroy(label.gameObject);
            AddShown(amount);
            yield return Eat();
        }

        void AddShown(int amount)
        {
            _shown += amount;
            if (_current != null)
                _current.text = _shown.ToString();
        }

        IEnumerator Eat()
        {
            if (_current == null)
                yield break;

            if (SkipWait())
            {
                RestoreCurrent();
                yield break;
            }

            var rect = _current.rectTransform;
            var rest = _currentRestScale;
            _current.color = new Color(1f, 0.95f, 0.62f, _currentRestColor.a);
            var squash = Live(rect.DOScale(new Vector3(rest.x * 1.28f, rest.y * 0.78f, rest.z), Span(EatSquashSeconds)).SetEase(Ease.OutQuad));
            yield return Wait(EatSquashSeconds);
            squash.Kill(false);
            var settle = Live(rect.DOScale(rest, Span(EatSettleSeconds)).SetEase(Ease.OutBack));
            var tint = Live(_current.DOColor(_currentRestColor, Span(EatSettleSeconds)));
            yield return Wait(EatSettleSeconds);
            settle.Kill(false);
            tint.Kill(false);
            RestoreCurrent();
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
            _shown = 0;
            _current.text = "0";
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
            if (label == null || !int.TryParse(label.text, out var gold))
                yield break;

            var end = gold + wage;
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
            _playhead.rectTransform.anchoredPosition = point.Value + new Vector2(0f, PlayheadLift);
        }

        void OnBlocked()
        {
            if (_paying)
            {
                _finishPay = true;
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

        Vector2 OriginOf(int slotIndex)
        {
            var point = PointOf(slotIndex);
            if (slotIndex >= 0 && point != null)
                return point.Value;
            return BesideTotal();
        }

        Vector2 BesideTotal() => CurrentPoint() + new Vector2(0f, -90f);

        Vector2 CurrentPoint()
        {
            if (_current == null || _blocker == null)
                return Vector2.zero;

            return _blocker.InverseTransformPoint(GlyphWorld(_current));
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

        Vector2 BlockerPoint(RectTransform rect) => _blocker.InverseTransformPoint(rect.position);

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

