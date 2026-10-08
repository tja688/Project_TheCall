using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class LandingResponseTests : RulesFixture
    {
        [Test]
        public void 汲取鼻在其他怪物落地能量时自己立刻再落地一点()
        {
            Open(
                "能量体",
                "左能量体",
                "右能量体",
                "汲取鼻",
                "能量体",
                "奇异香",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");

            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => Holds(monster, "能量体"))
                .ToArray();
            var glandId = IdOf("汲取鼻");
            App.SendCommand(new PlaceMonsterCommand(breaths[0].Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(glandId, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(breaths[1].Id, OperationArea.Extraction, 3));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(
                landings.Select(landing => landing.MonsterId).ToArray(),
                Is.EqualTo(new[] { breaths[0].Id, glandId, breaths[1].Id, glandId }));
            Assert.That(
                landings.Select(landing => landing.SkillName).ToArray(),
                Is.EqualTo(new[] { "能量体", "汲取鼻", "能量体", "汲取鼻" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 1, 5, 1 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 1, 5, 1 }));
        }

        [Test]
        public void 两只汲取鼻按从左到右各落地一点且不响应同名落地()
        {
            Open(
                "汲取鼻",
                "左能量体",
                "右能量体",
                "汲取鼻",
                "能量体",
                "奇异香",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");

            var glands = App.SendQuery(new MonsterCageQuery())
                .Where(monster => Holds(monster, "汲取鼻"))
                .ToArray();
            var rightId = glands[0].Id;
            var leftId = glands[1].Id;
            var breathId = IdOf("能量体");
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(rightId, OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 4));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(
                landings.Select(landing => landing.MonsterId).ToArray(),
                Is.EqualTo(new[] { breathId, leftId, rightId }));
            Assert.That(
                landings.Select(landing => landing.SkillName).ToArray(),
                Is.EqualTo(new[] { "能量体", "汲取鼻", "汲取鼻" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 1, 1 }));
        }

        [Test]
        public void 汲取鼻不响应自己这只怪物的落地()
        {
            Open(
                "汲取鼻",
                "左能量体",
                "右能量体",
                "能量体",
                "奇异香",
                "孤独心",
                "吞噬大嘴",
                "双头能量体",
                "蜜能量体");

            var hostId = IdOf("汲取鼻");
            App.SendCommand(new DiscardMonsterCommand(IdOf("能量体")));
            InstallFromSlot(hostId);
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { hostId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void 同一批从左到右响应且不在最上的汲取鼻也会落地()
        {
            Open(
                "奇异香",
                "左能量体",
                "右能量体",
                "汲取鼻",
                "汲取鼻",
                "能量体",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");

            var hostId = IdOf("奇异香");
            var glands = App.SendQuery(new MonsterCageQuery())
                .Where(monster => Holds(monster, "汲取鼻"))
                .ToArray();
            var rightId = glands[1].Id;
            var breathId = IdOf("能量体");
            App.SendCommand(new DiscardMonsterCommand(glands[0].Id));
            InstallFromSlot(hostId);
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(rightId, OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 4));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(
                landings.Select(landing => landing.MonsterId).ToArray(),
                Is.EqualTo(new[] { breathId, hostId, rightId }));
            Assert.That(
                landings.Select(landing => landing.SkillName).ToArray(),
                Is.EqualTo(new[] { "能量体", "汲取鼻", "汲取鼻" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 6, 1, 2 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 6, 1, 2 }));
        }

        [Test]
        public void 培育槽里的汲取鼻不响应提取落地()
        {
            Open(
                "能量体",
                "左能量体",
                "右能量体",
                "汲取鼻",
                "奇异香",
                "孤独心",
                "吞噬大嘴",
                "双头能量体",
                "蜜能量体");

            var breathId = IdOf("能量体");
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("汲取鼻"), OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void 蜜能量体落地两点且只给空档后的下一只后续产能量计分加三()
        {
            Open(
                "蜜能量体",
                "左能量体",
                "右能量体",
                "能量体",
                "能量体",
                "奇异香",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");

            var shareId = IdOf("蜜能量体");
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => Holds(monster, "能量体"))
                .ToArray();
            App.SendCommand(new PlaceMonsterCommand(shareId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breaths[0].Id, OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(breaths[1].Id, OperationArea.Extraction, 3));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(
                landings.Select(landing => landing.MonsterId).ToArray(),
                Is.EqualTo(new[] { shareId, breaths[0].Id, breaths[1].Id }));
            Assert.That(
                landings.Select(landing => landing.SkillName).ToArray(),
                Is.EqualTo(new[] { "蜜能量体", "能量体", "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 2, 8, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 2, 8, 5 }));
        }

        [Test]
        public void 没有下一个怪物时蜜能量体的加三消失()
        {
            Open(
                "能量体",
                "左能量体",
                "右能量体",
                "蜜能量体",
                "奇异香",
                "孤独心",
                "吞噬大嘴",
                "双头能量体",
                "汲取鼻");

            var breathId = IdOf("能量体");
            var shareId = IdOf("蜜能量体");
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(shareId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(
                landings.Select(landing => landing.MonsterId).ToArray(),
                Is.EqualTo(new[] { breathId, shareId }));
            Assert.That(
                landings.Select(landing => landing.SkillName).ToArray(),
                Is.EqualTo(new[] { "能量体", "蜜能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 2 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 2 }));
        }

        [Test]
        public void 蜜能量体的加三不写进技能报价且本次结算结束即失效()
        {
            Open(
                "蜜能量体",
                "左能量体",
                "右能量体",
                "能量体",
                "奇异香",
                "孤独心",
                "吞噬大嘴",
                "双头能量体",
                "汲取鼻");

            var shareId = IdOf("蜜能量体");
            var breathId = IdOf("能量体");
            App.SendCommand(new PlaceMonsterCommand(shareId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var first = Landings();
            Assert.That(first.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { shareId, breathId }));
            Assert.That(first.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "蜜能量体", "能量体" }));
            Assert.That(first.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 2, 8 }));
            Assert.That(first.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(first.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 2, 8 }));

            App.SendCommand(new ReturnMonsterCommand(shareId));
            App.SendCommand(new ConfirmSettlementCommand());

            var second = Landings();
            Assert.That(second.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breathId }));
            Assert.That(second.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体" }));
            Assert.That(second.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(second.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(second.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5 }));
        }

        void Open(params string[] names)
        {
            UseDraw(names);
            KeepOpened();
        }

        string IdOf(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, skillName)).Id;
    }
}
