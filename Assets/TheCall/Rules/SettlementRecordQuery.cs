using System.Collections.Generic;
using System.Linq;
using QFramework;

namespace TheCall
{
    public sealed class SettlementLanding
    {
        public SettlementLanding(string monsterId, string skillName, int baseValue, int multiplier, int energy)
        {
            MonsterId = monsterId;
            SkillName = skillName;
            Base = baseValue;
            Multiplier = multiplier;
            Energy = energy;
        }

        public string MonsterId { get; }

        public string SkillName { get; }

        public int Base { get; }

        public int Multiplier { get; }

        public int Energy { get; }
    }

    public sealed class SettlementRecordQuery : AbstractQuery<IReadOnlyList<SettlementLanding>>
    {
        protected override IReadOnlyList<SettlementLanding> OnDo() =>
            this.GetSystem<SettlementSystem>().Landings.ToArray();
    }
}
