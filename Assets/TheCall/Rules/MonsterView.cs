using System.Collections.Generic;
using System.Linq;

namespace TheCall
{
    internal static class MonsterViews
    {
        public static MonsterView From(Monster monster) =>
            new MonsterView(
                monster.Id,
                monster.Skills.Select(skill => skill.Name).ToArray(),
                monster.Modifier);
    }

    public sealed class MonsterView
    {
        public MonsterView(string id, IReadOnlyList<string> skillNames, int modifier = 0)
        {
            Id = id;
            SkillNames = skillNames;
            Modifier = modifier;
        }

        public string Id { get; }

        public IReadOnlyList<string> SkillNames { get; }

        public int Modifier { get; }
    }
}
