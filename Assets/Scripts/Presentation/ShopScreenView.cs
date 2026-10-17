using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;
using QFramework;

namespace TheCall
{
    public sealed class ShopScreenView : MonoBehaviour
    {
        public TMP_Text balanceLabel;
        public TMP_Text portraitBalanceLabel;
        public Button buyTabButton;
        public Button sellTabButton;
        public Button shopButton;
        public Button researchButton;
        public Button saveButton;
        public Button titleButton;
        public Button leaveButton;
        public Button refreshButton;
        public TMP_Text refreshPriceLabel;
        public GameObject buyPage;
        public GameObject sellPage;
        public ShopCardView[] cards;
        public MonsterSlotView[] sellSlots;
        public ToolArtView[] toolArt;
        public FacilityShopView facility;
        [Min(1)] public int sellColumns = 2;
        [Min(1)] public float sellRowSpacing = 156f;
        [Min(0)] public float sellPadding = 8f;

        public void Render(IArchitecture architecture, PresentationSpriteCatalog catalog, bool selling, Action<MonsterSlotView> bindNewSlot)
        {
            var run = architecture.SendQuery(new RunLedgerQuery());
            var shelf = architecture.SendQuery(new ShelfQuery());
            PresentationReadout.Text(balanceLabel, run.Gold.ToString());
            PresentationReadout.Text(portraitBalanceLabel, run.Gold.ToString());
            PresentationReadout.Text(refreshPriceLabel, "刷新 " + shelf.NextRefreshPrice);
            if (buyPage != null) buyPage.SetActive(!selling);
            if (sellPage != null) sellPage.SetActive(selling);
            PresentationReadout.Text(leaveButton != null ? leaveButton.GetComponentInChildren<TMP_Text>() : null,
                architecture.SendQuery(new LevelTargetQuery()).LevelNumber >= 7 ? "结束本局" : "下一天");
            if (cards != null)
            {
                var index = 0;
                foreach (var monster in shelf.Monsters)
                {
                    if (index >= cards.Length) break;
                    RenderMonster(cards[index++], monster, run.Gold);
                }
                foreach (var tool in shelf.Tools)
                {
                    if (index >= cards.Length) break;
                    RenderTool(cards[index++], tool, run.Gold, catalog);
                }
                for (; index < cards.Length; index++)
                    if (cards[index] != null) cards[index].gameObject.SetActive(false);
            }
            sellSlots = MonsterInventory.Render(sellSlots, architecture.SendQuery(new MonsterCageQuery()), null,
                catalog, "SellCard_", sellColumns, sellRowSpacing, sellPadding, bindNewSlot);
            if (facility != null) facility.Render(selling, shelf, run);
        }

        static void RenderMonster(ShopCardView card, ShelfMonster item, int gold)
        {
            if (card == null) return;
            card.gameObject.SetActive(true);
            card.monsterId = item.Id;
            card.toolName = null;
            if (card.portrait != null)
            {
                card.portrait.gameObject.SetActive(true);
                card.portrait.Show(item.Appearance);
            }
            if (card.icon != null) card.icon.enabled = false;
            PresentationReadout.Text(card.title, item.DisplayName);
            PresentationReadout.Text(card.body, PresentationReadout.Join(item.SkillNames));
            RenderPrice(card, item.Price, gold);
        }

        void RenderTool(ShopCardView card, ShelfTool item, int gold, PresentationSpriteCatalog catalog)
        {
            if (card == null) return;
            card.gameObject.SetActive(true);
            card.monsterId = null;
            card.toolName = item.Name;
            if (card.portrait != null) card.portrait.gameObject.SetActive(false);
            var art = catalog?.FindTool(item.Name);
            var fallback = art == null ? FindArt(item.Name) : null;
            if (card.icon != null)
            {
                card.icon.sprite = art?.icon != null ? art.icon : FindArt(item.Name)?.icon;
                card.icon.enabled = true;
                card.icon.type = UnityEngine.UI.Image.Type.Simple;
                card.icon.preserveAspect = true;
                card.icon.color = Color.white;
            }
            PresentationReadout.Text(card.title, item.Name);
            PresentationReadout.Text(card.body, art != null ? art.description : fallback?.description ?? "");
            RenderPrice(card, item.Price, gold);
        }

        static void RenderPrice(ShopCardView card, int price, int gold)
        {
            PresentationReadout.Text(card.price, price + " 金币");
            PresentationReadout.Text(card.stock, "本轮剩余 1 件");
            var affordable = gold >= price;
            PresentationReadout.Text(card.buyLabel, affordable ? "购买" : "金币不足");
            if (card.buyButton != null) card.buyButton.interactable = affordable;
        }

        ToolArtView FindArt(string toolName)
        {
            if (toolArt != null)
                foreach (var art in toolArt)
                    if (art != null && art.toolName == toolName) return art;
            return null;
        }

        public void EnsureRefreshControl()
        {
            if (refreshButton != null)
                return;
            if (leaveButton == null)
                return;

            var clone = Instantiate(leaveButton.gameObject, leaveButton.transform.parent);
            clone.name = "RefreshShelf";
            refreshButton = clone.GetComponent<UnityEngine.UI.Button>();
            refreshButton.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            clone.GetComponent<PresentationActionButton>()?.Configure(null, PresentationAction.RefreshShelf);
            refreshPriceLabel = clone.GetComponentInChildren<TMP_Text>();
            var rect = clone.GetComponent<RectTransform>();
            var leaveRect = leaveButton.GetComponent<RectTransform>();
            rect.anchoredPosition = leaveRect.anchoredPosition + new Vector2(0f, leaveRect.sizeDelta.y + 12f);
        }
    }
}
