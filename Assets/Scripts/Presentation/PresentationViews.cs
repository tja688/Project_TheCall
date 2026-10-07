using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class MonsterSlotView : MonoBehaviour
    {
        public Button button;
        public MonsterPortrait portrait;
        public TMP_Text title;
        public TMP_Text subtitle;
        public GameObject emptyMark;
        public GameObject selection;
        public int index;
        public string monsterId;
    }

    public sealed class SkillChipView : MonoBehaviour
    {
        public Button button;
        public TMP_Text label;
        public GameObject selection;
        public int index;
    }

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

    public sealed class TechNodeView : MonoBehaviour
    {
        public string techName;
        [TextArea] public string summary;
        public Button button;
        public TMP_Text title;
        public TMP_Text costLabel;
        public GameObject selection;
    }

    public sealed class ToolArtView : MonoBehaviour
    {
        public string toolName;
        public Sprite icon;
        [TextArea] public string description;
    }

    public sealed class OpeningScreenView : MonoBehaviour
    {
        public MonsterSlotView[] candidates;
    }

    public sealed class LevelStartScreenView : MonoBehaviour
    {
        public TMP_Text levelLabel;
        public TMP_Text dueLabel;
        public TMP_Text excessLabel;
        public Button beginButton;
    }

    public sealed class OperationScreenView : MonoBehaviour
    {
        public TMP_Text targetLabel;
        public TMP_Text energyLabel;
        public TMP_Text goldLabel;
        public TMP_Text nextDayLabel;
        public Button shopButton;
        public Button researchButton;
        public Button titleButton;
        public Button nextDayButton;
        public Button discardButton;
        public Button equipButton;
        public MonsterSlotView[] cageSlots;
        public MonsterSlotView[] extractionSlots;
        public MonsterSlotView[] breedingSlots;
        public SkillChipView[] breedingSkills;
        public SkillChipView[] skillChips;
    }

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

    public sealed class ResultScreenView : MonoBehaviour
    {
        public TMP_Text title;
        public TMP_Text body;
        public MonsterPortrait portrait;
        public Button restartButton;
    }
}
