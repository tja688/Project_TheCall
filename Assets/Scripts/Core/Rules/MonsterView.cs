using System.Collections.Generic;
using System.Linq;

namespace TheCall
{
    public sealed class SkillView
    {
        public SkillView(string name, int quote, Rarity rarity)
        {
            Name = name;
            Quote = quote;
            Rarity = rarity;
        }

        public string Name { get; }

        public int Quote { get; }

        public Rarity Rarity { get; }
    }

    internal static class MonsterViews
    {
        public static MonsterView From(Monster monster, SkillCatalog catalog) =>
            new MonsterView(
                monster.Id,
                monster.DisplayName,
                monster.Skills
                    .Select(skill => new SkillView(skill.Name, skill.Quote, catalog.RarityOf(skill.Name)))
                    .ToArray(),
                monster.Modifier,
                monster.Immovable,
                monster.Capacity,
                monster.Appearance);
    }

    public sealed class MonsterView
    {
        public MonsterView(string id, string displayName, IReadOnlyList<SkillView> skills, int modifier, bool immovable, int capacity, MonsterAppearance appearance)
        {
            Id = id;
            DisplayName = displayName;
            Skills = skills;
            SkillNames = skills.Select(skill => skill.Name).ToArray();
            Modifier = modifier;
            Immovable = immovable;
            Capacity = capacity;
            Appearance = appearance;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public IReadOnlyList<SkillView> Skills { get; }

        public IReadOnlyList<string> SkillNames { get; }

        public int Modifier { get; }

        public bool Immovable { get; }
        public MonsterAppearance Appearance { get; }

        public int Capacity { get; }
    }
}
