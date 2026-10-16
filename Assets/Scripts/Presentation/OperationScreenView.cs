using TMPro;
using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TheCall
{
    public sealed class OperationScreenView : MonoBehaviour
    {
        public TMP_Text dayLabel;
        public TMP_Text targetLabel;
        public TMP_Text energyLabel;
        public TMP_Text excessLabel;
        public TMP_Text overtimeLabel;
        public TMP_Text currentNumber;
        public TMP_Text goldLabel;
        public TMP_Text nextDayLabel;
        public Button shopButton;
        public Button researchButton;
        public Button settingsButton;
        public Button titleButton;
        public Button nextDayButton;
        public Button discardButton;
        public Button equipButton;
        public Button addGeneSlotButton;
        public MonsterSlotView[] cageSlots;
        public MonsterSlotView[] extractionSlots;
        public MonsterSlotView[] breedingSlots;
        public SkillChipView[] breedingSkills;
        public SkillChipView[] skillChips;
        public RectTransform toolsBar;
        public ToolArtView[] ownedToolViews;
        public RectTransform dragLayer;
        public OperationPointer pointer;
        public LabSceneView lab;
        [Min(1)] public int cageColumns = 2;
        [Min(1)] public float cageRowSpacing = 118f;
        [Min(0)] public float cagePadding = 8f;

        public void Render(IArchitecture architecture, PresentationSpriteCatalog catalog)
        {
            var run = architecture.SendQuery(new RunLedgerQuery());
            var target = architecture.SendQuery(new LevelTargetQuery());
            var energy = architecture.SendQuery(new LevelEnergyQuery());
            var shortfall = architecture.SendQuery(new LevelShortfallQuery());
            PresentationReadout.Label(transform, "TargetCaption", "目标");
            PresentationReadout.Text(targetLabel, target.EnergyDue.ToString());
            PresentationReadout.Text(energyLabel, "累计 " + energy + " · 尚差 " + shortfall + " · 科技 " + run.TechPoints);
            PresentationReadout.Text(dayLabel, "第 " + target.LevelNumber + " 天");
            PresentationReadout.Text(excessLabel, target.ExcessEnergy + "+");
            PresentationReadout.Text(overtimeLabel, target.InOvertime ? "加班中" : "正常工时");
            PresentationReadout.Text(currentNumber != null ? currentNumber : transform.Find("CurrentNumber")?.GetComponent<TMP_Text>(), energy.ToString());
            PresentationReadout.Text(goldLabel, run.Gold.ToString());
            PresentationReadout.Text(nextDayLabel, target.InOvertime ? "开始加班结算" : "开始结算");

            var armed = pointer != null ? pointer.Armed : null;
            var selectedId = armed.HasValue && armed.Value.Kind == PayloadKind.Monster ? armed.Value.MonsterId : null;
            cageSlots = MonsterInventory.Render(cageSlots, architecture.SendQuery(new MonsterCageQuery()), selectedId,
                catalog, "CageCard_", cageColumns, cageRowSpacing, cagePadding);
            if (lab != null && lab.cageScroll is LabCageLift lift)
                lift.Render(architecture.SendQuery(new MonsterCageQuery()).Count);
            if (extractionSlots != null)
            {
                var cells = architecture.SendQuery(new ExtractionSlotsQuery());
                for (var i = 0; i < extractionSlots.Length; i++)
                {
                    var slot = extractionSlots[i];
                    if (slot == null) continue;
                    slot.gameObject.SetActive(i < cells.Count);
                    if (i >= cells.Count) continue;
                    slot.index = cells[i].Index;
                    var id = cells[i].MonsterId;
                    PresentationReadout.Monster(slot, string.IsNullOrEmpty(id) ? null : architecture.SendQuery(new MonsterQuery(id)), id != null && id == selectedId, catalog);
                }
            }
            if (breedingSlots != null)
            {
                var parents = architecture.SendQuery(new BreedingSlotsQuery());
                for (var i = 0; i < breedingSlots.Length; i++)
                {
                    var slot = breedingSlots[i];
                    if (slot == null) continue;
                    slot.gameObject.SetActive(i < parents.Count);
                    if (i >= parents.Count) continue;
                    slot.index = parents[i].Index;
                    var id = parents[i].MonsterId;
                    PresentationReadout.Monster(slot, string.IsNullOrEmpty(id) ? null : architecture.SendQuery(new MonsterQuery(id)), id != null && id == selectedId, catalog);
                }
            }
            if (skillChips != null)
            {
                var genes = architecture.SendQuery(new GeneSlotsQuery());
                for (var i = 0; i < skillChips.Length; i++)
                {
                    var chip = skillChips[i];
                    if (chip == null) continue;
                    chip.gameObject.SetActive(i < run.GeneCapacity);
                    chip.RenderGene(i, i < run.GeneCapacity && i < genes.Count ? genes[i] : null,
                        armed.HasValue && armed.Value.Kind == PayloadKind.SkillChip && armed.Value.Index == i);
                }
            }
            if (addGeneSlotButton != null) addGeneSlotButton.gameObject.SetActive(run.GeneCapacity < 4);
            if (breedingSkills != null)
            {
                var plans = architecture.SendQuery(new BreedingPlansQuery());
                for (var i = 0; i < breedingSkills.Length; i++)
                {
                    var chip = breedingSkills[i];
                    if (chip == null) continue;
                    chip.gameObject.SetActive(i < plans.Count);
                    if (i < plans.Count) chip.RenderBreeding(plans[i], armed.HasValue && armed.Value.Kind == PayloadKind.BreedingSkill && armed.Value.Index == plans[i].Index);
                }
            }
            if (ownedToolViews != null)
                for (var i = 0; i < ownedToolViews.Length; i++)
                {
                    var view = ownedToolViews[i];
                    if (view == null) continue;
                    var owned = i < run.Tools.Count;
                    view.gameObject.SetActive(owned);
                    if (!owned) continue;
                    view.toolName = run.Tools[i];
                    var definition = catalog?.FindTool(view.toolName);
                    view.icon = definition?.icon;
                    view.description = definition?.description ?? "";
                    if (view.iconImage != null)
                    {
                        view.iconImage.sprite = view.icon;
                        view.iconImage.preserveAspect = true;
                    }
                    PresentationReadout.Text(view.nameLabel, view.toolName);
                }
            if (lab != null) lab.Render(architecture.SendQuery(new BreedingPlansQuery()), energy, target.EnergyDue,
                architecture.SendQuery(new MonsterCageQuery()).Count, run.GeneCapacity);
        }
    }
}
