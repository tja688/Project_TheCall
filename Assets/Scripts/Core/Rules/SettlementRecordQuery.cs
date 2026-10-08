using System;
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
        public LandingAdd(string label, int amount, string sourceId, int sourceSkillIndex, string sourceName)
        {
            Label = label;
            Amount = amount;
            SourceId = sourceId;
            SourceSkillIndex = sourceSkillIndex;
            SourceName = sourceName;
        }

        public static LandingAdd Of(string label, int amount, string sourceId, int sourceSkillIndex, string sourceName) =>
            new LandingAdd(label, amount, sourceId, sourceSkillIndex, sourceName);

        public string Label { get; }

        public int Amount { get; }

        public string SourceId { get; }

        public int SourceSkillIndex { get; }

        public string SourceName { get; }
    }

    public readonly struct LandingFactor
    {
        public LandingFactor(string label, int factor, string sourceId, int sourceSkillIndex, string sourceName)
        {
            Label = label;
            Factor = factor;
            SourceId = sourceId;
            SourceSkillIndex = sourceSkillIndex;
            SourceName = sourceName;
        }

        public static LandingFactor Of(string label, int factor, string sourceId, int sourceSkillIndex, string sourceName) =>
            new LandingFactor(label, factor, sourceId, sourceSkillIndex, sourceName);

        public string Label { get; }

        public int Factor { get; }

        public string SourceId { get; }

        public int SourceSkillIndex { get; }

        public string SourceName { get; }
    }

    public enum MarkMotion
    {
        Spring,
        Shake,
    }

    public enum MarkTone
    {
        Miss,
        Again,
        Gold,
        Skills,
    }

    public sealed class SettlementMark : SettlementEntry
    {
        SettlementMark(string monsterId, string label, MarkMotion motion, MarkTone tone)
        {
            if (!Fits(label, motion, tone))
                throw new InvalidOperationException("结算头顶字不成立");

            MonsterId = monsterId;
            Label = label;
            Motion = motion;
            Tone = tone;
        }

        public static SettlementMark Miss(string monsterId) =>
            new SettlementMark(monsterId, "+0", MarkMotion.Spring, MarkTone.Miss);

        public static SettlementMark Again(string monsterId) =>
            new SettlementMark(monsterId, "技能触发+1", MarkMotion.Shake, MarkTone.Again);

        public static SettlementMark Gold(string monsterId, int amount) =>
            new SettlementMark(monsterId, "+" + amount + "金", MarkMotion.Spring, MarkTone.Gold);

        public static SettlementMark Skills(string monsterId, int count)
        {
            if (count <= 0)
                throw new InvalidOperationException("技能个数为 0 时用未中");

            return new SettlementMark(monsterId, "技能+" + count, MarkMotion.Spring, MarkTone.Skills);
        }

        public string MonsterId { get; }

        public string Label { get; }

        public MarkMotion Motion { get; }

        public MarkTone Tone { get; }

        static bool Fits(string label, MarkMotion motion, MarkTone tone)
        {
            if (label == "+0")
                return motion == MarkMotion.Spring && tone == MarkTone.Miss;
            if (label == "技能触发+1")
                return motion == MarkMotion.Shake && tone == MarkTone.Again;
            if (label != null && label.Length > 1 && label[0] == '+' && label.EndsWith("金"))
                return motion == MarkMotion.Spring && tone == MarkTone.Gold;
            if (label != null && label.StartsWith("技能+") && label.Length > "技能+".Length)
                return motion == MarkMotion.Spring && tone == MarkTone.Skills;
            return false;
        }
    }

    internal sealed class SettlementHold : SettlementEntry
    {
        public SettlementHold(string actorId, int skillIndex)
        {
            ActorId = actorId;
            SkillIndex = skillIndex;
        }

        public string ActorId { get; }

        public int SkillIndex { get; }
    }

    internal sealed class SettlementCause : SettlementEntry
    {
        public SettlementCause(string sourceId, int skillIndex)
        {
            SourceId = sourceId;
            SkillIndex = skillIndex;
        }

        public string SourceId { get; }

        public int SkillIndex { get; }
    }

    public sealed class SettlementLanding : SettlementEntry
    {
        public SettlementLanding(string monsterId, string skillName, int baseValue, int multiplier, int energy, int writeback, string monsterName = null, int cell = -1)
            : this(monsterId, skillName, baseValue, multiplier, energy, writeback, baseValue, 0, false, System.Array.Empty<LandingAdd>(), System.Array.Empty<LandingFactor>(), monsterName, cell, null)
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
            System.Collections.Generic.IReadOnlyList<LandingFactor> factors,
            string monsterName = null,
            int cell = -1,
            string countedSideName = null)
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
            MonsterName = monsterName;
            Cell = cell;
            CountedSideName = countedSideName;
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

        public string MonsterName { get; }

        public int Cell { get; }

        public string CountedSideName { get; }

        public System.Collections.Generic.IReadOnlyList<LandingAdd> Adds { get; }

        public System.Collections.Generic.IReadOnlyList<LandingFactor> Factors { get; }

        void Check()
        {
            var added = 0;
            for (var i = 0; i < Adds.Count; i++)
                added += Adds[i].Amount;

            var expectedBase = Side ? Quote * SideCount + added : Quote + added;
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
        protected override IReadOnlyList<SettlementEntry> OnDo()
        {
            var system = this.GetSystem<SettlementSystem>();
            system.Seal();
            return system.Entries.ToArray();
        }
    }
}
