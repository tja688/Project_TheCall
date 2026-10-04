using QFramework;

namespace TheCall
{
    public interface ILevelCatalog : IUtility
    {
        int EnergyDue { get; }
    }

    internal sealed class LevelCatalog : ILevelCatalog
    {
        public int EnergyDue => 50;
    }
}
