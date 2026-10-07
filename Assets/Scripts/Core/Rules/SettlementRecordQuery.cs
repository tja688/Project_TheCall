using System.Collections.Generic;
using System.Linq;
using QFramework;

namespace TheCall
{
    public abstract class SettlementEntry
    {
    }

    public readonly struct LandingAdd
    {
        public LandingAdd(string label, int amount)
        {
            Label = label;
            Amount = amount;
        }

        public string Label { get; }

        public int Amount { get; }
    }

    public readonly struct LandingFactor
    {
        public LandingFactor(string label, int factor)
        {
            Label = label;
            Factor = factor;
        }

        public string Label { get; }

        public int Factor { get; }
    }

    public sealed class SettlementLanding : SettlementEntry
    {
        public SettlementLanding(string monsterId, string skillName, int baseValue, int multiplier, int energy, int writeback)
            : this(monsterId, skillName, baseValue, multiplier, energy, writeback, baseValue, 0, false, System.Array.Empty<LandingAdd>(), System.Array.Empty<LandingFactor>())
        {
        }

        public SettlementLanding(
            string monsterId,
            string skillName,
            int baseValue,
            int multiplier,
            int energy,
            int writeback,
            int quote,
            int sideCount,
            bool side,
            System.Collections.Generic.IReadOnlyList<LandingAdd> adds,
            System.Collections.Generic.IReadOnlyList<LandingFactor> factors)
        {
            MonsterId = monsterId;
            SkillName = skillName;
            Base = baseValue;
            Multiplier = multiplier;
            Energy = energy;
            Writeback = writeback;
            Quote = quote;
            SideCount = sideCount;
            Adds = adds ?? System.Array.Empty<LandingAdd>();
            Factors = factors ?? System.Array.Empty<LandingFactor>();
            Side = side;
            Check();
        }

        public string MonsterId { get; }

        public string SkillName { get; }

        public int Base { get; }

        public int Multiplier { get; }

        public int Energy { get; }

        public int Writeback { get; }

        public int Quote { get; }

        public int SideCount { get; }

        public bool Side { get; }

        public System.Collections.Generic.IReadOnlyList<LandingAdd> Adds { get; }

        public System.Collections.Generic.IReadOnlyList<LandingFactor> Factors { get; }

        void Check()
        {
            var added = 0;
            for (var i = 0; i < Adds.Count; i++)
                added += Adds[i].Amount;

            var expectedBase = Side ? (Quote + added) * SideCount : Quote + added;
            var expectedMultiplier = 1;
            for (var i = 0; i < Factors.Count; i++)
                expectedMultiplier *= Factors[i].Factor;

            if (Factors.Count == 0)
                expectedMultiplier = 1;

            if (Base != expectedBase || Multiplier != expectedMultiplier || Energy != Base * Multiplier)
                throw new System.InvalidOperationException(SkillName + " 的结算记录和算术不一致");
        }
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
            : this(monsterId, happened, null)
        {
        }

        public SettlementRemoval(string monsterId, bool happened, string sourceId)
        {
            MonsterId = monsterId;
            Happened = happened;
            SourceId = sourceId;
        }

        public string MonsterId { get; }

        public bool Happened { get; }

        public string SourceId { get; }
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
