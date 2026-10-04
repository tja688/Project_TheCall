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
            if (!EnterOperation(run) || run.SkillSlots.Count >= 3)
                return;

            var monster = run.Find(monsterId);
            if (monster == null)
                return;

            var names = new string[monster.Skills.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = monster.Skills[i].Name;

            var skillName = this.GetUtility<IDraw>().Choose(names);
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
        }

        void FitBreeding(RunModel run)
        {
            var tech = this.GetUtility<TechCatalog>();
            this.GetModel<LevelModel>().ApplyShape(tech.SlotCount(run.UnlockedTech), tech.ParentCapacity(run.UnlockedTech));
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
