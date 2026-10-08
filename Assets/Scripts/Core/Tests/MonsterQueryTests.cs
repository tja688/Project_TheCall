using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class MonsterQueryTests : RulesFixture
    {
        [Test]
        public void 摆上提取槽后怪物笼里没有它但查询仍返回技能报价和稀有度()
        {
            KeepOpened();
            var devourerId = App.SendQuery(new MonsterCageQuery())
                .Single(monster => Holds(monster, "吞噬大嘴")).Id;
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 2));

            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Not.Contain(devourerId));
            var placed = App.SendQuery(new MonsterQuery(devourerId));
            Assert.That(placed.SkillNames, Is.EqualTo(new[] { "吞噬大嘴" }));
            var devour = placed.Skills.Single(skill => skill.Name == "吞噬大嘴");
            Assert.That(devour.Quote, Is.EqualTo(0));
            Assert.That(devour.Rarity, Is.EqualTo(Rarity.White));
            Assert.That(placed.Immovable, Is.False);
            Assert.That(placed.Capacity, Is.EqualTo(0));

            App.SendCommand(new ConfirmSettlementCommand());

            var after = App.SendQuery(new MonsterQuery(devourerId));
            Assert.That(after.Skills.Single(skill => skill.Name == "吞噬大嘴").Quote, Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Operation));
        }

        [Test]
        public void 换位成功后查询能看到不动()
        {
            UseLevel(
                new ScriptedLevelCatalog(0),
                "镜眼",
                "左能量体",
                "右能量体",
                "换位手",
                "能量体",
                "奇异香",
                "怪异香",
                "孤独心",
                "双头能量体");

            KeepOpened();
            var solarId = IdOf("镜眼");
            var breathId = IdOf("能量体");
            var swapperId = IdOf("换位手");
            App.SendCommand(new PlaceMonsterCommand(solarId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(swapperId, OperationArea.Extraction, 3));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(App.SendQuery(new MonsterQuery(solarId)).Capacity, Is.EqualTo(0));
            Assert.That(App.SendQuery(new MonsterQuery(swapperId)).Immovable, Is.True);
            Assert.That(App.SendQuery(new MonsterQuery(swapperId)).Skills.Single().Rarity, Is.EqualTo(Rarity.Blue));
            Assert.That(App.SendQuery(new MonsterQuery("missing")), Is.Null);
        }

        string IdOf(string skillName) =>
            App.SendQuery(new MonsterCageQuery())
                .Single(monster => Holds(monster, skillName))
                .Id;
    }
}
