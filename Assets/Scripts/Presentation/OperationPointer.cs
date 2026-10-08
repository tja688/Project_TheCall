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
            if (_carried != null)
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
    }
}
