using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class UpdatedSkillTests : RulesFixture
    {
        [Test]
        public void 传能耳只给右侧相邻怪物加上二()
        {
            UseDraw(
                "奇异香",
                "怪异香",
                "蜜能量体",
                "能量体",
                "传能耳",
                "能量体",
                "能量体",
                "换位手",
                "汲取鼻");
            KeepOpened();
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => Holds(monster, "能量体"))
                .Select(monster => monster.Id)
                .ToArray();
            App.SendCommand(new PlaceMonsterCommand(breaths[0], OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("传能耳"), OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(breaths[1], OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(breaths[2], OperationArea.Extraction, 3));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings().Where(landing => landing.SkillName == "能量体").ToArray();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breaths[0], breaths[1], breaths[2] }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 7, 5 }));
        }

        [Test]
        public void 群能量体按培育槽提取槽和怪物笼各算一次()
        {
            UseDraw(
                "奇异香",
                "怪异香",
                "蜜能量体",
                "群能量体",
                "能量体",
                "换位手",
                "汲取鼻",
                "双头能量体",
                "回响嗓");
            KeepOpened();
            var groupId = IdOf("群能量体");
            var otherId = IdOf("能量体");
            App.SendCommand(new PlaceMonsterCommand(otherId, OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(groupId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single();
            Assert.That(landing.SkillName, Is.EqualTo("群能量体"));
            Assert.That(landing.Energy, Is.EqualTo(7));
        }

        string IdOf(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, skillName)).Id;
    }
}
