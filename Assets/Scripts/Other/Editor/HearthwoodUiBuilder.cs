using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public static class HearthwoodUiBuilder
{
    public static string Build()
    {
const float S = 6f;
const float Slice = 1f / S;
var ink = new Color32(0x55, 0x41, 0x4A, 0xFF);
var cream = new Color32(0xF4, 0xDF, 0xB3, 0xFF);
const float Frame = 1f / 14f;

var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Arts/Pngs/Textures/texture121.png");
importer.wrapMode = TextureWrapMode.Repeat;
importer.filterMode = FilterMode.Point;
importer.textureCompression = TextureImporterCompression.Uncompressed;
importer.mipmapEnabled = false;
importer.SaveAndReimport();

var sprites = new Dictionary<string, Sprite>();
foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Arts/Atlas/hearthwood.png"))
{
    if (asset is Sprite sprite)
        sprites[sprite.name] = sprite;
}

Sprite Sp(string name)
{
    if (!sprites.TryGetValue(name, out var sprite) || sprite == null)
        throw new System.Exception("Missing sprite " + name);
    return sprite;
}

var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Arts/Fronts/DeYiHei/SmileySans-Oblique-3 SDF.asset");
if (font == null)
    throw new System.Exception("Missing Smiley Sans font asset");
font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz0123456789");

var canvasGo = GameObject.Find("Canvas");
if (canvasGo == null)
    throw new System.Exception("Canvas is missing");

var canvas = canvasGo.GetComponent<Canvas>();
canvas.renderMode = RenderMode.ScreenSpaceOverlay;
canvas.pixelPerfect = false;
var scaler = canvasGo.GetComponent<CanvasScaler>();
scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
scaler.referenceResolution = new Vector2(1920f, 1080f);
scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
scaler.matchWidthOrHeight = 0.5f;
scaler.referencePixelsPerUnit = 100f;
if (canvasGo.GetComponent<GraphicRaycaster>() == null)
    canvasGo.AddComponent<GraphicRaycaster>();

for (int i = canvasGo.transform.childCount - 1; i >= 0; i--)
{
    var child = canvasGo.transform.GetChild(i).gameObject;
    if (child.name == "HearthwoodDemo" || child.name == "Background")
        Object.DestroyImmediate(child);
}

RectTransform Rt(GameObject go) => go.GetComponent<RectTransform>();

GameObject Node(string name, Transform parent)
{
    var go = new GameObject(name, typeof(RectTransform));
    go.transform.SetParent(parent, false);
    return go;
}

void Place(RectTransform rt, float x, float y, float w, float h, float parentW, float parentH)
{
    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.sizeDelta = new Vector2(w, h);
    rt.anchoredPosition = new Vector2(-parentW * 0.5f + x + w * 0.5f, parentH * 0.5f - y - h * 0.5f);
}

void Stretch(RectTransform rt)
{
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;
}

Image Sliced(GameObject go, Sprite sprite, bool raycast, float multiplier = Slice)
{
    var image = go.AddComponent<Image>();
    image.sprite = sprite;
    image.type = Image.Type.Sliced;
    image.pixelsPerUnitMultiplier = multiplier;
    image.color = Color.white;
    image.raycastTarget = raycast;
    return image;
}

Image Plain(GameObject go, Sprite sprite, bool raycast)
{
    var image = go.AddComponent<Image>();
    image.sprite = sprite;
    image.type = Image.Type.Simple;
    image.preserveAspect = true;
    image.color = Color.white;
    image.raycastTarget = raycast;
    return image;
}

TextMeshProUGUI Label(GameObject go, string text, float size, TextAlignmentOptions align, Color32 color)
{
    var label = go.AddComponent<TextMeshProUGUI>();
    label.font = font;
    label.text = text;
    label.fontSize = size;
    label.alignment = align;
    label.color = color;
    label.raycastTarget = false;
    label.textWrappingMode = TextWrappingModes.NoWrap;
    label.overflowMode = TextOverflowModes.Overflow;
    label.extraPadding = true;
    return label;
}

void SpriteButton(Button button, Sprite normal, Sprite hover, Sprite pressed, Sprite disabled)
{
    button.transition = Selectable.Transition.SpriteSwap;
    var state = button.spriteState;
    state.highlightedSprite = hover;
    state.pressedSprite = pressed;
    state.selectedSprite = normal;
    state.disabledSprite = disabled;
    button.spriteState = state;
    var colors = button.colors;
    colors.normalColor = Color.white;
    colors.highlightedColor = Color.white;
    colors.pressedColor = Color.white;
    colors.selectedColor = Color.white;
    colors.disabledColor = Color.white;
    button.colors = colors;
}

var backgroundGo = Node("Background", canvasGo.transform);
backgroundGo.transform.SetAsFirstSibling();
Stretch(Rt(backgroundGo));
var background = backgroundGo.AddComponent<RawImage>();
var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Arts/Pngs/Textures/texture121.png");
texture.wrapMode = TextureWrapMode.Repeat;
texture.filterMode = FilterMode.Point;
background.texture = texture;
background.color = new Color(0.57f, 0.55f, 0.40f, 1f);
background.raycastTarget = false;
background.uvRect = new Rect(0f, 0f, 1920f / (texture.width * S), 1080f / (texture.height * S));
var scroll = backgroundGo.AddComponent<DiagonalScrollBackground>();
var scrollSo = new SerializedObject(scroll);
scrollSo.FindProperty("target").objectReferenceValue = background;
scrollSo.FindProperty("texelScreenPixels").floatValue = S;
scrollSo.FindProperty("pixelsPerSecond").floatValue = 36f;
scrollSo.ApplyModifiedPropertiesWithoutUndo();

var demo = Node("HearthwoodDemo", canvasGo.transform);
Stretch(Rt(demo));
var shop = Node("FarmShop", demo.transform);
var settings = Node("Settings", demo.transform);
var satchel = Node("Satchel", demo.transform);
Stretch(Rt(shop));
Stretch(Rt(settings));
Stretch(Rt(satchel));

var catalogItems = new (string icon, string title, string description, int price)[]
{
    ("carrot", "CARROT", "Fresh crop", 12),
    ("turnip", "TURNIP", "Fresh crop", 8),
    ("beet", "BEET", "Fresh crop", 10),
    ("apple", "APPLE", "Fresh fruit", 15),
    ("tomato", "TOMATO", "Fresh crop", 9),
    ("orange", "ORANGE", "Fresh fruit", 14),
    ("corn", "CORN", "Fresh crop", 11),
    ("wheat", "WHEAT", "Fresh crop", 7),
    ("berry", "BERRY", "Fresh crop", 18),
    ("mushroom", "MUSHROOM", "Forage", 16),
    ("seed_bag", "SEEDS", "Planting", 6),
    ("axe", "AXE", "Tool", 40),
    ("pickaxe", "PICKAXE", "Tool", 45),
    ("hoe", "HOE", "Tool", 22),
    ("watering_can", "WATERING CAN", "Tool", 28),
    ("wood_log", "WOOD", "Material", 4),
    ("stone", "STONE", "Material", 3),
    ("iron_ore", "IRON ORE", "Material", 20),
};

void BuildChrome(Transform screen, string title, out RectTransform window)
{
    float ww = 292f * S;
    float wh = 156f * S;
    var windowGo = Node("Window", screen);
    window = Rt(windowGo);
    window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
    window.pivot = new Vector2(0.5f, 0.5f);
    window.sizeDelta = new Vector2(ww, wh);
    window.anchoredPosition = Vector2.zero;
    Sliced(windowGo, Sp("panel_9slice"), false, Slice);

    var titleGo = Node("Title", window);
    Place(Rt(titleGo), 94f * S, 4f * S, 104f * S, 24f * S, ww, wh);
    Sliced(titleGo, Sp("title_plate"), false);
    var titleLabel = Node("Label", titleGo.transform);
    Stretch(Rt(titleLabel));
    Label(titleLabel, title, 52f, TextAlignmentOptions.Center, cream);

    var closeGo = Node("Close", window);
    Place(Rt(closeGo), 260f * S, 4f * S, 24f * S, 24f * S, ww, wh);
    var closeImage = Sliced(closeGo, Sp("button_square_normal"), true);
    var closeButton = closeGo.AddComponent<Button>();
    closeButton.targetGraphic = closeImage;
    SpriteButton(closeButton, Sp("button_square_normal"), Sp("button_square_hover"), Sp("button_square_pressed"), Sp("button_square_disabled"));
    var closeIcon = Node("Icon", closeGo.transform);
    Place(Rt(closeIcon), 6f * S, 6f * S, 12f * S, 12f * S, 24f * S, 24f * S);
    Plain(closeIcon, Sp("close"), false);
}

void ContentPaper(Transform window, float x, float y, float w, float h, float parentW, float parentH)
{
    var paper = Node("Content", window);
    Place(Rt(paper), x * S, y * S, w * S, h * S, parentW, parentH);
    Sliced(paper, Sp("paper_9slice"), false, Slice);
    paper.transform.SetAsFirstSibling();
}

HearthwoodCatalogPanel BuildCatalog(Transform screen, string title, string footer, string action)
{
    BuildChrome(screen, title, out var window);
    float ww = 292f * S;
    float wh = 156f * S;
    ContentPaper(window, 10f, 34f, 181f, 116f, ww, wh);

    var panel = window.gameObject.AddComponent<HearthwoodCatalogPanel>();
    var frames = new Image[catalogItems.Length];
    var itemBuffer = new HearthwoodCatalogPanel.Item[catalogItems.Length];
    var selectedFrame = Sp("slot_hover");

    for (int i = 0; i < catalogItems.Length; i++)
    {
        int col = i % 6;
        int row = i / 6;
        float x = (18f + col * 28f) * S;
        float y = (42f + row * 29f) * S;
        var slotGo = Node("Slot" + i, window);
        Place(Rt(slotGo), x, y, 24f * S, 24f * S, ww, wh);
        var frame = Sliced(slotGo, i == 0 ? selectedFrame : Sp("slot_normal"), true);
        frames[i] = frame;
        var button = slotGo.AddComponent<Button>();
        button.targetGraphic = frame;
        button.transition = Selectable.Transition.None;
        var captured = i;
        UnityEventTools.AddIntPersistentListener(button.onClick, panel.Select, captured);

        var iconGo = Node("Icon", slotGo.transform);
        Place(Rt(iconGo), 4f * S, 4f * S, 16f * S, 16f * S, 24f * S, 24f * S);
        Plain(iconGo, Sp(catalogItems[i].icon), false);
        itemBuffer[i] = new HearthwoodCatalogPanel.Item
        {
            icon = Sp(catalogItems[i].icon),
            title = catalogItems[i].title,
            description = catalogItems[i].description,
            price = catalogItems[i].price
        };
    }

    var stockGo = Node("Footer", window);
    Place(Rt(stockGo), 12f * S, 132f * S, 150f * S, 16f * S, ww, wh);
    Label(stockGo, footer, 32f, TextAlignmentOptions.MidlineLeft, ink);

    float dx = 200f * S;
    float dy = 36f * S;
    float dw = 84f * S;
    float dh = 112f * S;
    var detailGo = Node("Detail", window);
    Place(Rt(detailGo), dx, dy, dw, dh, ww, wh);
    Sliced(detailGo, Sp("tooltip_9slice"), false, Frame);

    var detailIconGo = Node("Icon", detailGo.transform);
    Place(Rt(detailIconGo), 31f * S, 8f * S, 22f * S, 22f * S, dw, dh);
    var detailIcon = Plain(detailIconGo, Sp("carrot"), false);

    var nameGo = Node("Name", detailGo.transform);
    Place(Rt(nameGo), 4f * S, 34f * S, 76f * S, 16f * S, dw, dh);
    var detailName = Label(nameGo, "CARROT", 36f, TextAlignmentOptions.Center, ink);

    var descGo = Node("Description", detailGo.transform);
    Place(Rt(descGo), 4f * S, 50f * S, 76f * S, 14f * S, dw, dh);
    var detailDesc = Label(descGo, "Fresh crop", 28f, TextAlignmentOptions.Center, ink);

    var coinGo = Node("Coin", detailGo.transform);
    Place(Rt(coinGo), 18f * S, 66f * S, 14f * S, 14f * S, dw, dh);
    Plain(coinGo, Sp("coin"), false);
    var priceGo = Node("Price", detailGo.transform);
    Place(Rt(priceGo), 34f * S, 64f * S, 36f * S, 18f * S, dw, dh);
    var detailPrice = Label(priceGo, "12 G", 30f, TextAlignmentOptions.MidlineLeft, ink);

    var buyGo = Node("Action", detailGo.transform);
    Place(Rt(buyGo), 6f * S, 82f * S, 72f * S, 24f * S, dw, dh);
    var buyImage = Sliced(buyGo, Sp("button_wide_normal"), true);
    var buyButton = buyGo.AddComponent<Button>();
    buyButton.targetGraphic = buyImage;
    SpriteButton(buyButton, Sp("button_wide_normal"), Sp("button_wide_hover"), Sp("button_wide_pressed"), Sp("button_wide_disabled"));
    var buyLabel = Node("Label", buyGo.transform);
    Stretch(Rt(buyLabel));
    Label(buyLabel, action, 32f, TextAlignmentOptions.Center, cream);

    var so = new SerializedObject(panel);
    so.FindProperty("slotNormal").objectReferenceValue = Sp("slot_normal");
    so.FindProperty("slotSelected").objectReferenceValue = selectedFrame;
    so.FindProperty("detailIcon").objectReferenceValue = detailIcon;
    so.FindProperty("detailName").objectReferenceValue = detailName;
    so.FindProperty("detailDescription").objectReferenceValue = detailDesc;
    so.FindProperty("detailPrice").objectReferenceValue = detailPrice;
    so.FindProperty("selected").intValue = 0;
    var itemsProp = so.FindProperty("items");
    itemsProp.arraySize = itemBuffer.Length;
    for (int i = 0; i < itemBuffer.Length; i++)
    {
        var element = itemsProp.GetArrayElementAtIndex(i);
        element.FindPropertyRelative("icon").objectReferenceValue = itemBuffer[i].icon;
        element.FindPropertyRelative("title").stringValue = itemBuffer[i].title;
        element.FindPropertyRelative("description").stringValue = itemBuffer[i].description;
        element.FindPropertyRelative("price").intValue = itemBuffer[i].price;
    }
    var framesProp = so.FindProperty("slotFrames");
    framesProp.arraySize = frames.Length;
    for (int i = 0; i < frames.Length; i++)
        framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
    so.ApplyModifiedPropertiesWithoutUndo();
    return panel;
}

void BuildSettings(Transform screen)
{
    BuildChrome(screen, "SETTINGS", out var window);
    float ww = 292f * S;
    float wh = 156f * S;
    ContentPaper(window, 8f, 34f, 266f, 116f, ww, wh);

    void RowLabel(string text, float centerY)
    {
        var go = Node(text, window);
        Place(Rt(go), 18f * S, (centerY - 9f) * S, 140f * S, 18f * S, ww, wh);
        Label(go, text, 36f, TextAlignmentOptions.MidlineLeft, ink);
    }

    Slider MakeSlider(string name, float centerY, float value)
    {
        var go = Node(name, window);
        Place(Rt(go), 168f * S, (centerY - 8f) * S, 88f * S, 16f * S, ww, wh);
        var slider = go.AddComponent<Slider>();
        var track = Node("Track", go.transform);
        var trackRt = Rt(track);
        trackRt.anchorMin = new Vector2(0f, 0.5f);
        trackRt.anchorMax = new Vector2(1f, 0.5f);
        trackRt.pivot = new Vector2(0.5f, 0.5f);
        trackRt.anchoredPosition = Vector2.zero;
        trackRt.sizeDelta = new Vector2(0f, 12f * S);
        var trackImage = Sliced(track, Sp("slider_track"), false);
        var area = Node("HandleSlideArea", go.transform);
        Stretch(Rt(area));
        var areaRt = Rt(area);
        areaRt.offsetMin = new Vector2(6f * S, 0f);
        areaRt.offsetMax = new Vector2(-6f * S, 0f);
        var handle = Node("Handle", area.transform);
        var handleRt = Rt(handle);
        handleRt.anchorMin = new Vector2(0f, 0.5f);
        handleRt.anchorMax = new Vector2(0f, 0.5f);
        handleRt.pivot = new Vector2(0.5f, 0.5f);
        handleRt.sizeDelta = new Vector2(12f * S, 16f * S);
        var handleImage = Plain(handle, Sp("slider_thumb"), true);
        slider.targetGraphic = handleImage;
        slider.handleRect = handleRt;
        slider.fillRect = null;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = value;
        var colors = slider.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        slider.colors = colors;
        trackImage.raycastTarget = true;
        return slider;
    }

    RowLabel("Music volume", 58f);
    MakeSlider("Music", 58f, 0.72f);
    RowLabel("Sound effects", 84f);
    MakeSlider("Sound", 84f, 0.48f);
    RowLabel("Fullscreen", 110f);

    var toggleGo = Node("FullscreenToggle", window);
    Place(Rt(toggleGo), 248f * S, 102f * S, 16f * S, 16f * S, ww, wh);
    var toggle = toggleGo.AddComponent<Toggle>();
    var box = Node("Box", toggleGo.transform);
    Stretch(Rt(box));
    var boxImage = Plain(box, Sp("checkbox_off"), true);
    var mark = Node("Mark", toggleGo.transform);
    Stretch(Rt(mark));
    var markImage = Plain(mark, Sp("checkbox_on"), false);
    toggle.targetGraphic = boxImage;
    toggle.graphic = markImage;
    toggle.isOn = true;
    var toggleColors = toggle.colors;
    toggleColors.normalColor = Color.white;
    toggleColors.highlightedColor = Color.white;
    toggleColors.pressedColor = Color.white;
    toggleColors.selectedColor = Color.white;
    toggleColors.disabledColor = Color.white;
    toggle.colors = toggleColors;

    var applyGo = Node("Apply", window);
    Place(Rt(applyGo), 110f * S, 126f * S, 72f * S, 24f * S, ww, wh);
    var applyImage = Sliced(applyGo, Sp("button_wide_normal"), true);
    var applyButton = applyGo.AddComponent<Button>();
    applyButton.targetGraphic = applyImage;
    SpriteButton(applyButton, Sp("button_wide_normal"), Sp("button_wide_hover"), Sp("button_wide_pressed"), Sp("button_wide_disabled"));
    var applyLabel = Node("Label", applyGo.transform);
    Stretch(Rt(applyLabel));
    Label(applyLabel, "APPLY", 32f, TextAlignmentOptions.Center, cream);
}

BuildCatalog(shop.transform, "FARM SHOP", "21 IN STOCK", "BUY");
BuildSettings(settings.transform);
BuildCatalog(satchel.transform, "SATCHEL", "ITEMS", "USE");
settings.SetActive(false);
satchel.SetActive(false);

var screens = demo.AddComponent<HearthwoodDemoScreens>();
var screensSo = new SerializedObject(screens);
screensSo.FindProperty("index").intValue = 0;
var screensProp = screensSo.FindProperty("screens");
screensProp.arraySize = 3;
screensProp.GetArrayElementAtIndex(0).objectReferenceValue = shop;
screensProp.GetArrayElementAtIndex(1).objectReferenceValue = settings;
screensProp.GetArrayElementAtIndex(2).objectReferenceValue = satchel;
screensSo.ApplyModifiedPropertiesWithoutUndo();

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
return "built " + sprites.Count + " sprites";
    }
}
