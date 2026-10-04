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
            this.GetModel<LevelModel>().BeginFirstLevel();
        }

        public void Place(string monsterId, OperationArea area, int cell)
        {
            var run = this.GetModel<RunModel>();
            if (run.Phase != RunPhase.Operation)
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
            if (run.Phase != RunPhase.Operation)
                return;
            if (!this.GetModel<LevelModel>().TryRemove(monsterId))
                return;

            run.AddToCage(run.Find(monsterId));
        }
    }
}
