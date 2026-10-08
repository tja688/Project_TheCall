using System;
using System.Collections.Generic;

namespace TheCall
{
    public enum SkillUse
    {
        Active,
        Passive,
    }

    public sealed class MonsterSkillDetail
    {
        public MonsterSkillDetail(string name, string sentence, SkillUse kind, Rarity rarity, string function)
        {
            if (string.IsNullOrEmpty(function))
                throw new ArgumentException("A skill needs a function.", nameof(function));

            Name = name;
            Sentence = sentence;
            Kind = kind;
            Rarity = rarity;
            Function = function;
        }

        public string Name { get; }

        public string Sentence { get; }

        public SkillUse Kind { get; }

        public Rarity Rarity { get; }

        public string Function { get; }
    }

    public sealed class MonsterDetails
    {
        public MonsterDetails(
            string id,
            string displayName,
            IReadOnlyList<MonsterSkillDetail> skills,
            int modifier,
            bool immovable,
            int capacity,
            MonsterAppearance appearance)
        {
            if (skills == null || skills.Count == 0 || skills.Count > 4)
                throw new ArgumentException("A monster needs 1 to 4 skills.", nameof(skills));

            var copy = new MonsterSkillDetail[skills.Count];
            for (var i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                if (skill == null || string.IsNullOrEmpty(skill.Name))
                    throw new ArgumentException("A monster needs 1 to 4 skills.", nameof(skills));

                copy[i] = skill;
            }

            Id = id;
            DisplayName = displayName;
            Skills = copy;
            Modifier = modifier;
            Immovable = immovable;
            Capacity = capacity;
            Appearance = appearance;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public IReadOnlyList<MonsterSkillDetail> Skills { get; }

        public int Modifier { get; }

        public bool Immovable { get; }
        public MonsterAppearance Appearance { get; }

        public int Capacity { get; }
    }
}
