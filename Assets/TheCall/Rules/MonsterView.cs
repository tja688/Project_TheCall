using System.Collections.Generic;
using System.Linq;

namespace TheCall
{
    internal static class MonsterViews
    {
        public static MonsterView From(Monster monster) =>
            new MonsterView(monster.Id, monster.Skills.Select(skill => skill.Name).ToArray());
    }

    public sealed class MonsterView
    {
        public MonsterView(string id, IReadOnlyList<string> skillNames)
        {
            Id = id;
            SkillNames = skillNames;
        }

        public string Id { get; }

        public IReadOnlyList<string> SkillNames { get; }
    }
}
