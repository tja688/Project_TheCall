using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class ShopCardView : MonoBehaviour
    {
        public Button buyButton;
        public TMP_Text buyLabel;
        public TMP_Text title;
        public TMP_Text body;
        public TMP_Text price;
        public TMP_Text stock;
        public Image icon;
        public MonsterPortrait portrait;
        public string monsterId;
        public string toolName;
    }
}
