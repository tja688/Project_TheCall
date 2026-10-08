using System;
using System.Collections.Generic;
using System.Text;

namespace TheCall
{
    public enum SkillUse
    {
        Active,
        Passive,
    }

    public sealed class SentenceSpan
    {
        public const string ShiftedColor = "#FF8C1A";

        public SentenceSpan(string text, bool shifted)
        {
            if (string.IsNullOrEmpty(text))
                throw new ArgumentException("A sentence span needs text.", nameof(text));

            Text = text;
            Shifted = shifted;
        }

        public string Text { get; }

        public bool Shifted { get; }
    }

    public sealed class MonsterSkillDetail
    {
        public MonsterSkillDetail(string name, SkillUse kind, Rarity rarity, string function, IReadOnlyList<SentenceSpan> spans)
        {
            if (string.IsNullOrEmpty(function))
                throw new ArgumentException("A skill needs a function.", nameof(function));
            if (spans == null || spans.Count == 0)
                throw new ArgumentException("A skill needs a sentence.", nameof(spans));

            var copy = new SentenceSpan[spans.Count];
            var plain = new StringBuilder();
            var display = new StringBuilder();
            for (var i = 0; i < spans.Count; i++)
            {
                var span = spans[i];
                if (span == null || string.IsNullOrEmpty(span.Text))
                    throw new ArgumentException("A skill needs a sentence.", nameof(spans));

                copy[i] = span;
                plain.Append(span.Text);
                if (span.Shifted)
                    display.Append("<color=").Append(SentenceSpan.ShiftedColor).Append('>').Append(span.Text).Append("</color>");
                else
                    display.Append(span.Text);
            }

            Name = name;
            Kind = kind;
            Rarity = rarity;
            Function = function;
            Spans = copy;
            Sentence = plain.ToString();
            DisplaySentence = display.ToString();
        }

        public string Name { get; }

        public string Sentence { get; }

        public string DisplaySentence { get; }

        public IReadOnlyList<SentenceSpan> Spans { get; }

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
