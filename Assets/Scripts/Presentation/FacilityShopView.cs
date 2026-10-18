using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class FacilityShopView : MonoBehaviour
    {
        public ShopScreenView shop;
        public TMP_Text screenText;
        public TMP_Text stockText;
        public TMP_Text categoryText;
        public RectTransform lever;
        public RectTransform[] signalBars;
        public Image monsterIndicator;
        public Image toolIndicator;
        public Sprite doctorSprite;
        public Image doctorPortrait;
        public bool ToolsSelected { get; private set; }
        public int RefreshAnimations { get; private set; }

        // ShopShell.png trays, top-left of the 1920×1080 stage. Cards sit inside the paint, not across the divider.
        const float TrayX = 504f;
        const float TrayW = 755f;
        const float Inset = 14f;
        const float Gap = 14f;
        const float CardY0 = 186f;
        const float CardY1 = 442f;
        const float CardH = 188f;
        const float Pitch = CardY1 - CardY0;
        const float ShelfViewH = Pitch + CardH;

        ShopCardView[] _stock = System.Array.Empty<ShopCardView>();
        bool _hasRendered;
        bool _wasSelling;
        float _refreshTime = 10f;
        float _hop = 1f;
        bool _hopping;
        Vector2 _portraitHome;
        int _talk;
        int _gold;
        int _monsters;
        int _tools;
        int _refreshPrice;
        bool _selling;
        TMP_Text _doctorName;

        public void ShowMonsters() => ChooseCategory(false);
        public void ShowTools() => ChooseCategory(true);
        void ChooseCategory(bool tools)
        {
            ToolsSelected = tools;
            if (_selling) shop.buyTabButton.onClick.Invoke();
            Arrange();
        }
        public void Talk()
        {
            _talk++;
            _hop = 0f;
            _hopping = true;
            PaintTransmission();
        }

        public void Render(bool selling, Shelf shelf, RunLedger run)
        {
            _selling = selling;
            _gold = run.Gold;
            _monsters = shelf.Monsters.Count;
            _tools = shelf.Tools.Count;
            _refreshPrice = shelf.NextRefreshPrice;
            _stock = shop.cards.Where(c => c != null && c.gameObject.activeSelf).ToArray();
            // Only a paid refresh increases this quote; purchases leave it unchanged.
            if (_hasRendered && _previousRefreshPrice > 0 && shelf.NextRefreshPrice > _previousRefreshPrice)
            {
                _refreshTime = 0f;
                RefreshAnimations++;
                _talk = 0;
                _hop = 0f;
                _hopping = true;
            }
            _previousRefreshPrice = shelf.NextRefreshPrice;
            _hasRendered = true;
            Arrange();
            PaintTransmission();
        }
        int _previousRefreshPrice;

        void Arrange()
        {
            var stage = Stage();
            if (stage == null) return;
            ReleaseLayout(shop.buyPage);
            ReleaseLayout(shop.sellPage);
            LayoutTitle(stage);
            LayoutDoctor(stage);
            LayoutOffers(stage);
            LayoutSales(stage);
            if (categoryText != null)
            {
                categoryText.text = _selling ? "回购窗口 / 选择怪物出售" : ToolsSelected ? "工具货架" : "怪物货架";
                categoryText.alignment = TextAlignmentOptions.Center;
            }
            var stocked = CountVisibleOffers();
            if (stockText != null) stockText.text = _selling ? "出售会移除怪物，亲本锁定期间无法出售。" : "本轮库存 " + stocked + "  /  购买后即时入库";
            if (monsterIndicator != null) monsterIndicator.color = ToolsSelected ? new Color32(69, 106, 95, 255) : new Color32(164, 248, 216, 255);
            if (toolIndicator != null) toolIndicator.color = ToolsSelected ? new Color32(164, 248, 216, 255) : new Color32(69, 106, 95, 255);
            _wasSelling = _selling;
        }

        int CountVisibleOffers()
        {
            var count = 0;
            foreach (var card in _stock)
                if (card != null && card.gameObject.activeSelf && ToolsSelected == !string.IsNullOrEmpty(card.toolName)) count++;
            return count;
        }

        void LayoutTitle(RectTransform stage)
        {
            if (categoryText == null) return;
            PlaceOn(stage, categoryText.rectTransform, TrayX, 148f, TrayW, 32f);
            categoryText.fontSize = 22f;
            categoryText.alignment = TextAlignmentOptions.Center;
            categoryText.overflowMode = TextOverflowModes.Ellipsis;
        }

        void LayoutOffers(RectTransform stage)
        {
            var visible = new List<ShopCardView>();
            foreach (var card in _stock)
            {
                if (card == null) continue;
                var show = ToolsSelected == !string.IsNullOrEmpty(card.toolName);
                card.gameObject.SetActive(show);
                if (show) visible.Add(card);
            }
            if (visible.Count == 0 || shop.buyPage == null) return;
            var columns = visible.Count > 4 ? 3 : 2;
            var width = CardWidth(columns);
            var placed = 0;
            var row = 0;
            while (placed < visible.Count && row < 2)
            {
                var count = Mathf.Min(columns, visible.Count - placed);
                var rowWidth = count * width + (count - 1) * Gap;
                var x = TrayX + (TrayW - rowWidth) * 0.5f;
                var y = row == 0 ? CardY0 : CardY1;
                for (var i = 0; i < count; i++)
                {
                    var card = visible[placed + i];
                    PlaceOn(stage, (RectTransform)card.transform, x + i * (width + Gap), y, width, CardH);
                    DressOffer(card, width, CardH);
                }
                placed += count;
                row++;
            }
        }

        void LayoutSales(RectTransform stage)
        {
            if (shop.sellSlots == null || shop.sellSlots.Length == 0 || shop.sellSlots[0] == null) return;
            var scroll = shop.sellSlots[0].GetComponentInParent<ScrollRect>(true);
            if (scroll == null) return;
            ReleaseLayout(scroll.content != null ? scroll.content.gameObject : null);
            PlaceOn(stage, (RectTransform)scroll.transform, TrayX, CardY0, TrayW, ShelfViewH);
            var viewport = scroll.viewport;
            if (viewport != null)
            {
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            }
            var shown = new List<MonsterSlotView>();
            foreach (var slot in shop.sellSlots)
                if (slot != null && slot.gameObject.activeSelf) shown.Add(slot);
            var width = CardWidth(2);
            var rows = Mathf.Max(1, Mathf.CeilToInt(shown.Count / 2f));
            var content = scroll.content;
            if (content != null)
            {
                content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
                var contentH = shown.Count == 0 ? CardH : (rows - 1) * Pitch + CardH;
                content.sizeDelta = new Vector2(TrayW, contentH);
                var y = _selling && !_wasSelling ? 0f : content.anchoredPosition.y;
                content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, Mathf.Max(0f, contentH - ShelfViewH)));
                scroll.vertical = contentH > ShelfViewH + 1f;
            }
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            for (var i = 0; i < shown.Count; i++)
            {
                var rect = (RectTransform)shown[i].transform;
                LabSceneView.Place(rect, Inset + (i % 2) * (width + Gap), (i / 2) * Pitch, width, CardH);
                DressSale(shown[i], width, CardH);
            }
        }

        static float CardWidth(int columns)
        {
            var gaps = Mathf.Max(0, columns - 1);
            return (TrayW - Inset * 2f - Gap * gaps) / columns;
        }

        static void DressOffer(ShopCardView card, float w, float h)
        {
            var plate = card.GetComponent<Image>();
            if (plate != null) { plate.sprite = null; plate.color = new Color32(16, 40, 36, 236); plate.raycastTarget = false; }
            var artW = Mathf.Clamp(w * 0.34f, 76f, 124f);
            var artH = h - 52f;
            if (card.portrait != null)
            {
                card.portrait.gameObject.SetActive(string.IsNullOrEmpty(card.toolName));
                LabSceneView.Place((RectTransform)card.portrait.transform, 8f, 8f, artW, artH);
            }
            if (card.icon != null)
            {
                LabSceneView.Place((RectTransform)card.icon.transform, 8f, 8f, artW, artH);
                card.icon.preserveAspect = true;
                card.icon.raycastTarget = false;
            }
            var textX = 16f + artW;
            var textW = w - textX - 8f;
            FitText(card.title, textX, 8f, textW, 30f, 18f, new Color32(232, 255, 246, 255), TextAlignmentOptions.TopLeft);
            FitText(card.body, textX, 38f, textW, h - 90f, 14f, new Color32(186, 214, 204, 255), TextAlignmentOptions.TopLeft);
            FitText(card.price, 8f, h - 40f, artW, 30f, 16f, new Color32(255, 214, 140, 255), TextAlignmentOptions.Center);
            if (card.buyButton != null)
            {
                LabSceneView.Place((RectTransform)card.buyButton.transform, textX, h - 42f, textW, 32f);
                if (card.buyLabel != null)
                {
                    LabSceneView.Place(card.buyLabel.rectTransform, 2f, 1f, textW - 4f, 30f);
                    card.buyLabel.fontSize = 16f;
                    card.buyLabel.alignment = TextAlignmentOptions.Center;
                }
            }
        }

        static void DressSale(MonsterSlotView slot, float w, float h)
        {
            var plate = slot.GetComponent<Image>();
            if (plate != null) { plate.sprite = null; plate.color = new Color32(16, 40, 36, 236); plate.raycastTarget = true; }
            var artW = Mathf.Clamp(w * 0.34f, 76f, 124f);
            var artH = h - 16f;
            if (slot.portrait != null) LabSceneView.Place((RectTransform)slot.portrait.transform, 8f, 8f, artW, artH);
            var textX = 16f + artW;
            var textW = w - textX - 8f;
            FitText(slot.title, textX, 10f, textW, 32f, 17f, new Color32(232, 255, 246, 255), TextAlignmentOptions.TopLeft);
            FitText(slot.subtitle, textX, 44f, textW, h - 92f, 14f, new Color32(186, 214, 204, 255), TextAlignmentOptions.TopLeft);
            var bar = slot.transform.Find("ActionBar") as RectTransform;
            if (bar != null)
            {
                LabSceneView.Place(bar, textX, h - 40f, textW, 30f);
                var caption = bar.GetComponentInChildren<TMP_Text>(true);
                if (caption != null)
                {
                    LabSceneView.Place(caption.rectTransform, 0f, 0f, textW, 30f);
                    caption.fontSize = 15f;
                    caption.alignment = TextAlignmentOptions.Center;
                }
            }
            if (slot.selection != null) LabSceneView.Place((RectTransform)slot.selection.transform, 3f, 3f, w - 6f, h - 6f);
        }

        static void FitText(TMP_Text text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions alignment)
        {
            if (text == null) return;
            LabSceneView.Place(text.rectTransform, x, y, w, h);
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
        }

        public void LayoutDoctor(RectTransform stage)
        {
            if (screenText == null) return;
            var plate = screenText.rectTransform.parent as RectTransform;
            if (plate == null || stage == null) return;
            const float plateX = 1344f;
            const float plateY = 508f;
            const float plateW = 410f;
            const float plateH = 68f;
            PlaceOn(stage, plate, plateX, plateY, plateW, plateH);
            var plateImage = plate.GetComponent<Image>();
            if (plateImage != null)
            {
                plateImage.sprite = null;
                plateImage.color = new Color32(8, 28, 24, 232);
                plateImage.raycastTarget = false;
            }
            for (var i = 0; i < plate.childCount; i++)
            {
                var child = plate.GetChild(i);
                if (child.name.StartsWith("Signal")) child.gameObject.SetActive(false);
            }
            LabSceneView.Place(screenText.rectTransform, 96f, 8f, plateW - 108f, plateH - 16f);
            screenText.fontSize = 17f;
            screenText.color = new Color32(236, 255, 246, 255);
            screenText.alignment = TextAlignmentOptions.MidlineLeft;
            screenText.richText = true;
            screenText.textWrappingMode = TextWrappingModes.Normal;
            screenText.overflowMode = TextOverflowModes.Ellipsis;
            screenText.raycastTarget = false;

            var badge = plate.Find("DoctorBadge") as RectTransform;
            if (badge == null)
            {
                var node = new GameObject("DoctorBadge", typeof(RectTransform), typeof(Image));
                badge = node.GetComponent<RectTransform>();
                badge.SetParent(plate, false);
                var ink = badge.GetComponent<Image>();
                ink.color = new Color32(164, 248, 216, 255);
                ink.raycastTarget = false;
                var labelNode = new GameObject("Label", typeof(RectTransform));
                labelNode.transform.SetParent(badge, false);
                _doctorName = labelNode.AddComponent<TextMeshProUGUI>();
            }
            else _doctorName = badge.GetComponentInChildren<TMP_Text>(true);
            LabSceneView.Place(badge, 12f, 18f, 76f, 32f);
            if (_doctorName != null)
            {
                _doctorName.font = screenText.font;
                _doctorName.text = "博士";
                _doctorName.fontSize = 16f;
                _doctorName.fontStyle = FontStyles.Bold;
                _doctorName.color = new Color32(18, 48, 40, 255);
                _doctorName.alignment = TextAlignmentOptions.Center;
                _doctorName.raycastTarget = false;
                LabSceneView.Place(_doctorName.rectTransform, 0f, 0f, 76f, 32f);
            }
            var tail = plate.Find("DoctorTail");
            if (tail != null) tail.gameObject.SetActive(false);

            if (doctorSprite == null) return;
            if (doctorPortrait == null)
            {
                var found = stage.Find("DoctorPortrait");
                if (found != null) doctorPortrait = found.GetComponent<Image>();
            }
            if (doctorPortrait == null)
            {
                var node = new GameObject("DoctorPortrait", typeof(RectTransform), typeof(Image));
                doctorPortrait = node.GetComponent<Image>();
                doctorPortrait.transform.SetParent(stage, false);
            }
            doctorPortrait.sprite = doctorSprite;
            doctorPortrait.color = Color.white;
            doctorPortrait.preserveAspect = true;
            doctorPortrait.raycastTarget = false;
            var aspect = doctorSprite.rect.height > 0f ? doctorSprite.rect.width / doctorSprite.rect.height : 1.4f;
            var height = 274f;
            var width = height * aspect;
            const float wellW = 418f;
            if (width > wellW) { width = wellW; height = width / aspect; }
            var x = 1336f + (426f - width) * 0.5f;
            var portrait = doctorPortrait.rectTransform;
            portrait.anchorMin = portrait.anchorMax = new Vector2(0f, 1f);
            portrait.pivot = new Vector2(0.5f, 0.08f);
            portrait.sizeDelta = new Vector2(width, height);
            portrait.anchoredPosition = new Vector2(x + width * 0.5f, -(230f + height * 0.92f));
            portrait.localScale = Vector3.one;
            portrait.localRotation = Quaternion.identity;
            _portraitHome = portrait.anchoredPosition;
            portrait.SetSiblingIndex(plate.GetSiblingIndex());
            plate.SetSiblingIndex(portrait.GetSiblingIndex() + 1);
        }

        void PaintTransmission()
        {
            if (screenText == null) return;
            var pool = Lines();
            screenText.text = pool[_talk % pool.Count];
        }

        List<string> Lines()
        {
            var pool = new List<string>();
            if (_refreshTime < 4f)
                pool.Add("货架翻过了。\n上一批没买的，就当没见过。");
            if (_selling)
            {
                pool.Add("回购开着。锁成亲本的，我不收。\n孩子还没落地呢。");
                pool.Add("卖掉再买，买了再卖。\n你这是在循环利用人生。");
                pool.Add("窗口只收能走的。\n培育槽里那几位，先请出来。");
            }
            else if (ToolsSelected)
            {
                pool.Add("工具每样只卖一件。\n打卡机不讲价，财务比我还硬。");
                pool.Add("批发合同能压后面的价。\n它自己不打折。账上是 " + _gold + "。");
                pool.Add("验货单能多翻一件。\n是福是祸，开箱才算数。");
            }
            else
            {
                pool.Add("到了 " + _monsters + " 只怪物、" + _tools + " 件工具。\n先看性格，再看价钱。");
                pool.Add("便宜的不一定肯上班。\n贵的那只，眼神倒是很想干活。");
                pool.Add("基因还热乎。\n光盯标价，它会在宿舍跟你赌气。");
            }
            if (_gold < _refreshPrice)
                pool.Add("金币只剩 " + _gold + "。\n杆先别拉，刷新要 " + _refreshPrice + "。");
            pool.Add("连续开工会疲倦。\n回宿舍看看，别把人用到摆脸色。");
            pool.Add("下一天之前，把亲本再对一眼。\n乱炖也是一种品味。");
            pool.Add("我是博士。公司算我的。\n这些怪物，暂时算你的。");
            return pool;
        }

        RectTransform Stage()
        {
            if (shop == null) return null;
            var stage = shop.transform.Find("FacilityShopStage") as RectTransform;
            return stage != null ? stage : shop.transform as RectTransform;
        }

        static void PlaceOn(RectTransform stage, RectTransform rect, float x, float y, float w, float h)
        {
            var origin = DownOffset(stage, (RectTransform)rect.parent);
            LabSceneView.Place(rect, x - origin.x, y - origin.y, w, h);
        }

        // Parents are top-left anchored, so anchored Y is already "up".
        static Vector2 DownOffset(RectTransform stage, RectTransform rect)
        {
            float x = 0f, y = 0f;
            var cursor = rect;
            while (cursor != null && cursor != stage)
            {
                x += cursor.anchoredPosition.x;
                y += -cursor.anchoredPosition.y;
                cursor = cursor.parent as RectTransform;
            }
            return new Vector2(x, y);
        }

        static void ReleaseLayout(GameObject node)
        {
            if (node == null) return;
            foreach (var layout in node.GetComponents<LayoutGroup>()) layout.enabled = false;
            var fitter = node.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;
        }

        void Update()
        {
            _refreshTime += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_refreshTime / 0.65f);
            if (lever != null) lever.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * Mathf.PI) * -42f);
            if (_hopping)
            {
                _hop += Time.unscaledDeltaTime * 2.1f;
                if (_hop >= 1f) { _hop = 1f; _hopping = false; }
            }
            if (doctorPortrait != null)
            {
                var pulse = Mathf.Sin(Mathf.Clamp01(_hop) * Mathf.PI);
                var lift = Mathf.Sin(Time.unscaledTime * 1.55f) * 4.5f + pulse * 14f;
                var body = doctorPortrait.rectTransform;
                body.anchoredPosition = _portraitHome + new Vector2(0f, lift);
                body.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(Time.unscaledTime * 0.8f) * 1.25f);
                var squash = 1f + pulse * 0.045f;
                body.localScale = new Vector3(2f - squash, squash, 1f);
            }
        }

        void OnDisable()
        {
            _refreshTime = 10f;
            _hopping = false;
            _hop = 1f;
            if (lever != null) lever.localRotation = Quaternion.identity;
            if (doctorPortrait != null)
            {
                doctorPortrait.rectTransform.localRotation = Quaternion.identity;
                doctorPortrait.rectTransform.localScale = Vector3.one;
            }
        }
    }
}
