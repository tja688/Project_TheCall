using System.Collections.Generic;
using System.Linq;
using QFramework;

namespace TheCall
{
    public abstract class SettlementEntry
    {
    }

    public sealed class SettlementLanding : SettlementEntry
    {
        public SettlementLanding(string monsterId, string skillName, int baseValue, int multiplier, int energy, int writeback)
        {
            MonsterId = monsterId;
            SkillName = skillName;
            Base = baseValue;
            Multiplier = multiplier;
            Energy = energy;
            Writeback = writeback;
        }

        public string MonsterId { get; }

        public string SkillName { get; }

        public int Base { get; }

        public int Multiplier { get; }

        public int Energy { get; }

        public int Writeback { get; }
    }

    public sealed class SettlementSwap : SettlementEntry
    {
        public SettlementSwap(string actorId, string targetId, bool happened)
        {
            ActorId = actorId;
            TargetId = targetId;
            Happened = happened;
        }

        public string ActorId { get; }

        public string TargetId { get; }

        public bool Happened { get; }
    }

    public sealed class SettlementRemoval : SettlementEntry
    {
        public SettlementRemoval(string monsterId, bool happened)
        {
            MonsterId = monsterId;
            Happened = happened;
        }

        public string MonsterId { get; }

        public bool Happened { get; }
    }

    public sealed class SettlementPayment : SettlementEntry
    {
        public SettlementPayment(int deducted, int shortfall, bool overtime, bool failed, bool excess, int wage)
        {
            Deducted = deducted;
            Shortfall = shortfall;
            Overtime = overtime;
            Failed = failed;
            Excess = excess;
            Wage = wage;
        }

        public int Deducted { get; }

        public int Shortfall { get; }

        public bool Overtime { get; }

        public bool Failed { get; }

        public bool Excess { get; }

        public int Wage { get; }
    }

    public sealed class SettlementRecordQuery : AbstractQuery<IReadOnlyList<SettlementEntry>>
    {
        protected override IReadOnlyList<SettlementEntry> OnDo() =>
            this.GetSystem<SettlementSystem>().Entries.ToArray();
    }
}
