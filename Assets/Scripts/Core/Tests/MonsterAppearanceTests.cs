using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class MonsterAppearanceTests
    {
        [Test]
        public void 同一外观种子返回相同配方和色板()
        {
            var first = MonsterAppearance.FromSeed(48291);
            var second = MonsterAppearance.FromSeed(48291);

            Assert.That(first.Recipe, Is.EqualTo(second.Recipe));
            Assert.That(first.Palette, Is.EqualTo(second.Palette));
            Assert.That(first.Recipe, Is.GreaterThanOrEqualTo(0).And.LessThan(MonsterAppearance.RecipeCount));
            Assert.That(first.Palette, Is.GreaterThanOrEqualTo(0).And.LessThan(MonsterAppearance.PaletteCount));
        }

        [Test]
        public void 子代色板跟随次代亲本且同一亲本种子配方稳定()
        {
            var first = new MonsterAppearance(1, 1);
            var second = new MonsterAppearance(4, 4);
            var child = MonsterAppearance.Breed(first, second, 1234);
            var again = MonsterAppearance.Breed(first, second, 1234);

            Assert.That(child.Palette, Is.EqualTo(second.Palette));
            Assert.That(child.Recipe, Is.EqualTo(again.Recipe));
            Assert.That(child.Recipe, Is.GreaterThanOrEqualTo(0).And.LessThan(MonsterAppearance.RecipeCount));
        }

        [Test]
        public void 所有结构基因在正负极值和连续种子下都合法且确定()
        {
            var seeds = Enumerable.Range(-2048, 4096).Concat(new[] { int.MinValue, int.MaxValue });
            var limits = new[] { 6, 6, 3, 6, 4, 4, 4, 4, 32 };
            foreach (var seed in seeds)
            {
                var first = MonsterAppearance.FromSeed(seed);
                Assert.That(first, Is.EqualTo(MonsterAppearance.FromSeed(seed)), "seed=" + seed);
                var values = Genes(first);
                for (var i = 0; i < values.Length; i++)
                    Assert.That(values[i], Is.InRange(0, limits[i] - 1), "gene=" + i + ", seed=" + seed);
            }
        }

        [Test]
        public void 同一体型和色板仍可以有不同结构组合()
        {
            var groups = Enumerable.Range(0, 512).Select(MonsterAppearance.FromSeed)
                .GroupBy(value => new { value.Recipe, value.Palette });
            Assert.That(groups.Any(group => group.Select(value => string.Join(",", Genes(value))).Distinct().Count() > 1), Is.True);
        }

        [Test]
        public void 完整构造会规范每个结构槽且保留两参构造约定()
        {
            var value = new MonsterAppearance(-1, 7, -1, 7, -1, 5, -1, 5, -1);
            Assert.That(Genes(value), Is.EqualTo(new[] { 5, 1, 2, 1, 3, 1, 3, 1, 31 }));
            var legacy = new MonsterAppearance(-1, 7);
            Assert.That(legacy.Recipe, Is.EqualTo(5));
            Assert.That(legacy.Palette, Is.EqualTo(1));
            Assert.That(legacy, Is.EqualTo(new MonsterAppearance(-1, 7)));
        }

        [Test]
        public void 三名亲本的每个结构槽都可遗传且不会出现无来源变异()
        {
            var parents = new[] { Uniform(0), Uniform(1), Uniform(2) };
            var seen = new HashSet<int>[9];
            for (var i = 0; i < seen.Length; i++) seen[i] = new HashSet<int>();
            for (var seed = -256; seed < 256; seed++)
            {
                var child = MonsterAppearance.Breed(parents, seed);
                Assert.That(child, Is.EqualTo(MonsterAppearance.Breed(parents, seed)));
                var genes = Genes(child);
                for (var i = 0; i < genes.Length; i++)
                {
                    Assert.That(new[] { 0, 1, 2 }, Does.Contain(genes[i]));
                    seen[i].Add(genes[i]);
                }
                Assert.That(child.Palette, Is.EqualTo(parents[1].Palette));
            }
            for (var i = 0; i < seen.Length; i++)
                Assert.That(seen[i], Is.EquivalentTo(i == 1 ? new[] { 1 } : new[] { 0, 1, 2 }), "gene=" + i);
        }

        [Test]
        public void 二亲重载和多亲入口得到相同结果()
        {
            var first = MonsterAppearance.FromSeed(37);
            var second = MonsterAppearance.FromSeed(91);
            for (var seed = 0; seed < 128; seed++)
                Assert.That(MonsterAppearance.Breed(first, second, seed),
                    Is.EqualTo(MonsterAppearance.Breed(new[] { first, second }, seed)));
        }

        [Test]
        public void 不完整的亲本集合在遗传入口被拒绝()
        {
            Assert.Throws<ArgumentNullException>(() => MonsterAppearance.Breed(null, 0));
            Assert.Throws<ArgumentException>(() => MonsterAppearance.Breed(Array.Empty<MonsterAppearance>(), 0));
            Assert.Throws<ArgumentException>(() => MonsterAppearance.Breed(new[] { Uniform(0) }, 0));
        }

        internal static MonsterAppearance Uniform(int value) => new MonsterAppearance(value, value, value, value, value, value, value, value, value);
        internal static int[] Genes(MonsterAppearance value) => new[]
        {
            value.Recipe, value.Palette, value.Head, value.Mouth, value.Arms,
            value.Legs, value.Crest, value.Pattern, value.Variation
        };
    }

    public sealed class MonsterAppearanceFlowTests : RulesFixture
    {
        [Test]
        public void 真实三亲培育保留来源和技能且外观不额外消耗玩法抽取()
        {
            OpenWithThreeParents(out var parents);
            var before = App.SendQuery(new MonsterCageQuery()).Select(value => value.Id).ToArray();
            for (var i = 0; i < parents.Length; i++)
                App.SendCommand(new PlaceMonsterCommand(parents[i].Id, OperationArea.Breeding, i));
            App.SendCommand(new ConfirmSettlementCommand());
            var draw = new AppearanceDraw();
            App.RegisterUtility<IDraw>(draw);

            App.SendCommand(new LeaveShopCommand());

            var child = App.SendQuery(new MonsterCageQuery()).Single(value => !before.Contains(value.Id));
            var model = App.GetModel<RunModel>().Find(child.Id);
            Assert.That(model.ParentIds, Is.EqualTo(parents.Select(value => value.Id).ToArray()));
            Assert.That(child.SkillNames, Is.EqualTo(new[] { "能量体", "左能量体", "右能量体" }));
            Assert.That(MonsterAppearanceTests.Genes(child.Appearance).All(value => value >= 0 && value <= 2), Is.True);
            Assert.That(MonsterAppearanceTests.Genes(child.Appearance), Does.Contain(2), "第三亲本必须能进入真实培育的外观来源");
            Assert.That(child.Appearance.Palette, Is.EqualTo(1));
            Assert.That(draw.Choices, Is.EqualTo(3), "现有规则抽取性格稀有度、性格和一名种族亲本");
            Assert.That(draw.Chances, Is.EqualTo(1), "现有规则只需一次性格继承判定");
        }

        [Test]
        public void 相同亲本跨关培育产生不同组合且亲本基因不变()
        {
            OpenWithThreeParents(out var parents);
            var births = new List<MonsterAppearance>();
            for (var day = 0; day < 4; day++)
            {
                if (day > 0) App.SendCommand(new BeginLevelCommand());
                var before = App.SendQuery(new MonsterCageQuery()).Select(value => value.Id).ToArray();
                for (var i = 0; i < parents.Length; i++)
                    App.SendCommand(new PlaceMonsterCommand(parents[i].Id, OperationArea.Breeding, i));
                App.SendCommand(new ConfirmSettlementCommand());
                App.SendCommand(new LeaveShopCommand());
                births.Add(App.SendQuery(new MonsterCageQuery()).Single(value => !before.Contains(value.Id)).Appearance);
            }
            Assert.That(births.Distinct().Count(), Is.GreaterThan(1));
            for (var i = 0; i < parents.Length; i++)
                Assert.That(App.GetModel<RunModel>().Find(parents[i].Id).Appearance, Is.EqualTo(MonsterAppearanceTests.Uniform(i)));
        }

        void OpenWithThreeParents(out Monster[] parents)
        {
            UseLevel(new ScriptedLevelCatalog(0), "能量体", "左能量体", "右能量体", "奇异香", "怪异香", "汲取鼻", "优质肉体", "良好肉体", "双头能量体");
            KeepOpened();
            var run = App.GetModel<RunModel>();
            run.AddTechPoint();
            App.SendCommand(new UnlockTechCommand("大乱炖"));
            var names = new[] { "能量体", "左能量体", "右能量体" };
            parents = new Monster[3];
            for (var i = 0; i < parents.Length; i++)
            {
                parents[i] = run.CreateMonster(new[] { names[i] }, appearance: MonsterAppearanceTests.Uniform(i));
                run.AddToCage(parents[i]);
            }
        }

        sealed class AppearanceDraw : IDraw
        {
            public int Choices { get; private set; }
            public int Chances { get; private set; }
            public T Choose<T>(IReadOnlyList<T> options) { Choices++; return options[0]; }
            public bool Chance(int percent) { Chances++; return false; }
        }
    }
}
