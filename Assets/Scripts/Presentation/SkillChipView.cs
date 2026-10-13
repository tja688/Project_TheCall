using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class SkillChipView : MonoBehaviour
    {
        public Button button;
        public TMP_Text label;
        public TMP_Text reservedLabel;
        public GameObject reservedMark;
        public GameObject selection;
        public Image icon;
        public int index;
        public string SkillName { get; private set; }
        public bool HasPayload => !string.IsNullOrEmpty(SkillName);

        public void RenderGene(int slotIndex, GeneSlotView gene, bool selected)
        {
            index = slotIndex;
            SkillName = gene?.SkillName;
            PresentationReadout.Text(label, HasPayload ? SkillName : "空");
            PresentationReadout.Text(reservedLabel, gene != null && gene.IsReserved
                ? (gene.ReservedBreedingSlot.HasValue ? $"[已预约 培育{gene.ReservedBreedingSlot.Value + 1}]" : "[已预约]") : "");
            if (reservedMark != null) reservedMark.SetActive(gene != null && gene.IsReserved);
            if (button != null) button.interactable = HasPayload;
            if (selection != null) selection.SetActive(HasPayload && selected);
        }

        public void RenderBreeding(BreedingPlanView plan, bool selected)
        {
            index = plan.Index;
            SkillName = plan.SkillName;
            PresentationReadout.Text(label, HasPayload ? SkillName : "投入技能");
            if (selection != null) selection.SetActive(HasPayload && selected);
        }
    }
}
