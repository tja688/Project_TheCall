using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class ResearchScreenView : MonoBehaviour
    {
        public TMP_Text techPointLabel;
        public TMP_Text goldLabel;
        public TMP_Text detailTitle;
        public TMP_Text detailBody;
        public TMP_Text detailCost;
        public Button researchButton;
        public TMP_Text researchLabel;
        public Button backButton;
        public TechNodeView[] nodes;
    }
}
