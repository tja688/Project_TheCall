using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public sealed class BreedingPlanView
    {
        public BreedingPlanView(int index, IReadOnlyList<string> parentIds, string skillName)
        {
            Index = index;
            ParentIds = parentIds;
            SkillName = skillName;
        }

        public int Index { get; }

        public IReadOnlyList<string> ParentIds { get; }

        public string SkillName { get; }
    }

    public sealed class BreedingPlansQuery : AbstractQuery<IReadOnlyList<BreedingPlanView>>
    {
        protected override IReadOnlyList<BreedingPlanView> OnDo()
        {
            var level = this.GetModel<LevelModel>();
            var views = new BreedingPlanView[level.BreedingSlotCount];
            for (var slot = 0; slot < views.Length; slot++)
            {
                var parents = new string[level.ParentCount(slot)];
                for (var seat = 0; seat < parents.Length; seat++)
                    parents[seat] = level.ParentAt(slot, seat);

                views[slot] = new BreedingPlanView(slot, parents, level.SkillAt(slot));
            }

            return views;
        }
    }
}
