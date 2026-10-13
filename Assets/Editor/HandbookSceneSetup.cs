using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall.Editor
{
    /// <summary>只补建共用设置与手册，不重建玩家已经调整的游戏界面。</summary>
    public static class HandbookSceneSetup
    {
        static readonly Color Panel = new Color32(18, 28, 40, 255);
        static readonly Color Face = new Color32(36, 52, 66, 255);
        static readonly Color Ink = new Color32(232, 244, 240, 255);
        static readonly Color Muted = new Color32(162, 188, 195, 255);
        static readonly Color Mint = new Color32(72, 214, 168, 255);
        static TMP_FontAsset Font => Resources.Load<TMP_FontAsset>("SmileySans-Oblique-3 SDF");

        [MenuItem("TheCall/UI/修复设置与玩家手册")]
        public static void ApplyAndSave()
        {
            Ensure();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        public static SettingsChromeView Ensure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出播放模式再保存界面。");
            var canvas = GameObject.Find("UICanvas")?.transform;
            if (canvas == null) throw new InvalidOperationException("当前场景缺少 UICanvas。");
            var chrome = UnityEngine.Object.FindAnyObjectByType<SettingsChromeView>(FindObjectsInactive.Include);
            if (chrome == null)
            {
                var root = Rect(canvas, "SettingsChrome"); Stretch(root);
                chrome = root.gameObject.AddComponent<SettingsChromeView>();
            }
            if (chrome.settingsWindow == null) BuildSettings(chrome);
            foreach (var slider in new[] { chrome.masterVolume, chrome.musicVolume, chrome.sfxVolume })
            {
                if (slider == null) continue;
                slider.handleRect.anchorMin = new Vector2(.5f, 0);
                slider.handleRect.anchorMax = new Vector2(.5f, 1);
                slider.handleRect.sizeDelta = new Vector2(24, 6);
            }
            if (chrome.codex == null) chrome.codex = BuildHandbook(chrome.transform);
            EnsureSearchViewport(chrome.codex);
            chrome.log = UnityEngine.Object.FindAnyObjectByType<ProductionLogView>(FindObjectsInactive.Include);
            if (chrome.log == null) chrome.log = BuildLog(chrome.transform);
            var screens = new Component[] {
                UnityEngine.Object.FindAnyObjectByType<OpeningScreenView>(FindObjectsInactive.Include),
                UnityEngine.Object.FindAnyObjectByType<LevelStartScreenView>(FindObjectsInactive.Include),
                UnityEngine.Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include),
                UnityEngine.Object.FindAnyObjectByType<ResearchScreenView>(FindObjectsInactive.Include),
                UnityEngine.Object.FindAnyObjectByType<ShopScreenView>(FindObjectsInactive.Include),
                UnityEngine.Object.FindAnyObjectByType<ResultScreenView>(FindObjectsInactive.Include) };
            chrome.openButtons = screens.Where(s => s != null).Select(screen => {
                var existing = screen.transform.Find("SettingsButton")?.GetComponent<Button>();
                if (existing != null)
                {
                    if (screen is ResearchScreenView) At(existing, 1044, 20, 112, 48);
                    if (screen is ShopScreenView) At(existing, 1792, 18, 112, 46);
                    return existing;
                }
                var button = Click(screen.transform, "SettingsButton", "设置", 20);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = new Vector2(-30, -24); rect.sizeDelta = new Vector2(112, 48);
                if (screen is ResearchScreenView) At(button, 1044, 20, 112, 48);
                if (screen is ShopScreenView) At(button, 1792, 18, 112, 46);
                return button;
            }).ToArray();
            chrome.HideAll();
            chrome.transform.SetAsLastSibling();
            var presentation = UnityEngine.Object.FindAnyObjectByType<TheCallPresentation>();
            if (presentation != null)
            {
                var serialized = new SerializedObject(presentation);
                serialized.FindProperty("_settings").objectReferenceValue = chrome;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(chrome);
            return chrome;
        }

        static void BuildSettings(SettingsChromeView chrome)
        {
            var root = Rect(chrome.transform, "SettingsWindow"); Stretch(root);
            chrome.settingsWindow = root.gameObject;
            chrome.settingsShadeButton = Shade(root);
            var panel = Box(root, "Panel", Panel);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.sizeDelta = new Vector2(740, 760);
            At(Text(panel, "Title", "设置", 38, Ink), 40, 30, 600, 58);
            At(Text(panel, "Subtitle", "查阅规则，调整声音，继续你的育成计划", 20, Muted), 40, 92, 650, 38);
            chrome.settingsCloseButton = Click(panel, "CloseButton", "返回游戏", 20); At(chrome.settingsCloseButton, 500, 676, 200, 52);
            chrome.codexButton = Click(panel, "HandbookButton", "玩家手册   ›\n<size=65%>技能 · 性格 · 种族 · 心情 · 工具 · 科技 · 玩法</size>", 28);
            chrome.codexButton.targetGraphic.color = new Color32(32, 99, 82, 255); At(chrome.codexButton, 40, 158, 660, 118);
            chrome.logButton = Click(panel, "LogButton", "结算记录   ›", 24); At(chrome.logButton, 40, 292, 660, 64);
            At(Text(panel, "AudioTitle", "声音", 24, Mint), 40, 388, 620, 38);
            chrome.masterVolume = Volume(panel, "Master", "总音量", 442);
            chrome.musicVolume = Volume(panel, "Music", "音乐", 503);
            chrome.sfxVolume = Volume(panel, "Sfx", "音效", 564);
            chrome.quitButton = Click(panel, "QuitButton", "退出游戏", 20); At(chrome.quitButton, 40, 676, 200, 52);
            root.gameObject.SetActive(false);
        }

        static Slider Volume(Transform parent, string name, string title, float y)
        {
            At(Text(parent, name + "Label", title, 20, Ink), 40, y, 120, 44);
            var root = Rect(parent, name + "Volume"); At(root, 176, y + 10, 520, 24);
            var background = Box(root, "Track", Face); Stretch(background); background.offsetMin = new Vector2(0, 8); background.offsetMax = new Vector2(0, -8);
            var fillArea = Rect(root, "FillArea"); Stretch(fillArea); fillArea.offsetMin = new Vector2(12, 8); fillArea.offsetMax = new Vector2(-12, -8);
            var fill = Box(fillArea, "Fill", Mint); Stretch(fill);
            var handleArea = Rect(root, "HandleArea"); Stretch(handleArea); handleArea.offsetMin = new Vector2(12, 0); handleArea.offsetMax = new Vector2(-12, 0);
            var handle = Box(handleArea, "Handle", Ink); handle.anchorMin = new Vector2(.5f, 0); handle.anchorMax = new Vector2(.5f, 1); handle.sizeDelta = new Vector2(24, 6);
            var slider = root.gameObject.AddComponent<Slider>(); slider.fillRect = fill; slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>(); slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            return slider;
        }

        static void EnsureSearchViewport(HandbookView view)
        {
            if (view.search.textViewport != view.search.transform) return;
            var viewport = Rect(view.search.transform, "TextViewport"); Stretch(viewport);
            viewport.offsetMin = new Vector2(16, 8); viewport.offsetMax = new Vector2(-52, -8);
            viewport.gameObject.AddComponent<RectMask2D>();
            view.search.textComponent.transform.SetParent(viewport, false); Stretch(view.search.textComponent.rectTransform);
            view.search.placeholder.transform.SetParent(viewport, false); Stretch(view.search.placeholder.rectTransform);
            view.search.textComponent.textWrappingMode = TextWrappingModes.NoWrap;
            view.search.textViewport = viewport;
        }

        static HandbookView BuildHandbook(Transform parent)
        {
            var root = Rect(parent, "PlayerHandbook"); Stretch(root);
            var view = root.gameObject.AddComponent<HandbookView>();
            view.shadeButton = Shade(root);
            var panel = Box(root, "Panel", Panel); Stretch(panel); panel.offsetMin = new Vector2(54, 46); panel.offsetMax = new Vector2(-54, -46);
            At(Text(panel, "Title", "玩家手册", 36, Ink), 30, 20, 700, 56);
            At(Text(panel, "Subtitle", "了解每个效果，安排你的下一步", 20, Muted), 30, 80, 900, 32);
            view.closeButton = Click(panel, "CloseButton", "返回设置  Esc", 20);
            var close = (RectTransform)view.closeButton.transform; close.anchorMin = close.anchorMax = new Vector2(1, 1); close.pivot = new Vector2(1, 1); close.anchoredPosition = new Vector2(-30, -28); close.sizeDelta = new Vector2(190, 52);
            var categories = Column(panel, "Categories", 0, .17f);
            view.categoryButtons = new Button[HandbookView.Categories.Length];
            for (var i = 0; i < view.categoryButtons.Length; i++)
            { view.categoryButtons[i] = Click(categories, "Category_" + i, HandbookView.Categories[i], 22); At(view.categoryButtons[i], 0, i * 76, 260, 62); }
            At(Text(categories, "Help", "先选分类，再搜索\n滚轮浏览条目\n点击关联内容继续阅读", 18, Muted), 0, 646, 265, 100);
            var list = Column(panel, "Index", .18f, .43f);
            var searchRoot = Box(list, "Search", Face); TopStretch(searchRoot, 0, 52);
            var inputText = Text(searchRoot, "Text", "", 22, Ink); Stretch(inputText.rectTransform); inputText.rectTransform.offsetMin = new Vector2(16, 8); inputText.rectTransform.offsetMax = new Vector2(-52, -8); inputText.richText = false;
            var placeholder = Text(searchRoot, "Placeholder", "搜索名称或效果…", 20, Muted); Stretch(placeholder.rectTransform); placeholder.rectTransform.offsetMin = new Vector2(16, 8); placeholder.rectTransform.offsetMax = new Vector2(-52, -8);
            view.search = searchRoot.gameObject.AddComponent<TMP_InputField>(); view.search.textViewport = searchRoot; view.search.textComponent = inputText; view.search.placeholder = placeholder; view.search.targetGraphic = searchRoot.GetComponent<Image>();
            view.clearButton = Click(searchRoot, "ClearSearch", "×", 24); var clear = (RectTransform)view.clearButton.transform; clear.anchorMin = new Vector2(1, 0); clear.anchorMax = Vector2.one; clear.offsetMin = new Vector2(-48, 0); clear.offsetMax = Vector2.zero;
            var filters = Rect(list, "RarityFilters"); TopStretch(filters, 62, 42); var filterLayout = filters.gameObject.AddComponent<HorizontalLayoutGroup>(); filterLayout.spacing = 6; filterLayout.childControlWidth = true; filterLayout.childControlHeight = true; filterLayout.childForceExpandWidth = true;
            view.rarityButtons = new Button[5]; var rarities = new[] { "全部", "白", "蓝", "金", "负面" };
            for (var i = 0; i < rarities.Length; i++) { view.rarityButtons[i] = Click(filters, "Rarity_" + i, rarities[i], 19); view.rarityButtons[i].gameObject.AddComponent<LayoutElement>().minWidth = 54; }
            view.countLabel = Text(list, "Count", "", 19, Muted); TopStretch(view.countLabel.rectTransform, 114, 34);
            view.scroll = Scroll(list, "EntryList", out var content); Stretch((RectTransform)view.scroll.transform); ((RectTransform)view.scroll.transform).offsetMax = new Vector2(0, -158);
            view.listContent = content; view.rowTemplate = Click(content, "EntryTemplate", "条目", 23); RowHeight(view.rowTemplate, 64); view.rowTemplate.gameObject.SetActive(false);
            view.emptyLabel = Text(list, "Empty", "没有匹配结果\n清空搜索或选择其他稀有度", 21, Muted); TopStretch(view.emptyLabel.rectTransform, 172, 110);
            var detail = Column(panel, "Detail", .45f, 1);
            view.detailScroll = Scroll(detail, "DetailScroll", out var body); Stretch((RectTransform)view.detailScroll.transform);
            body.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(28, 28, 22, 28); body.GetComponent<VerticalLayoutGroup>().spacing = 16;
            view.locationLabel = Text(body, "Location", "玩家手册", 19, Mint);
            var header = Rect(body, "Header"); var headerLayout = header.gameObject.AddComponent<HorizontalLayoutGroup>(); headerLayout.spacing = 22; headerLayout.childControlHeight = true; headerLayout.childControlWidth = true; headerLayout.childForceExpandWidth = false;
            var icon = Box(header, "Icon", Color.white); view.icon = icon.GetComponent<Image>(); view.icon.preserveAspect = true; var iconSize = icon.gameObject.AddComponent<LayoutElement>(); iconSize.preferredWidth = iconSize.minWidth = 84; iconSize.preferredHeight = 84;
            var labels = Rect(header, "Labels"); var labelLayout = labels.gameObject.AddComponent<VerticalLayoutGroup>(); labelLayout.childControlWidth = labelLayout.childControlHeight = true; labelLayout.childForceExpandHeight = false; labels.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            view.nameLabel = Text(labels, "Name", "", 36, Ink); view.metadataLabel = Text(labels, "Metadata", "", 21, Muted);
            Text(body, "RuleCaption", "效果与规则", 22, Mint);
            view.ruleLabel = Text(body, "Rule", "", 25, Ink);
            Text(body, "AdviceCaption", "怎么使用", 22, Mint);
            view.adviceLabel = Text(body, "Advice", "", 22, Muted);
            Text(body, "RelatedCaption", "关联阅读", 22, Mint);
            view.relatedContent = Rect(body, "Related"); var relatedLayout = view.relatedContent.gameObject.AddComponent<VerticalLayoutGroup>(); relatedLayout.spacing = 8; relatedLayout.childControlWidth = relatedLayout.childControlHeight = true; relatedLayout.childForceExpandHeight = false;
            view.relatedTemplate = Click(view.relatedContent, "RelatedTemplate", "关联", 21); RowHeight(view.relatedTemplate, 52); view.relatedTemplate.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            return view;
        }

        static ProductionLogView BuildLog(Transform parent)
        {
            var root = Rect(parent, "ProductionLogWindow"); Stretch(root); var view = root.gameObject.AddComponent<ProductionLogView>(); view.shadeButton = Shade(root);
            var panel = Box(root, "Panel", Panel); Stretch(panel); panel.offsetMin = new Vector2(210, 100); panel.offsetMax = new Vector2(-210, -100);
            At(Text(panel, "Title", "结算记录", 32, Ink), 28, 22, 1000, 56);
            view.closeButton = Click(panel, "CloseButton", "返回", 20); var close = (RectTransform)view.closeButton.transform; close.anchorMin = close.anchorMax = close.pivot = Vector2.one; close.anchoredPosition = new Vector2(-28, -22); close.sizeDelta = new Vector2(150, 52);
            view.scroll = Scroll(panel, "Records", out var body); Stretch((RectTransform)view.scroll.transform); ((RectTransform)view.scroll.transform).offsetMin = new Vector2(28, 28); ((RectTransform)view.scroll.transform).offsetMax = new Vector2(-28, -100);
            view.body = Text(body, "Body", "尚无结算记录。", 22, Ink); root.gameObject.SetActive(false); return view;
        }

        static RectTransform Column(Transform parent, string name, float left, float right)
        { var rect = Rect(parent, name); rect.anchorMin = new Vector2(left, 0); rect.anchorMax = new Vector2(right, 1); rect.offsetMin = new Vector2(30, 28); rect.offsetMax = new Vector2(-10, -140); return rect; }
        static ScrollRect Scroll(Transform parent, string name, out RectTransform content)
        {
            var root = Rect(parent, name); var scroll = root.gameObject.AddComponent<ScrollRect>();
            var viewport = Box(root, "Viewport", new Color32(27, 38, 52, 255)); Stretch(viewport); viewport.offsetMax = new Vector2(-12, 0); viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            content = Rect(viewport, "Content"); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 8; layout.padding = new RectOffset(8, 8, 8, 8); layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 32; scroll.movementType = ScrollRect.MovementType.Clamped;
            var barRoot = Box(root, "Scrollbar", Face); barRoot.anchorMin = new Vector2(1, 0); barRoot.anchorMax = Vector2.one; barRoot.offsetMin = new Vector2(-8, 0); barRoot.offsetMax = Vector2.zero;
            var handle = Box(barRoot, "Handle", new Color32(74, 112, 120, 255)); Stretch(handle);
            var bar = barRoot.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle; bar.targetGraphic = handle.GetComponent<Image>(); bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }
        static Button Shade(Transform parent) { var button = Click(parent, "Shade", "", 20); Stretch((RectTransform)button.transform); button.targetGraphic.color = new Color(0, 0, 0, .82f); return button; }
        static void RowHeight(Button button, float height) { var layout = button.gameObject.AddComponent<LayoutElement>(); layout.minHeight = layout.preferredHeight = height; }
        static Button Click(Transform parent, string name, string title, float size)
        {
            var rect = Box(parent, name, Face); var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var label = Text(rect, "Label", title, size, Ink); Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(14, 4); label.rectTransform.offsetMax = new Vector2(-14, -4); label.alignment = TextAlignmentOptions.MidlineLeft;
            var colors = button.colors; colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f); colors.pressedColor = new Color(.7f, .9f, .8f); colors.fadeDuration = .08f; button.colors = colors; return button;
        }
        static TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color)
        { var rect = Rect(parent, name); var text = rect.gameObject.AddComponent<TextMeshProUGUI>(); text.font = Font; text.text = value; text.fontSize = size; text.color = color; text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow; text.alignment = TextAlignmentOptions.TopLeft; return text; }
        static RectTransform Box(Transform parent, string name, Color color) { var rect = Rect(parent, name); rect.gameObject.AddComponent<Image>().color = color; return rect; }
        static RectTransform Rect(Transform parent, string name) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void TopStretch(RectTransform rect, float y, float height) { rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = new Vector2(0, -y); rect.sizeDelta = new Vector2(0, height); }
        static void At(Component component, float x, float y, float width, float height) { var rect = (RectTransform)component.transform; rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); }
    }
}
