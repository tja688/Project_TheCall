using System.Collections.Generic;
using System.Linq;
using QFramework;

namespace TheCall
{
    public sealed class OpeningCandidatesQuery : AbstractQuery<IReadOnlyList<MonsterView>>
    {
        protected override IReadOnlyList<MonsterView> OnDo()
        {
            var run = this.GetModel<RunModel>();
            return run.Candidates.Select(run.ToView).ToArray();
        }
    }
}
