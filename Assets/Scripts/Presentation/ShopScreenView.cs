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
        public GameObject buyPage;
        public GameObject sellPage;
        public ShopCardView[] cards;
        public MonsterSlotView[] sellSlots;
        public ToolArtView[] toolArt;
    }
}
