using System;
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
        readonly List<PendingSwap> _swaps = new List<PendingSwap>();
        readonly List<PendingRemoval> _endRemovals = new List<PendingRemoval>();
        readonly List<string> _removed = new List<string>();
        readonly Dictionary<string, decimal> _forceReady = new Dictionary<string, decimal>();
        readonly Dictionary<string, int> _nextBonus = new Dictionary<string, int>();
        int _removalSequence;

        public IReadOnlyList<SettlementLanding> Landings => _landings;

        public IReadOnlyList<string> Removed => _removed;

        public int Deducted { get; private set; }

        public PaymentResult Settle()
        {
            Score();
            ExecuteSwaps();
            ExecuteEndRemovals();
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
            _swaps.Clear();
            _endRemovals.Clear();
            _removed.Clear();
            _forceReady.Clear();
            _nextBonus.Clear();
            _removalSequence = 0;
            var level = this.GetModel<LevelModel>();
            var run = this.GetModel<RunModel>();
            var catalog = this.GetUtility<SkillCatalog>();
            var intents = this.GetUtility<ClockIntents>();
            level.ClearEnergy();
            GrantPermanentImmovable(run, intents);

            var ticks = new List<ClockTick>();
            var order = 0;
            var width = level.Extraction.Count;
            decimal start = 0;
            SchedulePass(ticks, ref order, start, width, false, level, run, catalog);
            start += width;
            if (intents != null && intents.PlaysReverse)
            {
                SchedulePass(ticks, ref order, start, width, true, level, run, catalog);
                start += width;
            }

            if (intents != null && intents.WalksAgain)
                SchedulePass(ticks, ref order, start, width, false, level, run, catalog);

            if (intents != null)
                ScheduleIntents(ticks, ref order, level, run, catalog, intents);

            ticks.Sort((left, right) =>
            {
                var byTime = left.Time.CompareTo(right.Time);
                return byTime != 0 ? byTime : left.Order.CompareTo(right.Order);
            });
            for (var i = 0; i < ticks.Count; i++)
                ticks[i].Run();
        }

        void SchedulePass(
            List<ClockTick> ticks,
            ref int order,
            decimal start,
            int width,
            bool reverse,
            LevelModel level,
            RunModel run,
            SkillCatalog catalog)
        {
            for (var step = 0; step < width; step++)
            {
                var cell = reverse ? width - 1 - step : step;
                var at = start + step;
                AddTick(ticks, ref order, at, () =>
                {
                    var cells = level.Extraction;
                    if (cell >= cells.Count || cells[cell] == null)
                        return;

                    ScoreStay(level, run, catalog, cells, cell, cells[cell]);
                });
            }
        }

        void ScheduleIntents(
            List<ClockTick> ticks,
            ref int order,
            LevelModel level,
            RunModel run,
            SkillCatalog catalog,
            ClockIntents intents)
        {
            for (var i = 0; i < intents.Writes.Count; i++)
            {
                var write = intents.Writes[i];
                var at = Logical(write.AtTime);
                AddTick(ticks, ref order, at, () => ApplyWrite(run, write));
            }

            for (var i = 0; i < intents.Forces.Count; i++)
            {
                var force = intents.Forces[i];
                var at = Logical(force.AtTime);
                var monsterId = force.MonsterId;
                AddTick(ticks, ref order, at, () => Force(level, run, catalog, monsterId, at));
            }

            for (var i = 0; i < intents.EndRemovals.Count; i++)
            {
                var removal = intents.EndRemovals[i];
                var at = Logical(removal.AtTime);
                var monsterId = removal.MonsterId;
                AddTick(ticks, ref order, at, () => RegisterEndRemoval(level, monsterId, at));
            }
        }

        static void AddTick(List<ClockTick> ticks, ref int order, decimal time, Action run)
        {
            ticks.Add(new ClockTick { Time = time, Order = order, Run = run });
            order++;
        }

        void ScoreStay(
            LevelModel level,
            RunModel run,
            SkillCatalog catalog,
            IReadOnlyList<string> cells,
            int cell,
            string monsterId)
        {
            if (cell >= cells.Count || cells[cell] == null)
                return;

            var stayingId = cells[cell];
            var extraTrigger = ExtraWalksAtStayStart(run, catalog, cells, cell);
            var capacityExtra = CapacityExtraAtStayStart(run, catalog, cells, cell);
            var points = SecondPointCount(stayingId);
            for (var point = 0; point < points; point++)
            {
                if (cell >= cells.Count || cells[cell] != stayingId)
                    return;

                WalkSkills(level, run, catalog, cells, cell, stayingId);
                if (cell < cells.Count && cells[cell] == stayingId)
                    SettlePoison(stayingId);
            }

            if (cell >= cells.Count || cells[cell] != stayingId)
                return;

            var monster = run.Find(stayingId);
            if (monster == null)
                return;

            var skills = monster.Skills;
            for (var index = 0; index < skills.Count; index++)
            {
                var skill = skills[index];
                var extra = catalog.ProducesEnergy(skill.Name) ? extraTrigger + capacityExtra : extraTrigger;
                for (var time = 0; time < extra; time++)
                {
                    if (cell >= cells.Count || cells[cell] != stayingId)
                        return;

                    ScoreSkill(level, run, catalog, cells, cell, stayingId, skill);
                }
            }
        }

        static int CapacityExtraAtStayStart(RunModel run, SkillCatalog catalog, IReadOnlyList<string> cells, int cell)
        {
            if (cell + 1 >= cells.Count || cells[cell + 1] == null)
                return 0;

            return SumNeighbor(run, cells[cell + 1], catalog.CapacityExtraForLeftNeighbor);
        }

        static int ExtraWalksAtStayStart(RunModel run, SkillCatalog catalog, IReadOnlyList<string> cells, int cell)
        {
            if (cell == 0 || cells[cell - 1] == null)
                return 0;

            return SumNeighbor(run, cells[cell - 1], catalog.ExtraWalksForRightNeighbor);
        }

        static int SumNeighbor(RunModel run, string monsterId, System.Func<string, int> amount)
        {
            var neighbor = run.Find(monsterId);
            if (neighbor == null)
                return 0;

            var extra = 0;
            var skills = neighbor.Skills;
            for (var index = 0; index < skills.Count; index++)
                extra += amount(skills[index].Name);

            return extra;
        }

        void WalkSkills(
            LevelModel level,
            RunModel run,
            SkillCatalog catalog,
            IReadOnlyList<string> cells,
            int cell,
            string monsterId)
        {
            var monster = run.Find(monsterId);
            if (monster == null)
                return;

            var skills = monster.Skills;
            for (var index = 0; index < skills.Count; index++)
                ScoreSkill(level, run, catalog, cells, cell, monsterId, skills[index]);
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
            if (catalog.TryGainCapacity(skillName, out var layers))
            {
                var host = run.Find(monsterId);
                if (host != null)
                    host.AddCapacity(layers);

                return;
            }

            if (catalog.SwapsWithLeft(skillName))
            {
                var target = cell > 0 ? cells[cell - 1] : null;
                _swaps.Add(new PendingSwap(monsterId, target));
                OpenCapacity(level, run, catalog, cells, cell, monsterId);
                return;
            }

            if (catalog.TryDevour(skillName, out var writeback, out var permanent))
            {
                Quote(level, run, catalog, cells, cell, monsterId, skill, 1, writeback, permanent);
                DestroyAdjacent(cells, cell);
                OpenCapacity(level, run, catalog, cells, cell, monsterId);
                return;
            }

            if (catalog.TryRepeatedQuote(skillName, out var times))
            {
                Quote(level, run, catalog, cells, cell, monsterId, skill, times, 0, false);
                OpenCapacity(level, run, catalog, cells, cell, monsterId);
                return;
            }

            var nextBonus = catalog.NextEnergyBonus(skillName);
            if (nextBonus > 0)
            {
                Land(level, run, catalog, cells, cell, monsterId, skillName, skill.Quote);
                AddNextBonus(level.Extraction, cell, nextBonus);
                OpenCapacity(level, run, catalog, cells, cell, monsterId);
                return;
            }

            if (!TryQuote(catalog, cells, cell, skillName, out var quote))
                return;

            Land(level, run, catalog, cells, cell, monsterId, skillName, quote);
            OpenCapacity(level, run, catalog, cells, cell, monsterId);
        }

        void OpenCapacity(
            LevelModel level,
            RunModel run,
            SkillCatalog catalog,
            IReadOnlyList<string> cells,
            int cell,
            string monsterId)
        {
            var monster = run.Find(monsterId);
            if (monster == null)
                return;

            var layers = monster.Capacity;
            for (var layer = 0; layer < layers; layer++)
                Land(level, run, catalog, cells, cell, monsterId, "产能", 1);
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
            var modifier = skillName == "产能" ? 0 : run.Find(monsterId).Modifier;
            _nextBonus.TryGetValue(monsterId, out var bonus);
            var baseValue = quote + AddedByOthers(catalog, run, cells, monsterId) + modifier + bonus;
            var multiplier = 1;
            if (catalog.DoublesWhenIsolated(skillName) && !HasNeighbor(cells, cell))
                multiplier = 2;

            var energy = baseValue * multiplier;
            _landings.Add(new SettlementLanding(monsterId, skillName, baseValue, multiplier, energy));
            level.AddEnergy(energy);
            RespondToLanding(level, run, catalog, monsterId, skillName);
        }

        void RespondToLanding(
            LevelModel level,
            RunModel run,
            SkillCatalog catalog,
            string sourceId,
            string causeSkill)
        {
            var cells = level.Extraction;
            for (var cell = 0; cell < cells.Count; cell++)
            {
                var id = cells[cell];
                if (id == null || id == sourceId)
                    continue;

                var monster = run.Find(id);
                if (monster == null)
                    continue;

                var skills = monster.Skills;
                for (var index = 0; index < skills.Count; index++)
                {
                    if (!catalog.TryLandingResponse(skills[index].Name, causeSkill, out var responseQuote))
                        continue;

                    Land(level, run, catalog, cells, cell, id, skills[index].Name, responseQuote);
                }
            }
        }

        void AddNextBonus(IReadOnlyList<string> cells, int cell, int bonus)
        {
            var next = NextMonster(cells, cell);
            if (next == null)
                return;

            _nextBonus.TryGetValue(next, out var current);
            _nextBonus[next] = current + bonus;
        }

        static string NextMonster(IReadOnlyList<string> cells, int cell)
        {
            for (var index = cell + 1; index < cells.Count; index++)
            {
                if (cells[index] != null)
                    return cells[index];
            }

            return null;
        }

        void DestroyAdjacent(IReadOnlyList<string> cells, int cell)
        {
            var options = new List<string>();
            if (cell > 0 && cells[cell - 1] != null)
                options.Add(cells[cell - 1]);
            if (cell + 1 < cells.Count && cells[cell + 1] != null)
                options.Add(cells[cell + 1]);
            if (options.Count == 0)
                return;

            var target = this.GetUtility<IDraw>().Choose(options);
            RemoveFromPlay(target);
        }

        void ExecuteSwaps()
        {
            var level = this.GetModel<LevelModel>();
            var run = this.GetModel<RunModel>();
            for (var i = 0; i < _swaps.Count; i++)
            {
                var swap = _swaps[i];
                if (string.IsNullOrEmpty(swap.TargetId))
                    continue;

                var actor = run.Find(swap.ActorId);
                var target = run.Find(swap.TargetId);
                if (actor == null || target == null || target.Immovable)
                    continue;
                if (IndexOf(level.Extraction, swap.ActorId) < 0 || IndexOf(level.Extraction, swap.TargetId) < 0)
                    continue;
                if (!level.TrySwapExtraction(swap.ActorId, swap.TargetId))
                    continue;

                actor.MakeImmovable(false);
            }
        }

        void ExecuteEndRemovals()
        {
            _endRemovals.Sort((left, right) =>
            {
                var byTime = left.Time.CompareTo(right.Time);
                if (byTime != 0)
                    return byTime;

                var byCell = left.Cell.CompareTo(right.Cell);
                return byCell != 0 ? byCell : left.Sequence.CompareTo(right.Sequence);
            });
            for (var i = 0; i < _endRemovals.Count; i++)
                RemoveFromPlay(_endRemovals[i].MonsterId);
        }

        void Force(LevelModel level, RunModel run, SkillCatalog catalog, string monsterId, decimal time)
        {
            if (string.IsNullOrEmpty(monsterId))
                return;
            if (_forceReady.TryGetValue(monsterId, out var ready) && time < ready)
                return;

            var cell = IndexOf(level.Extraction, monsterId);
            if (cell < 0)
                return;

            _forceReady[monsterId] = time + 0.1m;
            WalkSkills(level, run, catalog, level.Extraction, cell, monsterId);
            SettlePoison(monsterId);
        }

        static void ApplyWrite(RunModel run, ClockIntents.LaterWrite write)
        {
            var monster = run.Find(write.MonsterId);
            if (monster == null)
                return;

            for (var i = 0; i < monster.Skills.Count; i++)
            {
                if (monster.Skills[i].Name == write.SkillName)
                    monster.Skills[i].Add(write.Amount, false);
            }
        }

        void RegisterEndRemoval(LevelModel level, string monsterId, decimal time)
        {
            var cell = IndexOf(level.Extraction, monsterId);
            if (cell < 0)
                return;

            _endRemovals.Add(new PendingRemoval(time, cell, _removalSequence, monsterId));
            _removalSequence++;
        }

        static void GrantPermanentImmovable(RunModel run, ClockIntents intents)
        {
            if (intents == null)
                return;

            for (var i = 0; i < intents.PermanentImmovable.Count; i++)
            {
                var monster = run.Find(intents.PermanentImmovable[i]);
                if (monster != null)
                    monster.MakeImmovable(true);
            }
        }

        void SettlePoison(string monsterId)
        {
            var intents = this.GetUtility<ClockIntents>();
            if (intents == null)
                return;

            var damage = intents.PoisonOf(monsterId);
            if (damage <= 0)
                return;

            LandPoison(monsterId, damage);
            if (intents.RetriggerSources(monsterId) > 0)
                LandPoison(monsterId, damage);
        }

        void LandPoison(string monsterId, int damage)
        {
            _landings.Add(new SettlementLanding(monsterId, "毒跳伤", damage, 1, damage));
            this.GetModel<LevelModel>().AddEnergy(damage);
        }

        int SecondPointCount(string monsterId)
        {
            var intents = this.GetUtility<ClockIntents>();
            if (intents == null)
                return 1;

            return intents.SecondPointsOf(monsterId);
        }

        bool RemoveFromPlay(string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId))
                return false;

            var level = this.GetModel<LevelModel>();
            var run = this.GetModel<RunModel>();
            if (IndexOf(level.Extraction, monsterId) < 0)
                return false;

            level.TryRemove(monsterId);
            if (!run.TryDestroy(monsterId))
                return false;

            _removed.Add(monsterId);
            return true;
        }

        protected override void OnInit()
        {
        }

        static decimal Logical(double time) =>
            decimal.Round((decimal)time, 2, MidpointRounding.AwayFromZero);

        static int IndexOf(IReadOnlyList<string> cells, string monsterId)
        {
            if (monsterId == null)
                return -1;

            for (var i = 0; i < cells.Count; i++)
            {
                if (cells[i] == monsterId)
                    return i;
            }

            return -1;
        }

        struct ClockTick
        {
            public decimal Time;
            public int Order;
            public Action Run;
        }

        readonly struct PendingSwap
        {
            public PendingSwap(string actorId, string targetId)
            {
                ActorId = actorId;
                TargetId = targetId;
            }

            public string ActorId { get; }

            public string TargetId { get; }
        }

        readonly struct PendingRemoval
        {
            public PendingRemoval(decimal time, int cell, int sequence, string monsterId)
            {
                Time = time;
                Cell = cell;
                Sequence = sequence;
                MonsterId = monsterId;
            }

            public decimal Time { get; }

            public int Cell { get; }

            public int Sequence { get; }

            public string MonsterId { get; }
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
