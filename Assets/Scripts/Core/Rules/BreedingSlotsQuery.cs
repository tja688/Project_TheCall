using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public sealed class BreedingParentView
    {
        public BreedingParentView(int index, string monsterId)
        {
            Index = index;
            MonsterId = monsterId;
        }

        public int Index { get; }

        public string MonsterId { get; }
    }

    public sealed class BreedingSlotsQuery : AbstractQuery<IReadOnlyList<BreedingParentView>>
    {
        protected override IReadOnlyList<BreedingParentView> OnDo()
        {
            var parents = this.GetModel<LevelModel>().Breeding;
            var views = new BreedingParentView[parents.Count];
            for (var i = 0; i < parents.Count; i++)
                views[i] = new BreedingParentView(i, parents[i]);

            return views;
        }
    }
}
