using System.Collections.Generic;

namespace TheCall
{
    // 悬停句子里的能量数。加项先加进报价，倍率再乘。只算摆上去就已经成立的那部分。
    internal static class SkillReadout
    {
        static SettlementSight _sight;

        public static SentenceSpan[] Render(
            SkillDef skill,
            int quote,
            string monsterId,
            RunModel run,
            LevelModel level,
            SkillCatalog catalog,
            IReadOnlyList<ToolDefinition> tools,
            SettlementSight sight = null)
        {
            var previous = _sight;
            _sight = sight;
            try
            {
                return RenderNow(skill, quote, monsterId, run, level, catalog, tools);
            }
            finally
            {
                _sight = previous;
            }
        }

        static SentenceSpan[] RenderNow(
            SkillDef skill,
            int quote,
            string monsterId,
            RunModel run,
            LevelModel level,
            SkillCatalog catalog,
            IReadOnlyList<ToolDefinition> tools)
        {
            var pieces = SkillSentences.Pieces(skill);
            var cells = CellsOf(level);
            var cell = CellOf(cells, monsterId);
            var adds = StandingAdds(catalog, run, cells, cell, monsterId, catalog.ProducesEnergy(skill.Name), tools);
            var mult = Multiplier(catalog, run, cells, cell, monsterId, skill.Name, tools);
            var spans = new List<SentenceSpan>(pieces.Length + 2);
            for (var i = 0; i < pieces.Length; i++)
            {
                var piece = pieces[i];
                if (piece.Kind == FigureKind.None)
                {
                    spans.Add(new SentenceSpan(piece.Text, false));
                    continue;
                }

                if (piece.Kind == FigureKind.Energy)
                {
                    var shown = (quote + adds) * mult;
                    spans.Add(new SentenceSpan(shown.ToString(), shown != piece.Energy));
                    continue;
                }

                if (piece.Kind == FigureKind.PerHead)
                {
                    var shown = PerHead(quote, adds, mult, SideCount(catalog, cells, cell, skill.Name));
                    spans.Add(new SentenceSpan(shown.ToString(), shown != piece.Energy));
                    continue;
                }

                if (piece.Kind == FigureKind.Population)
                {
                    var population = Population(level, run);
                    var shown = (population + quote + adds) * mult;
                    spans.Add(new SentenceSpan(shown.ToString(), shown != population));
                    continue;
                }

                if (piece.Kind == FigureKind.SkillMultiple)
                {
                    int factor;
                    catalog.TryOwnMultiple(skill.Name, out factor);
                    var bare = SkillCount(run, monsterId) * factor;
                    var shown = (bare + quote + adds) * mult;
                    spans.Add(new SentenceSpan("（", false));
                    spans.Add(new SentenceSpan(shown.ToString(), shown != bare));
                    spans.Add(new SentenceSpan("）", false));
                }
            }

            return spans.ToArray();
        }

        // 演出进行时读开场记下的那一排，换位和消失改这一排，不读已经写完的结算结果。
        static IReadOnlyList<string> CellsOf(LevelModel level) =>
            _sight != null && _sight.Showing ? _sight.Cells : level.Extraction;

        static Monster FindMonster(RunModel run, string monsterId)
        {
            if (_sight != null && _sight.Showing)
            {
                var cast = _sight.Find(monsterId);
                if (cast != null)
                    return cast;
            }

            return run.Find(monsterId);
        }

        // 平加不按只数摊。一侧恰好一只时，句子里的单价就是这一下的全部，可以并进去。
        static int PerHead(int quote, int adds, int mult, int sideCount)
        {
            if (sideCount == 1)
                return (quote + adds) * mult;

            return quote * mult;
        }

        static int StandingAdds(
            SkillCatalog catalog,
            RunModel run,
            IReadOnlyList<string> cells,
            int cell,
            string monsterId,
            bool selfBuffs,
            IReadOnlyList<ToolDefinition> tools)
        {
            var host = FindMonster(run, monsterId);
            if (host == null)
                return 0;

            var added = host.Modifier;
            var skills = host.Skills;
            if (selfBuffs)
            {
                for (var index = 0; index < skills.Count; index++)
                {
                    int when;
                    int amount;
                    if (catalog.TrySkillCountAdd(skills[index].Name, out when, out amount) && skills.Count == when)
                        added += amount;
                }
            }

            if (cell < 0)
                return added;

            for (var other = 0; other < cells.Count; other++)
            {
                var otherId = cells[other];
                if (otherId == null || otherId == monsterId)
                    continue;

                var otherMonster = FindMonster(run, otherId);
                if (otherMonster == null)
                    continue;

                var others = otherMonster.Skills;
                for (var index = 0; index < others.Count; index++)
                    added += catalog.AddedToOthers(others[index].Name);
            }

            if (selfBuffs)
            {
                for (var index = 0; index < skills.Count; index++)
                {
                    CountedSide side;
                    int amount;
                    if (catalog.TryEdge(skills[index].Name, out side, out amount) && AtEdge(cells, cell, side))
                        added += amount;
                }
            }

            added += NextBonus(catalog, run, cells, monsterId);
            added += RowBonus(catalog, run, cells, cell, tools);
            return added;
        }

        static int NextBonus(SkillCatalog catalog, RunModel run, IReadOnlyList<string> cells, string monsterId)
        {
            var added = 0;
            for (var index = 0; index < cells.Count; index++)
            {
                var id = cells[index];
                if (id == null || id == monsterId || NextMonster(cells, index) != monsterId)
                    continue;

                var donor = FindMonster(run, id);
                if (donor == null)
                    continue;

                var skills = donor.Skills;
                for (var skill = 0; skill < skills.Count; skill++)
                    added += catalog.NextEnergyBonus(skills[skill].Name);
            }

            return added;
        }

        static int RowBonus(
            SkillCatalog catalog,
            RunModel run,
            IReadOnlyList<string> cells,
            int cell,
            IReadOnlyList<ToolDefinition> tools)
        {
            var added = 0;
            for (var ear = 0; ear < cell; ear++)
            {
                var earId = cells[ear];
                if (earId == null || ear == 0 || cells[ear - 1] == null)
                    continue;

                var earMonster = FindMonster(run, earId);
                if (earMonster == null || FindMonster(run, cells[ear - 1]) == null)
                    continue;

                var bonus = 0;
                var earSkills = earMonster.Skills;
                for (var index = 0; index < earSkills.Count; index++)
                    bonus += catalog.RightRowBonus(earSkills[index].Name);
                if (bonus == 0)
                    continue;

                added += bonus * ActivePushes(catalog, run, cells, ear - 1, tools);
            }

            return added;
        }

        // 传能耳在左侧每张主动技能执行时加一次。再走的次数在停留开始就能确定。
        static int ActivePushes(
            SkillCatalog catalog,
            RunModel run,
            IReadOnlyList<string> cells,
            int cell,
            IReadOnlyList<ToolDefinition> tools)
        {
            var monster = FindMonster(run, cells[cell]);
            if (monster == null)
                return 0;

            var echoed = 0;
            var capacity = 0;
            if (cell > 0 && cells[cell - 1] != null)
            {
                echoed += SumSkills(run, cells[cell - 1], catalog.ExtraWalksForRightNeighbor);
                capacity += SumSkills(run, cells[cell - 1], catalog.CapacityExtraForRightNeighbor);
            }

            if (cell + 1 < cells.Count && cells[cell + 1] != null)
                capacity += SumSkills(run, cells[cell + 1], catalog.CapacityExtraForLeftNeighbor);

            var affixWalk = ToolRules.HeldName(tools, run.Tools, ToolEffect.DoubleSingleAffix) != null &&
                IsSingleAffix(catalog, run, monster.Id);

            var pushes = 0;
            var skills = monster.Skills;
            for (var index = 0; index < skills.Count; index++)
            {
                if (!catalog.IsActive(skills[index].Name))
                    continue;

                var times = 1 + echoed + SkillCountExtra(catalog, monster, skills[index].Name);
                if (catalog.ProducesEnergy(skills[index].Name))
                {
                    times += capacity;
                    if (affixWalk)
                        times++;
                }

                pushes += times;
            }

            return pushes;
        }

        static int SkillCountExtra(SkillCatalog catalog, Monster monster, string skillName)
        {
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

        static int SumSkills(RunModel run, string monsterId, System.Func<string, int> amount)
        {
            var monster = FindMonster(run, monsterId);
            if (monster == null)
                return 0;

            var extra = 0;
            var skills = monster.Skills;
            for (var index = 0; index < skills.Count; index++)
                extra += amount(skills[index].Name);

            return extra;
        }

        static int Multiplier(
            SkillCatalog catalog,
            RunModel run,
            IReadOnlyList<string> cells,
            int cell,
            string monsterId,
            string skillName,
            IReadOnlyList<ToolDefinition> tools)
        {
            if (cell < 0)
                return 1;

            var mult = 1;
            if (!HasNeighbor(cells, cell) && HasIsolatedDouble(catalog, run, monsterId))
                mult *= 2;

            mult *= NeighborDouble(catalog, run, cells, cell - 1);
            mult *= NeighborDouble(catalog, run, cells, cell + 1);
            if (ToolRules.HeldName(tools, run.Tools, ToolEffect.DoubleSingleAffix) != null &&
                IsSingleAffix(catalog, run, monsterId))
                mult *= 2;

            string hasteMonster;
            string hasteSkill;
            if (ToolRules.HeldName(tools, run.Tools, ToolEffect.DoubleFirstEnergy) != null &&
                TryFirstEnergy(catalog, run, cells, out hasteMonster, out hasteSkill) &&
                hasteMonster == monsterId &&
                hasteSkill == skillName)
                mult *= 2;

            return mult;
        }

        static bool TryFirstEnergy(
            SkillCatalog catalog,
            RunModel run,
            IReadOnlyList<string> cells,
            out string monsterId,
            out string skillName)
        {
            monsterId = null;
            skillName = null;
            for (var index = 0; index < cells.Count; index++)
            {
                if (cells[index] == null)
                    continue;

                var monster = FindMonster(run, cells[index]);
                if (monster == null)
                    return false;

                var skills = monster.Skills;
                for (var skill = 0; skill < skills.Count; skill++)
                {
                    if (!catalog.ProducesEnergy(skills[skill].Name))
                        continue;

                    monsterId = monster.Id;
                    skillName = skills[skill].Name;
                    return true;
                }

                return false;
            }

            return false;
        }

        static bool IsSingleAffix(SkillCatalog catalog, RunModel run, string monsterId)
        {
            var monster = FindMonster(run, monsterId);
            if (monster == null)
                return false;

            var seen = SkillAffix.None;
            var skills = monster.Skills;
            for (var index = 0; index < skills.Count; index++)
                seen |= catalog.Affixes(skills[index].Name);

            var bits = (int)seen;
            return bits != 0 && (bits & (bits - 1)) == 0;
        }

        static bool HasIsolatedDouble(SkillCatalog catalog, RunModel run, string monsterId)
        {
            var monster = FindMonster(run, monsterId);
            if (monster == null)
                return false;

            var skills = monster.Skills;
            for (var index = 0; index < skills.Count; index++)
            {
                if (catalog.DoublesWhenIsolated(skills[index].Name))
                    return true;
            }

            return false;
        }

        static int NeighborDouble(SkillCatalog catalog, RunModel run, IReadOnlyList<string> cells, int cell)
        {
            if (cell < 0 || cell >= cells.Count || cells[cell] == null)
                return 1;

            var neighbor = FindMonster(run, cells[cell]);
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

        static int SideCount(SkillCatalog catalog, IReadOnlyList<string> cells, int cell, string skillName)
        {
            if (cell < 0 || !catalog.TrySideCount(skillName, out var side, out _))
                return -1;

            return CountSide(cells, cell, side);
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

        static bool HasNeighbor(IReadOnlyList<string> cells, int cell)
        {
            if (cell > 0 && cells[cell - 1] != null)
                return true;

            return cell + 1 < cells.Count && cells[cell + 1] != null;
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

        static int Population(LevelModel level, RunModel run)
        {
            var count = run.Cage.Count;
            var cells = CellsOf(level);
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

        static string NextMonster(IReadOnlyList<string> cells, int cell)
        {
            for (var index = cell + 1; index < cells.Count; index++)
            {
                if (cells[index] != null)
                    return cells[index];
            }

            return null;
        }

        static int CellOf(IReadOnlyList<string> cells, string monsterId)
        {
            for (var index = 0; index < cells.Count; index++)
            {
                if (cells[index] == monsterId)
                    return index;
            }

            return -1;
        }

        static int SkillCount(RunModel run, string monsterId)
        {
            var monster = FindMonster(run, monsterId);
            return monster == null ? 0 : monster.Skills.Count;
        }
    }
}
