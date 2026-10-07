using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class UpdatedSkillTests : RulesFixture
    {
        [Test]
        public void 传能耳让右侧每一只怪物都加上二()
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
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "能量体")
                .Select(monster => monster.Id)
                .ToArray();
            App.SendCommand(new PlaceMonsterCommand(breaths[0], OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("传能耳"), OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(breaths[1], OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(breaths[2], OperationArea.Extraction, 3));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings().Where(landing => landing.SkillName == "能量体").ToArray();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breaths[0], breaths[1], breaths[2] }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 7, 7 }));
        }

        [Test]
        public void 重血脉心按技能名给提取槽里每一只同名技能加一次()
        {
            UseLevel(
                new ScriptedLevelCatalog(0),
                "奇异香",
                "怪异香",
                "蜜能量体",
                "能量体",
                "能量体",
                "重血脉心",
                "换位手",
                "汲取鼻",
                "双头能量体");
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "能量体")
                .Select(monster => monster.Id)
                .ToArray();
            var before = App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id).ToArray();
            App.SendCommand(new PlaceMonsterCommand(breaths[0], OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            var child = App.SendQuery(new MonsterCageQuery()).Single(monster => !before.Contains(monster.Id));
            App.SendCommand(new DiscardMonsterCommand(IdOf("重血脉心")));
            App.SendCommand(new EquipSkillCommand(child.Id, 0));
            App.SendCommand(new PlaceMonsterCommand(breaths[0], OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(child.Id, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(breaths[1], OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings().Where(landing => landing.SkillName == "能量体").ToArray();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[]
            {
                breaths[0], breaths[0], child.Id, child.Id, breaths[1], breaths[1],
            }));
            Assert.That(landings.All(landing => landing.Energy == 5), Is.True);
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
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
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
            App.SendQuery(new MonsterCageQuery()).Single(monster => monster.SkillNames.Single() == skillName).Id;
    }
}
