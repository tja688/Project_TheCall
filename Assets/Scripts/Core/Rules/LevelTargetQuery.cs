using QFramework;

namespace TheCall
{
    public sealed class LevelTarget
    {
        public LevelTarget(int levelNumber, int energyDue, int excessEnergy)
        {
            LevelNumber = levelNumber;
            EnergyDue = energyDue;
            ExcessEnergy = excessEnergy;
        }

        public int LevelNumber { get; }

        public int EnergyDue { get; }

        public int ExcessEnergy { get; }
    }

    public sealed class LevelTargetQuery : AbstractQuery<LevelTarget>
    {
        readonly int _levelNumber;

        public LevelTargetQuery()
        {
        }

        public LevelTargetQuery(int levelNumber) => _levelNumber = levelNumber;

        protected override LevelTarget OnDo()
        {
            if (_levelNumber > 0)
            {
                var catalog = this.GetArchitecture().GetUtility<ILevelCatalog>();
                return new LevelTarget(
                    _levelNumber,
                    catalog.EnergyDue(_levelNumber),
                    catalog.ExcessEnergy(_levelNumber));
            }

            var run = this.GetModel<RunModel>();
            var level = this.GetModel<LevelModel>();
            return new LevelTarget(run.LevelNumber, level.EnergyDue, level.ExcessEnergy);
        }
    }
}
