using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class HandbookIndexTests : RulesFixture
    {
        [Test]
        public void 手册完整覆盖当前内容且每条关联都能到达()
        {
            var entries = App.SendQuery(new HandbookIndexQuery());
            Assert.That(entries.Select(e => e.Id).Distinct().Count(), Is.EqualTo(entries.Count));
            Assert.That(entries.Count(e => e.Category == HandbookCategory.Skill), Is.EqualTo(App.SendQuery(new SkillIndexQuery()).Count));
            Assert.That(entries.Count(e => e.Category == HandbookCategory.Personality), Is.EqualTo(ContentGate.Current.Personalities.Count));
            Assert.That(entries.Count(e => e.Category == HandbookCategory.Race), Is.EqualTo(ContentGate.Current.Races.Count));
            Assert.That(entries.Count(e => e.Category == HandbookCategory.Mood), Is.EqualTo(ContentGate.Current.Moods.Count));
            Assert.That(entries.Count(e => e.Category == HandbookCategory.Tool), Is.EqualTo(ContentGate.Current.Tools.Count));
            Assert.That(entries.Count(e => e.Category == HandbookCategory.Tech), Is.EqualTo(ContentGate.Current.Techs.Count));
            foreach (var entry in entries)
            {
                Assert.That(entry.Rule, Is.Not.Empty, entry.Id);
                foreach (var id in entry.Related) Assert.That(entries.Any(e => e.Id == id), Is.True, entry.Id + " → " + id);
            }
        }

        [Test]
        public void 手册效果和价格直接取自当前游戏目录并保留负面性格()
        {
            var entries = App.SendQuery(new HandbookIndexQuery());
            foreach (var skill in App.SendQuery(new SkillIndexQuery()))
                Assert.That(entries.Single(e => e.Id == "Skill:" + skill.Name).Rule, Is.EqualTo(skill.Sentence));
            foreach (var p in ContentGate.Current.Personalities)
            {
                var entry = entries.Single(e => e.Id == "Personality:" + p.Name);
                Assert.That(entry.Rule, Is.EqualTo(p.Description));
                Assert.That(entry.Rarity, Is.EqualTo(p.Rarity));
            }
            Assert.That(entries.Any(e => e.Category == HandbookCategory.Personality && e.Rarity == Rarity.Negative), Is.True);
            foreach (var tool in ContentGate.Current.Tools)
            {
                var entry = entries.Single(e => e.Id == "Tool:" + tool.Name);
                Assert.That(entry.Rule, Is.EqualTo(tool.Description));
                Assert.That(entry.Metadata, Does.Contain(tool.Price + " 金币"));
            }
            foreach (var tech in ContentGate.Current.Techs)
                Assert.That(entries.Single(e => e.Id == "Tech:" + tech.Name).Rule, Is.EqualTo(tech.Description));
        }

        [Test]
        public void 搜索可查效果且反复读取不改变对局()
        {
            var run = App.GetModel<RunModel>();
            var phase = run.Phase; var gold = run.Gold; var tech = run.TechPoints; var cage = run.Cage.ToArray();
            for (var i = 0; i < 3; i++)
            {
                var entries = App.SendQuery(new HandbookIndexQuery());
                Assert.That(entries.Single(e => e.Id == "Skill:能量体").Matches("  产生5点能量  "), Is.True);
                Assert.That(entries.Any(e => e.Matches("this_is_not_an_entry")), Is.False);
            }
            Assert.That(run.Phase, Is.EqualTo(phase)); Assert.That(run.Gold, Is.EqualTo(gold));
            Assert.That(run.TechPoints, Is.EqualTo(tech)); Assert.That(run.Cage, Is.EqualTo(cage));
        }

        [Test]
        public void 心情条目解释当前阈值并包含所有关卡需求()
        {
            var entries = App.SendQuery(new HandbookIndexQuery());
            Assert.That(entries.Single(e => e.Id == "Mood:疲倦").Rule, Does.Contain("已连续工作 2 天后"));
            Assert.That(entries.Single(e => e.Id == "Mood:倦怠").Rule, Does.Contain("已连续工作 4 天后"));
            Assert.That(entries.Single(e => e.Id == "Mood:热情").Rule, Does.Contain("已连续休息 2 天后"));
            Assert.That(entries.Single(e => e.Id == "Mood:热爱").Rule, Does.Contain("已连续休息 4 天后"));
            var levels = App.GetUtility<ILevelCatalog>();
            for (var day = 1; day <= 7; day++)
                Assert.That(entries.Single(e => e.Id == "Guide:七天目标与加班").Rule, Does.Contain("第 " + day + " 天    需求 " + levels.EnergyDue(day) + "    超额门槛 " + levels.ExcessEnergy(day)));
        }
    }
}
