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

            var name = _names.Dequeue();
            foreach (var option in options)
            {
                if (option is string text && text == name)
                    return option;
            }

            throw new System.InvalidOperationException("抽取名单里没有 " + name);
        }

        public bool ChanceHits { get; set; }

        public bool Chance(int percent) => ChanceHits;
    }
}
