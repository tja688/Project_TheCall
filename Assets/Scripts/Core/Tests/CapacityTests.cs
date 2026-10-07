using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class CapacityTests : RulesFixture
    {
        [Test]
        public void 镜眼获得一层产能且获得的那一次主动执行不计()
        {
            var hostId = PlaceSolarAndBreath();
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { hostId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void 同一关之后的主动执行按层数另开一点且产能计分自身不再听产能()
        {
            var hostId = PlaceSolarAndBreath();
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { hostId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void 产能按主动执行计次因此双头能量体只另开一次()
        {
            Open(
                "镜眼",
                "左能量体",
                "右能量体",
                "双头能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心",
                "能量体");

            var hostId = IdOf("镜眼");
            App.SendCommand(new DiscardMonsterCommand(IdOf("双头能量体")));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { hostId, hostId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "双头能量体", "双头能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 2, 2 }));
        }

        [Test]
        public void 回响嗓在右侧相邻停留开始时给技能再走一次且中途新次数不算()
        {
            Open(
                "能量体",
                "左能量体",
                "右能量体",
                "回响嗓",
                "能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心");

            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "能量体")
                .ToArray();
            var leftId = breaths[0].Id;
            var rightId = breaths[1].Id;
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("回响嗓"), OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(rightId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { leftId, rightId, rightId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体", "能量体", "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 5, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 5, 5 }));
        }

        [Test]
        public void 左复制腺体只给左侧相邻的产能量技能加一次产能额外次数()
        {
            Open(
                "镜眼",
                "左能量体",
                "右能量体",
                "能量体",
                "左复制腺体",
                "能量体",
                "奇异香",
                "怪异香",
                "汲取鼻");

            var hostId = IdOf("镜眼");
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "能量体")
                .ToArray();
            App.SendCommand(new DiscardMonsterCommand(breaths[0].Id));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            var plainId = breaths[1].Id;
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("左复制腺体"), OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(plainId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(
                landings.Select(landing => landing.MonsterId).ToArray(),
                Is.EqualTo(new[] { hostId, hostId, plainId }));
            Assert.That(
                landings.Select(landing => landing.SkillName).ToArray(),
                Is.EqualTo(new[] { "能量体", "能量体", "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 5, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 5, 5 }));
        }

        [Test]
        public void 产能量技能的再走次数是额外触发与产能额外次数相加且秒点先走完()
        {
            Open(
                "镜眼",
                "左能量体",
                "右能量体",
                "回响嗓",
                "能量体",
                "左复制腺体",
                "奇异香",
                "怪异香",
                "汲取鼻");

            var hostId = IdOf("镜眼");
            var headId = IdOf("回响嗓");
            var organId = IdOf("左复制腺体");
            App.SendCommand(new DiscardMonsterCommand(IdOf("能量体")));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(headId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(organId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { hostId, hostId, hostId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体", "能量体", "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 5, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 5, 5 }));
        }

        [Test]
        public void 再走是新的主动执行并重新报价且停留开始时记下的次数不因来源消失而取消()
        {
            Open(
                "吞噬大嘴",
                "左能量体",
                "右能量体",
                "回响嗓",
                "能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心");

            var devourerId = IdOf("吞噬大嘴");
            var headId = IdOf("回响嗓");
            App.SendCommand(new PlaceMonsterCommand(headId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings, Is.Empty);
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[0].MonsterId, Is.Null);
            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Not.Contain(headId));
        }

        [Test]
        public void 没有永久的产能离开关卡时清掉()
        {
            UseLevel(
                new ScriptedLevelCatalog(0),
                "镜眼",
                "左能量体",
                "右能量体",
                "能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心",
                "双头能量体");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var hostId = IdOf("镜眼");
            App.SendCommand(new DiscardMonsterCommand(IdOf("能量体")));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void 隔一格不算相邻所以回响嗓和左复制腺体都不给再走()
        {
            Open(
                "能量体",
                "左能量体",
                "右能量体",
                "回响嗓",
                "左复制腺体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心");

            var breathId = IdOf("能量体");
            App.SendCommand(new PlaceMonsterCommand(IdOf("回响嗓"), OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(IdOf("左复制腺体"), OperationArea.Extraction, 4));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void 只有镜眼时下一次主动执行支付已经持有的层()
        {
            Open(
                "镜眼",
                "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心",
                "双头能量体",
                "蜜能量体");

            var hostId = IdOf("镜眼");
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Landings(), Is.Empty);

            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Landings(), Is.Empty);
        }

        [Test]
        public void 不报价的技能也会支付已经持有的产能()
        {
            Open(
                "镜眼",
                "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心",
                "双头能量体",
                "蜜能量体");

            var hostId = IdOf("镜眼");
            App.SendCommand(new DiscardMonsterCommand(IdOf("奇异香")));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(Landings(), Is.Empty);
        }

        string PlaceSolarAndBreath()
        {
            Open(
                "镜眼",
                "左能量体",
                "右能量体",
                "能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心",
                "双头能量体");

            var hostId = IdOf("镜眼");
            App.SendCommand(new DiscardMonsterCommand(IdOf("能量体")));
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
