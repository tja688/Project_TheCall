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
        readonly List<SettlementEntry> _entries = new List<SettlementEntry>();
        readonly List<PendingSwap> _swaps = new List<PendingSwap>();
        readonly List<PendingRemoval> _endRemovals = new List<PendingRemoval>();
        readonly Dictionary<string, decimal> _forceReady = new Dictionary<string, decimal>();
        readonly Dictionary<string, int> _nextBonus = new Dictionary<string, int>();
        int _removalSequence;
        bool _doubleFirstEnergy;
        bool _doubleSingleAffix;

        public IReadOnlyList<SettlementEntry> Entries => _entries;

        public PaymentResult Settle()
        {
            Score();
            ExecuteSwaps();
            ExecuteEndRemovals();
            var level = this.GetModel<LevelModel>();
            var wasOvertime = level.InOvertime;
            var due = wasOvertime ? level.Shortfall : level.EnergyDue;
            if (level.Energy < due)
            {
                var gap = due - level.Energy;
                if (wasOvertime)
                {
                    level.ClearEnergy();
                    _entries.Add(new SettlementPayment(0, gap, true, true, false, 0));
                    return PaymentResult.Failed;
                }

                level.RecordShortfall(gap);
                _entries.Add(new SettlementPayment(0, gap, true, false, false, 0));
                return PaymentResult.Short;
            }

            var produced = level.Energy;
            level.Pay(due);
            level.ClearEnergy();
            var run = this.GetModel<RunModel>();
            var excess = produced >= level.ExcessEnergy;
            if (excess)
                run.AddTechPoint();

            var wage = ContentGate.Current.Wage(wasOvertime);
            run.AddGold(wage);
            level.ClearDebt();
            _entries.Add(new SettlementPayment(due, 0, wasOvertime, false, excess, wage));
            return PaymentResult.Paid;
        }

        void Score()
        {
            _entries.Clear();
            _swaps.Clear();
            _endRemovals.Clear();
            _forceReady.Clear();
            _nextBonus.Clear();
            _removalSequence = 0;
            var level = this.GetModel<LevelModel>();
            var run = this.GetModel<RunModel>();
            var catalog = this.GetUtility<SkillCatalog>();
            var intents = this.GetUtility<ClockIntents>();
            level.ClearEnergy();
            var tools = this.GetUtility<IToolCatalog>();
            _doubleFirstEnergy = tools.DoublesFirstEnergyExecution(run.Tools);
            _doubleSingleAffix = tools.DoublesSingleAffixEnergy(run.Tools);
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
                OpenCapacity(level, run, catalog, cells, cell, monsterId);
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

            var doubleFirstExecution = TakeFirstEnergyDouble(catalog, skillName);
            if (catalog.TryDevour(skillName, out var writeback, out var permanent))
            {
                Quote(level, run, catalog, cells, cell, monsterId, skill, 1, writeback, permanent, doubleFirstExecution);
                DestroyAdjacent(cells, cell);
                OpenCapacity(level, run, catalog, cells, cell, monsterId);
                return;
            }

            if (catalog.TryRepeatedQuote(skillName, out var times))
            {
                Quote(level, run, catalog, cells, cell, monsterId, skill, times, 0, false, doubleFirstExecution);
                OpenCapacity(level, run, catalog, cells, cell, monsterId);
                return;
            }

            var nextBonus = catalog.NextEnergyBonus(skillName);
            if (nextBonus > 0)
            {
                Land(level, run, catalog, cells, cell, monsterId, skillName, skill.Quote, doubleFirstExecution);
                AddNextBonus(level.Extraction, cell, nextBonus);
                OpenCapacity(level, run, catalog, cells, cell, monsterId);
                return;
            }

            if (catalog.TryEnergyQuote(skillName, out _))
            {
                Land(level, run, catalog, cells, cell, monsterId, skillName, skill.Quote, doubleFirstExecution);
                OpenCapacity(level, run, catalog, cells, cell, monsterId);
                return;
            }

            if (!TryQuote(catalog, cells, cell, skillName, out var quote))
            {
                OpenCapacity(level, run, catalog, cells, cell, monsterId);
                return;
            }

            Land(level, run, catalog, cells, cell, monsterId, skillName, quote, doubleFirstExecution);
            OpenCapacity(level, run, catalog, cells, cell, monsterId);
        }

        bool TakeFirstEnergyDouble(SkillCatalog catalog, string skillName)
        {
            if (!_doubleFirstEnergy || !catalog.ProducesEnergy(skillName))
                return false;

            _doubleFirstEnergy = false;
            return true;
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
                Land(level, run, catalog, cells, cell, monsterId, "产能", 1, false);
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
            bool permanent,
            bool doubleFirstExecution)
        {
            for (var time = 0; time < times; time++)
            {
                Land(level, run, catalog, cells, cell, monsterId, skill.Name, skill.Quote, doubleFirstExecution, writeback);
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
            int quote,
            bool doubleFirstExecution,
            int writeback = 0)
        {
            var host = run.Find(monsterId);
            var modifier = host == null ? 0 : host.Modifier;
            _nextBonus.TryGetValue(monsterId, out var bonus);
            var adds = new List<LandingAdd>();
            var added = CollectAdds(catalog, run, cells, monsterId, modifier, bonus, adds);
            var recordedQuote = quote;
            var sideCount = 0;
            var sideSkill = catalog.TrySideCount(skillName, out var countedSide, out var perMonster);
            if (sideSkill)
            {
                recordedQuote = perMonster;
                sideCount = CountSide(cells, cell, countedSide);
            }

            var baseValue = BaseAfterAdds(catalog, cells, cell, skillName, quote, added);
            var factors = new List<LandingFactor>();
            var multiplier = 1;
            if (catalog.DoublesWhenIsolated(skillName) && !HasNeighbor(cells, cell))
            {
                multiplier *= 2;
                factors.Add(new LandingFactor("孤独心", 2));
            }

            multiplier *= RecordAdjacent(run, catalog, cells, cell, factors);
            if (doubleFirstExecution)
            {
                multiplier *= 2;
                factors.Add(new LandingFactor("急急装置", 2));
            }

            if (_doubleSingleAffix && IsSingleAffix(run, catalog, monsterId))
            {
                multiplier *= 2;
                factors.Add(new LandingFactor("独孤装置", 2));
            }

            var energy = baseValue * multiplier;
            _entries.Add(new SettlementLanding(
                monsterId,
                skillName,
                baseValue,
                multiplier,
                energy,
                writeback,
                recordedQuote,
                sideCount,
                sideSkill,
                adds,
                factors));
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

                    Land(level, run, catalog, cells, cell, id, skills[index].Name, responseQuote, false);
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
            {
                _entries.Add(new SettlementRemoval(null, false, cells[cell]));
                return;
            }

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
                _entries.Add(new SettlementSwap(swap.ActorId, swap.TargetId, TrySwap(level, run, swap)));
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
            _entries.Add(new SettlementLanding(monsterId, "毒跳伤", damage, 1, damage, 0));
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
            {
                _entries.Add(new SettlementRemoval(monsterId, false));
                return false;
            }

            var level = this.GetModel<LevelModel>();
            var run = this.GetModel<RunModel>();
            if (IndexOf(level.Extraction, monsterId) < 0)
            {
                _entries.Add(new SettlementRemoval(monsterId, false));
                return false;
            }

            level.TryRemove(monsterId);
            if (!run.TryDestroy(monsterId))
            {
                _entries.Add(new SettlementRemoval(monsterId, false));
                return false;
            }

            _entries.Add(new SettlementRemoval(monsterId, true));
            return true;
        }

        static bool TrySwap(LevelModel level, RunModel run, PendingSwap swap)
        {
            if (string.IsNullOrEmpty(swap.TargetId))
                return false;

            var actor = run.Find(swap.ActorId);
            var target = run.Find(swap.TargetId);
            if (actor == null || target == null || target.Immovable)
                return false;
            if (IndexOf(level.Extraction, swap.ActorId) < 0 || IndexOf(level.Extraction, swap.TargetId) < 0)
                return false;
            if (!level.TrySwapExtraction(swap.ActorId, swap.TargetId))
                return false;

            actor.MakeImmovable(false);
            return true;
        }

        protected override void OnInit()
        {
        }

        static bool IsSingleAffix(RunModel run, SkillCatalog catalog, string monsterId)
        {
            var monster = run.Find(monsterId);
            if (monster == null)
                return false;

            var seen = SkillAffix.None;
            var skills = monster.Skills;
            for (var i = 0; i < skills.Count; i++)
                seen |= catalog.Affixes(skills[i].Name);

            var bits = (int)seen;
            return bits != 0 && (bits & (bits - 1)) == 0;
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

        static int BaseAfterAdds(
            SkillCatalog catalog,
            IReadOnlyList<string> cells,
            int cell,
            string skillName,
            int quote,
            int added)
        {
            if (!catalog.TrySideCount(skillName, out var side, out var perMonster))
                return quote + added;

            return (perMonster + added) * CountSide(cells, cell, side);
        }

        static int CountSide(IReadOnlyList<string> cells, int cell, CountedSide side)
        {
            var count = 0;
            var direction = (int)side;
            for (var index = cell + direction; index >= 0 && index < cells.Count; index += direction)
            {
                if (cells[index] != null)
                    count++;
            }

            return count;
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

            quote = perMonster * CountSide(cells, cell, side);
            return true;
        }

        static int CollectAdds(
            SkillCatalog catalog,
            RunModel run,
            IReadOnlyList<string> cells,
            string monsterId,
            int modifier,
            int bonus,
            List<LandingAdd> adds)
        {
            for (var cell = 0; cell < cells.Count; cell++)
            {
                var otherId = cells[cell];
                if (otherId == null || otherId == monsterId)
                    continue;

                var skills = run.Find(otherId).Skills;
                for (var index = 0; index < skills.Count; index++)
                {
                    var amount = catalog.AddedToOthers(skills[index].Name);
                    if (amount == 0)
                        continue;

                    adds.Add(new LandingAdd(skills[index].Name, amount));
                }
            }

            if (modifier != 0)
                adds.Add(new LandingAdd("宿主修正", modifier));
            if (bonus != 0)
                adds.Add(new LandingAdd("下家", bonus));

            var added = 0;
            for (var i = 0; i < adds.Count; i++)
                added += adds[i].Amount;

            return added;
        }

        int RecordAdjacent(RunModel run, SkillCatalog catalog, IReadOnlyList<string> cells, int cell, List<LandingFactor> factors)
        {
            var multiplier = 1;
            multiplier *= RecordNeighbor(run, catalog, cells, cell - 1, factors);
            multiplier *= RecordNeighbor(run, catalog, cells, cell + 1, factors);
            return multiplier;
        }

        static int RecordNeighbor(RunModel run, SkillCatalog catalog, IReadOnlyList<string> cells, int cell, List<LandingFactor> factors)
        {
            var value = NeighborEnergyDouble(run, catalog, cells, cell);
            if (value != 1)
                factors.Add(new LandingFactor("鼓励嘴", value));

            return value;
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

        static int AdjacentEnergyDouble(RunModel run, SkillCatalog catalog, IReadOnlyList<string> cells, int cell)
        {
            var multiplier = 1;
            multiplier *= NeighborEnergyDouble(run, catalog, cells, cell - 1);
            multiplier *= NeighborEnergyDouble(run, catalog, cells, cell + 1);
            return multiplier;
        }

        static int NeighborEnergyDouble(RunModel run, SkillCatalog catalog, IReadOnlyList<string> cells, int cell)
        {
            if (cell < 0 || cell >= cells.Count || cells[cell] == null)
                return 1;

            var neighbor = run.Find(cells[cell]);
            if (neighbor == null)
                return 1;

            var skills = neighbor.Skills;
            for (var index = 0; index < skills.Count; index++)
            {
                if (catalog.DoublesAdjacentEnergy(skills[index].Name))
                    return 2;
            }

            return 1;
        }
    }
}
