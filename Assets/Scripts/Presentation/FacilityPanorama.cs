using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

namespace TheCall
{
    // There are two stops. A release always completes or cancels the horizontal transition.
    public sealed class FacilityPanorama : MonoBehaviour
    {
        public RectTransform content;
        public TMP_Text locationLabel;
        public float roomWidth = 1920;
        public float slideSeconds = 0.48f;
        public bool Production { get; private set; } = true;
        public bool Moving { get; private set; }
        public bool Locked { get; set; }
        float _from;
        float _elapsed;
        Vector2 _press;
        float _pressOffset;
        bool _dragging;

        public void ShowDorm() { if (!Locked) Focus(false); }
        public void ShowProduction() { if (!Locked) Focus(true); }
        public void Focus(bool production, bool immediate = false)
        {
            Production = production;
            _dragging = false;
            _from = content.anchoredPosition.x;
            _elapsed = 0;
            Moving = !immediate && Mathf.Abs(_from - Destination) > 0.05f;
            if (!Moving) Offset(Destination);
            Paint();
        }

        float Destination => Production ? -roomWidth : 0;

        public bool BeginBackground(PointerEventData e)
        {
            if (Locked || Mathf.Abs(e.position.x - e.pressPosition.x) < Mathf.Abs(e.position.y - e.pressPosition.y)) return false;
            _press = e.pressPosition;
            _pressOffset = content.anchoredPosition.x;
            _dragging = true;
            Moving = false;
            return true;
        }

        public void DragBackground(PointerEventData e)
        {
            if (!_dragging) return;
            var scale = content.GetComponentInParent<Canvas>().scaleFactor;
            Offset(Mathf.Clamp(_pressOffset + (e.position.x - _press.x) / Mathf.Max(0.01f, scale), -roomWidth, 0));
        }

        public void EndBackground(PointerEventData e)
        {
            if (!_dragging) return;
            var delta = e.position.x - _press.x;
            var scale = content.GetComponentInParent<Canvas>().scaleFactor;
            var crossed = Mathf.Abs(delta) > 90 * scale;
            Focus(crossed ? delta < 0 : Production);
        }

        void Update()
        {
            if (!Moving) return;
            _elapsed += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_elapsed / slideSeconds);
            var ease = t * t * t * (t * (t * 6 - 15) + 10);
            Offset(Mathf.Lerp(_from, Destination, ease));
            if (t >= 1) { Offset(Destination); Moving = false; }
        }

        void OnDisable()
        {
            _dragging = Moving = false;
            Locked = false;
            if (content != null) Offset(Destination);
        }

        void Offset(float x) => content.anchoredPosition = new Vector2(x, content.anchoredPosition.y);
        void Paint()
        {
            if (locationLabel != null) locationLabel.text = Production ? "02  /  生产培育区" : "01  /  生活宿舍区";
        }
    }
}
