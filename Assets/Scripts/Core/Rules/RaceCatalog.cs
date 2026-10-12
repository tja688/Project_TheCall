using System;
using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public sealed class RaceDef
    {
        public RaceDef(string name, string element, string generates, string overcomes)
        {
            Name = name;
            Element = element;
            Generates = generates;
            Overcomes = overcomes;
        }

        public string Name { get; }

        public string Element { get; }

        public string Generates { get; }

        public string Overcomes { get; }
    }

    public interface IRaceCatalog : IUtility
    {
        IReadOnlyList<RaceDef> Races { get; }
        RaceDef Find(string name);
        IReadOnlyList<string> Names();
        bool IsGenerating(string raceA, string raceB);
        bool IsOvercoming(string raceA, string raceB);
        string Draw(IDraw draw);
    }

    internal sealed class RaceCatalog : IRaceCatalog
    {
        readonly ContentBook _book;

        public RaceCatalog(ContentBook book) => _book = book;

        public IReadOnlyList<RaceDef> Races => _book.Races;

        public RaceDef Find(string name) => _book.FindRace(name);

        public IReadOnlyList<string> Names()
        {
            var names = new string[_book.Races.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = _book.Races[i].Name;
            return names;
        }

        public string Draw(IDraw draw) => draw.Choose(Names());

        public bool IsGenerating(string raceA, string raceB)
        {
            if (string.IsNullOrEmpty(raceA) || string.IsNullOrEmpty(raceB))
                return false;

            var defA = Find(raceA);
            var defB = Find(raceB);
            if (defA == null || defB == null)
                return false;

            return defA.Generates == defB.Name || defB.Generates == defA.Name;
        }

        public bool IsOvercoming(string attacker, string defender)
        {
            if (string.IsNullOrEmpty(attacker) || string.IsNullOrEmpty(defender))
                return false;

            var defA = Find(attacker);
            return defA != null && defA.Overcomes == defender;
        }
    }
}
