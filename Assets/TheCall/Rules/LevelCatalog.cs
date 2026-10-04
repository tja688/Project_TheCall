using QFramework;

namespace TheCall
{
    public interface ILevelCatalog : IUtility
    {
        int EnergyDue(int levelNumber);

        int ExcessEnergy(int levelNumber);
    }

    internal sealed class LevelCatalog : ILevelCatalog
    {
        static readonly int[] Due = { 50, 75, 100, 150, 200, 250, 300 };

        static readonly int[] Excess = { 60, 90, 120, 175, 230, 285, 350 };

        public int EnergyDue(int levelNumber) => At(Due, levelNumber);

        public int ExcessEnergy(int levelNumber) => At(Excess, levelNumber);

        static int At(int[] values, int levelNumber)
        {
            var index = levelNumber - 1;
            if (index < 0 || index >= values.Length)
                return 0;

            return values[index];
        }
    }
}
