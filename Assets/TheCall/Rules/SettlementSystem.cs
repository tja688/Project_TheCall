using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class SettlementSystem : AbstractSystem
    {
        readonly List<SettlementLanding> _landings = new List<SettlementLanding>();

        public IReadOnlyList<SettlementLanding> Landings => _landings;

        public bool Settle()
        {
            Score();
            var level = this.GetModel<LevelModel>();
            if (level.Energy < level.EnergyDue)
                return false;

            level.Pay(level.EnergyDue);
            level.ClearEnergy();
            this.GetModel<RunModel>().AddGold(40);
            return true;
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
