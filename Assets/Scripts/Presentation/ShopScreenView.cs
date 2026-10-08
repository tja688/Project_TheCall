using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
            refreshPriceLabel = clone.GetComponentInChildren<TMP_Text>();
            var rect = clone.GetComponent<RectTransform>();
            var leaveRect = leaveButton.GetComponent<RectTransform>();
            rect.anchoredPosition = leaveRect.anchoredPosition + new Vector2(0f, leaveRect.sizeDelta.y + 12f);
        }
    }
}
