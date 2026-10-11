using System.Collections.Generic;
using System.Linq;

namespace TheCall
{
    public sealed class SkillView
    {
        public SkillView(string name, int quote, Rarity rarity, bool isTemporary = false)
        {
            Name = name;
            Quote = quote;
            Rarity = rarity;
            IsTemporary = isTemporary;
        }

        public string Name { get; }

        public int Quote { get; }

        public Rarity Rarity { get; }

        public bool IsTemporary { get; }
    }

    internal static class MonsterViews
    {
        public static MonsterView From(Monster monster, SkillCatalog catalog, MoodCatalog moodCatalog = null)
        {
            var moodDelta = moodCatalog != null ? moodCatalog.EnergyDelta(monster.Mood) : 0;
            return new MonsterView(
                monster.Id,
                monster.DisplayName,
                monster.Skills
                    .Select(skill => new SkillView(skill.Name, skill.Quote, catalog.RarityOf(skill.Name), skill.IsTemporary))
                    .ToArray(),
                monster.Modifier,
                monster.Immovable,
                monster.Capacity,
                monster.Appearance,
                monster.Personality,
                monster.Race,
                monster.Mood,
                moodDelta,
                monster.ConsecutiveWorkDays,
                monster.ConsecutiveRestDays);
        }
    }

    public sealed class MonsterView
    {
        public MonsterView(
            string id,
            string displayName,
            IReadOnlyList<SkillView> skills,
            int modifier,
            bool immovable,
            int capacity,
            MonsterAppearance appearance,
            string personality = "稳定",
            string race = "森林",
            string mood = "普通",
            int moodEnergyDelta = 0,
            int consecutiveWorkDays = 0,
            int consecutiveRestDays = 0)
        {
            Id = id;
            DisplayName = displayName;
            Skills = skills;
            SkillNames = skills.Select(skill => skill.Name).ToArray();
            Modifier = modifier;
            Immovable = immovable;
            Capacity = capacity;
            Appearance = appearance;
            Personality = string.IsNullOrEmpty(personality) ? "稳定" : personality;
            Race = string.IsNullOrEmpty(race) ? "森林" : race;
            Mood = string.IsNullOrEmpty(mood) ? "普通" : mood;
            MoodEnergyDelta = moodEnergyDelta;
            ConsecutiveWorkDays = consecutiveWorkDays;
            ConsecutiveRestDays = consecutiveRestDays;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public IReadOnlyList<SkillView> Skills { get; }

        public IReadOnlyList<string> SkillNames { get; }

        public int Modifier { get; }

        public bool Immovable { get; }

        public MonsterAppearance Appearance { get; }

        public int Capacity { get; }

        public string Personality { get; }

        public string Race { get; }

        public string Mood { get; }

        public int MoodEnergyDelta { get; }

        public int ConsecutiveWorkDays { get; }

        public int ConsecutiveRestDays { get; }
    }
}
