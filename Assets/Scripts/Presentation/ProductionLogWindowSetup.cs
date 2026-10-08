#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    public static class ProductionLogWindowSetup
    {
        public static string Build()
        {
            var existing = GameObject.Find("LogWindow");
            if (existing != null)
                return "already-exists";

            var canvas = GameObject.Find("UICanvas");
            if (canvas == null)
                return "missing-canvas";

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/SmileySans-Oblique-3 SDF.asset");
            var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (font == null || sprite == null)
                return "missing-font-or-sprite";

            var window = New("LogWindow", canvas.transform);
            Stretch(window);
            window.gameObject.AddComponent<ProductionLogView>();

            var shade = New("Shade", window);
            Stretch(shade);
            var shadeImage = Paint(shade.gameObject, new Color(0f, 0f, 0f, 0.62f), sprite, false);
            var shadeButton = shade.gameObject.AddComponent<Button>();
            shadeButton.targetGraphic = shadeImage;
            shadeButton.transition = Selectable.Transition.None;

            var panel = New("Panel", window);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(880f, 900f);
            panel.anchoredPosition = Vector2.zero;
            Paint(panel.gameObject, new Color(0.12f, 0.11f, 0.10f, 0.98f), sprite, true);

            var title = New("Title", panel);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.offsetMin = new Vector2(32f, -76f);
            title.offsetMax = new Vector2(-140f, -20f);
            var titleText = Write(title.gameObject, "计算记录", 32f, new Color(0.96f, 0.93f, 0.86f, 1f), font, TextAlignmentOptions.MidlineLeft);
            titleText.fontStyle = FontStyles.Bold;

            var close = New("CloseButton", panel);
            close.anchorMin = new Vector2(1f, 1f);
            close.anchorMax = new Vector2(1f, 1f);
            close.pivot = new Vector2(1f, 1f);
            close.sizeDelta = new Vector2(96f, 44f);
            close.anchoredPosition = new Vector2(-24f, -20f);
            var closeImage = Paint(close.gameObject, new Color(0.30f, 0.26f, 0.22f, 1f), sprite, true);
            var closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            var closeColors = closeButton.colors;
            closeColors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            closeColors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            closeColors.fadeDuration = 0.08f;
            closeButton.colors = closeColors;
            var closeLabel = New("Label", close);
            Stretch(closeLabel);
            Write(closeLabel.gameObject, "关闭", 22f, new Color(0.96f, 0.93f, 0.86f, 1f), font, TextAlignmentOptions.Center);

            var scroll = New("Scroll", panel);
            scroll.anchorMin = Vector2.zero;
            scroll.anchorMax = Vector2.one;
            scroll.offsetMin = new Vector2(28f, 28f);
            scroll.offsetMax = new Vector2(-28f, -92f);
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 36f;
            scrollRect.inertia = true;

            var viewport = New("Viewport", scroll);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-16f, 0f);
            var viewportImage = Paint(viewport.gameObject, new Color(0.08f, 0.075f, 0.07f, 1f), sprite, false);
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            var content = New("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(20f, 0f);
            content.offsetMax = new Vector2(-20f, 0f);
            content.sizeDelta = new Vector2(-40f, 0f);
            var body = Write(content.gameObject, "", 22f, new Color(0.91f, 0.87f, 0.78f, 1f), font, TextAlignmentOptions.TopLeft);
            body.lineSpacing = 8f;
            body.raycastTarget = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bar = New("Scrollbar", scroll);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(10f, 0f);
            bar.anchoredPosition = Vector2.zero;
            var barImage = Paint(bar.gameObject, new Color(0.16f, 0.14f, 0.12f, 1f), sprite, true);
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = barImage;

            var sliding = New("Sliding Area", bar);
            Stretch(sliding);
            sliding.offsetMin = new Vector2(2f, 2f);
            sliding.offsetMax = new Vector2(-2f, -2f);

            var handle = New("Handle", sliding);
            Stretch(handle);
            var handleImage = Paint(handle.gameObject, new Color(0.62f, 0.52f, 0.36f, 1f), sprite, true);
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.verticalScrollbarSpacing = 4f;

            var view = window.GetComponent<ProductionLogView>();
            view.body = body;
            view.scroll = scrollRect;
            view.closeButton = closeButton;
            view.shadeButton = shadeButton;

            window.SetAsLastSibling();
            window.gameObject.SetActive(false);
            Undo.RegisterCreatedObjectUndo(window.gameObject, "Add production log window");
            EditorSceneManager.MarkSceneDirty(canvas.scene);
            EditorSceneManager.SaveOpenScenes();
            return "created";
        }

        static RectTransform New(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        static Image Paint(GameObject go, Color color, Sprite sprite, bool sliced)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        static TMP_Text Write(GameObject go, string text, float size, Color color, TMP_FontAsset font, TextAlignmentOptions align)
        {
            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.enableWordWrapping = true;
            label.richText = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }
    }
}
#endif
