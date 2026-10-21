using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
namespace TheCall.Tests
{
    public sealed class MonsterAppearanceTests
    {
        [Test] public void WildBirthsCoverOnlyTheSixBaseDrawingsWithStableColor()
        {
            var seen=new HashSet<int>();var palettes=new HashSet<int>();
            foreach(var seed in Enumerable.Range(-6000,12000).Concat(new[]{int.MinValue,int.MaxValue}))
            {
                var a=MonsterAppearance.FromSeed(seed);seen.Add(a.Recipe);palettes.Add(a.Palette);
                Assert.That(a,Is.EqualTo(MonsterAppearance.FromSeed(seed)));
                Assert.That(a.IsCultivated,Is.False);Assert.That(a.Palette,Is.InRange(0,5));
            }
            Assert.That(seen.OrderBy(i=>i),Is.EqualTo(new[]{5,6,8,9,10,11}));
            Assert.That(palettes.Count,Is.EqualTo(6));
        }
        [Test] public void TwoAndThreeParentBirthsUseOnlyCompleteCultivatedDrawings()
        {
            foreach(var count in new[]{2,3})
            {
                var parents=Enumerable.Range(0,count).Select(Uniform).ToArray();
                var seen=new HashSet<int>();var colors=new HashSet<int>();
                for(var seed=0;seed<10000;seed++)
                {
                    var child=MonsterAppearance.Breed(parents,seed);seen.Add(child.Recipe);colors.Add(child.Palette);
                    Assert.That(child.IsCultivated,Is.True);
                    Assert.That(child,Is.EqualTo(MonsterAppearance.Breed(parents,seed)));
                    Assert.That(child,Is.EqualTo(MonsterAppearance.Breed(new[]{Uniform(9),Uniform(10)},seed)),"Parent features never assemble body parts");
                }
                Assert.That(seen.OrderBy(i=>i),Is.EqualTo(new[]{0,1,2,3,4,7}));Assert.That(colors.Count,Is.EqualTo(6));
            }
        }
        [Test] public void BreedingRequiresTwoParentsAndOverloadsAgree()
        {
            Assert.Throws<ArgumentNullException>(()=>MonsterAppearance.Breed(null,0));
            Assert.Throws<ArgumentException>(()=>MonsterAppearance.Breed(new[]{Uniform(0)},0));
            for(var i=0;i<128;i++)Assert.That(MonsterAppearance.Breed(Uniform(0),Uniform(1),i),Is.EqualTo(MonsterAppearance.Breed(new[]{Uniform(0),Uniform(1)},i)));
        }
        internal static MonsterAppearance Uniform(int value)=>new MonsterAppearance(value,value,value);
        internal static int[] Genes(MonsterAppearance value)=>new[]{value.Recipe,value.Palette,value.Variation};
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
            Assert.That(child.Appearance.IsCultivated, Is.True);
            Assert.That(child.Appearance.Palette, Is.InRange(0,5));
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
