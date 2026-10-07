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
            for (var i = 0; i < 3; i++)
                run.AddCandidate(draw.Choose(catalog.Names));
        }

        public void Keep(string monsterId)
        {
            var run = this.GetModel<RunModel>();
            if (!run.TryTakeCandidate(monsterId, out var kept))
                return;

            run.AddToCage(kept);
            var catalog = this.GetUtility<SkillCatalog>();
            var draw = this.GetUtility<IDraw>();
            for (var i = 0; i < 6; i++)
                run.AddNewToCage(draw.Choose(catalog.Names));

            run.EnterOperation(1);
            var levels = this.GetUtility<ILevelCatalog>();
            this.GetModel<LevelModel>().BeginLevel(levels.EnergyDue(1), levels.ExcessEnergy(1));
            FitBreeding(run);
            FitExtraction(run);
        }

        public void BeginLevel()
        {
            var run = this.GetModel<RunModel>();
            if (run.Phase == RunPhase.LevelStart)
                run.EnterOperation(run.LevelNumber);
        }

        public bool Commit(OperationDrop drop)
        {
            var run = this.GetModel<RunModel>();
            if (!EnterOperation(run))
                return false;

            var level = this.GetModel<LevelModel>();
            var facts = Facts(run, level);
            if (!OperationTransition.TryPlan(facts, drop, out var plan))
                return false;

            return Apply(plan, run, level);
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

            var catalog = this.GetUtility<SkillCatalog>();
            var legacy = false;
            for (var index = 0; index < monster.Skills.Count; index++)
            {
                if (catalog.HasLegacy(monster.Skills[index].Name))
                    legacy = true;
            }

            if (!legacy && run.SkillSlots.Count >= 3)
                return;

            if (legacy)
            {
                this.GetModel<LevelModel>().TryRemove(monsterId);
                if (!run.TryDestroy(monsterId))
                    return;

                var names = catalog.Names;
                if (names.Count == 0)
                    return;

                run.AddToCage(run.CreateMonster(new[] { this.GetUtility<IDraw>().Choose(names) }));
                return;
            }

            var owned = new string[monster.Skills.Count];
            for (var i = 0; i < owned.Length; i++)
                owned[i] = monster.Skills[i].Name;

            var skillName = this.GetUtility<IDraw>().Choose(owned);
            this.GetModel<LevelModel>().TryRemove(monsterId);
            if (!run.TryDestroy(monsterId))
                return;

            run.PutSkillInSlot(skillName);
        }

        public void Equip(string monsterId, int skillSlotIndex)
        {
            var run = this.GetModel<RunModel>();
            if (!EnterOperation(run))
                return;

            run.TryEquip(monsterId, skillSlotIndex);
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
            if (!run.TryTakeSkillAt(skillSlotIndex, out var skillName))
                return;
            if (!level.TryPutSkill(slot, skillName))
                run.PutSkillInSlot(skillName);
        }

        public void ReturnBreedingSkill(int slot)
        {
            var run = this.GetModel<RunModel>();
            if (!EnterOperation(run) || run.SkillSlots.Count >= 3)
                return;
            if (!this.GetModel<LevelModel>().TryTakeSkill(slot, out var skillName))
                return;

            run.PutSkillInSlot(skillName);
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

            ReturnBoard(run);
            run.GrowAfterClear(this.GetUtility<SkillCatalog>());
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
            this.GetModel<LevelModel>().BeginLevel(levels.EnergyDue(next), levels.ExcessEnergy(next));
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
            this.GetModel<LevelModel>().BeginLevel(levels.EnergyDue(board.LevelNumber), levels.ExcessEnergy(board.LevelNumber));
            FitExtraction(run);
            var level = this.GetModel<LevelModel>();
            for (var cell = 0; cell < board.Row.Length; cell++)
            {
                if (board.Row[cell] == null)
                    continue;

                var monster = run.CreateMonster(board.Row[cell].Value.SkillNames);
                level.Put(OperationArea.Extraction, cell, monster.Id);
            }
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
            var skillSlots = run.CaptureSkillSlots();
            var extraction = level.CaptureExtraction();
            var breeding = level.CaptureBreeding();

            void Restore()
            {
                run.RestoreCage(cage);
                run.RestoreSkillSlots(skillSlots);
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
                    return true;
                case TransitionEffect.Equip:
                    return run.TryEquip(plan.EquipMonsterId, plan.SkillSlotIndex);
                case TransitionEffect.Invest:
                    if (!run.TryTakeSkillAt(plan.SkillSlotIndex, out var invested))
                        return false;

                    return level.TryPutSkill(plan.BreedingSlot, invested);
                case TransitionEffect.ReturnBreedingSkill:
                    if (!level.TryTakeSkill(plan.BreedingSlot, out var returned))
                        return false;

                    run.PutSkillInSlot(returned);
                    return true;
                default:
                    return false;
            }
        }

        TransitionFacts Facts(RunModel run, LevelModel level)
        {
            var skillNames = run.CaptureSkillSlots();
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
                monsters.ToArray());
        }

        static MonsterFact Fact(Monster monster, MonsterWhere where, OperationArea area, int cell)
        {
            var names = new string[monster.Skills.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = monster.Skills[i].Name;

            return new MonsterFact(monster.Id, where, area, cell, names);
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

        static bool EnterOperation(RunModel run)
        {
            if (run.Phase == RunPhase.LevelStart)
                run.EnterOperation(run.LevelNumber);

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
    }
}
