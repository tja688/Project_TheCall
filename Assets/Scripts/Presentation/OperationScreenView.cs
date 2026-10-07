using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class OperationScreenView : MonoBehaviour
    {
        public TMP_Text targetLabel;
        public TMP_Text energyLabel;
        public TMP_Text currentNumber;
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
        public RectTransform dragLayer;
        public OperationPointer pointer;
    }
}
