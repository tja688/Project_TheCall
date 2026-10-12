using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class FlowSystem : AbstractSystem
    {
        protected override void OnInit()
        {
            var run = this.GetModel<RunModel>();
            var catalog = this.GetUtility<SkillCatalog>();
            var draw = this.GetUtility<IDraw>();
            var personalities = this.GetUtility<IPersonalityCatalog>();
            var races = this.GetUtility<IRaceCatalog>();
            for (var i = 0; i < 3; i++)
            {
                var p = personalities.Draw(draw);
                var r = races.Draw(draw);
                run.AddCandidate(StockDraw.OpeningPair(catalog, draw), p, r);
            }
        }

        internal Monster GenerateSingleMonster()
        {
            var draw = this.GetUtility<IDraw>();
            var personality = this.GetUtility<IPersonalityCatalog>().Draw(draw);
            var race = this.GetUtility<IRaceCatalog>().Draw(draw);
            var name = StockDraw.One(this.GetUtility<SkillCatalog>(), draw, DrawnPool.NoAmplifyBit, null);
            return this.GetModel<RunModel>().CreateMonster(new[] { name }, personality: personality, race: race, sourceKind: SkillSourceKind.SkillEffect);
        }

        public void Keep(string monsterId)
        {
            var run = this.GetModel<RunModel>();
            if (!run.TryTakeCandidate(monsterId, out var kept))
                return;

            run.AddToCage(kept);
            var catalog = this.GetUtility<SkillCatalog>();
            var draw = this.GetUtility<IDraw>();
            var personalities = this.GetUtility<IPersonalityCatalog>();
            var races = this.GetUtility<IRaceCatalog>();
            for (var i = 0; i < 8; i++)
            {
                var p = personalities.Draw(draw);
                var r = races.Draw(draw);
                run.AddToCage(run.CreateMonster(new[] { StockDraw.One(catalog, draw, i < 4 ? DrawnPool.Produce : DrawnPool.Support, null) }, personality: p, race: r, sourceKind: SkillSourceKind.Opening));
            }

            run.EnterOperation(1);
            var levels = this.GetUtility<ILevelCatalog>();
            this.GetModel<LevelModel>().BeginLevel(levels.EnergyDue(1), levels.ExcessEnergy(1), run.RetainedEnergy);
            run.SetRetainedEnergy(0);
            FitBreeding(run);
            FitExtraction(run);
            TriggerStartPhase(run);
        }

        public void BeginLevel()
        {
            var run = this.GetModel<RunModel>();
            if (run.Phase == RunPhase.LevelStart)
            {
                run.EnterOperation(run.LevelNumber);
                TriggerStartPhase(run);
            }
        }

        public bool Commit(OperationDrop drop)
        {
            var run = this.GetModel<RunModel>();
            if (!EnterOperation(run))
                return false;

            var level = this.GetModel<LevelModel>();
            var facts = Facts(run, level);
            if (!OperationTransition.TryPlan(facts, drop, out var plan))
            {
                run.SetOperationFailure(RejectReason(run, drop));
                return false;
            }
            run.SetOperationFailure(null);

            if (!Apply(plan, run, level))
                return false;

            if (plan.Effect == TransitionEffect.Move || plan.Effect == TransitionEffect.Replace || plan.Effect == TransitionEffect.Exchange)
            {
                if (plan.ToArea == OperationArea.Extraction) HandleClockIn(plan.SourceMonsterId);
                if (plan.Effect == TransitionEffect.Exchange && plan.FromArea == OperationArea.Extraction) HandleClockIn(plan.OtherMonsterId);
            }

            return true;
        }

        public void Place(string monsterId, OperationArea area, int cell)
        {
            var run = this.GetModel<RunModel>();
            if (!EnterOperation(run))
                return;

            var level = this.GetModel<LevelModel>();
            if (level.Occupies(monsterId) || !level.CanPlace(area, cell))
                return;
            if (!run.TryRemoveFromCage(monsterId, out _))
                return;

            level.Put(area, cell, monsterId);
            if (area == OperationArea.Extraction)
                HandleClockIn(monsterId);
        }

        public void ReturnToCage(string monsterId)
        {
            var run = this.GetModel<RunModel>();
            if (!EnterOperation(run))
                return;
            if (!this.GetModel<LevelModel>().TryRemove(monsterId))
                return;

            run.AddToCage(run.Find(monsterId));
        }

        public void Discard(string monsterId)
        {
            var run = this.GetModel<RunModel>();
            if (!EnterOperation(run))
                return;

            var monster = run.Find(monsterId);
            if (monster == null)
                return;

            if (run.GeneSlots.Count >= run.GeneCapacity)
                return;

            var owned = new string[monster.Skills.Count];
            for (var i = 0; i < owned.Length; i++)
                owned[i] = monster.Skills[i].Name;

            var skillName = this.GetUtility<IDraw>().Choose(owned);
            this.GetModel<LevelModel>().TryRemove(monsterId);
            if (!run.TryDestroy(monsterId))
                return;

            run.PutSkillInSlot(skillName);
            if (run.HasTool("研磨机"))
                run.AddGold(5);
        }

        public bool InstallGene(string monsterId, int geneSlotIndex)
        {
            var run = this.GetModel<RunModel>();
            if (!EnterOperation(run))
                return false;

            var monster = run.Find(monsterId);
            if (monster == null || monster.Personality != "迷恋死亡") return false;
            if (geneSlotIndex < 0 || geneSlotIndex >= run.GeneSlots.Count) return false;
            var slot = run.GeneSlots[geneSlotIndex];
            if (slot.IsReserved || run.SkillGainFailure(monsterId, slot.SkillName) != null) return false;
            var result = run.GainSkill(monsterId, slot.SkillName, false, SkillSourceKind.Gene, slot.Id);
            if (!result.Success) return false;
            run.TryTakeSkillAt(geneSlotIndex, out _);
            return true;
        }

        public void Unlock(string name)
        {
            var run = this.GetModel<RunModel>();
            if (!EnterOperation(run) || !this.GetUtility<TechCatalog>().Contains(name) || run.HasTech(name))
                return;
            if (!run.TrySpendTechPoint(1))
                return;

            run.AddUnlockedTech(name);
            FitBreeding(run);
        }

        public void PlaceBreedingSkill(int slot, int skillSlotIndex)
        {
            var run = this.GetModel<RunModel>();
            if (!EnterOperation(run) || !this.GetUtility<TechCatalog>().AllowsBreedingSkill(run.UnlockedTech))
                return;

            var level = this.GetModel<LevelModel>();
            if (slot < 0 || slot >= level.BreedingSlotCount || level.SkillAt(slot) != null)
                return;
            if (skillSlotIndex < 0 || skillSlotIndex >= run.GeneSlots.Count)
                return;

            var gene = run.GeneSlots[skillSlotIndex];
            if (gene.IsReserved)
                return;

            if (!level.TryPutSkill(slot, gene.SkillName))
                return;

            run.TryReserveGeneSlot(skillSlotIndex, slot);
        }

        public void ReturnBreedingSkill(int slot)
        {
            var run = this.GetModel<RunModel>();
            if (!EnterOperation(run))
                return;
            if (!this.GetModel<LevelModel>().TryTakeSkill(slot, out _))
                return;

            run.UnreserveGene(slot);
        }

        public void Confirm()
        {
            var run = this.GetModel<RunModel>();
            if (!EnterOperation(run))
                return;

            var payment = this.GetSystem<SettlementSystem>().Settle();
            if (payment == PaymentResult.Paid)
            {
                this.GetModel<LevelModel>().LockBreeding();
                run.EnterShop();
                this.GetSystem<ShopSystem>().Open();
            }
            else if (payment == PaymentResult.Failed)
            {
                this.GetModel<LevelModel>().ClearProgress();
                run.Lose();
            }
        }

        public void LeaveShop()
        {
            var run = this.GetModel<RunModel>();
            if (run.Phase != RunPhase.Shop)
                return;

            if (run.HasTool("打卡机"))
                run.AddGold(10);

            RecordDailyWorkAndMood(run);
            ReturnBoard(run);
            run.GrowAfterClear(this.GetUtility<SkillCatalog>());
            run.ClearTemporarySkills();
            run.ClearDailyEffects();
            run.ClearTemporaryQuotes();
            run.ClearTemporaryImmovable();
            run.ClearCapacity();
            if (run.LevelNumber >= 7)
            {
                this.GetModel<LevelModel>().ClearProgress();
                run.Win();
                return;
            }

            this.GetSystem<BreedingSystem>().Spawn();
            var next = run.LevelNumber + 1;
            run.EnterLevelStart(next);
            var levels = this.GetUtility<ILevelCatalog>();
            this.GetModel<LevelModel>().BeginLevel(levels.EnergyDue(next), levels.ExcessEnergy(next), run.RetainedEnergy);
            run.SetRetainedEnergy(0);
            FitBreeding(run);
            FitExtraction(run);
        }

        internal void Lay(BenchBoard board)
        {
            var run = this.GetModel<RunModel>();
            run.EnterOperation(board.LevelNumber);
            for (var i = 0; i < board.Tools.Count; i++)
                run.AddTool(board.Tools[i]);

            var levels = this.GetUtility<ILevelCatalog>();
            this.GetModel<LevelModel>().BeginLevel(levels.EnergyDue(board.LevelNumber), levels.ExcessEnergy(board.LevelNumber), board.InitialEnergy);
            FitExtraction(run);
            var level = this.GetModel<LevelModel>();
            var built = new Monster[board.Row.Length];
            var visiting = new bool[board.Row.Length];
            Monster Build(int cell)
            {
                if (built[cell] != null) return built[cell];
                if (visiting[cell]) throw new BenchRejectedException("亲本关系不能形成循环");
                visiting[cell] = true;
                var bm = board.Row[cell].Value;
                var parentIds = new List<string>();
                if (bm.ParentCells != null)
                    foreach (var parentCell in bm.ParentCells)
                    {
                        if (parentCell < 0 || parentCell >= board.Row.Length || board.Row[parentCell] == null)
                            throw new BenchRejectedException("亲本格必须引用实际怪物");
                        parentIds.Add(Build(parentCell).Id);
                    }
                var monster = run.CreateMonster(bm.SkillNames, bm.Modifier, parentIds: parentIds,
                    personality: bm.Personality, mood: bm.Mood, race: bm.Race,
                    consecutiveWorkDays: bm.WorkDays, consecutiveRestDays: bm.RestDays, sourceKind: SkillSourceKind.Fixture);
                if (bm.Instances != null && bm.Instances.Count != monster.Skills.Count) throw new BenchRejectedException("技能实例数量须与技能名单一致");
                if (bm.Instances != null)
                    for (var index = 0; index < bm.Instances.Count; index++)
                    {
                        var source = bm.Instances[index];
                        var instance = run.MakeSkill(source.Name, source.Temporary, SkillSourceKind.Fixture);
                        instance.Add(source.Growth, true);
                        monster.Skills[index] = instance;
                    }
                built[cell] = monster;
                visiting[cell] = false;
                return monster;
            }
            for (var cell = 0; cell < board.Row.Length; cell++)
                if (board.Row[cell] != null) level.Put(OperationArea.Extraction, cell, Build(cell).Id);
        }

        void FitBreeding(RunModel run)
        {
            var tech = this.GetUtility<TechCatalog>();
            this.GetModel<LevelModel>().ApplyShape(tech.SlotCount(run.UnlockedTech), tech.ParentCapacity(run.UnlockedTech));
        }

        void FitExtraction(RunModel run)
        {
            var tools = this.GetUtility<IToolCatalog>();
            this.GetModel<LevelModel>().FitExtraction(tools.ExtractionCells(run.Tools));
        }

        bool Apply(TransitionPlan plan, RunModel run, LevelModel level)
        {
            string drawn = null;
            if (plan.Effect == TransitionEffect.Discard)
            {
                var monster = run.Find(plan.SourceMonsterId);
                if (monster == null || monster.Skills.Count == 0)
                    return false;

                var names = new string[monster.Skills.Count];
                for (var i = 0; i < names.Length; i++)
                    names[i] = monster.Skills[i].Name;

                drawn = this.GetUtility<IDraw>().Choose(names);
            }

            // 笼、技能槽、提取格、培育位分属两个模型。写入中途失败时写回这四份副本。
            var cage = run.CaptureCage();
            var geneSlots = run.CaptureGeneSlots();
            var extraction = level.CaptureExtraction();
            var breeding = level.CaptureBreeding();

            void Restore()
            {
                run.RestoreCage(cage);
                run.RestoreGeneSlots(geneSlots);
                level.RestoreExtraction(extraction);
                level.RestoreBreeding(breeding);
            }

            try
            {
                if (Write(plan, drawn, run, level))
                    return true;

                Restore();
                return false;
            }
            catch
            {
                Restore();
                throw;
            }
        }

        static bool Write(TransitionPlan plan, string drawnSkill, RunModel run, LevelModel level)
        {
            switch (plan.Effect)
            {
                case TransitionEffect.Move:
                    if (plan.SourceWhere == MonsterWhere.Cage)
                    {
                        if (!run.TryRemoveFromCage(plan.SourceMonsterId, out _))
                            return false;

                        level.Put(plan.ToArea, plan.ToCell, plan.SourceMonsterId);
                        return true;
                    }

                    level.MoveCell(plan.FromArea, plan.FromCell, plan.ToArea, plan.ToCell);
                    return true;
                case TransitionEffect.Replace:
                    var occupant = run.Find(plan.OtherMonsterId);
                    if (occupant == null)
                        return false;

                    level.Put(plan.ToArea, plan.ToCell, plan.SourceMonsterId);
                    run.AddToCage(occupant);
                    return run.TryRemoveFromCage(plan.SourceMonsterId, out _);
                case TransitionEffect.Exchange:
                    level.ExchangeCells(plan.FromArea, plan.FromCell, plan.ToArea, plan.ToCell);
                    return true;
                case TransitionEffect.Return:
                    if (!level.TryRemove(plan.SourceMonsterId))
                        return false;

                    var returning = run.Find(plan.SourceMonsterId);
                    if (returning == null)
                        return false;

                    run.AddToCage(returning);
                    return true;
                case TransitionEffect.Discard:
                    if (plan.SourceWhere != MonsterWhere.Cage && !level.TryRemove(plan.SourceMonsterId))
                        return false;
                    if (!run.TryDestroy(plan.SourceMonsterId))
                        return false;

                    run.PutSkillInSlot(drawnSkill);
                    if (run.HasTool("研磨机"))
                        run.AddGold(5);
                    return true;
                case TransitionEffect.Install:
                    var installGene = run.GeneSlots[plan.SkillSlotIndex];
                    var acquired = run.GainSkill(plan.SourceMonsterId, installGene.SkillName, false, SkillSourceKind.Gene, installGene.Id);
                    if (!acquired.Success) return false;
                    return run.TryTakeSkillAt(plan.SkillSlotIndex, out _);
                case TransitionEffect.Invest:
                    if (plan.SkillSlotIndex < 0 || plan.SkillSlotIndex >= run.GeneSlots.Count)
                        return false;
                    var gene = run.GeneSlots[plan.SkillSlotIndex];
                    if (gene.IsReserved)
                        return false;
                    if (!level.TryPutSkill(plan.BreedingSlot, gene.SkillName))
                        return false;
                    return run.TryReserveGeneSlot(plan.SkillSlotIndex, plan.BreedingSlot);
                case TransitionEffect.ReturnBreedingSkill:
                    if (!level.TryTakeSkill(plan.BreedingSlot, out _))
                        return false;
                    run.UnreserveGene(plan.BreedingSlot);
                    return true;
                default:
                    return false;
            }
        }

        TransitionFacts Facts(RunModel run, LevelModel level)
        {
            var geneSlots = run.GeneSlots;
            var skillNames = new string[geneSlots.Count];
            var geneReserved = new bool[geneSlots.Count];
            for (var i = 0; i < geneSlots.Count; i++)
            {
                skillNames[i] = geneSlots[i].SkillName;
                geneReserved[i] = geneSlots[i].IsReserved;
            }

            var extraction = level.CaptureExtraction();
            var breedingCells = level.Breeding;
            var breeding = new string[breedingCells.Count];
            for (var i = 0; i < breeding.Length; i++)
                breeding[i] = breedingCells[i];

            var breedingSkills = new string[level.BreedingSlotCount];
            for (var slot = 0; slot < breedingSkills.Length; slot++)
                breedingSkills[slot] = level.SkillAt(slot);

            var monsters = new List<MonsterFact>();
            var cage = run.Cage;
            for (var i = 0; i < cage.Count; i++)
                SetMonster(monsters, Fact(cage[i], MonsterWhere.Cage, OperationArea.Extraction, -1));

            for (var i = 0; i < extraction.Length; i++)
            {
                if (extraction[i] == null)
                    continue;

                var monster = run.Find(extraction[i]);
                if (monster != null)
                    SetMonster(monsters, Fact(monster, MonsterWhere.Extraction, OperationArea.Extraction, i));
            }

            for (var i = 0; i < breeding.Length; i++)
            {
                if (breeding[i] == null)
                    continue;

                var monster = run.Find(breeding[i]);
                if (monster != null)
                    SetMonster(monsters, Fact(monster, MonsterWhere.Breeding, OperationArea.Breeding, i));
            }

            return new TransitionFacts(
                run.Phase == RunPhase.Operation,
                this.GetUtility<TechCatalog>().AllowsBreedingSkill(run.UnlockedTech),
                skillNames,
                extraction,
                breeding,
                breedingSkills,
                monsters.ToArray(),
                geneReserved, run.GeneCapacity, run.HasTool("复读腺"));
        }

        static MonsterFact Fact(Monster monster, MonsterWhere where, OperationArea area, int cell)
        {
            var names = new string[monster.Skills.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = monster.Skills[i].Name;

            return new MonsterFact(monster.Id, where, area, cell, names, monster.Personality);
        }

        static void SetMonster(List<MonsterFact> monsters, MonsterFact fact)
        {
            for (var i = 0; i < monsters.Count; i++)
            {
                if (monsters[i].Id != fact.Id)
                    continue;

                monsters[i] = fact;
                return;
            }

            monsters.Add(fact);
        }

        static string RejectReason(RunModel run, OperationDrop drop)
        {
            if (drop.Landing.Place == LandingPlace.Discard && run.GeneSlots.Count >= run.GeneCapacity) return "基因仓储已满（含培育预约）";
            if (drop.Payload.Kind == PayloadKind.SkillChip)
            {
                if (drop.Payload.Index < 0 || drop.Payload.Index >= run.GeneSlots.Count) return "基因材料不存在";
                var gene = run.GeneSlots[drop.Payload.Index];
                if (gene.IsReserved) return "基因正在培育槽预约";
                if (drop.Landing.Place != LandingPlace.BreedingSocket)
                {
                    var target = run.Find(drop.Landing.MonsterId);
                    if (target == null) return "请选择怪物作为安装目标";
                    if (target.Personality != "迷恋死亡") return "只有迷恋死亡可直接安装基因";
                    return run.SkillGainFailure(target.Id, gene.SkillName) ?? "目标槽位不可用";
                }
                if (!run.HasTech("基因实验")) return "请先研究基因实验";
            }
            return "此投放位置不可用";
        }

        bool EnterOperation(RunModel run)
        {
            if (run.Phase == RunPhase.LevelStart)
            {
                run.EnterOperation(run.LevelNumber);
                TriggerStartPhase(run);
            }

            return run.Phase == RunPhase.Operation;
        }

        void ReturnBoard(RunModel run)
        {
            var level = this.GetModel<LevelModel>();
            ReturnSlots(level.Extraction, run);
            ReturnSlots(level.Breeding, run);
        }

        static void ReturnSlots(IReadOnlyList<string> slots, RunModel run)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var monsterId = slots[i];
                if (monsterId == null)
                    continue;

                var monster = run.Find(monsterId);
                if (monster != null)
                    run.AddToCage(monster);
            }
        }

        void RecordDailyWorkAndMood(RunModel run)
        {
            var moodCatalog = this.GetUtility<MoodCatalog>();
            var hasSoundproofCotton = run.HasTool("隔音棉");
            foreach (var monster in run.LivingMonsters)
            {
                if (run.DidWorkToday(monster.Id))
                    monster.RecordWorkDay();
                else
                    monster.RecordRestDay();

                var newMood = moodCatalog.CalculateDailyMood(monster, hasSoundproofCotton);
                monster.BeginDailyMood(moodCatalog.CalculateDailyMood(moodCatalog.FixedMood(monster.Personality) != null ? null : monster.Personality, monster.ConsecutiveWorkDays, monster.ConsecutiveRestDays, hasSoundproofCotton), newMood);
            }

            run.ClearWorkedToday();
            run.ResetDailyClockIn();
        }

        void TriggerStartPhase(RunModel run)
        {
            var draw = this.GetUtility<IDraw>();
            var moodCatalog = this.GetUtility<MoodCatalog>();
            var skillCatalog = this.GetUtility<SkillCatalog>();

            // 1. 怪物严选
            if (run.HasTool("怪物严选"))
            {
                var upgradeable = new List<Monster>();
                foreach (var monster in run.LivingMonsters)
                {
                    if (monster.Personality != "稳定" &&
                        monster.Personality != "乐天派" &&
                        monster.Personality != "摸鱼派" &&
                        monster.Personality != "厌世" &&
                        moodCatalog.TierOf(monster.Mood) < 4)
                    {
                        upgradeable.Add(monster);
                    }
                }

                if (upgradeable.Count > 0)
                {
                    var chosen = draw.Choose(upgradeable);
                    chosen.BoostDailyMood(moodCatalog.UpgradeMood(chosen.Mood));
                }
            }

            // 2. 好奇 & 上进心
            foreach (var monster in run.LivingMonsters)
            {
                if (monster.Personality == "上进心")
                {
                    monster.AddModifier(1);
                }
                else if (monster.Personality == "好奇")
                {
                    var maxSkills = monster.Personality == "安于现状" ? 2 : 4;
                    if (monster.Skills.Count < maxSkills)
                    {
                        var allNames = skillCatalog.Names();
                        var candidates = new List<string>();
                        for (var i = 0; i < allNames.Count; i++)
                        {
                            var name = allNames[i];
                            if (run.HasTool("复读腺") || !OwnsSkill(monster, name))
                                candidates.Add(name);
                        }

                        if (candidates.Count > 0)
                        {
                            var addedSkillName = draw.Choose(candidates);
                            run.GainSkill(monster.Id, addedSkillName, false, SkillSourceKind.Personality, "好奇", monster.Id);

                            for (var i = 0; i < monster.Skills.Count - 1; i++)
                            {
                                var oldName = monster.Skills[i].Name;
                                var rewriteCandidates = new List<string>();
                                for (var j = 0; j < allNames.Count; j++)
                                {
                                    var name = allNames[j];
                                    if (name == oldName)
                                        continue;
                                    if (!run.HasTool("复读腺") && OwnsSkill(monster, name))
                                        continue;
                                    rewriteCandidates.Add(name);
                                }

                                if (rewriteCandidates.Count > 0)
                                {
                                    var rewrittenName = draw.Choose(rewriteCandidates);
                                    monster.Skills[i] = run.MakeSkill(rewrittenName, false, SkillSourceKind.Personality, "好奇", monster.Id);
                                }
                            }
                        }
                    }
                }
            }
        }

        void HandleClockIn(string monsterId)
        {
            var run = this.GetModel<RunModel>();
            var monster = run.Find(monsterId);
            if (monster == null)
                return;

            if (!monster.TryClockIn())
                return;

            if (monster.Personality == "好学")
            {
                var level = this.GetModel<LevelModel>();
                var cells = level.Extraction;
                var otherIds = new List<string>();
                for (var i = 0; i < cells.Count; i++)
                {
                    if (cells[i] != null && cells[i] != monsterId)
                        otherIds.Add(cells[i]);
                }

                if (otherIds.Count == 0)
                    return;

                var draw = this.GetUtility<IDraw>();
                var targetId = draw.Choose(otherIds);
                var target = run.Find(targetId);
                if (target == null || target.Skills.Count == 0)
                    return;

                var sourceInstance = draw.Choose(target.Skills);
                var chosenSkill = sourceInstance.Name;
                if (run.GainSkill(monsterId, chosenSkill, false, SkillSourceKind.Personality, sourceInstance.Id, target.Id).Success)
                {
                    run.ChangePersonality(monsterId, target.Personality);
                    var moodCatalog = this.GetUtility<MoodCatalog>();
                    var hasSoundproofCotton = run.HasTool("隔音棉");
                    var fixedMood = moodCatalog.FixedMood(monster.Personality);
                    if (fixedMood != null) monster.SetMood(fixedMood);
                }
            }
        }

        static bool OwnsSkill(Monster monster, string skillName)
        {
            for (var i = 0; i < monster.Skills.Count; i++)
            {
                if (monster.Skills[i].Name == skillName)
                    return true;
            }
            return false;
        }
    }
}
