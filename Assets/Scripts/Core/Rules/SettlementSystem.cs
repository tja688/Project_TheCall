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
        readonly Dictionary<string, List<LandingAdd>> _nextAdds = new Dictionary<string, List<LandingAdd>>();
        readonly Dictionary<string, int> _fullDoubles = new Dictionary<string, int>();
        readonly Dictionary<string, List<LandingAdd>> _rowAdds = new Dictionary<string, List<LandingAdd>>();
        readonly Dictionary<string, int> _skillExtra = new Dictionary<string, int>();
        int _removalSequence;
        string _hasteToolName;
        string _hasteMonsterId;
        string _hasteSkillName;
        string _singleAffixToolName;

        public IReadOnlyList<SettlementEntry> Entries => _entries;

        public PaymentResult Settle()
        {
            Score();
            ExecuteSwaps();
            ExecuteEndRemovals();
            var level = this.GetModel<LevelModel>();
            var run = this.GetModel<RunModel>();
            var wasOvertime = level.InOvertime;
            var due = wasOvertime ? level.Shortfall : level.EnergyDue;
            var produced = level.Energy;
            SettlementPayment payment;
            PaymentResult result;
            if (level.Energy < due)
            {
                var gap = due - level.Energy;
                if (wasOvertime)
                {
                    level.ClearEnergy();
                    payment = new SettlementPayment(0, gap, true, true, false, 0);
                    result = PaymentResult.Failed;
                }
                else
                {
                    level.RecordShortfall(gap);
                    payment = new SettlementPayment(0, gap, true, false, false, 0);
                    result = PaymentResult.Short;
                }
            }
            else
            {
                level.Pay(due);
                level.ClearEnergy();
                var excess = produced >= level.ExcessEnergy;
                if (excess)
                    run.AddTechPoint();

                var wage = ContentGate.Current.Wage(wasOvertime);
                run.AddGold(wage);
                level.ClearDebt();
                payment = new SettlementPayment(due, 0, wasOvertime, false, excess, wage);
                result = PaymentResult.Paid;
            }

            _entries.Add(payment);
            run.AppendProduction(new ProductionSubmission(
                run.LevelNumber,
                wasOvertime,
                produced,
                due,
                payment,
                _entries.ToArray()));
            return result;
        }

        void Score()
        {
            _entries.Clear();
            _swaps.Clear();
            _endRemovals.Clear();
            _forceReady.Clear();
            _nextAdds.Clear();
            _fullDoubles.Clear();
            _rowAdds.Clear();
            _skillExtra.Clear();
            _removalSequence = 0;
            var level = this.GetModel<LevelModel>();
            var run = this.GetModel<RunModel>();
            var catalog = this.GetUtility<SkillCatalog>();
            var intents = this.GetUtility<ClockIntents>();
            level.ClearEnergy();
            var tools = this.GetUtility<IToolCatalog>().Tools;
            _hasteToolName = ToolRules.HeldName(tools, run.Tools, ToolEffect.DoubleFirstEnergy);
            _singleAffixToolName = ToolRules.HeldName(tools, run.Tools, ToolEffect.DoubleSingleAffix);
            BindHaste(level, run, catalog);
            GrantPermanentImmovable(run, intents);
            ApplyKin(level, run, catalog);

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
            var affixEnergy = AffixEnergySnapshot(run, catalog, stayingId);
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
                extra += CountExtra(run, catalog, stayingId, skill.Name);
                extra += PendingExtra(stayingId, skill.Name);
                extra += AffixExtra(affixEnergy, skill.Name);
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
            var extra = 0;
            if (cell + 1 < cells.Count && cells[cell + 1] != null)
                extra += SumNeighbor(run, cells[cell + 1], catalog.CapacityExtraForLeftNeighbor);
            if (cell > 0 && cells[cell - 1] != null)
                extra += SumNeighbor(run, cells[cell - 1], catalog.CapacityExtraForRightNeighbor);

            return extra;
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
            if (catalog.IsActive(skillName))
                GrantRightRow(run, catalog, cells, cell);

            try
            {
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

                if (catalog.DoublesWhenIsolated(skillName))
                {
                    if (!HasNeighbor(cells, cell))
                        DoubleRecorded(level, monsterId);

                    return;
                }

                if (catalog.TryDevour(skillName, out var writeback, out var permanent))
                {
                    BuffProduce(run, catalog, monsterId, skillName, writeback, permanent);
                    DestroyAdjacent(cells, cell);
                    return;
                }

                var gold = catalog.GoldOf(skillName);
                if (gold > 0)
                {
                    run.AddGold(gold);
                    return;
                }

                if (catalog.FillsSkills(skillName))
                {
                    FillSkills(run, catalog, monsterId);
                    return;
                }

                if (catalog.CopiesBreeding(skillName))
                {
                    CopyBreeding(level, run, catalog, monsterId);
                    return;
                }

                if (catalog.TryChance(skillName, out var percent, out _))
                {
                    if (!this.GetUtility<IDraw>().Chance(percent))
                        return;

                    Land(level, run, catalog, cells, cell, monsterId, skillName, skill.Quote);
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
                    AddNextBonus(run, level.Extraction, cell, skillName, nextBonus);
                    OpenCapacity(level, run, catalog, cells, cell, monsterId);
                    return;
                }

                if (catalog.TryOwnMultiple(skillName, out var factor))
                {
                    var host = run.Find(monsterId);
                    var count = host == null ? 0 : host.Skills.Count;
                    Land(level, run, catalog, cells, cell, monsterId, skillName, count * factor + skill.Quote);
                    OpenCapacity(level, run, catalog, cells, cell, monsterId);
                    return;
                }

                if (catalog.IsPopulation(skillName))
                {
                    Land(level, run, catalog, cells, cell, monsterId, skillName, Population(level, run) + skill.Quote);
                    OpenCapacity(level, run, catalog, cells, cell, monsterId);
                    return;
                }

                if (catalog.TrySideCount(skillName, out _, out _))
                {
                    Land(level, run, catalog, cells, cell, monsterId, skillName, skill.Quote);
                    OpenCapacity(level, run, catalog, cells, cell, monsterId);
                    return;
                }

                if (catalog.TryEnergyQuote(skillName, out _))
                {
                    Land(level, run, catalog, cells, cell, monsterId, skillName, skill.Quote);
                    OpenCapacity(level, run, catalog, cells, cell, monsterId);
                }
            }
            finally
            {
                if (catalog.GrantsSameNameExtra(skillName))
                    GrantSameName(run, cells, skillName, monsterId);
            }
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
                Land(level, run, catalog, cells, cell, monsterId, skill.Name, skill.Quote, writeback);
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
            int writeback = 0)
        {
            var host = run.Find(monsterId);
            var modifier = host == null ? 0 : host.Modifier;
            var adds = new List<LandingAdd>();
            var added = CollectAdds(catalog, run, cells, cell, monsterId, skillName, modifier, adds);
            var recordedQuote = quote;
            var sideCount = 0;
            string countedSideName = null;
            var sideSkill = catalog.TrySideCount(skillName, out var countedSide, out _);
            if (sideSkill)
            {
                recordedQuote = quote;
                sideCount = CountSide(cells, cell, countedSide);
                countedSideName = countedSide == CountedSide.Left ? "左侧" : "右侧";
            }

            var baseValue = sideSkill ? recordedQuote * sideCount + added : quote + added;
            var factors = new List<LandingFactor>();
            var multiplier = 1;
            if (catalog.DoublesWhenIsolated(skillName) && !HasNeighbor(cells, cell))
            {
                multiplier *= 2;
                factors.Add(new LandingFactor("孤独心", 2));
            }

            multiplier *= RecordAdjacent(run, catalog, cells, cell, factors);
            if (_hasteToolName != null && monsterId == _hasteMonsterId && skillName == _hasteSkillName)
            {
                multiplier *= 2;
                factors.Add(new LandingFactor(_hasteToolName, 2));
            }

            if (_singleAffixToolName != null && IsSingleAffix(run, catalog, monsterId))
            {
                multiplier *= 2;
                factors.Add(new LandingFactor(_singleAffixToolName, 2));
            }

            if (_fullDoubles.TryGetValue(monsterId, out var fullDoubles))
            {
                for (var time = 0; time < fullDoubles; time++)
                {
                    multiplier *= 2;
                    factors.Add(new LandingFactor("孤独心", 2));
                }
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
                factors,
                host == null ? null : host.DisplayName,
                cell,
                countedSideName));
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
                    if (!catalog.TryLandingResponse(skills[index].Name, causeSkill, out _))
                        continue;

                    Land(level, run, catalog, cells, cell, id, skills[index].Name, skills[index].Quote);
                }
            }
        }

        void AddNextBonus(RunModel run, IReadOnlyList<string> cells, int cell, string skillName, int bonus)
        {
            var next = NextMonster(cells, cell);
            if (next == null)
                return;

            var source = run.Find(cells[cell]);
            Remember(_nextAdds, next, new LandingAdd(skillName, bonus, source == null ? null : source.DisplayName));
        }

        static void Remember(Dictionary<string, List<LandingAdd>> book, string monsterId, LandingAdd add)
        {
            if (!book.TryGetValue(monsterId, out var list))
            {
                list = new List<LandingAdd>();
                book[monsterId] = list;
            }

            list.Add(add);
        }

        static void Flush(Dictionary<string, List<LandingAdd>> book, string monsterId, List<LandingAdd> adds)
        {
            if (!book.TryGetValue(monsterId, out var list))
                return;

            for (var i = 0; i < list.Count; i++)
                adds.Add(list[i]);
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
            var run = this.GetModel<RunModel>();
            var monster = run.Find(monsterId);
            var cell = IndexOf(this.GetModel<LevelModel>().Extraction, monsterId);
            _entries.Add(new SettlementLanding(
                monsterId,
                "毒跳伤",
                damage,
                1,
                damage,
                0,
                monster == null ? null : monster.DisplayName,
                cell));
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

        void BindHaste(LevelModel level, RunModel run, SkillCatalog catalog)
        {
            _hasteMonsterId = null;
            _hasteSkillName = null;
            if (_hasteToolName == null)
                return;

            var cells = level.Extraction;
            for (var i = 0; i < cells.Count; i++)
            {
                if (cells[i] == null)
                    continue;

                var monster = run.Find(cells[i]);
                if (monster == null)
                    return;

                var skills = monster.Skills;
                for (var skill = 0; skill < skills.Count; skill++)
                {
                    if (!catalog.ProducesEnergy(skills[skill].Name))
                        continue;

                    _hasteMonsterId = monster.Id;
                    _hasteSkillName = skills[skill].Name;
                    return;
                }

                return;
            }
        }

        string[] AffixEnergySnapshot(RunModel run, SkillCatalog catalog, string monsterId)
        {
            if (_singleAffixToolName == null || !IsSingleAffix(run, catalog, monsterId))
                return null;

            var monster = run.Find(monsterId);
            var names = new List<string>();
            var skills = monster.Skills;
            for (var i = 0; i < skills.Count; i++)
            {
                if (catalog.ProducesEnergy(skills[i].Name))
                    names.Add(skills[i].Name);
            }

            return names.ToArray();
        }

        static int AffixExtra(string[] names, string skillName)
        {
            if (names == null)
                return 0;

            for (var i = 0; i < names.Length; i++)
            {
                if (names[i] == skillName)
                    return 1;
            }

            return 0;
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

        void DoubleRecorded(LevelModel level, string monsterId)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var landing = _entries[i] as SettlementLanding;
                if (landing == null || landing.MonsterId != monsterId || landing.Energy == 0)
                    continue;

                var factors = new List<LandingFactor>(landing.Factors.Count + 1);
                for (var factor = 0; factor < landing.Factors.Count; factor++)
                    factors.Add(landing.Factors[factor]);

                factors.Add(new LandingFactor("孤独心", 2));
                _entries[i] = new SettlementLanding(
                    landing.MonsterId,
                    landing.SkillName,
                    landing.Base,
                    landing.Multiplier * 2,
                    landing.Energy * 2,
                    landing.Writeback,
                    landing.Quote,
                    landing.SideCount,
                    landing.Side,
                    landing.Adds,
                    factors,
                    landing.MonsterName,
                    landing.Cell,
                    landing.CountedSideName);
                level.AddEnergy(landing.Energy);
            }

            _fullDoubles.TryGetValue(monsterId, out var times);
            _fullDoubles[monsterId] = times + 1;
        }

        void GrantRightRow(RunModel run, SkillCatalog catalog, IReadOnlyList<string> cells, int cell)
        {
            var ear = cell + 1;
            if (ear >= cells.Count || cells[ear] == null)
                return;

            var neighbor = run.Find(cells[ear]);
            if (neighbor == null)
                return;

            var skills = neighbor.Skills;
            for (var skillIndex = 0; skillIndex < skills.Count; skillIndex++)
            {
                var amount = catalog.RightRowBonus(skills[skillIndex].Name);
                if (amount == 0)
                    continue;

                var add = new LandingAdd(skills[skillIndex].Name, amount, neighbor.DisplayName);
                for (var index = ear + 1; index < cells.Count; index++)
                {
                    var id = cells[index];
                    if (id == null)
                        continue;

                    Remember(_rowAdds, id, add);
                }
            }
        }

        void GrantSameName(RunModel run, IReadOnlyList<string> cells, string skillName, string selfId)
        {
            for (var index = 0; index < cells.Count; index++)
            {
                var id = cells[index];
                if (id == null || id == selfId)
                    continue;

                var monster = run.Find(id);
                if (monster == null)
                    continue;

                for (var skill = 0; skill < monster.Skills.Count; skill++)
                {
                    if (monster.Skills[skill].Name == skillName)
                        AddExtra(id, skillName, 1);
                }
            }
        }

        void ApplyKin(LevelModel level, RunModel run, SkillCatalog catalog)
        {
            var present = new HashSet<string>();
            var cells = level.Extraction;
            for (var index = 0; index < cells.Count; index++)
            {
                if (cells[index] != null)
                    present.Add(cells[index]);
            }

            var names = new HashSet<string>();
            foreach (var id in present)
            {
                var monster = run.Find(id);
                if (monster == null || !HoldsKin(monster, catalog))
                    continue;

                var parents = monster.ParentIds;
                for (var parentIndex = 0; parentIndex < parents.Count; parentIndex++)
                {
                    if (!present.Contains(parents[parentIndex]))
                        continue;

                    var parent = run.Find(parents[parentIndex]);
                    if (parent == null)
                        continue;

                    for (var skill = 0; skill < parent.Skills.Count; skill++)
                        names.Add(parent.Skills[skill].Name);
                }
            }

            if (names.Count == 0)
                return;

            foreach (var id in present)
            {
                var monster = run.Find(id);
                if (monster == null)
                    continue;

                for (var skill = 0; skill < monster.Skills.Count; skill++)
                {
                    if (names.Contains(monster.Skills[skill].Name))
                        AddExtra(id, monster.Skills[skill].Name, 1);
                }
            }
        }

        static bool HoldsKin(Monster monster, SkillCatalog catalog)
        {
            for (var index = 0; index < monster.Skills.Count; index++)
            {
                if (catalog.HasKin(monster.Skills[index].Name))
                    return true;
            }

            return false;
        }

        void BuffProduce(RunModel run, SkillCatalog catalog, string monsterId, string selfName, int amount, bool permanent)
        {
            var monster = run.Find(monsterId);
            if (monster == null)
                return;

            for (var index = 0; index < monster.Skills.Count; index++)
            {
                var name = monster.Skills[index].Name;
                if (name == selfName || !catalog.IsProduce(name))
                    continue;

                monster.Skills[index].Add(amount, permanent);
            }
        }

        void FillSkills(RunModel run, SkillCatalog catalog, string monsterId)
        {
            var draw = this.GetUtility<IDraw>();
            var guard = 0;
            while (guard++ < 8)
            {
                var monster = run.Find(monsterId);
                if (monster == null || monster.Skills.Count >= 4)
                    return;

                var options = Missing(catalog, monster);
                if (options.Count == 0)
                    return;

                run.TryGainSkill(monsterId, draw.Choose(options));
            }
        }

        void CopyBreeding(LevelModel level, RunModel run, SkillCatalog catalog, string monsterId)
        {
            var draw = this.GetUtility<IDraw>();
            for (var slot = 0; slot < level.BreedingSlotCount; slot++)
            {
                var seats = level.ParentCount(slot);
                for (var seat = 0; seat < seats; seat++)
                {
                    var sourceId = level.ParentAt(slot, seat);
                    if (sourceId == null)
                        continue;

                    var source = run.Find(sourceId);
                    var host = run.Find(monsterId);
                    if (source == null || host == null || host.Skills.Count >= 4)
                        return;

                    var options = new List<string>();
                    for (var index = 0; index < source.Skills.Count; index++)
                    {
                        var name = source.Skills[index].Name;
                        if (!Owns(host, name))
                            options.Add(name);
                    }

                    if (options.Count == 0)
                        continue;

                    run.TryGainSkill(monsterId, draw.Choose(options));
                }
            }
        }

        static List<string> Missing(SkillCatalog catalog, Monster monster)
        {
            var options = new List<string>();
            var names = catalog.Names();
            for (var index = 0; index < names.Count; index++)
            {
                if (!Owns(monster, names[index]))
                    options.Add(names[index]);
            }

            return options;
        }

        static bool Owns(Monster monster, string skillName)
        {
            for (var index = 0; index < monster.Skills.Count; index++)
            {
                if (monster.Skills[index].Name == skillName)
                    return true;
            }

            return false;
        }

        static int Population(LevelModel level, RunModel run)
        {
            var count = run.Cage.Count;
            var cells = level.Extraction;
            for (var index = 0; index < cells.Count; index++)
            {
                if (cells[index] != null)
                    count++;
            }

            for (var slot = 0; slot < level.BreedingSlotCount; slot++)
            {
                var seats = level.ParentCount(slot);
                for (var seat = 0; seat < seats; seat++)
                {
                    if (level.ParentAt(slot, seat) != null)
                        count++;
                }
            }

            return count;
        }

        void AddSelf(
            SkillCatalog catalog,
            RunModel run,
            IReadOnlyList<string> cells,
            int cell,
            string monsterId,
            string skillName,
            List<LandingAdd> adds)
        {
            if (!catalog.ProducesEnergy(skillName))
                return;

            var host = run.Find(monsterId);
            if (host == null)
                return;

            var skills = host.Skills;
            for (var index = 0; index < skills.Count; index++)
            {
                int when;
                int amount;
                if (catalog.TrySkillCountAdd(skills[index].Name, out when, out amount) && skills.Count == when)
                    adds.Add(new LandingAdd(skills[index].Name, amount, host.DisplayName));

                CountedSide side;
                if (catalog.TryEdge(skills[index].Name, out side, out amount) && AtEdge(cells, cell, side))
                    adds.Add(new LandingAdd(skills[index].Name, amount, host.DisplayName));
            }
        }

        static bool AtEdge(IReadOnlyList<string> cells, int cell, CountedSide side)
        {
            if (side == CountedSide.Left)
            {
                for (var index = 0; index < cells.Count; index++)
                {
                    if (cells[index] != null)
                        return index == cell;
                }

                return false;
            }

            for (var index = cells.Count - 1; index >= 0; index--)
            {
                if (cells[index] != null)
                    return index == cell;
            }

            return false;
        }

        int CountExtra(RunModel run, SkillCatalog catalog, string monsterId, string skillName)
        {
            var monster = run.Find(monsterId);
            if (monster == null)
                return 0;

            var extra = 0;
            var skills = monster.Skills;
            for (var index = 0; index < skills.Count; index++)
            {
                int when;
                int walks;
                if (!catalog.TrySkillCountExtra(skills[index].Name, out when, out walks))
                    continue;
                if (skills[index].Name == skillName)
                    continue;
                if (skills.Count == when)
                    extra += walks;
            }

            return extra;
        }

        int PendingExtra(string monsterId, string skillName)
        {
            int extra;
            _skillExtra.TryGetValue(monsterId + "|" + skillName, out extra);
            return extra;
        }

        void AddExtra(string monsterId, string skillName, int amount)
        {
            var key = monsterId + "|" + skillName;
            int current;
            _skillExtra.TryGetValue(key, out current);
            _skillExtra[key] = current + amount;
        }

        int CollectAdds(
            SkillCatalog catalog,
            RunModel run,
            IReadOnlyList<string> cells,
            int cell,
            string monsterId,
            string skillName,
            int modifier,
            List<LandingAdd> adds)
        {
            for (var other = 0; other < cells.Count; other++)
            {
                var otherId = cells[other];
                if (otherId == null || otherId == monsterId)
                    continue;

                var otherMonster = run.Find(otherId);
                var skills = otherMonster.Skills;
                for (var index = 0; index < skills.Count; index++)
                {
                    var amount = catalog.AddedToOthers(skills[index].Name);
                    if (amount == 0)
                        continue;

                    adds.Add(new LandingAdd(skills[index].Name, amount, otherMonster.DisplayName));
                }
            }

            if (modifier != 0)
                adds.Add(new LandingAdd("怪物修正", modifier));
            Flush(_nextAdds, monsterId, adds);
            Flush(_rowAdds, monsterId, adds);
            AddSelf(catalog, run, cells, cell, monsterId, skillName, adds);

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
            string label;
            var value = NeighborEnergyDouble(run, catalog, cells, cell, out label);
            if (value != 1)
            {
                var neighbor = run.Find(cells[cell]);
                factors.Add(new LandingFactor(label, value, neighbor == null ? null : neighbor.DisplayName));
            }

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
            string ignored;
            multiplier *= NeighborEnergyDouble(run, catalog, cells, cell - 1, out ignored);
            multiplier *= NeighborEnergyDouble(run, catalog, cells, cell + 1, out ignored);
            return multiplier;
        }

        static int NeighborEnergyDouble(RunModel run, SkillCatalog catalog, IReadOnlyList<string> cells, int cell, out string label)
        {
            label = null;
            if (cell < 0 || cell >= cells.Count || cells[cell] == null)
                return 1;

            var neighbor = run.Find(cells[cell]);
            if (neighbor == null)
                return 1;

            var skills = neighbor.Skills;
            for (var index = 0; index < skills.Count; index++)
            {
                if (!catalog.DoublesAdjacentEnergy(skills[index].Name))
                    continue;

                label = skills[index].Name;
                return 2;
            }

            return 1;
        }
    }
}
