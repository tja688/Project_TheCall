using QFramework;

namespace TheCall
{
    public sealed class LevelTarget
    {
        public LevelTarget(int levelNumber, int energyDue)
        {
            LevelNumber = levelNumber;
            EnergyDue = energyDue;
        }

        public int LevelNumber { get; }

        public int EnergyDue { get; }
    }

    public sealed class LevelTargetQuery : AbstractQuery<LevelTarget>
    {
        protected override LevelTarget OnDo()
        {
            var run = this.GetModel<RunModel>();
            var level = this.GetModel<LevelModel>();
            return new LevelTarget(run.LevelNumber, level.EnergyDue);
        }
    }
}
