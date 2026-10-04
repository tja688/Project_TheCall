using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class CapacityTests : RulesFixture
    {
        [Test]
        public void 太阳能头获得一层产能且获得的那一次主动执行不计()
        {
            var hostId = PlaceSolarAndBreath();
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { hostId, hostId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息", "产能" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 1 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 1 }));
        }

        [Test]
        public void 同一关之后的主动执行按层数另开一点且产能计分自身不再听产能()
        {
            var hostId = PlaceSolarAndBreath();
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { hostId, hostId, hostId, hostId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "产能", "能量吐息", "产能", "产能" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 1, 5, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 1, 5, 1, 1 }));
        }

        [Test]
        public void 产能按主动执行计次因此双重吐息只另开一次()
        {
            Open(
                "太阳能头",
                "左能量体",
                "右能量体",
                "双重吐息",
                "增量小手",
                "增量大手",
                "残留提取腺体",
                "孤独心",
                "能量吐息");

            var hostId = IdOf("太阳能头");
            App.SendCommand(new DiscardMonsterCommand(IdOf("双重吐息")));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { hostId, hostId, hostId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "双重吐息", "双重吐息", "产能" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 2, 2, 1 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 2, 2, 1 }));
        }

        [Test]
        public void 再回首头在右侧相邻停留开始时给技能再走一次且中途新次数不算()
        {
            Open(
                "能量吐息",
                "左能量体",
                "右能量体",
                "再回首头",
                "能量吐息",
                "增量小手",
                "增量大手",
                "残留提取腺体",
                "孤独心");

            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "能量吐息")
                .ToArray();
            var leftId = breaths[0].Id;
            var rightId = breaths[1].Id;
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("再回首头"), OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(rightId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { leftId, rightId, rightId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息", "能量吐息", "能量吐息" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 5, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 5, 5 }));
        }

        [Test]
        public void 时间操控器官只给左侧相邻的产能量技能加一次产能额外次数()
        {
            Open(
                "太阳能头",
                "左能量体",
                "右能量体",
                "能量吐息",
                "时间操控器官",
                "能量吐息",
                "增量小手",
                "增量大手",
                "残留提取腺体");

            var hostId = IdOf("太阳能头");
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "能量吐息")
                .ToArray();
            App.SendCommand(new DiscardMonsterCommand(breaths[0].Id));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            var plainId = breaths[1].Id;
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("时间操控器官"), OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(plainId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(
                landings.Select(landing => landing.MonsterId).ToArray(),
                Is.EqualTo(new[] { hostId, hostId, hostId, hostId, plainId }));
            Assert.That(
                landings.Select(landing => landing.SkillName).ToArray(),
                Is.EqualTo(new[] { "能量吐息", "产能", "能量吐息", "产能", "能量吐息" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 1, 5, 1, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 1, 5, 1, 5 }));
        }

        [Test]
        public void 产能量技能的再走次数是额外触发与产能额外次数相加且秒点先走完()
        {
            Open(
                "太阳能头",
                "左能量体",
                "右能量体",
                "再回首头",
                "能量吐息",
                "时间操控器官",
                "增量小手",
                "增量大手",
                "残留提取腺体");

            var hostId = IdOf("太阳能头");
            var headId = IdOf("再回首头");
            var organId = IdOf("时间操控器官");
            App.SendCommand(new DiscardMonsterCommand(IdOf("能量吐息")));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(headId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(organId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[]
            {
                hostId, hostId, hostId, hostId, hostId, hostId, hostId, hostId, hostId,
            }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[]
            {
                "能量吐息", "产能", "产能", "能量吐息", "产能", "产能", "能量吐息", "产能", "产能",
            }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 1, 1, 5, 1, 1, 5, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1, 1, 1, 1, 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 1, 1, 5, 1, 1, 5, 1, 1 }));
        }

        [Test]
        public void 再走是新的主动执行并重新报价且停留开始时记下的次数不因来源消失而取消()
        {
            Open(
                "吞噬大嘴",
                "左能量体",
                "右能量体",
                "再回首头",
                "能量吐息",
                "增量小手",
                "增量大手",
                "残留提取腺体",
                "孤独心");

            var devourerId = IdOf("吞噬大嘴");
            var headId = IdOf("再回首头");
            App.SendCommand(new PlaceMonsterCommand(headId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { devourerId, devourerId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "吞噬大嘴", "吞噬大嘴" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 4, 6 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 4, 6 }));
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[0].MonsterId, Is.Null);
            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Not.Contain(headId));
        }

        [Test]
        public void 没有永久的产能离开关卡时清掉()
        {
            UseLevel(
                new ScriptedLevelCatalog(0),
                "太阳能头",
                "左能量体",
                "右能量体",
                "能量吐息",
                "增量小手",
                "增量大手",
                "残留提取腺体",
                "孤独心",
                "双重吐息");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var hostId = IdOf("太阳能头");
            App.SendCommand(new DiscardMonsterCommand(IdOf("能量吐息")));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { hostId, hostId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息", "产能" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 1 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 1 }));
        }

        [Test]
        public void 隔一格不算相邻所以再回首头和时间操控器官都不给再走()
        {
            Open(
                "能量吐息",
                "左能量体",
                "右能量体",
                "再回首头",
                "时间操控器官",
                "增量小手",
                "增量大手",
                "残留提取腺体",
                "孤独心");

            var breathId = IdOf("能量吐息");
            App.SendCommand(new PlaceMonsterCommand(IdOf("再回首头"), OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(IdOf("时间操控器官"), OperationArea.Extraction, 4));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void 只有太阳能头时下一次主动执行支付已经持有的层()
        {
            Open(
                "太阳能头",
                "左能量体",
                "右能量体",
                "增量小手",
                "增量大手",
                "残留提取腺体",
                "孤独心",
                "双重吐息",
                "分享之手");

            var hostId = IdOf("太阳能头");
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Landings(), Is.Empty);

            App.SendCommand(new ConfirmSettlementCommand());
            var landing = Landings().Single();
            Assert.That(landing.MonsterId, Is.EqualTo(hostId));
            Assert.That(landing.SkillName, Is.EqualTo("产能"));
            Assert.That(landing.Base, Is.EqualTo(1));
            Assert.That(landing.Energy, Is.EqualTo(1));
        }

        [Test]
        public void 不报价的技能也会支付已经持有的产能()
        {
            Open(
                "太阳能头",
                "左能量体",
                "右能量体",
                "增量小手",
                "增量大手",
                "残留提取腺体",
                "孤独心",
                "双重吐息",
                "分享之手");

            var hostId = IdOf("太阳能头");
            App.SendCommand(new DiscardMonsterCommand(IdOf("增量小手")));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single();
            Assert.That(landing.MonsterId, Is.EqualTo(hostId));
            Assert.That(landing.SkillName, Is.EqualTo("产能"));
            Assert.That(landing.Base, Is.EqualTo(1));
            Assert.That(landing.Energy, Is.EqualTo(1));
        }

        string PlaceSolarAndBreath()
        {
            Open(
                "太阳能头",
                "左能量体",
                "右能量体",
                "能量吐息",
                "增量小手",
                "增量大手",
                "残留提取腺体",
                "孤独心",
                "双重吐息");

            var hostId = IdOf("太阳能头");
            App.SendCommand(new DiscardMonsterCommand(IdOf("能量吐息")));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            return hostId;
        }

        void Open(params string[] names)
        {
            UseDraw(names);
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
        }

        string IdOf(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => monster.SkillNames.Single() == skillName).Id;
    }
}
