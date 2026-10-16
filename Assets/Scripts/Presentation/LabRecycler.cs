using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class LabRecycler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform upperJaw;
        public RectTransform lowerJaw;
        public Image glow;
        bool _hover;
        float _chew;
        float _open;
        public int ConsumedCount { get; private set; }
        public float Openness => _open;
        public void OnPointerEnter(PointerEventData data) => _hover = true;
        public void OnPointerExit(PointerEventData data) => _hover = false;
        public void Consume() { _hover = false; _chew = 0.75f; ConsumedCount++; }
        void Update()
        {
            _chew = Mathf.Max(0, _chew - Time.unscaledDeltaTime);
            var target = _chew > 0 ? 0 : (_hover ? 1f : 0f);
            _open = Mathf.MoveTowards(_open, target, Time.unscaledDeltaTime * 5);
            Paint();
        }
        void OnDisable() { _hover = false; _chew = _open = 0; Paint(); }
        void Paint()
        {
            if (upperJaw != null) upperJaw.anchoredPosition = new Vector2(-3, 74 + _open * 12);
            if (lowerJaw != null) lowerJaw.anchoredPosition = new Vector2(-3, 37 - _open * 12);
            if (glow != null) glow.color = new Color(1, 1, 1, _chew > 0 ? 0.85f : 0.08f * (1 - _open));
        }
    }
}
