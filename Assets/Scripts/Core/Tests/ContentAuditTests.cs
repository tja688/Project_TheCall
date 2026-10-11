using System.IO;
using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    [TestFixture]
    public sealed class ContentAuditTests : RulesFixture
    {
        ContentBook _book;

        [SetUp]
        public void InitBook()
        {
#if UNITY_5_3_OR_NEWER
            var path = Path.Combine(UnityEngine.Application.streamingAssetsPath, "call-book.json");
#else
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "call-book.json");
#endif
            _book = ContentBook.Parse(File.ReadAllText(path));
        }

        [Test]
        public void 技能库包含三十四个有效技能且稀有度分布为十六白十四蓝四金()
        {
            Assert.That(_book.Skills.Count, Is.EqualTo(34));

            var whites = _book.Skills.Where(s => s.Rarity == Rarity.White).ToList();
            var blues = _book.Skills.Where(s => s.Rarity == Rarity.Blue).ToList();
            var golds = _book.Skills.Where(s => s.Rarity == Rarity.Gold).ToList();

            Assert.That(whites.Count, Is.EqualTo(16), "白技能应为 16 个");
            Assert.That(blues.Count, Is.EqualTo(14), "蓝技能应为 14 个");
            Assert.That(golds.Count, Is.EqualTo(4), "金技能应为 4 个");
        }

        [Test]
        public void 五个退役技能不得出现在技能库中()
        {
            var retired = new[] { "吞噬大嘴", "争锋基因", "潜藏基因", "重血脉心", "孤独心" };
            var names = _book.Skills.Select(s => s.Name).ToHashSet();

            foreach (var r in retired)
            {
                Assert.That(names.Contains(r), Is.False, $"退役技能 {r} 不应存在于技能库中");
            }
        }

        [Test]
        public void 关键改动技能属性符合最新规格要求()
        {
            var honey = _book.FindSkill("蜜能量体");
            Assert.That(honey, Is.Not.Null);
            Assert.That(honey.Rarity, Is.EqualTo(Rarity.White));
            var honeyQuote = honey.Get(EffectKind.EnergyQuote);
            Assert.That(honeyQuote.A, Is.EqualTo(3), "蜜能量体基础报价应修改为 3");

            var ear = _book.FindSkill("传能耳");
            Assert.That(ear, Is.Not.Null);
            Assert.That(ear.Rarity, Is.EqualTo(Rarity.White), "传能耳应为白稀有度");
            Assert.That(ear.Has(EffectKind.RightNeighborOnLeftActive), Is.True, "传能耳应仅对右侧相邻生效");
            Assert.That(ear.Get(EffectKind.RightNeighborOnLeftActive).A, Is.EqualTo(2));
        }

        [Test]
        public void 性格库包含二十五个性格且稀有度分布为十二白五蓝五负面三金()
        {
            Assert.That(_book.Personalities.Count, Is.EqualTo(25));

            var whites = _book.Personalities.Where(p => p.Rarity == Rarity.White).ToList();
            var blues = _book.Personalities.Where(p => p.Rarity == Rarity.Blue).ToList();
            var negatives = _book.Personalities.Where(p => p.Rarity == Rarity.Negative).ToList();
            var golds = _book.Personalities.Where(p => p.Rarity == Rarity.Gold).ToList();

            Assert.That(whites.Count, Is.EqualTo(12), "白性格应为 12 个");
            Assert.That(blues.Count, Is.EqualTo(5), "蓝性格应为 5 个");
            Assert.That(negatives.Count, Is.EqualTo(5), "负面性格应为 5 个");
            Assert.That(golds.Count, Is.EqualTo(3), "金性格应为 3 个");

            var lonely = _book.FindPersonality("孤僻");
            Assert.That(lonely, Is.Not.Null, "退役技能孤独心应平移为白性格孤僻");
            Assert.That(lonely.Rarity, Is.EqualTo(Rarity.White));

            var kin = _book.FindPersonality("重视血脉");
            Assert.That(kin, Is.Not.Null, "退役技能重血脉心应平移为白性格重视血脉");
            Assert.That(kin.Rarity, Is.EqualTo(Rarity.White));
        }

        [Test]
        public void 工具库包含十八个工具且价格分为三十五十和八十()
        {
            Assert.That(_book.Tools.Count, Is.EqualTo(18));

            var whites = _book.Tools.Where(t => t.Rarity == Rarity.White).ToList();
            var blues = _book.Tools.Where(t => t.Rarity == Rarity.Blue).ToList();
            var golds = _book.Tools.Where(t => t.Rarity == Rarity.Gold).ToList();

            Assert.That(whites.Count, Is.EqualTo(10), "白工具应为 10 个");
            Assert.That(blues.Count, Is.EqualTo(5), "蓝工具应为 5 个");
            Assert.That(golds.Count, Is.EqualTo(3), "金工具应为 3 个");

            Assert.That(whites.All(t => t.Price == 30), Is.True, "白工具售价应全部为 30");
            Assert.That(blues.All(t => t.Price == 50), Is.True, "蓝工具售价应全部为 50");
            Assert.That(golds.All(t => t.Price == 80), Is.True, "金工具售价应全部为 80");

            var badge = _book.Tools.FirstOrDefault(t => t.Name == "上级员工证");
            Assert.That(badge, Is.Not.Null);
            Assert.That(badge.Price, Is.EqualTo(50));
            Assert.That(badge.Rarity, Is.EqualTo(Rarity.Blue));

            var single = _book.Tools.FirstOrDefault(t => t.Name == "劣胜装置");
            Assert.That(single, Is.Not.Null);
            Assert.That(single.Price, Is.EqualTo(80));
            Assert.That(single.Rarity, Is.EqualTo(Rarity.Gold));
            Assert.That(single.Effect, Is.EqualTo(ToolEffect.SingleSkillBonus));
        }

        [Test]
        public void 科技库包含六项科技且花费皆为一点科技点()
        {
            Assert.That(_book.Techs.Count, Is.EqualTo(6));

            var expectedFlags = new[]
            {
                TechFlag.BreedingSkill,
                TechFlag.ExtraBreedingSlot,
                TechFlag.ExtraSkill,
                TechFlag.ExtraParent,
                TechFlag.Modifier,
                TechFlag.GeneStorage,
            };

            var actualFlags = _book.Techs.Select(t => t.Flag).ToArray();
            Assert.That(actualFlags, Is.EquivalentTo(expectedFlags));
            Assert.That(_book.Techs.All(t => t.Cost == 1), Is.True, "所有科技研发费用应均为 1 点");
        }

        [Test]
        public void 种族库包含五大种族相生闭环与相克闭环()
        {
            Assert.That(_book.Races.Count, Is.EqualTo(5));

            var races = _book.Races.ToDictionary(r => r.Name);
            Assert.That(races.ContainsKey("森林"), Is.True);
            Assert.That(races.ContainsKey("天空"), Is.True);
            Assert.That(races.ContainsKey("沙漠"), Is.True);
            Assert.That(races.ContainsKey("山脉"), Is.True);
            Assert.That(races.ContainsKey("海生"), Is.True);

            Assert.That(races["森林"].Generates, Is.EqualTo("天空"));
            Assert.That(races["天空"].Generates, Is.EqualTo("沙漠"));
            Assert.That(races["沙漠"].Generates, Is.EqualTo("山脉"));
            Assert.That(races["山脉"].Generates, Is.EqualTo("海生"));
            Assert.That(races["海生"].Generates, Is.EqualTo("森林"));

            Assert.That(races["森林"].Overcomes, Is.EqualTo("沙漠"));
            Assert.That(races["天空"].Overcomes, Is.EqualTo("山脉"));
            Assert.That(races["沙漠"].Overcomes, Is.EqualTo("海生"));
            Assert.That(races["山脉"].Overcomes, Is.EqualTo("森林"));
            Assert.That(races["海生"].Overcomes, Is.EqualTo("天空"));
        }

        [Test]
        public void 心情库包含五档且能量加成从负二到正二()
        {
            Assert.That(_book.Moods.Count, Is.EqualTo(5));

            var moods = _book.Moods.OrderBy(m => m.Tier).ToList();
            Assert.That(moods[0].Name, Is.EqualTo("倦怠"));
            Assert.That(moods[0].EnergyDelta, Is.EqualTo(-2));

            Assert.That(moods[1].Name, Is.EqualTo("疲倦"));
            Assert.That(moods[1].EnergyDelta, Is.EqualTo(-1));

            Assert.That(moods[2].Name, Is.EqualTo("普通"));
            Assert.That(moods[2].EnergyDelta, Is.EqualTo(0));

            Assert.That(moods[3].Name, Is.EqualTo("热情"));
            Assert.That(moods[3].EnergyDelta, Is.EqualTo(1));

            Assert.That(moods[4].Name, Is.EqualTo("热爱"));
            Assert.That(moods[4].EnergyDelta, Is.EqualTo(2));
        }

        [Test]
        public void 关卡库定义七个关卡且达标能量与超额能量正确()
        {
            Assert.That(_book.Levels.Count, Is.EqualTo(7));

            var expected = new (int Due, int Excess)[]
            {
                (50, 60),
                (75, 90),
                (100, 120),
                (150, 175),
                (200, 230),
                (250, 285),
                (300, 350)
            };

            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(_book.Levels[i].Due, Is.EqualTo(expected[i].Due), $"关卡 {i + 1} 达标线错误");
                Assert.That(_book.Levels[i].Excess, Is.EqualTo(expected[i].Excess), $"关卡 {i + 1} 超额线错误");
            }
        }

        [Test]
        public void 经济与权重参数符合规格配置()
        {
            Assert.That(_book.Economy.Wage, Is.EqualTo(60));
            Assert.That(_book.Economy.OvertimeWage, Is.EqualTo(40));
            Assert.That(_book.Price(Rarity.White), Is.EqualTo(10));
            Assert.That(_book.Price(Rarity.Blue), Is.EqualTo(20));
            Assert.That(_book.Price(Rarity.Gold), Is.EqualTo(30));

            Assert.That(_book.Weight(Rarity.White), Is.EqualTo(7));
            Assert.That(_book.Weight(Rarity.Blue), Is.EqualTo(2));
            Assert.That(_book.Weight(Rarity.Gold), Is.EqualTo(1));

            Assert.That(_book.Economy.BaseExtractionCells, Is.EqualTo(5));
            Assert.That(_book.Economy.BaseBreedingSlots, Is.EqualTo(1));
            Assert.That(_book.Economy.ExpandedBreedingSlots, Is.EqualTo(2));
            Assert.That(_book.Economy.BaseParents, Is.EqualTo(2));
            Assert.That(_book.Economy.ExpandedParents, Is.EqualTo(3));

            Assert.That(_book.Economy.PersonalityWhiteWeight, Is.EqualTo(5));
            Assert.That(_book.Economy.PersonalityBlueWeight, Is.EqualTo(2));
            Assert.That(_book.Economy.PersonalityNegativeWeight, Is.EqualTo(2));
            Assert.That(_book.Economy.PersonalityGoldWeight, Is.EqualTo(1));
        }

        [Test]
        public void 状态边界符合ADR0003模型划分()
        {
            TheCallApp.Reset();
            ContentGate.Use(_book);
            var app = TheCallApp.Interface;

            var run = app.GetModel<RunModel>();
            var level = app.GetModel<LevelModel>();

            Assert.That(run, Is.Not.Null, "RunModel 必须被注册");
            Assert.That(level, Is.Not.Null, "LevelModel 必须被注册");

            // RunModel 拥有全局局内生命周期状态
            Assert.That(run.Gold, Is.GreaterThanOrEqualTo(0));
            Assert.That(run.LevelNumber, Is.GreaterThanOrEqualTo(0));
            Assert.That(run.TechPoints, Is.GreaterThanOrEqualTo(0));
            Assert.That(run.Tools, Is.Not.Null);
            Assert.That(run.SkillSlots, Is.Not.Null);
            Assert.That(run.UnlockedTech, Is.Not.Null);

            // LevelModel 拥有关卡内阶段状态
            level.ClearProgress();
            Assert.That(level.Extraction.Count, Is.EqualTo(_book.Economy.BaseExtractionCells));
            Assert.That(level.BreedingSlotCount, Is.EqualTo(_book.Economy.BaseBreedingSlots));
            Assert.That(level.RefreshPrice, Is.EqualTo(5));
            Assert.That(level.Energy, Is.EqualTo(0));
        }
    }
}
