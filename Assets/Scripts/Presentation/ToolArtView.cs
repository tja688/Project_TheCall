using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace TheCall
{
    public sealed class ToolArtView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        GameObject _tooltip;
        public void OnPointerEnter(PointerEventData data)
        {
            OnPointerExit(data);
            if (string.IsNullOrEmpty(toolName) || string.IsNullOrEmpty(description)) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            _tooltip = new GameObject("ToolDetails", typeof(RectTransform), typeof(Image));
            _tooltip.transform.SetParent(canvas.transform, false);
            var root = (RectTransform)_tooltip.transform;
            root.sizeDelta = new Vector2(340, 140); root.position = transform.position + new Vector3(0, 100, 0);
            var bounds = ((RectTransform)canvas.transform).rect;
            var local = root.anchoredPosition;
            local.x = Mathf.Clamp(local.x, bounds.xMin + 170, bounds.xMax - 170);
            local.y = Mathf.Clamp(local.y, bounds.yMin + 70, bounds.yMax - 70);
            root.anchoredPosition = local;
            var background = _tooltip.GetComponent<Image>(); background.color = new Color32(18, 28, 40, 250); background.raycastTarget = false;
            var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(root, false);
            var rect = (RectTransform)label.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(12, 8); rect.offsetMax = new Vector2(-12, -8);
            var text = label.GetComponent<TextMeshProUGUI>(); text.font = nameLabel != null ? nameLabel.font : TMP_Settings.defaultFontAsset; text.fontSize = 18; text.text = "<b>" + toolName + "</b>\n" + description; text.raycastTarget = false;
        }
        public void OnPointerExit(PointerEventData data) { if (_tooltip != null) Destroy(_tooltip); _tooltip = null; }
        void OnDisable() => OnPointerExit(null);
        public string toolName;
        public Sprite icon;
        public Image iconImage;
        public TMP_Text nameLabel;
        [TextArea] public string description;
    }
}
