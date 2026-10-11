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
    }
}
