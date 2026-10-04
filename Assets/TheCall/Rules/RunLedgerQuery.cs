using System.Collections.Generic;
using System.Linq;
using QFramework;

namespace TheCall
{
    public sealed class RunLedger
    {
        public RunLedger(
            int gold,
            int techPoints,
            IReadOnlyList<string> tools,
            IReadOnlyList<string> skillSlots,
            IReadOnlyList<string> unlockedTech)
        {
            Gold = gold;
            TechPoints = techPoints;
            Tools = tools;
            SkillSlots = skillSlots;
            UnlockedTech = unlockedTech;
        }

        public int Gold { get; }

        public int TechPoints { get; }

        public IReadOnlyList<string> Tools { get; }

        public IReadOnlyList<string> SkillSlots { get; }

        public IReadOnlyList<string> UnlockedTech { get; }
    }

    public sealed class RunLedgerQuery : AbstractQuery<RunLedger>
    {
        protected override RunLedger OnDo()
        {
            var run = this.GetModel<RunModel>();
            return new RunLedger(
                run.Gold,
                run.TechPoints,
                run.Tools.ToArray(),
                run.SkillSlots.ToArray(),
                run.UnlockedTech.ToArray());
        }
    }
}
