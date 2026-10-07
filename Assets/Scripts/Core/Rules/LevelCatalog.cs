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
        readonly ContentBook _book;

        public LevelCatalog(ContentBook book) => _book = book;

        public int EnergyDue(int levelNumber) => At(levelNumber, true);

        public int ExcessEnergy(int levelNumber) => At(levelNumber, false);

        int At(int levelNumber, bool due)
        {
            var index = levelNumber - 1;
            if (index < 0 || index >= _book.Levels.Count)
                return 0;

            return due ? _book.Levels[index].Due : _book.Levels[index].Excess;
        }
    }
}
