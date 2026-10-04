using QFramework;

namespace TheCall
{
    public interface ILevelCatalog : IUtility
    {
        int EnergyDue { get; }

        int ExcessEnergy { get; }
    }

    internal sealed class LevelCatalog : ILevelCatalog
    {
        public int EnergyDue => 50;

        public int ExcessEnergy => 60;
    }
}
