using System;
using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public sealed class PersonalityDef
    {
        public PersonalityDef(string name, Rarity rarity, string kind, string description)
        {
            Name = name;
            Rarity = rarity;
            Kind = kind;
            Description = description;
        }

        public string Name { get; }

        public Rarity Rarity { get; }

        public string Kind { get; }

        public string Description { get; }

        public bool IsAura => Kind == "Aura" || Kind == "光环被动";

        public bool IsTrigger => Kind == "Trigger" || Kind == "触发被动";
    }

    public interface IPersonalityCatalog : IUtility
    {
        IReadOnlyList<PersonalityDef> Personalities { get; }
        PersonalityDef Find(string name);
        IReadOnlyList<string> Names();
        IReadOnlyList<string> NamesOf(Rarity rarity);
        string Draw(IDraw draw);
    }

    internal sealed class PersonalityCatalog : IPersonalityCatalog
    {
        readonly ContentBook _book;

        public PersonalityCatalog(ContentBook book) => _book = book;

        public IReadOnlyList<PersonalityDef> Personalities => _book.Personalities;

        public PersonalityDef Find(string name) => _book.FindPersonality(name);

        public IReadOnlyList<string> Names()
        {
            var names = new string[_book.Personalities.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = _book.Personalities[i].Name;
            return names;
        }

        public IReadOnlyList<string> NamesOf(Rarity rarity)
        {
            var list = new List<string>();
            for (var i = 0; i < _book.Personalities.Count; i++)
            {
                if (_book.Personalities[i].Rarity == rarity)
                    list.Add(_book.Personalities[i].Name);
            }
            return list;
        }

        public string Draw(IDraw draw)
        {
            var rarities = new List<Rarity>();
            var kinds = new[] { Rarity.White, Rarity.Blue, Rarity.Negative, Rarity.Gold };
            var weights = new[] { 50, 20, 20, 10 };
            for (var i = 0; i < kinds.Length; i++)
                if (NamesOf(kinds[i]).Count > 0)
                    for (var n = 0; n < weights[i]; n++) rarities.Add(kinds[i]);
            if (rarities.Count == 0) throw new InvalidOperationException("性格池为空");
            var chosenRarity = draw.Choose(rarities);
            var names = NamesOf(chosenRarity);
            return draw.Choose(names);
        }
    }
}
