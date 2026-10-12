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
        readonly Dictionary<string, List<int>> _fullDoubles = new Dictionary<string, List<int>>();
        readonly Dictionary<string, List<LandingAdd>> _rowAdds = new Dictionary<string, List<LandingAdd>>();
        readonly Dictionary<string, List<GrantedExtra>> _skillExtra = new Dictionary<string, List<GrantedExtra>>();
        readonly List<LandingAdd> _legacyAdds = new List<LandingAdd>();
        int _removalSequence;
        bool _sealed;
        string _hasteToolName;
        string _hasteMonsterId;
        string _hasteSkillName;
        string _singleAffixToolName;

        public IReadOnlyList<SettlementEntry> Entries => _entries;

        public PaymentResult Settle()
        {
            var level = this.GetModel<LevelModel>();
            var run = this.GetModel<RunModel>();
            this.GetModel<SettlementSight>().Capture(level, run);
            Score();
            ExecuteSwaps();
            ExecuteEndRemovals();
            var wasOvertime = level.InOvertime;
            var due = level.EnergyDue;
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
                    run.Lose();
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
                var excess = produced >= level.ExcessEnergy;
                if (excess)
                    run.AddTechPoint();

                level.Pay(due);
                var retained = 0;
                if (run.HasTool("蓄热熔炉"))
                    retained = (int)Math.Floor(level.Energy * 0.2);
                run.SetRetainedEnergy(retained);

                level.ClearEnergy();
                var wage = ContentGate.Current.Wage(wasOvertime);
                run.AddGold(wage);
                level.ClearDebt();
                payment = new SettlementPayment(due, 0, wasOvertime, false, excess, wage, retained);
                result = PaymentResult.Paid;
            }

            Seal();
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
            _legacyAdds.Clear();
            _sealed = false;
            _removalSequence = 0;
            var level = this.GetModel<LevelModel>();
            var run = this.GetModel<RunModel>();
            var catalog = this.GetUtility<SkillCatalog>();
            var intents = this.GetUtility<ClockIntents>();
            var tools = this.GetUtility<IToolCatalog>().Tools;
            _hasteToolName = ToolRules.HeldName(tools, run.Tools, ToolEffect.DoubleFirstEnergy);
            _singleAffixToolName = ToolRules.HeldName(tools, run.Tools, ToolEffect.DoubleSingleAffix);
            BindHaste(level, run, catalog);
            GrantPermanentImmovable(run, intents);
            ApplyKin(level, run, catalog);

            for (var i = 0; i < level.Extraction.Count; i++)
            {
                if (level.Extraction[i] != null)
                    run.MarkWorkedToday(level.Extraction[i]);
            }

            for (var i = 0; i < level.Extraction.Count; i++)
            {
                var mId = level.Extraction[i];
                if (mId == null) continue;
                var m = run.Find(mId);
                if (m != null && m.Personality == "自律")
                    Land(level, run, catalog, level.Extraction, i, mId, "自律", 5);
            }

            if (run.HasTool("电化能机"))
                Land(level, run, catalog, level.Extraction, -1, null, "电化能机", 10);

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
            var monsterIds = new List<string>();
            for (var step = 0; step < width; step++)
            {
                var cell = reverse ? width - 1 - step : step;
                if (cell < level.Extraction.Count && level.Extraction[cell] != null)
                    monsterIds.Add(level.Extraction[cell]);
            }

            for (var step = 0; step < monsterIds.Count; step++)
            {
                var mId = monsterIds[step];
                var at = start + step;
                AddTick(ticks, ref order, at, () =>
                {
                    var curCell = IndexOf(level.Extraction, mId);
                    if (curCell < 0)
                        return;

                    ScoreStay(level, run, catalog, level.Extraction, curCell, mId);
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
            var curCell = IndexOf(level.Extraction, monsterId);
            if (curCell < 0)
                return;

            var stayingId = monsterId;
            var echoGrants = NeighborGrants(run, level.Extraction, curCell - 1, catalog.ExtraWalksForRightNeighbor);
            var capacityFromRight = NeighborGrants(run, level.Extraction, curCell + 1, catalog.CapacityExtraForLeftNeighbor);
            var capacityFromLeft = NeighborGrants(run, level.Extraction, curCell - 1, catalog.CapacityExtraForRightNeighbor);
            var affixEnergy = AffixEnergySnapshot(run, catalog, stayingId);
            var points = SecondPointCount(stayingId);
            for (var point = 0; point < points; point++)
            {
                var nowCell = IndexOf(level.Extraction, stayingId);
                if (nowCell < 0)
                    return;

                WalkSkills(level, run, catalog, level.Extraction, nowCell, stayingId);
                if (IndexOf(level.Extraction, stayingId) >= 0)
                    SettlePoison(stayingId);
            }

            var afterCell = IndexOf(level.Extraction, stayingId);
            if (afterCell < 0)
                return;

            var monster = run.Find(stayingId);
            if (monster == null)
                return;

            var skills = monster.Skills.ToArray();
            for (var index = 0; index < skills.Length; index++)
            {
                if (IndexOf(level.Extraction, stayingId) < 0)
                    return;

                var skill = skills[index];
                var grants = new List<ExtraGrant>();
                grants.AddRange(echoGrants);
                if (catalog.ProducesEnergy(skill.Name))
                {
                    grants.AddRange(capacityFromRight);
                    grants.AddRange(capacityFromLeft);
                }

                AppendSkillCountGrants(run, catalog, level.Extraction, stayingId, skill.Name, grants);
                AppendPendingGrants(stayingId, skill.Name, grants);
                if (AffixExtra(affixEnergy, skill.Name) == 1)
                    grants.Add(new ExtraGrant(null, -1, false));

                for (var time = 0; time < grants.Count; time++)
                {
                    var nowCell = IndexOf(level.Extraction, stayingId);
                    if (nowCell < 0)
                        return;

                    var grant = grants[time];
                    if (grant.Cause)
                        _entries.Add(new SettlementCause(grant.SourceId, grant.SkillIndex));

                    ScoreSkill(level, run, catalog, level.Extraction, nowCell, stayingId, skill, index);
                }
            }
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

            var skills = monster.Skills.ToArray();
            for (var index = 0; index < skills.Length; index++)
            {
                var nowCell = IndexOf(level.Extraction, monsterId);
                if (nowCell < 0)
                    return;

                ScoreSkill(level, run, catalog, level.Extraction, nowCell, monsterId, skills[index], index);
            }
        }

        void ScoreSkill(
            LevelModel level,
            RunModel run,
            SkillCatalog catalog,
            IReadOnlyList<string> cells,
            int cell,
            string monsterId,
            SkillInstance skill,
            int skillIndex)
        {
            var skillName = skill.Name;
            var host = run.Find(monsterId);
            if (catalog.IsActive(skillName))
            {
                if (host != null && host.Personality == "随心所欲")
                {
                    if (this.GetUtility<IDraw>().Chance(20))
                    {
                        if (host.Personality == "贪财")
                        {
                            run.AddGold(1);
                            _entries.Add(SettlementMark.Gold(monsterId, 1));
                        }
                        GrantRightRow(run, catalog, cells, cell);
                        _entries.Add(SettlementMark.Miss(monsterId));
                        return;
                    }
                }

                if (host != null && host.Personality == "贪财")
                {
                    run.AddGold(1);
                    _entries.Add(SettlementMark.Gold(monsterId, 1));
                }
                GrantRightRow(run, catalog, cells, cell);
            }

            try
            {
                if (catalog.TryGainCapacity(skillName, out var layers))
                {
                    OpenCapacity(level, run, catalog, cells, cell, monsterId);
                    if (host != null)
                        host.AddCapacity(layers);

                    return;
                }

                if (catalog.SwapsWithLeft(skillName))
                {
                    var curCell = IndexOf(level.Extraction, monsterId);
                    var targetId = curCell > 0 ? level.Extraction[curCell - 1] : null;
                    var actor = run.Find(monsterId);
                    var target = targetId != null ? run.Find(targetId) : null;
                    bool canSwap = target != null && !actor.Immovable && !target.Immovable;
                    if (canSwap)
                    {
                        level.TrySwapExtraction(monsterId, targetId);
                        _entries.Add(new SettlementSwap(monsterId, targetId, true));
                        actor.MakeImmovable(false);

                        var targetSkills = target.Skills;
                        for (var tIdx = 0; tIdx < targetSkills.Count; tIdx++)
                        {
                            var tSkill = targetSkills[tIdx];
                            if (!catalog.IsActive(tSkill.Name))
                                continue;

                            var tCell = IndexOf(level.Extraction, targetId);
                            if (tCell >= 0)
                                ScoreSkill(level, run, catalog, level.Extraction, tCell, targetId, tSkill, tIdx);
                        }
                    }
                    else
                    {
                        _entries.Add(new SettlementSwap(monsterId, targetId, false));
                        if (actor != null)
                            actor.MakeImmovable(false);
                    }

                    var afterSwapCell = IndexOf(level.Extraction, monsterId);
                    if (afterSwapCell >= 0)
                        OpenCapacity(level, run, catalog, level.Extraction, afterSwapCell, monsterId);
                    return;
                }

                if (catalog.TrySelfDestruct(skillName))
                {
                    Land(level, run, catalog, cells, cell, monsterId, skillName, 10);
                    EliminateMonster(monsterId);
                    return;
                }

                if (catalog.TryDevourLeftNeighbor(skillName, out var devourMult))
                {
                    var leftTarget = cell > 0 ? cells[cell - 1] : null;
                    if (leftTarget != null)
                    {
                        var victim = run.Find(leftTarget);
                        var sCount = victim != null ? victim.Skills.Count : 0;
                        EliminateMonster(leftTarget);
                        var nowCell = IndexOf(level.Extraction, monsterId);
                        Land(level, run, catalog, level.Extraction, nowCell, monsterId, skillName, sCount * devourMult);
                    }
                    else
                    {
                        Land(level, run, catalog, cells, cell, monsterId, skillName, 0);
                    }
                    var afterCell = IndexOf(level.Extraction, monsterId);
                    if (afterCell >= 0)
                        OpenCapacity(level, run, catalog, level.Extraction, afterCell, monsterId);
                    return;
                }

                if (catalog.TryKillRandomOther(skillName))
                {
                    var others = new List<string>();
                    for (var i = 0; i < cells.Count; i++)
                    {
                        if (cells[i] != null && cells[i] != monsterId)
                            others.Add(cells[i]);
                    }
                    if (others.Count > 0)
                    {
                        var victim = this.GetUtility<IDraw>().Choose(others);
                        EliminateMonster(victim);
                    }
                    var nowCell = IndexOf(level.Extraction, monsterId);
                    Land(level, run, catalog, level.Extraction, nowCell, monsterId, skillName, 10);
                    if (nowCell >= 0)
                        OpenCapacity(level, run, catalog, level.Extraction, nowCell, monsterId);
                    return;
                }

                if (catalog.TrySacrificeNeighbors(skillName, out var sacrificeQuote))
                {
                    var leftTarget = cell > 0 ? cells[cell - 1] : null;
                    var rightTarget = cell + 1 < cells.Count ? cells[cell + 1] : null;
                    var killed = 0;
                    if (leftTarget != null)
                    {
                        if (EliminateMonster(leftTarget))
                            killed++;
                    }
                    if (rightTarget != null && IndexOf(level.Extraction, rightTarget) >= 0)
                    {
                        if (EliminateMonster(rightTarget))
                            killed++;
                    }
                    var nowCell = IndexOf(level.Extraction, monsterId);
                    Land(level, run, catalog, level.Extraction, nowCell, monsterId, skillName, killed * sacrificeQuote);
                    if (nowCell >= 0)
                        OpenCapacity(level, run, catalog, level.Extraction, nowCell, monsterId);
                    return;
                }

                if (catalog.TryCullUntilAlone(skillName, out var cullQuote))
                {
                    var killed = 0;
                    while (true)
                    {
                        var others = new List<string>();
                        for (var i = 0; i < level.Extraction.Count; i++)
                        {
                            if (level.Extraction[i] != null && level.Extraction[i] != monsterId)
                                others.Add(level.Extraction[i]);
                        }
                        if (others.Count == 0)
                            break;
                        var victim = this.GetUtility<IDraw>().Choose(others);
                        if (EliminateMonster(victim))
                            killed++;
                    }
                    var nowCell = IndexOf(level.Extraction, monsterId);
                    if (killed > 0)
                        Land(level, run, catalog, level.Extraction, nowCell, monsterId, skillName, killed * cullQuote);
                    else
                        Land(level, run, catalog, level.Extraction, nowCell, monsterId, skillName, 0);
                    if (nowCell >= 0)
                        OpenCapacity(level, run, catalog, level.Extraction, nowCell, monsterId);
                    return;
                }

                if (catalog.TryPersonalityCountMultiple(skillName, out var pMult))
                {
                    var seen = new HashSet<string>();
                    for (var i = 0; i < cells.Count; i++)
                    {
                        if (cells[i] == null) continue;
                        var m = run.Find(cells[i]);
                        if (m != null && !string.IsNullOrEmpty(m.Personality))
                            seen.Add(m.Personality);
                    }
                    Land(level, run, catalog, cells, cell, monsterId, skillName, seen.Count * pMult);
                    OpenCapacity(level, run, catalog, cells, cell, monsterId);
                    return;
                }

                if (catalog.DoublesWhenIsolated(skillName))
                {
                    if (!HasNeighbor(cells, cell))
                        DoubleRecorded(level, monsterId, skillIndex);

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
                    _entries.Add(SettlementMark.Gold(monsterId, gold));
                    return;
                }

                if (catalog.FillsSkills(skillName))
                {
                    var gained = FillSkills(run, catalog, monsterId);
                    _entries.Add(gained == 0 ? SettlementMark.Miss(monsterId) : SettlementMark.Skills(monsterId, gained));
                    return;
                }

                if (catalog.CopiesBreeding(skillName))
                {
                    var gained = CopyBreeding(level, run, catalog, monsterId);
                    _entries.Add(gained == 0 ? SettlementMark.Miss(monsterId) : SettlementMark.Skills(monsterId, gained));
                    return;
                }

                if (catalog.TryChance(skillName, out var percent, out _))
                {
                    if (!this.GetUtility<IDraw>().Chance(percent))
                    {
                        _entries.Add(SettlementMark.Miss(monsterId));
                        return;
                    }

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
                    AddNextBonus(run, level.Extraction, cell, skillName, nextBonus, skillIndex);
                    OpenCapacity(level, run, catalog, cells, cell, monsterId);
                    return;
                }

                if (catalog.TryOwnMultiple(skillName, out var factor))
                {
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
                    GrantSameName(run, cells, skillName, monsterId, skillIndex);

                NoteHolds(catalog, monsterId, skillName, skillIndex);
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
            var host = monsterId != null ? run.Find(monsterId) : null;
            var modifier = host == null ? 0 : host.Modifier;
            var adds = new List<LandingAdd>();
            var added = CollectAdds(catalog, run, cells, cell, monsterId, skillName, modifier, adds);

            if (host != null && host.Personality == "依赖" && added != 0)
            {
                adds.Add(LandingAdd.Of("依赖", added, monsterId, -1, host.DisplayName));
                added *= 2;
            }

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

            if (host != null && host.Personality == "孤僻" && !HasNeighbor(cells, cell))
            {
                multiplier *= 2;
                factors.Add(LandingFactor.Of("孤僻", 2, monsterId, -1, host.DisplayName));
            }

            if (host != null && host.Personality == "自私")
            {
                multiplier *= 2;
                factors.Add(LandingFactor.Of("自私", 2, monsterId, -1, host.DisplayName));
            }

            if (run.HasTool("劣胜装置") && host != null && host.Skills.Count == 1)
            {
                multiplier *= 2;
                factors.Add(LandingFactor.Of("劣胜装置", 2, null, -1, null));
            }

            if (catalog.DoublesWhenIsolated(skillName) && !HasNeighbor(cells, cell))
            {
                multiplier *= 2;
                factors.Add(LandingFactor.Of("孤独心", 2, monsterId, SkillIndex(run, monsterId, skillName), null));
            }

            var isIndependent = host != null && host.Personality == "独立";
            if (!isIndependent)
            {
                multiplier *= RecordAdjacent(run, catalog, cells, cell, factors);
            }

            if (_hasteToolName != null && monsterId == _hasteMonsterId && skillName == _hasteSkillName)
            {
                multiplier *= 2;
                factors.Add(LandingFactor.Of(_hasteToolName, 2, null, -1, null));
            }

            if (_singleAffixToolName != null && IsSingleAffix(run, catalog, monsterId))
            {
                multiplier *= 2;
                factors.Add(LandingFactor.Of(_singleAffixToolName, 2, null, -1, null));
            }

            if (monsterId != null && _fullDoubles.TryGetValue(monsterId, out var fullDoubles))
            {
                for (var time = 0; time < fullDoubles.Count; time++)
                {
                    multiplier *= 2;
                    factors.Add(LandingFactor.Of("孤独心", 2, monsterId, fullDoubles[time], null));
                }
            }

            var energy = Math.Max(0, baseValue * multiplier);
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
            if (causeSkill == "汲取鼻")
                return;

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

                    var echoGrants = NeighborGrants(run, cells, cell - 1, catalog.ExtraWalksForRightNeighbor);
                    var times = 1 + echoGrants.Count;
                    for (var t = 0; t < times; t++)
                    {
                        Land(level, run, catalog, cells, cell, id, skills[index].Name, skills[index].Quote);
                    }
                }
            }
        }

        void AddNextBonus(RunModel run, IReadOnlyList<string> cells, int cell, string skillName, int bonus, int skillIndex)
        {
            var next = NextMonster(cells, cell);
            if (next == null)
                return;

            var source = run.Find(cells[cell]);
            Remember(_nextAdds, next, LandingAdd.Of(skillName, bonus, cells[cell], skillIndex, source == null ? null : source.DisplayName));
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
            if (string.IsNullOrEmpty(monsterId))
                return;

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

        bool EliminateMonster(string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId))
            {
                _entries.Add(new SettlementRemoval(monsterId, false));
                return false;
            }

            var level = this.GetModel<LevelModel>();
            var run = this.GetModel<RunModel>();
            var catalog = this.GetUtility<SkillCatalog>();
            var draw = this.GetUtility<IDraw>();

            var cell = IndexOf(level.Extraction, monsterId);
            var victim = run.Find(monsterId);
            if (cell < 0 || victim == null)
            {
                _entries.Add(new SettlementRemoval(monsterId, false));
                return false;
            }

            var victimSkills = new List<string>(victim.Skills.Count);
            for (var i = 0; i < victim.Skills.Count; i++)
                victimSkills.Add(victim.Skills[i].Name);

            level.TryRemove(monsterId);
            if (!run.TryDestroy(monsterId))
            {
                _entries.Add(new SettlementRemoval(monsterId, false));
                return false;
            }

            _entries.Add(new SettlementRemoval(monsterId, true));

            // Legacy from victim (遗赠)
            for (var i = 0; i < victimSkills.Count; i++)
            {
                int legacyAmt;
                if (catalog.TryLegacyAddToOthers(victimSkills[i], out legacyAmt))
                    _legacyAdds.Add(LandingAdd.Of(victimSkills[i], legacyAmt, null, -1, victim.DisplayName));

                if (catalog.HasLegacy(victimSkills[i]))
                {
                    var pool = catalog.Names(DrawnPool.NotAmplifyOnly, Rarity.White, null);
                    if (pool.Count > 0)
                    {
                        var chosenSkill = draw.Choose(pool);
                        run.AddToCage(run.CreateMonster(new[] { chosenSkill }));
                    }
                }
            }

            // Snapshot living listeners in extraction
            var livingIds = new List<string>();
            for (var i = 0; i < level.Extraction.Count; i++)
            {
                if (level.Extraction[i] != null && level.Extraction[i] != monsterId)
                    livingIds.Add(level.Extraction[i]);
            }

            for (var i = 0; i < livingIds.Count; i++)
            {
                var listenerId = livingIds[i];
                var curCell = IndexOf(level.Extraction, listenerId);
                if (curCell < 0)
                    continue;

                var listener = run.Find(listenerId);
                if (listener == null)
                    continue;

                var lSkills = new List<SkillInstance>(listener.Skills);
                for (var sIdx = 0; sIdx < lSkills.Count; sIdx++)
                {
                    var sName = lSkills[sIdx].Name;
                    int corpseQuote;
                    if (catalog.TryCorpseEnergyQuote(sName, out corpseQuote))
                    {
                        var nowCell = IndexOf(level.Extraction, listenerId);
                        if (nowCell >= 0)
                            Land(level, run, catalog, level.Extraction, nowCell, listenerId, sName, corpseQuote);
                    }
                    else if (catalog.TryStealSkillOnOtherDestroyed(sName))
                    {
                        if (victimSkills.Count > 0)
                        {
                            var stolen = draw.Choose(victimSkills);
                            run.TryGainSkill(listenerId, stolen, false);
                        }
                    }
                }
            }

            return true;
        }

        bool RemoveFromPlay(string monsterId) => EliminateMonster(monsterId);

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

        void DoubleRecorded(LevelModel level, string monsterId, int skillIndex)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var landing = _entries[i] as SettlementLanding;
                if (landing == null || landing.MonsterId != monsterId || landing.Energy == 0)
                    continue;

                var factors = new List<LandingFactor>(landing.Factors.Count + 1);
                for (var factor = 0; factor < landing.Factors.Count; factor++)
                    factors.Add(landing.Factors[factor]);

                factors.Add(LandingFactor.Of("孤独心", 2, monsterId, skillIndex, null));
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

            if (!_fullDoubles.TryGetValue(monsterId, out var sources))
            {
                sources = new List<int>();
                _fullDoubles[monsterId] = sources;
            }

            sources.Add(skillIndex);
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
                var amount = catalog.RightNeighborBonus(skills[skillIndex].Name);
                if (amount == 0)
                    amount = catalog.RightRowBonus(skills[skillIndex].Name);
                if (amount == 0)
                    continue;

                var add = LandingAdd.Of(skills[skillIndex].Name, amount, neighbor.Id, skillIndex, neighbor.DisplayName);
                var target = ear + 1;
                if (target < cells.Count && cells[target] != null)
                {
                    Remember(_rowAdds, cells[target], add);
                }
            }
        }

        void GrantSameName(RunModel run, IReadOnlyList<string> cells, string skillName, string selfId, int skillIndex)
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
                        AddExtra(id, skillName, 1, selfId, skillIndex);
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
            var nameOwners = new Dictionary<string, GrantedExtra>();
            foreach (var id in present)
            {
                var monster = run.Find(id);
                if (monster == null || !HoldsKin(monster, catalog))
                    continue;

                var kinIndex = KinIndex(monster, catalog);
                var parents = monster.ParentIds;
                for (var parentIndex = 0; parentIndex < parents.Count; parentIndex++)
                {
                    if (!present.Contains(parents[parentIndex]))
                        continue;

                    var parent = run.Find(parents[parentIndex]);
                    if (parent == null)
                        continue;

                    for (var skill = 0; skill < parent.Skills.Count; skill++)
                    {
                        var skillName = parent.Skills[skill].Name;
                        names.Add(skillName);
                        if (!nameOwners.ContainsKey(skillName))
                            nameOwners[skillName] = new GrantedExtra(monster.Id, kinIndex);
                    }
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
                    {
                        GrantedExtra owner;
                        nameOwners.TryGetValue(monster.Skills[skill].Name, out owner);
                        AddExtra(id, monster.Skills[skill].Name, 1, owner.SourceId, owner.SkillIndex);
                    }
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

        int FillSkills(RunModel run, SkillCatalog catalog, string monsterId)
        {
            var draw = this.GetUtility<IDraw>();
            var gained = 0;
            var guard = 0;
            while (guard++ < 8)
            {
                var monster = run.Find(monsterId);
                if (monster == null || monster.Skills.Count >= 4)
                    return gained;

                var options = Missing(catalog, monster);
                if (options.Count == 0)
                    return gained;

                if (run.TryGainSkill(monsterId, draw.Choose(options)))
                    gained++;
            }

            return gained;
        }

        int CopyBreeding(LevelModel level, RunModel run, SkillCatalog catalog, string monsterId)
        {
            var draw = this.GetUtility<IDraw>();
            var gained = 0;
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
                        return gained;

                    var options = new List<string>();
                    for (var index = 0; index < source.Skills.Count; index++)
                    {
                        var name = source.Skills[index].Name;
                        if (!Owns(host, name))
                            options.Add(name);
                    }

                    if (options.Count == 0)
                        continue;

                    if (run.TryGainSkill(monsterId, draw.Choose(options)))
                        gained++;
                }
            }

            return gained;
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
                    adds.Add(LandingAdd.Of(skills[index].Name, amount, host.Id, index, host.DisplayName));

                CountedSide side;
                if (catalog.TryEdge(skills[index].Name, out side, out amount) && AtEdge(cells, cell, side))
                    adds.Add(LandingAdd.Of(skills[index].Name, amount, host.Id, index, host.DisplayName));
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

        void AddExtra(string monsterId, string skillName, int amount, string sourceId, int skillIndex)
        {
            var key = monsterId + "|" + skillName;
            List<GrantedExtra> list;
            if (!_skillExtra.TryGetValue(key, out list))
            {
                list = new List<GrantedExtra>();
                _skillExtra[key] = list;
            }

            for (var i = 0; i < amount; i++)
                list.Add(new GrantedExtra(sourceId, skillIndex));
        }

        static List<ExtraGrant> NeighborGrants(RunModel run, IReadOnlyList<string> cells, int cell, System.Func<string, int> amount)
        {
            var grants = new List<ExtraGrant>();
            if (cell < 0 || cell >= cells.Count || cells[cell] == null)
                return grants;

            var neighbor = run.Find(cells[cell]);
            if (neighbor == null)
                return grants;

            var skills = neighbor.Skills;
            for (var index = 0; index < skills.Count; index++)
            {
                var times = amount(skills[index].Name);
                for (var n = 0; n < times; n++)
                    grants.Add(new ExtraGrant(neighbor.Id, index, true));
            }

            return grants;
        }

        static void AppendSkillCountGrants(RunModel run, SkillCatalog catalog, IReadOnlyList<string> cells, string monsterId, string skillName, List<ExtraGrant> into)
        {
            var monster = run.Find(monsterId);
            if (monster == null)
                return;

            var skills = monster.Skills;
            for (var index = 0; index < skills.Count; index++)
            {
                int when;
                int walks;
                if (!catalog.TrySkillCountExtra(skills[index].Name, out when, out walks))
                    continue;
                if (skills[index].Name == skillName)
                    continue;
                if (skills.Count != when)
                    continue;

                for (var n = 0; n < walks; n++)
                    into.Add(new ExtraGrant(monster.Id, index, true));
            }

            if (monster.Personality == "安于现状" && catalog.IsActive(skillName))
            {
                into.Add(new ExtraGrant(monster.Id, -1, true));
            }

            if (run.HasTool("劣胜装置") && monster.Skills.Count == 1 && catalog.ProducesEnergy(skillName))
            {
                into.Add(new ExtraGrant(null, -1, true));
            }

            if (cells != null)
            {
                for (var c = 0; c < cells.Count; c++)
                {
                    var otherId = cells[c];
                    if (otherId == null || otherId == monsterId) continue;
                    var other = run.Find(otherId);
                    if (other == null) continue;

                    if (HasParent(other, monster.Id) && other.HasSkill(skillName))
                    {
                        if (monster.Personality == "重视血脉" || other.Personality == "重视血脉")
                            into.Add(new ExtraGrant(other.Personality == "重视血脉" ? other.Id : monster.Id, -1, true));
                    }
                }
            }
        }

        static bool HasParent(Monster monster, string parentId)
        {
            if (monster?.ParentIds == null || parentId == null) return false;
            for (var i = 0; i < monster.ParentIds.Count; i++)
            {
                if (monster.ParentIds[i] == parentId) return true;
            }
            return false;
        }

        void AppendPendingGrants(string monsterId, string skillName, List<ExtraGrant> into)
        {
            List<GrantedExtra> list;
            if (!_skillExtra.TryGetValue(monsterId + "|" + skillName, out list) || list == null)
                return;

            for (var i = 0; i < list.Count; i++)
                into.Add(new ExtraGrant(list[i].SourceId, list[i].SkillIndex, true));
        }

        readonly struct GrantedExtra
        {
            public GrantedExtra(string sourceId, int skillIndex)
            {
                SourceId = sourceId;
                SkillIndex = skillIndex;
            }

            public string SourceId { get; }

            public int SkillIndex { get; }
        }

        readonly struct ExtraGrant
        {
            public ExtraGrant(string sourceId, int skillIndex, bool cause)
            {
                SourceId = sourceId;
                SkillIndex = skillIndex;
                Cause = cause;
            }

            public string SourceId { get; }

            public int SkillIndex { get; }

            public bool Cause { get; }
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
            var host = monsterId != null ? run.Find(monsterId) : null;
            var isIndependent = host != null && host.Personality == "独立";
            var moodCatalog = this.GetUtility<IMoodCatalog>();
            var raceCatalog = this.GetUtility<IRaceCatalog>();
            var hasMindControl = run.HasTool("精神操控仪");

            int PersonalityBonus(int amount) => (amount > 0 && hasMindControl) ? amount + 1 : amount;

            if (host != null && !string.IsNullOrEmpty(host.Mood) && moodCatalog != null)
            {
                var moodDelta = moodCatalog.EnergyDelta(host.Mood);
                if (moodDelta != 0)
                    adds.Add(LandingAdd.Of("心情", moodDelta, monsterId, -1, host.DisplayName));
            }

            if (modifier != 0)
                adds.Add(LandingAdd.Of("怪物修正", modifier, null, -1, null));

            if (!isIndependent)
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

                        adds.Add(LandingAdd.Of(skills[index].Name, amount, otherId, index, otherMonster.DisplayName));
                    }
                }

                for (var i = 0; i < _legacyAdds.Count; i++)
                    adds.Add(_legacyAdds[i]);

                Flush(_nextAdds, monsterId, adds);
                Flush(_rowAdds, monsterId, adds);
            }

            AddSelf(catalog, run, cells, cell, monsterId, skillName, adds);

            if (host != null && cell >= 0 && cell < cells.Count)
            {
                var leftmost = -1;
                var rightmost = -1;
                for (var i = 0; i < cells.Count; i++)
                {
                    if (cells[i] != null)
                    {
                        if (leftmost == -1) leftmost = i;
                        rightmost = i;
                    }
                }

                var leftNeighborId = cell > 0 ? cells[cell - 1] : null;
                var rightNeighborId = cell + 1 < cells.Count ? cells[cell + 1] : null;
                var neighborCount = (leftNeighborId != null ? 1 : 0) + (rightNeighborId != null ? 1 : 0);

                if (host.Personality == "急躁" && cell == leftmost)
                    adds.Add(LandingAdd.Of("急躁", PersonalityBonus(3), monsterId, -1, host.DisplayName));

                if (host.Personality == "稳重" && cell == rightmost)
                    adds.Add(LandingAdd.Of("稳重", PersonalityBonus(3), monsterId, -1, host.DisplayName));

                if (host.Personality == "合群" && neighborCount > 0)
                    adds.Add(LandingAdd.Of("合群", PersonalityBonus(neighborCount), monsterId, -1, host.DisplayName));

                if (host.Personality == "恋爱脑" && neighborCount == 1)
                    adds.Add(LandingAdd.Of("恋爱脑", PersonalityBonus(2), monsterId, -1, host.DisplayName));

                if (leftNeighborId != null)
                {
                    var leftMonster = run.Find(leftNeighborId);
                    if (leftMonster != null)
                    {
                        if (leftMonster.Personality == "恋爱脑")
                        {
                            var leftNeighborCount = (cell - 1 > 0 && cells[cell - 2] != null ? 1 : 0) + 1;
                            if (leftNeighborCount == 1)
                                adds.Add(LandingAdd.Of("恋爱脑", PersonalityBonus(2), leftMonster.Id, -1, leftMonster.DisplayName));
                        }
                        if (leftMonster.Personality == "乐于助人")
                            adds.Add(LandingAdd.Of("乐于助人", PersonalityBonus(2), leftMonster.Id, -1, leftMonster.DisplayName));
                        if (leftMonster.Personality == "自私")
                            adds.Add(LandingAdd.Of("自私", -3, leftMonster.Id, -1, leftMonster.DisplayName));
                    }
                }

                if (rightNeighborId != null)
                {
                    var rightMonster = run.Find(rightNeighborId);
                    if (rightMonster != null)
                    {
                        if (rightMonster.Personality == "恋爱脑")
                        {
                            var rightNeighborCount = 1 + (cell + 2 < cells.Count && cells[cell + 2] != null ? 1 : 0);
                            if (rightNeighborCount == 1)
                                adds.Add(LandingAdd.Of("恋爱脑", PersonalityBonus(2), rightMonster.Id, -1, rightMonster.DisplayName));
                        }
                        if (rightMonster.Personality == "乐于助人")
                            adds.Add(LandingAdd.Of("乐于助人", PersonalityBonus(2), rightMonster.Id, -1, rightMonster.DisplayName));
                        if (rightMonster.Personality == "自私")
                            adds.Add(LandingAdd.Of("自私", -3, rightMonster.Id, -1, rightMonster.DisplayName));
                    }
                }

                for (var c = 0; c < cells.Count; c++)
                {
                    var otherId = cells[c];
                    if (otherId == null || otherId == monsterId) continue;
                    var other = run.Find(otherId);
                    if (other == null) continue;

                    if (HasParent(host, otherId) && other.HasSkill(skillName))
                    {
                        if (host.Personality == "仇恨血脉")
                            adds.Add(LandingAdd.Of("仇恨血脉", -3, host.Id, -1, host.DisplayName));
                        if (other.Personality == "仇恨血脉")
                            adds.Add(LandingAdd.Of("仇恨血脉", -3, other.Id, -1, other.DisplayName));
                    }
                }

                if (run.HasTool("小喇叭") && cell == rightmost)
                    adds.Add(LandingAdd.Of("小喇叭", 3, null, -1, null));

                if (run.HasTool("情绪收割机") && moodCatalog != null && moodCatalog.TierOf(host.Mood) >= 2)
                    adds.Add(LandingAdd.Of("情绪收割机", 2, null, -1, null));

                if (run.HasTool("五行罗盘") && raceCatalog != null)
                {
                    var pairCount = 0;
                    if (leftNeighborId != null)
                    {
                        var leftMonster = run.Find(leftNeighborId);
                        if (leftMonster != null && raceCatalog.IsGenerating(host.Race, leftMonster.Race))
                            pairCount++;
                    }
                    if (rightNeighborId != null)
                    {
                        var rightMonster = run.Find(rightNeighborId);
                        if (rightMonster != null && raceCatalog.IsGenerating(host.Race, rightMonster.Race))
                            pairCount++;
                    }
                    if (pairCount > 0)
                        adds.Add(LandingAdd.Of("五行罗盘", 3 * pairCount, null, -1, null));
                }
            }

            if (run.HasTool("稀有度检测仪"))
            {
                var goldSkills = 0;
                for (var c = 0; c < cells.Count; c++)
                {
                    if (cells[c] == null) continue;
                    var m = run.Find(cells[c]);
                    if (m == null) continue;
                    for (var s = 0; s < m.Skills.Count; s++)
                    {
                        if (catalog.RarityOf(m.Skills[s].Name) == Rarity.Gold)
                            goldSkills++;
                    }
                }
                if (goldSkills > 0)
                    adds.Add(LandingAdd.Of("稀有度检测仪", goldSkills, null, -1, null));
            }

            var added = 0;
            for (var i = 0; i < adds.Count; i++)
                added += adds[i].Amount;

            return added;
        }

        int RecordAdjacent(RunModel run, SkillCatalog catalog, IReadOnlyList<string> cells, int cell, List<LandingFactor> factors)
        {
            if (cell < 0 || cell >= cells.Count)
                return 1;

            var multiplier = 1;
            multiplier *= RecordNeighbor(run, catalog, cells, cell - 1, factors);
            multiplier *= RecordNeighbor(run, catalog, cells, cell + 1, factors);
            return multiplier;
        }

        static int RecordNeighbor(RunModel run, SkillCatalog catalog, IReadOnlyList<string> cells, int cell, List<LandingFactor> factors)
        {
            string label;
            string sourceId;
            int skillIndex;
            var value = NeighborEnergyDouble(run, catalog, cells, cell, out label, out sourceId, out skillIndex);
            if (value != 1)
            {
                var neighbor = run.Find(cells[cell]);
                factors.Add(LandingFactor.Of(label, value, sourceId, skillIndex, neighbor == null ? null : neighbor.DisplayName));
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
            if (cell < 0 || cell >= cells.Count)
                return 1;

            var multiplier = 1;
            string ignored;
            string ignoredId;
            int ignoredIndex;
            multiplier *= NeighborEnergyDouble(run, catalog, cells, cell - 1, out ignored, out ignoredId, out ignoredIndex);
            multiplier *= NeighborEnergyDouble(run, catalog, cells, cell + 1, out ignored, out ignoredId, out ignoredIndex);
            return multiplier;
        }

        static int NeighborEnergyDouble(
            RunModel run,
            SkillCatalog catalog,
            IReadOnlyList<string> cells,
            int cell,
            out string label,
            out string sourceId,
            out int skillIndex)
        {
            label = null;
            sourceId = null;
            skillIndex = -1;
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
                sourceId = neighbor.Id;
                skillIndex = index;
                return 2;
            }

            return 1;
        }

        void NoteHolds(SkillCatalog catalog, string monsterId, string skillName, int skillIndex)
        {
            var effects = catalog.Effects(skillName);
            for (var i = 0; i < effects.Count; i++)
            {
                var spec = BeatBook.Spec(effects[i].Kind);
                if (spec.Site == BeatSite.Carry || spec.Site == BeatSite.Cause)
                    _entries.Add(new SettlementHold(monsterId, skillIndex));
            }
        }

        static int SkillIndex(RunModel run, string monsterId, string skillName)
        {
            var monster = run.Find(monsterId);
            if (monster == null)
                return -1;

            for (var index = 0; index < monster.Skills.Count; index++)
            {
                if (monster.Skills[index].Name == skillName)
                    return index;
            }

            return -1;
        }

        static string SkillNameOf(RunModel run, string monsterId, int skillIndex)
        {
            var monster = run.Find(monsterId);
            if (monster == null || skillIndex < 0 || skillIndex >= monster.Skills.Count)
                return null;

            return monster.Skills[skillIndex].Name;
        }

        static int KinIndex(Monster monster, SkillCatalog catalog)
        {
            for (var index = 0; index < monster.Skills.Count; index++)
            {
                if (catalog.HasKin(monster.Skills[index].Name))
                    return index;
            }

            return -1;
        }

        internal void Seal()
        {
            if (_sealed)
                return;

            var run = this.GetModel<RunModel>();
            var catalog = this.GetUtility<SkillCatalog>();
            var causeUsed = new bool[_entries.Count];
            var landingUsed = new bool[_entries.Count];
            var addUsed = new HashSet<long>();
            var factorUsed = new HashSet<long>();
            var drop = new bool[_entries.Count];
            for (var i = 0; i < _entries.Count; i++)
            {
                var hold = _entries[i] as SettlementHold;
                if (hold != null && Spent(run, catalog, hold, i, causeUsed, addUsed, factorUsed, landingUsed))
                    drop[i] = true;
            }

            var next = new List<SettlementEntry>(_entries.Count);
            for (var i = 0; i < _entries.Count; i++)
            {
                if (drop[i])
                    continue;

                var hold = _entries[i] as SettlementHold;
                if (hold != null)
                {
                    next.Add(SettlementMark.Miss(hold.ActorId));
                    continue;
                }

                var cause = _entries[i] as SettlementCause;
                if (cause != null)
                {
                    next.Add(SettlementMark.Again(cause.SourceId, SkillNameOf(run, cause.SourceId, cause.SkillIndex)));
                    continue;
                }

                next.Add(_entries[i]);
            }

            _entries.Clear();
            _entries.AddRange(next);
            _sealed = true;
        }

        bool Spent(
            RunModel run,
            SkillCatalog catalog,
            SettlementHold hold,
            int holdIndex,
            bool[] causeUsed,
            HashSet<long> addUsed,
            HashSet<long> factorUsed,
            bool[] landingUsed)
        {
            var monster = run.Find(hold.ActorId);
            if (monster == null || hold.SkillIndex < 0 || hold.SkillIndex >= monster.Skills.Count)
                return false;

            var skillName = monster.Skills[hold.SkillIndex].Name;
            var effects = catalog.Effects(skillName);
            var proofs = new List<BeatSpec>();
            for (var i = 0; i < effects.Count; i++)
            {
                var spec = BeatBook.Spec(effects[i].Kind);
                if (spec.Site == BeatSite.Carry || spec.Site == BeatSite.Cause)
                    proofs.Add(spec);
            }

            if (proofs.Count == 0)
                return false;

            var prior = 0;
            for (var i = 0; i < holdIndex; i++)
            {
                var earlier = _entries[i] as SettlementHold;
                if (earlier != null && earlier.ActorId == hold.ActorId && earlier.SkillIndex == hold.SkillIndex)
                    prior++;
            }

            var specForHold = proofs[prior % proofs.Count];
            if (specForHold.Site == BeatSite.Cause)
                return TakeCause(hold, causeUsed);
            if (specForHold.Proof == BeatProof.AddOnSelf)
                return TakeAdd(hold, true, false, addUsed);
            if (specForHold.Proof == BeatProof.AddFromSelf)
                return TakeAdd(hold, false, true, addUsed);
            if (specForHold.Proof == BeatProof.FactorFromSelf)
                return TakeFactor(hold, factorUsed);
            if (specForHold.Proof == BeatProof.OwnLanding)
                return TakeOwn(hold, skillName, landingUsed);

            return false;
        }

        bool TakeCause(SettlementHold hold, bool[] used)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (used[i])
                    continue;

                var cause = _entries[i] as SettlementCause;
                if (cause == null || cause.SourceId != hold.ActorId || cause.SkillIndex != hold.SkillIndex)
                    continue;

                used[i] = true;
                return true;
            }

            return false;
        }

        bool TakeAdd(SettlementHold hold, bool onSelf, bool excludeActor, HashSet<long> used)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var landing = _entries[i] as SettlementLanding;
                if (landing == null)
                    continue;
                if (onSelf && landing.MonsterId != hold.ActorId)
                    continue;
                if (excludeActor && landing.MonsterId == hold.ActorId)
                    continue;

                for (var addIndex = 0; addIndex < landing.Adds.Count; addIndex++)
                {
                    var add = landing.Adds[addIndex];
                    if (add.SourceId != hold.ActorId || add.SourceSkillIndex != hold.SkillIndex)
                        continue;

                    var key = ((long)i << 32) + addIndex;
                    if (!used.Add(key))
                        continue;

                    return true;
                }
            }

            return false;
        }

        bool TakeFactor(SettlementHold hold, HashSet<long> used)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var landing = _entries[i] as SettlementLanding;
                if (landing == null)
                    continue;

                for (var factorIndex = 0; factorIndex < landing.Factors.Count; factorIndex++)
                {
                    var factor = landing.Factors[factorIndex];
                    if (factor.SourceId != hold.ActorId || factor.SourceSkillIndex != hold.SkillIndex)
                        continue;

                    var key = ((long)i << 32) + factorIndex;
                    if (!used.Add(key))
                        continue;

                    return true;
                }
            }

            return false;
        }

        bool TakeOwn(SettlementHold hold, string skillName, bool[] used)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (used[i])
                    continue;

                var landing = _entries[i] as SettlementLanding;
                if (landing == null || landing.MonsterId != hold.ActorId || landing.SkillName != skillName)
                    continue;

                used[i] = true;
                return true;
            }

            return false;
        }
    }
}
