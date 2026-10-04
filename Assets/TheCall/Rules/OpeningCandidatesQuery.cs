using System.Collections.Generic;
using System.Linq;
using QFramework;

namespace TheCall
{
    public sealed class OpeningCandidatesQuery : AbstractQuery<IReadOnlyList<MonsterView>>
    {
        protected override IReadOnlyList<MonsterView> OnDo() =>
            this.GetModel<RunModel>().Candidates.Select(MonsterViews.From).ToArray();
    }
}
