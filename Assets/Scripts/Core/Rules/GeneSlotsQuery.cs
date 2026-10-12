using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public sealed class GeneSlotView
    {
        public GeneSlotView(int index, string skillName, bool isReserved, int? reservedBreedingSlot)
        {
            Index = index;
            SkillName = skillName;
            IsReserved = isReserved;
            ReservedBreedingSlot = reservedBreedingSlot;
        }

        public int Index { get; }

        public string SkillName { get; }

        public bool IsReserved { get; }

        public int? ReservedBreedingSlot { get; }
    }

    public sealed class GeneSlotsQuery : AbstractQuery<IReadOnlyList<GeneSlotView>>
    {
        protected override IReadOnlyList<GeneSlotView> OnDo()
        {
            var run = this.GetModel<RunModel>();
            var views = new GeneSlotView[run.GeneSlots.Count];
            for (var i = 0; i < views.Length; i++)
            {
                var slot = run.GeneSlots[i];
                views[i] = new GeneSlotView(i, slot.SkillName, slot.IsReserved, slot.ReservedBreedingSlot);
            }

            return views;
        }
    }
}
