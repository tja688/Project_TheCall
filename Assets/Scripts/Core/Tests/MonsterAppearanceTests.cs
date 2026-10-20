using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
namespace TheCall.Tests
{
    public sealed class MonsterAppearanceTests
    {
        [Test] public void BirthsCoverTwelveTemplatesWithDeterministicLegalAccessories()
        {
            var counts = new int[12];
            foreach (var seed in Enumerable.Range(-6000,12000).Concat(new[] { int.MinValue,int.MaxValue }))
            {
                var a = MonsterAppearance.FromSeed(seed); counts[a.Recipe]++;
                Assert.That(a,Is.EqualTo(MonsterAppearance.FromSeed(seed)));
                Assert.That(a.Palette,Is.InRange(0,5)); Assert.That(a.Variation,Is.InRange(0,31));
                Assert.That(MonsterAppearance.Compatible(a.Recipe,a.HeadAccessory,a.BackAccessory),Is.True);
            }
            Assert.That(counts.All(n => n > 850 && n < 1150),Is.True,string.Join(",",counts));
        }
        [Test] public void BreedingKeepsTheWholePaletteAndUsesTwentyPercentOtherTemplates()
        {
            foreach (var count in new[] {2,3})
            {
                var parents = Enumerable.Range(0,count).Select(Uniform).ToArray(); var mutation = 0; var seen = new HashSet<int>();
                for (var seed = 0; seed < 10000; seed++)
                {
                    var a = MonsterAppearance.Breed(parents,seed); seen.Add(a.Recipe);
                    Assert.That(a,Is.EqualTo(MonsterAppearance.Breed(parents,seed)));
                    Assert.That(a.Palette,Is.EqualTo(parents[1].Palette));
                    Assert.That(MonsterAppearance.Compatible(a.Recipe,a.HeadAccessory,a.BackAccessory),Is.True);
                    if (!parents.Any(p => p.Recipe == a.Recipe)) mutation++;
                }
                Assert.That(mutation,Is.InRange(1850,2150)); Assert.That(seen.Count,Is.EqualTo(12));
            }
        }
        [Test] public void BreedingOnlyUsesParentAccessoriesOrTheSafeEmptyPair()
        {
            var parents = new[] { new MonsterAppearance(8,2,2,1,3),new MonsterAppearance(9,4,3,2,4) };
            for (var seed=0;seed<2000;seed++)
            {
                var c=MonsterAppearance.Breed(parents,seed);
                Assert.That(c.HeadAccessory == 0 && c.BackAccessory == 0 || parents.Any(p=>p.HeadAccessory==c.HeadAccessory) && parents.Any(p=>p.BackAccessory==c.BackAccessory),Is.True);
                Assert.That(c.Palette,Is.EqualTo(4));
            }
        }
        [Test] public void InvalidAccessoryPairsAreRejectedAndTwoParentOverloadsAgree()
        {
            Assert.Throws<ArgumentException>(()=>new MonsterAppearance(0,0,1,1,0));
            Assert.Throws<ArgumentNullException>(()=>MonsterAppearance.Breed(null,0));
            Assert.Throws<ArgumentException>(()=>MonsterAppearance.Breed(new[] {Uniform(0)},0));
            for (var i=0;i<128;i++) Assert.That(MonsterAppearance.Breed(Uniform(0),Uniform(1),i),Is.EqualTo(MonsterAppearance.Breed(new[] {Uniform(0),Uniform(1)},i)));
        }
        internal static MonsterAppearance Uniform(int value) => new MonsterAppearance(value,value,0,0,value);
        internal static int[] Genes(MonsterAppearance value) => new[] { value.Recipe,value.Palette,value.HeadAccessory,value.BackAccessory,value.Variation };
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
            Assert.That(MonsterAppearance.Compatible(child.Appearance.Recipe, child.Appearance.HeadAccessory, child.Appearance.BackAccessory), Is.True);
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
