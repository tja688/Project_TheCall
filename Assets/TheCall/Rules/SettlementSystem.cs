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
                    ScoreSkill(level, run, catalog, cells, cell, monsterId, skills[index]);
            }
        }

        void ScoreSkill(
            LevelModel level,
            RunModel run,
            SkillCatalog catalog,
            IReadOnlyList<string> cells,
            int cell,
            string monsterId,
            SkillInstance skill)
        {
            var skillName = skill.Name;
            if (catalog.TryDevour(skillName, out var writeback, out var permanent))
            {
                Quote(level, run, catalog, cells, cell, monsterId, skill, 1, writeback, permanent);
                DestroyAdjacent(level, run, cells, cell);
                return;
            }

            if (catalog.TryRepeatedQuote(skillName, out var times))
            {
                Quote(level, run, catalog, cells, cell, monsterId, skill, times, 0, false);
                return;
            }

            if (!TryQuote(catalog, cells, cell, skillName, out var quote))
                return;

            Land(level, run, catalog, cells, cell, monsterId, skillName, quote);
        }

        void Quote(
            LevelModel level,
            RunModel run,
            SkillCatalog catalog,
            IReadOnlyList<string> cells,
            int cell,
            string monsterId,
            SkillInstance skill,
            int times,
            int writeback,
            bool permanent)
        {
            for (var time = 0; time < times; time++)
            {
                Land(level, run, catalog, cells, cell, monsterId, skill.Name, skill.Quote);
                if (writeback != 0)
                    skill.Add(writeback, permanent);
            }
        }

        void Land(
            LevelModel level,
            RunModel run,
            SkillCatalog catalog,
            IReadOnlyList<string> cells,
            int cell,
            string monsterId,
            string skillName,
            int quote)
        {
            var baseValue = quote + AddedByOthers(catalog, run, cells, monsterId);
            var multiplier = 1;
            if (catalog.DoublesWhenIsolated(skillName) && !HasNeighbor(cells, cell))
                multiplier = 2;

            var energy = baseValue * multiplier;
            _landings.Add(new SettlementLanding(monsterId, skillName, baseValue, multiplier, energy));
            level.AddEnergy(energy);
        }

        void DestroyAdjacent(LevelModel level, RunModel run, IReadOnlyList<string> cells, int cell)
        {
            var options = new List<string>();
            if (cell > 0 && cells[cell - 1] != null)
                options.Add(cells[cell - 1]);
            if (cell + 1 < cells.Count && cells[cell + 1] != null)
                options.Add(cells[cell + 1]);
            if (options.Count == 0)
                return;

            var target = this.GetUtility<IDraw>().Choose(options);
            level.TryRemove(target);
            run.TryDestroy(target);
        }

        protected override void OnInit()
        {
        }

        static bool TryQuote(SkillCatalog catalog, IReadOnlyList<string> cells, int cell, string skillName, out int quote)
        {
            if (catalog.TryEnergyQuote(skillName, out quote))
                return true;

            if (!catalog.TrySideCount(skillName, out var side, out var perMonster))
            {
                quote = 0;
                return false;
            }

            var count = 0;
            var direction = (int)side;
            for (var index = cell + direction; index >= 0 && index < cells.Count; index += direction)
            {
                if (cells[index] != null)
                    count++;
            }

            quote = perMonster * count;
            return true;
        }

        static int AddedByOthers(SkillCatalog catalog, RunModel run, IReadOnlyList<string> cells, string monsterId)
        {
            var added = 0;
            for (var cell = 0; cell < cells.Count; cell++)
            {
                var otherId = cells[cell];
                if (otherId == null || otherId == monsterId)
                    continue;

                var skills = run.Find(otherId).Skills;
                for (var index = 0; index < skills.Count; index++)
                    added += catalog.AddedToOthers(skills[index].Name);
            }

            return added;
        }

        static bool HasNeighbor(IReadOnlyList<string> cells, int cell)
        {
            if (cell > 0 && cells[cell - 1] != null)
                return true;

            return cell + 1 < cells.Count && cells[cell + 1] != null;
        }
    }
}
