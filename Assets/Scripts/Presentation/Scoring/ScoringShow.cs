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
            Action<string> say,
            Action completed)
        {
            Architecture = architecture;
            ExtractionSlots = extractionSlots;
            BoardRoot = boardRoot;
            DragLayer = dragLayer;
            GoldLabel = goldLabel;
            Say = say;
            Completed = completed;
        }

        public IArchitecture Architecture { get; }
        public MonsterSlotView[] ExtractionSlots { get; }
        public RectTransform BoardRoot { get; }
        public RectTransform DragLayer { get; }
        public TMP_Text GoldLabel { get; }
        public Action<string> Say { get; }
        public Action Completed { get; }
    }

    public sealed class ScoringShow : MonoBehaviour, ICueSink
    {
        const float PlayheadLift = 70f;
        const float NameLift = 16f;
        const float FigureLift = 0f;
        const float WalkSeconds = 0.18f;
        const float FigureSeconds = 0.32f;
        const float FlightSeconds = 0.36f;
        const float SwapSeconds = 0.28f;
        const float RemoveSeconds = 0.22f;
        const float PaySeconds = 0.9f;
        const float ShakeSeconds = 0.28f;
        const float AddPunch = 1.08f;
        const float BasePunch = 1.24f;
        const float MultiplierPunch = 1.42f;

        static readonly object TweenId = new object();
        static ScoringShow _active;

        ScoringStage _stage;
        ScoringTape _tape;
        BoardSlot[] _slots;
        RectTransform _blocker;
        TextMeshProUGUI _total;
        Image _playhead;
        Image _flash;
        Material _flashMaterial;
        Sprite _pixel;
        Texture2D _pixelTexture;
        Vector2 _restBoardPosition;
        bool _boardHeld;
        int _shown;
        float _pace = 1f;
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
                _restBoardPosition = _stage.BoardRoot.anchoredPosition;
                _boardHeld = true;
                BuildOverlay();
                for (var i = 0; i < _tape.Cues.Count; i++)
                {
                    var cue = _tape.Cues[i];
                    if (cue is PayCue)
                    {
                        _cutToPay = false;
                        _pace = 1f;
                        _paying = true;
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

            DOTween.Kill(TweenId);
            RestoreSlots();
            if (_boardHeld && _stage.BoardRoot != null)
                _stage.BoardRoot.anchoredPosition = _restBoardPosition;

            if (_flashMaterial != null)
                Destroy(_flashMaterial);
            if (_pixel != null)
                Destroy(_pixel);
            if (_pixelTexture != null)
                Destroy(_pixelTexture);
            if (_blocker != null)
                Destroy(_blocker.gameObject);

            var completed = _stage.Completed;
            Destroy(gameObject);
            completed?.Invoke();
        }

        void OnDestroy()
        {
            if (!_finished)
                Finish();
            if (_active == this)
                _active = null;
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
            var portrait = PortraitAt(cue.SlotIndex);
            var root = RootAt(cue.SlotIndex);
            var rest = root != null ? _slots[cue.SlotIndex].RestScale : Vector3.one;
            if (portrait != null)
                portrait.SetSpringMotion(-10f, 7f);

            var origin = OriginOf(cue.SlotIndex);
            if (!string.IsNullOrEmpty(cue.SkillName))
            {
                var nameLabel = Spawn(cue.SkillName, origin + new Vector2(0f, NameLift));
                yield return Wait(0.22f);
                Destroy(nameLabel.gameObject);
            }

            for (var i = 0; i < cue.Figures.Count; i++)
            {
                var figure = cue.Figures[i];
                if (figure.Role == FigureRole.Energy)
                {
                    yield return FlyEnergy(figure.Text, cue.Energy, origin);
                    continue;
                }

                var label = Spawn(figure.Text, StayPoint(figure.Role, origin));
                var punch = PunchOf(figure.Role);
                if (root != null && cue.SlotIndex >= 0 && punch > 1f)
                    yield return Punch(root, rest, punch);
                else
                    yield return Wait(FigureSeconds);

                if (figure.Role == FigureRole.Multiplier && cue.Multiplier > 1)
                {
                    Flash(new Color(1f, 0.95f, 0.7f, 0.45f));
                    yield return ShakeBoard(16f);
                }

                Destroy(label.gameObject);
            }

            if (root != null)
                root.localScale = rest;
            if (portrait != null)
                portrait.RestoreMotion();
        }

        IEnumerator Swap(SwapCue cue)
        {
            if (!cue.Happened)
            {
                if (cue.ActorIndex >= 0)
                    yield return SpringOnce(cue.ActorIndex);
                yield break;
            }

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
                    slot.MonsterId = null;
                    _slots[cue.VictimIndex] = slot;
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
            if (cue.Kind == PayKind.Paid)
                label = Spawn("交 " + cue.Deducted, BesideTotal());
            else if (cue.Kind == PayKind.Short)
                label = Spawn("欠额 " + cue.Shortfall, BesideTotal());

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

            _total = Spawn("0", Vector2.zero);
            _total.fontSize = 64f;
            _total.rectTransform.sizeDelta = new Vector2(220f, 90f);
            _total.color = new Color(1f, 0.96f, 0.82f, 1f);

            _playhead = CreateImage("ScoringPlayhead", _blocker);
            _playhead.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _playhead.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _playhead.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _playhead.rectTransform.sizeDelta = new Vector2(36f, 8f);
            _playhead.color = new Color(1f, 0.82f, 0.25f, 1f);
            _playhead.raycastTarget = false;
            _playhead.gameObject.SetActive(false);
            _flash.transform.SetAsFirstSibling();
            PlaceTotal();
            _total.transform.SetAsLastSibling();
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
                if (root != null && slot.View != null && root.parent != slot.View.transform)
                {
                    root.SetParent(slot.View.transform, false);
                    root.anchoredPosition = slot.RestAnchoredPosition;
                    root.localScale = slot.RestScale;
                }
                else if (root != null)
                {
                    root.localScale = slot.RestScale;
                    if (slot.View != null && root.parent == slot.View.transform)
                        root.anchoredPosition = slot.RestAnchoredPosition;
                }

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
                var id = actor.View.monsterId;
                actor.View.monsterId = target.View.monsterId;
                target.View.monsterId = id;
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
            _slots[actorIndex] = actor;
            _slots[targetIndex] = target;
        }

        IEnumerator FlyEnergy(string text, int energy, Vector2 origin)
        {
            var label = Spawn(text, origin);
            var tween = Live(label.rectTransform.DOAnchorPos(BlockerPoint(_total.rectTransform), Span(FlightSeconds)));
            yield return CountEnergy(energy, FlightSeconds);
            tween.Kill(false);
            Destroy(label.gameObject);
        }

        IEnumerator CountEnergy(int energy, float seconds)
        {
            var start = _shown;
            var end = start + energy;
            if (SkipWait() || seconds <= 0f)
            {
                _shown = end;
                _total.text = _shown.ToString();
                yield break;
            }

            var driver = 0f;
            var tween = Live(DOTween.To(
                () => driver,
                value =>
                {
                    driver = value;
                    _total.text = Mathf.RoundToInt(Mathf.Lerp(start, end, value)).ToString();
                },
                1f,
                Span(seconds)));
            yield return Wait(seconds);
            tween.Kill(false);
            _shown = end;
            _total.text = _shown.ToString();
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

        IEnumerator Punch(RectTransform root, Vector3 rest, float factor)
        {
            root.localScale = rest;
            var up = Live(root.DOScale(rest * factor, Span(0.08f)));
            yield return Wait(0.08f);
            up.Kill(false);
            var down = Live(root.DOScale(rest, Span(0.1f)));
            yield return Wait(0.1f);
            down.Kill(false);
            root.localScale = rest;
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

        void PlaceTotal()
        {
            var index = _slots != null && _slots.Length > 4 ? 4 : (_slots != null && _slots.Length > 0 ? _slots.Length - 1 : -1);
            var point = PointOf(index);
            if (point == null)
                return;

            _total.rectTransform.anchoredPosition = point.Value;
        }

        void OnBlocked()
        {
            if (_paying)
            {
                _finishPay = true;
                return;
            }

            if (_pace <= 1f && !_cutToPay)
                _pace = 4f;
            else
                _cutToPay = true;
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

        Vector2 BesideTotal() => BlockerPoint(_total.rectTransform) + new Vector2(0f, -90f);

        static Vector2 StayPoint(FigureRole role, Vector2 origin)
        {
            if (role == FigureRole.Writeback)
                return origin + new Vector2(150f, 36f);
            return origin + new Vector2(0f, FigureLift);
        }

        static float PunchOf(FigureRole role)
        {
            if (role == FigureRole.Add)
                return AddPunch;
            if (role == FigureRole.Base)
                return BasePunch;
            if (role == FigureRole.Multiplier)
                return MultiplierPunch;
            return 0f;
        }

        TextMeshProUGUI Spawn(string text, Vector2 anchored)
        {
            var figure = new GameObject("ScoringFigure", typeof(RectTransform));
            figure.transform.SetParent(_blocker, false);
            var rect = figure.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(460f, 84f);
            rect.anchoredPosition = anchored;
            var label = figure.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 40f;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.outlineWidth = 0.16f;
            label.outlineColor = new Color(0f, 0f, 0f, 0.9f);
            var font = Font();
            if (font != null)
                label.font = font;
            label.text = text;
            return label;
        }

        TMP_FontAsset Font()
        {
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
