using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class OperationPointer : MonoBehaviour,
        IPointerDownHandler,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerUpHandler
    {
        // 拖拽怪物时，原位只保留一个半透明的影子。没落到槽位时，怪物自己回到原位再淡出。
        // 落到槽位时怪物已经挪走，拖着的那只直接消失，不再飞回原位。
        const float SourceAlpha = 0.28f;
        const float MaxReleaseSeconds = 1.5f;
        const float FadeSeconds = 0.16f;

        public RectTransform dragLayer;

        TheCallPresentation _presentation;
        OperationPayloadDrag _pressed;
        DropPayload _pressedPayload;
        bool _hasPressedPayload;
        DropPayload _armed;
        bool _hasArmed;
        bool _dragging;
        bool _finished = true;
        GameObject _card;
        Vector2 _cardOffset;
        MonsterPortrait _carried;
        MonsterPortrait _home;
        bool _releasing;
        float _releaseClock;
        bool _fading;
        float _fadeClock;
        float _carryFrom;
        float _carryStartDistance;
        float _returnFrom;
        float _returnClock;
        bool _returning;
        readonly List<RaycastResult> _hits = new List<RaycastResult>();

        public DropPayload? Armed => _hasArmed ? _armed : (DropPayload?)null;

        public void Attach(TheCallPresentation presentation)
        {
            _presentation = presentation;
            WirePayloadDrags();
        }

        static void WirePayloadDrags()
        {
            var drags = FindObjectsByType<OperationPayloadDrag>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < drags.Length; i++)
            {
                if (drags[i] == null || drags[i].session != null)
                    continue;

                drags[i].session = FindAnyObjectByType<OperationPointer>();
            }
        }

        public void Press(OperationPayloadDrag source, PointerEventData eventData)
        {
            _finished = false;
            _dragging = false;
            _pressed = source;
            _hasPressedPayload = source != null && source.HasPayload();
            if (_hasPressedPayload)
                _pressedPayload = source.ReadPayload();
        }

        public void BeginDrag(OperationPayloadDrag source, PointerEventData eventData)
        {
            if (_finished)
                return;

            _dragging = true;
            _hasArmed = false;
            ResetVisuals();
            if (_hasPressedPayload && source == _pressed)
                Carry(source, eventData);
        }

        public void MoveGhost(PointerEventData eventData)
        {
            if (_card != null)
                _card.transform.position = eventData.position + _cardOffset;
            if (_carried == null)
                return;

            // 先按靠近生产区、培育区的程度放大，再把指针交给布娃娃。
            // 放大会改骨架本地坐标，必须发生在 Hold 之前，抓取弹簧才读得到同一套单位。
            if (!_releasing && !_returning)
                ApplyCarryApproach(eventData.position);
            _carried.Hold(eventData.position, eventData.pressEventCamera);
        }

        public void Finish(PointerEventData eventData)
        {
            if (_finished)
                return;

            _finished = true;
            if (_dragging)
            {
                var applied = false;
                if (_hasPressedPayload && _presentation != null)
                    applied = _presentation.CommitDrop(new OperationDrop(_pressedPayload, RayLanding(eventData)));

                LetGo(applied);
                ClearGesture();
                Paint();
                return;
            }

            if (!_hasArmed)
            {
                if (_hasPressedPayload)
                {
                    _armed = _pressedPayload;
                    _hasArmed = true;
                    Paint();
                }

                _pressed = null;
                return;
            }

            var armed = _armed;
            _hasArmed = false;
            if (_presentation != null)
                _presentation.CommitDrop(new OperationDrop(armed, RayLanding(eventData)));

            _pressed = null;
            Paint();
        }

        public void OnPointerDown(PointerEventData eventData) => Press(null, eventData);

        public void OnInitializePotentialDrag(PointerEventData eventData) =>
            eventData.useDragThreshold = true;

        public void OnBeginDrag(PointerEventData eventData) => BeginDrag(null, eventData);

        public void OnDrag(PointerEventData eventData) => MoveGhost(eventData);

        public void OnEndDrag(PointerEventData eventData) => Finish(eventData);

        public void OnPointerUp(PointerEventData eventData) => Finish(eventData);

        void OnDisable()
        {
            _finished = true;
            ClearGesture();
            ResetVisuals();
        }

        /// <summary>
        /// 怪物拖拽：在原位生成一只同样的怪物，拖的是身体中心。按下点偏了，中心会自己移到指针上。
        /// 原位压暗成影子；怪物本身带着抓取、摆动和回弹，不复制卡片。
        /// </summary>
        void Carry(OperationPayloadDrag source, PointerEventData eventData)
        {
            if (source.payloadKind != PayloadKind.Monster)
            {
                SpawnCard(source, eventData);
                return;
            }

            if (dragLayer == null)
                return;

            var home = source.GetComponentInChildren<MonsterPortrait>(true);
            if (home == null || !home.HasCreature)
                return;

            _home = home;
            _home.SetVisibility(SourceAlpha);
            _carried = MonsterPortrait.SpawnGhost(dragLayer, _home);
            var homeRect = _home.transform as RectTransform;
            _carryFrom = CanvasMap.ScreenUniform(homeRect);
            _carryStartDistance = BoardDistance(eventData.pressPosition);
            _returning = false;
            ApplyCarryApproach(eventData.position);
            _carried.Grab(eventData.pressPosition, eventData.pressEventCamera);
            _carried.Hold(eventData.position, eventData.pressEventCamera);
        }

        /// <summary>技能芯片没有怪物可以承载，仍按原来的方式跟着指针走。</summary>
        void SpawnCard(OperationPayloadDrag source, PointerEventData eventData)
        {
            ClearCard();
            if (dragLayer == null)
                return;

            _card = Instantiate(source.gameObject, dragLayer);
            _cardOffset = (Vector2)source.transform.position - eventData.position;
            var group = _card.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var copy = _card.GetComponent<OperationPayloadDrag>();
            if (copy != null)
                copy.enabled = false;
        }

        /// <summary>松手。落到槽位时拖着的怪物直接消失；没落到槽位时它自己回到原位，再淡出。</summary>
        void LetGo(bool applied)
        {
            if (_carried == null)
                return;

            if (applied)
            {
                RestoreHome();
                var ghost = _carried.gameObject;
                _carried = null;
                Destroy(ghost);
                return;
            }

            _carried.Release();
            _releasing = true;
            _releaseClock = 0f;
            _returning = true;
            _returnFrom = CanvasMap.ScreenUniform(_carried.transform as RectTransform);
            _returnClock = 0f;
        }

        DropLanding RayLanding(PointerEventData eventData)
        {
            _hits.Clear();
            if (EventSystem.current == null)
                return new DropLanding(LandingPlace.EmptySpace, 0, null);

            EventSystem.current.RaycastAll(eventData, _hits);
            for (var i = 0; i < _hits.Count; i++)
            {
                var zone = _hits[i].gameObject.GetComponentInParent<OperationZone>();
                if (zone != null)
                    return zone.ReadLanding();
            }

            return new DropLanding(LandingPlace.EmptySpace, 0, null);
        }

        void ClearGesture()
        {
            _dragging = false;
            _hasArmed = false;
            _hasPressedPayload = false;
            _pressed = null;
            ClearCard();
        }

        void ClearCard()
        {
            if (_card != null)
                Destroy(_card);
            _card = null;
        }

        void RestoreHome()
        {
            if (_home != null)
                _home.SetVisibility(1f);
            _home = null;
        }

        void ResetVisuals()
        {
            ClearCard();
            RestoreHome();
            if (_carried != null)
                Destroy(_carried.gameObject);
            _carried = null;
            _releasing = false;
            _releaseClock = 0f;
            _fading = false;
            _fadeClock = 0f;
            _returning = false;
            _returnClock = 0f;
        }

        void BeginFade()
        {
            _fading = true;
            _fadeClock = 0f;
        }

        void Update()
        {
            if (_carried == null)
                return;

            var dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            if (_returning)
                EaseBackHome(dt);
            if (_fading)
            {
                _fadeClock += dt;
                var t = Mathf.Clamp01(_fadeClock / FadeSeconds);
                _carried.SetVisibility(1f - t);
                if (_home != null)
                    _home.SetVisibility(Mathf.Lerp(SourceAlpha, 1f, t));
                if (t >= 1f)
                    ResetVisuals();
                return;
            }

            if (!_releasing)
                return;

            _releaseClock += dt;
            if (_carried.Settled || _releaseClock >= MaxReleaseSeconds)
                BeginFade();
        }

        void Paint()
        {
            if (_presentation != null)
                _presentation.Refresh();
        }

        /// <summary>
        /// 指针离生产区、培育区卡片越近，虚影越接近那一块容器里怪物的屏幕大小。
        /// 还在怪物栏里时距离几乎没缩短，尺寸保持不变。世界画布和覆盖层不能直接比 lossyScale。
        /// </summary>
        void ApplyCarryApproach(Vector2 screen)
        {
            if (_carried == null)
                return;

            var distance = BoardDistance(screen);
            var approach = CarryScale.Approach(distance, _carryStartDistance);
            var board = DestinationScreen(screen);
            _carried.MatchScreenUniform(CarryScale.Blend(_carryFrom, board, approach));
        }

        void EaseBackHome(float dt)
        {
            if (_carried == null)
                return;

            _returnClock += dt;
            _carried.MatchScreenUniform(CarryScale.Return(_returnFrom, _carryFrom, _returnClock));
        }

        OperationScreenView OperationView()
        {
            return GetComponent<OperationScreenView>();
        }

        /// <summary>更近的那一块容器（生产区或培育区）里，怪物肖像的屏幕尺寸。</summary>
        float DestinationScreen(Vector2 screen)
        {
            var view = OperationView();
            if (view == null)
                return _carryFrom;

            var extraction = ZoneDistance(view.extractionSlots, screen);
            var breeding = ZoneDistance(view.breedingSlots, screen);
            var nearer = extraction <= breeding ? view.extractionSlots : view.breedingSlots;
            var farther = extraction <= breeding ? view.breedingSlots : view.extractionSlots;
            var scale = PortraitScreen(nearer);
            if (scale <= 0f)
                scale = PortraitScreen(farther);
            return scale > 0f ? scale : _carryFrom;
        }

        static float PortraitScreen(MonsterSlotView[] slots)
        {
            if (slots == null)
                return 0f;

            for (var i = 0; i < slots.Length; i++)
            {
                var portrait = slots[i] != null ? slots[i].portrait : null;
                var rect = portrait != null ? portrait.transform as RectTransform : null;
                var scale = CanvasMap.ScreenUniform(rect);
                if (scale > 0f)
                    return scale;
            }

            return 0f;
        }

        float BoardDistance(Vector2 screen)
        {
            var view = OperationView();
            if (view == null)
                return float.PositiveInfinity;

            var best = float.PositiveInfinity;
            best = Mathf.Min(best, ZoneDistance(view.extractionSlots, screen));
            best = Mathf.Min(best, ZoneDistance(view.breedingSlots, screen));
            return best;
        }

        float ZoneDistance(MonsterSlotView[] slots, Vector2 screen)
        {
            if (slots == null)
                return float.PositiveInfinity;

            var best = float.PositiveInfinity;
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null)
                    continue;

                var rect = slot.transform as RectTransform;
                if (rect == null || !rect.gameObject.activeInHierarchy)
                    continue;

                var screenRect = CanvasMap.ScreenRect(rect);
                var distance = CarryScale.DistanceToRect(screen, screenRect.min, screenRect.max);
                if (distance < best)
                    best = distance;
            }

            return best;
        }
    }
}

