using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal enum PaymentResult
    {
        Paid,
        Short,
        Failed,
    }

    internal sealed class SettlementSystem : AbstractSystem
    {
        readonly List<SettlementLanding> _landings = new List<SettlementLanding>();

        public IReadOnlyList<SettlementLanding> Landings => _landings;

        public int Deducted { get; private set; }

        public PaymentResult Settle()
        {
            Score();
            var level = this.GetModel<LevelModel>();
            var due = level.InOvertime ? level.Shortfall : level.EnergyDue;
            if (level.Energy < due)
            {
                Deducted = 0;
                if (level.InOvertime)
                {
                    level.ClearEnergy();
                    return PaymentResult.Failed;
                }

                level.RecordShortfall(level.EnergyDue - level.Energy);
                return PaymentResult.Short;
            }

            var remaining = level.Energy - due;
            Deducted = due;
            level.Pay(due);
            level.ClearEnergy();
            var run = this.GetModel<RunModel>();
            if (remaining >= level.ExcessEnergy)
                run.AddTechPoint();

            run.AddGold(level.InOvertime ? 20 : 40);
            return PaymentResult.Paid;
        }

        void Score()
        {
            _landings.Clear();
            var level = this.GetModel<LevelModel>();
            var run = this.GetModel<RunModel>();
            var catalog = this.GetUtility<SkillCatalog>();
            level.ClearEnergy();

            var cells = level.Extraction;
            for (var cell = 0; cell < cells.Count; cell++)
            {
                var monsterId = cells[cell];
                if (monsterId == null)
                    continue;

                var skills = run.Find(monsterId).Skills;
                for (var index = 0; index < skills.Count; index++)
                {
                    var skillName = skills[index].Name;
                    if (!catalog.TryEnergyQuote(skillName, out var quote))
                        continue;

                    const int multiplier = 1;
                    var energy = quote * multiplier;
                    _landings.Add(new SettlementLanding(monsterId, skillName, quote, multiplier, energy));
                    level.AddEnergy(energy);
                }
            }
        }

        protected override void OnInit()
        {
        }
    }
}
