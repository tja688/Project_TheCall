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
        public RectTransform dragLayer;

        TheCallPresentation _presentation;
        OperationPayloadDrag _pressed;
        DropPayload _pressedPayload;
        bool _hasPressedPayload;
        DropPayload _armed;
        bool _hasArmed;
        bool _dragging;
        bool _finished = true;
        GameObject _ghost;
        Vector2 _previousPointer;
        Vector2 _pointerVelocity;
        MonsterPortrait _ghostPortrait;
        float _headAngle;
        float _headVelocity;
        float _footAngle;
        float _footVelocity;
        Vector2 _ghostOffset;
        readonly List<RaycastResult> _hits = new List<RaycastResult>();

        public DropPayload? Armed => _hasArmed ? _armed : (DropPayload?)null;

        public void Attach(TheCallPresentation presentation) => _presentation = presentation;

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
            _previousPointer = eventData.position;
            _pointerVelocity = Vector2.zero;
            ResetSpring();
            if (_hasPressedPayload && source == _pressed)
                SpawnGhost(source, eventData);
        }

        public void MoveGhost(PointerEventData eventData)
        {
            if (_ghost == null)
                return;

            var deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _pointerVelocity = Vector2.ClampMagnitude((eventData.position - _previousPointer) / deltaTime, 1000f);
            _previousPointer = eventData.position;
            _ghost.transform.position = eventData.position + _ghostOffset;
            AdvanceSpring(deltaTime);
        }

        public void Finish(PointerEventData eventData)
        {
            if (_finished)
                return;

            _finished = true;
            if (_dragging)
            {
                if (_hasPressedPayload && _presentation != null)
                    _presentation.CommitDrop(new OperationDrop(_pressedPayload, RayLanding(eventData)));

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
        }

        void SpawnGhost(OperationPayloadDrag source, PointerEventData eventData)
        {
            ClearGhost();
            if (dragLayer == null)
                return;

            _ghost = Instantiate(source.gameObject, dragLayer);
            _ghostOffset = (Vector2)source.transform.position - eventData.position;
            var group = _ghost.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var copy = _ghost.GetComponent<OperationPayloadDrag>();
            if (copy != null)
                copy.enabled = false;
            _ghostPortrait = _ghost.GetComponentInChildren<MonsterPortrait>(true);
            if (_ghostPortrait != null && _ghostPortrait.gameObject.activeInHierarchy)
                _ghostPortrait.SetDragMotion(_pointerVelocity);
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
            ClearGhost();
        }

        void ClearGhost()
        {
            if (_ghostPortrait != null)
                _ghostPortrait.RestoreMotion();
            _ghostPortrait = null;
            if (_ghost == null)
                return;

            Destroy(_ghost);
            _ghost = null;
        }

        void AdvanceSpring(float deltaTime)
        {
            if (_ghostPortrait == null)
                return;

            var drive = Mathf.Clamp(_pointerVelocity.x / 700f, -1f, 1f);
            StepSpring(ref _headAngle, ref _headVelocity, -drive * 7f, deltaTime);
            StepSpring(ref _footAngle, ref _footVelocity, drive * 5f, deltaTime);
            _ghostPortrait.SetSpringMotion(_headAngle, _footAngle);
        }

        static void StepSpring(ref float angle, ref float velocity, float target, float deltaTime)
        {
            const float stiffness = 90f;
            const float damping = 18f;
            var acceleration = (target - angle) * stiffness - velocity * damping;
            velocity += acceleration * deltaTime;
            angle += velocity * deltaTime;
        }

        void ResetSpring()
        {
            _headAngle = _headVelocity = _footAngle = _footVelocity = 0f;
        }

        void Paint()
        {
            if (_presentation != null)
                _presentation.Refresh();
        }
    }
}
