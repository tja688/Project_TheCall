using System.Collections.Generic;
using System.Linq;
using QFramework;

namespace TheCall
{
    public sealed class MonsterCageQuery : AbstractQuery<IReadOnlyList<MonsterView>>
    {
        protected override IReadOnlyList<MonsterView> OnDo()
        {
            var run = this.GetModel<RunModel>();
            return run.Cage.Select(run.ToView).ToArray();
        }
    }
}
