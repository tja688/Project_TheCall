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
        public bool ToolsSelected { get; private set; }
        public int RefreshAnimations { get; private set; }
        ShopCardView[] _stock = System.Array.Empty<ShopCardView>();
        bool _hasRendered;
        float _refreshTime = 2;
        int _talk;
        int _gold;
        int _monsters;
        int _tools;
        int _refreshPrice;
        bool _selling;

        public void ShowMonsters() => ChooseCategory(false);
        public void ShowTools() => ChooseCategory(true);
        void ChooseCategory(bool tools)
        {
            ToolsSelected = tools;
            if (_selling) shop.buyTabButton.onClick.Invoke();
            Arrange();
        }
        public void Talk() { _talk++; PaintTransmission(); }

        public void Render(bool selling, Shelf shelf, RunLedger run)
        {
            _selling = selling;
            _gold = run.Gold;
            _monsters = shelf.Monsters.Count;
            _tools = shelf.Tools.Count;
            _refreshPrice = shelf.NextRefreshPrice;
            _stock = shop.cards.Where(c => c != null && c.gameObject.activeSelf).ToArray();
            // Only a paid refresh increases this quote; purchases leave it unchanged.
            if (_hasRendered && shelf.NextRefreshPrice > _previousRefreshPrice)
            { _refreshTime = 0; RefreshAnimations++; }
            _previousRefreshPrice = shelf.NextRefreshPrice;
            _hasRendered = true;
            Arrange();
            PaintTransmission();
        }
        int _previousRefreshPrice;

        void Arrange()
        {
            var index = 0;
            foreach (var card in _stock)
            {
                var visible = ToolsSelected == !string.IsNullOrEmpty(card.toolName);
                card.gameObject.SetActive(visible);
                if (!visible) continue;
                LabSceneView.Place((RectTransform)card.transform, 20 + index % 2 * 382, 16 + index / 2 * 260, 360, 244);
                index++;
            }
            if (categoryText != null) categoryText.text = _selling ? "回购窗口 / 选择怪物出售" : ToolsSelected ? "工具货架" : "怪物货架";
            if (stockText != null) stockText.text = _selling ? "出售会移除怪物，亲本锁定期间无法出售。" : "本轮库存 " + index + "  /  购买后即时入库";
            monsterIndicator.color = ToolsSelected ? new Color32(69, 106, 95, 255) : new Color32(164, 248, 216, 255);
            toolIndicator.color = ToolsSelected ? new Color32(164, 248, 216, 255) : new Color32(69, 106, 95, 255);
        }

        void PaintTransmission()
        {
            var lines = new[] {
                "信道已连接\n\n今天有 " + _monsters + " 只怪物、" + _tools + " 件工具。\n\n先看看基因与性格，再决定谁加入公司。",
                "账户核对\n\n可用金币 " + _gold + "。\n刷新报价 " + _refreshPrice + "。\n\n拉下左侧摇杆，可以换一批货。",
                "值班提示\n\n连续工作的怪物会疲倦。\n\n回到宿舍查看全员状态，安排下一天的工作与休息。"
            };
            screenText.text = "博士通讯 / LIVE\n________________________\n\n" + lines[_talk % lines.Length];
        }

        void Update()
        {
            _refreshTime += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_refreshTime / 0.65f);
            lever.localEulerAngles = new Vector3(0, 0, Mathf.Sin(t * Mathf.PI) * -42);
            for (var i = 0; i < signalBars.Length; i++)
                signalBars[i].localScale = new Vector3(1, 0.25f + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.8f + i * 0.8f)) * 0.75f, 1);
        }

        void OnDisable() { _refreshTime = 2; if (lever != null) lever.localRotation = Quaternion.identity; }
    }
}
