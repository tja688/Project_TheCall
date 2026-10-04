using System.Collections.Generic;
using System.Linq;
using QFramework;

namespace TheCall
{
    public sealed class MonsterCageQuery : AbstractQuery<IReadOnlyList<MonsterView>>
    {
        protected override IReadOnlyList<MonsterView> OnDo() =>
            this.GetModel<RunModel>().Cage.Select(MonsterViews.From).ToArray();
    }
}
