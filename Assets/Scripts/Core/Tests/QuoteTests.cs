using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class QuoteTests : RulesFixture
    {
        [Test]
        public void 双头能量体在一次主动执行里报价两次且每次落地两点()
        {
            UseDraw(
                "双头能量体",
                "能量体",
                "左能量体",
                "右能量体",
                "奇异香",
                "汲取鼻",
                "良好肉体",
                "优质肉体",
                "蜜能量体");

            KeepOpened();
            var breathId = App.SendQuery(new MonsterCageQuery())
                .Single(monster => Holds(monster, "双头能量体")).Id;
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breathId, breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "双头能量体", "双头能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 2, 2 }));
        }

        string IdOf(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, skillName)).Id;
    }

    sealed class PinnedDraw : IDraw
    {
        readonly System.Collections.Generic.Queue<string> _names;

        public PinnedDraw(params string[] names) =>
            _names = new System.Collections.Generic.Queue<string>(DrawAdapt.Adapt(names));

        public string Pinned { get; set; }

        public T Choose<T>(System.Collections.Generic.IReadOnlyList<T> options)
        {
            if (Pinned != null)
            {
                foreach (var option in options)
                {
                    if (option is string text && text == Pinned)
                        return option;
                }
            }

            if (_names.Count == 0)
            {
                if (options == null || options.Count == 0)
                    throw new System.InvalidOperationException("抽取名单是空的。");

                return options[0];
            }

            var name = _names.Peek();
            foreach (var option in options)
            {
                if (option is string text && text == name)
                {
                    _names.Dequeue();
                    return option;
                }
            }

            if (options.Count > 0 && (options[0] is Rarity || options[0] is RaceDef || options[0] is PersonalityDef))
                return options[0];

            if (ContentGate.Current != null && options.Count > 0 && options[0] is string optName)
            {
                if (ContentGate.Current.FindRace(optName) != null && ContentGate.Current.FindRace(name) == null)
                    return options[0];
                if (ContentGate.Current.FindPersonality(optName) != null && ContentGate.Current.FindPersonality(name) == null)
                    return options[0];
            }

            name = _names.Dequeue();
            throw new System.InvalidOperationException("抽取名单里没有 " + name);
        }

        public bool ChanceHits { get; set; }

        public bool Chance(int percent) => ChanceHits;
    }
}
