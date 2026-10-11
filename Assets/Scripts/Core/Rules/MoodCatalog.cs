using System;
using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public sealed class MoodDef
    {
        public MoodDef(string name, int energyDelta, int tier)
        {
            Name = name;
            EnergyDelta = energyDelta;
            Tier = tier;
        }

        public string Name { get; }

        public int EnergyDelta { get; }

        public int Tier { get; }
    }

    public interface IMoodCatalog : IUtility
    {
        IReadOnlyList<MoodDef> Moods { get; }
        MoodDef Find(string name);
        int EnergyDelta(string moodName);
        int TierOf(string moodName);
        string TierName(int tier);
        string UpgradeMood(string currentMood);
        string CalculateBaseMood(int consecutiveWorkDays, int consecutiveRestDays);
    }

    internal sealed class MoodCatalog : IMoodCatalog
    {
        readonly ContentBook _book;

        public MoodCatalog(ContentBook book) => _book = book;

        public IReadOnlyList<MoodDef> Moods => _book.Moods;

        public MoodDef Find(string name) => _book.FindMood(name);

        public int EnergyDelta(string moodName)
        {
            var def = Find(moodName);
            return def == null ? 0 : def.EnergyDelta;
        }

        public int TierOf(string moodName)
        {
            var def = Find(moodName);
            return def == null ? 2 : def.Tier;
        }

        public string TierName(int tier)
        {
            for (var i = 0; i < _book.Moods.Count; i++)
            {
                if (_book.Moods[i].Tier == tier)
                    return _book.Moods[i].Name;
            }
            return "普通";
        }

        public string UpgradeMood(string currentMood)
        {
            var currentTier = TierOf(currentMood);
            var nextTier = Math.Min(4, currentTier + 1);
            return TierName(nextTier);
        }

        public string CalculateBaseMood(int consecutiveWorkDays, int consecutiveRestDays)
        {
            if (consecutiveWorkDays >= 4)
                return "倦怠";
            if (consecutiveWorkDays >= 2)
                return "疲倦";
            if (consecutiveRestDays >= 4)
                return "热爱";
            if (consecutiveRestDays >= 2)
                return "热情";
            return "普通";
        }
    }
}
