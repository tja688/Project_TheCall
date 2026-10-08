using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class PassiveToolTests : RulesFixture
    {
        [Test]
        public void 上级员工证使提取槽多一格且第六格参与结算()
        {
            OpenAndReachShop(
                "能量体",
                "左能量体",
                "右能量体",
                "能量体",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");

            Assert.That(App.SendQuery(new ExtractionSlotsQuery()).Count, Is.EqualTo(5));

            App.SendCommand(new BuyToolCommand("上级员工证"));
            App.SendCommand(new LeaveShopCommand());

            var breathId = App.SendQuery(new MonsterCageQuery())
                .First(monster => Holds(monster, "能量体")).Id;
            Assert.That(App.SendQuery(new ExtractionSlotsQuery()).Count, Is.EqualTo(6));

            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 5));
            App.SendCommand(new ConfirmSettlementCommand());

            var cells = App.SendQuery(new ExtractionSlotsQuery());
            Assert.That(cells.Count, Is.EqualTo(6));
            Assert.That(cells[5].MonsterId, Is.EqualTo(breathId));
            var landing = Landings().Single();
            Assert.That(landing.MonsterId, Is.EqualTo(breathId));
            Assert.That(landing.SkillName, Is.EqualTo("能量体"));
            Assert.That(landing.Base, Is.EqualTo(5));
            Assert.That(landing.Multiplier, Is.EqualTo(1));
            Assert.That(landing.Energy, Is.EqualTo(5));
        }

        [Test]
        public void 急急装置把第一段产能量主动执行的落地能量翻倍之后的不翻()
        {
            OpenAndReachShop(TwoBreaths);

            App.SendCommand(new BuyToolCommand("急急装置"));
            App.SendCommand(new LeaveShopCommand());

            var breaths = Breaths();
            App.SendCommand(new PlaceMonsterCommand(breaths[0].Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breaths[1].Id, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breaths[0].Id, breaths[1].Id }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体", "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 2, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 10, 5 }));
        }

        [Test]
        public void 急急装置把第一段主动执行里的每次报价都翻倍()
        {
            OpenAndReachShop(
                "能量体",
                "左能量体",
                "右能量体",
                "双头能量体",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "镜眼");
            App.SendCommand(new BuyToolCommand("急急装置"));
            App.SendCommand(new LeaveShopCommand());

            var doubleId = IdOf("双头能量体");
            var breathId = IdOf("能量体");
            App.SendCommand(new PlaceMonsterCommand(doubleId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { doubleId, doubleId, breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "双头能量体", "双头能量体", "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 2, 2, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 2, 2, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 4, 4, 5 }));
        }

        [Test]
        public void 再走是之后的主动执行急急装置不翻()
        {
            OpenAndReachShop(
                "能量体",
                "左能量体",
                "右能量体",
                "回响嗓",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");
            App.SendCommand(new BuyToolCommand("急急装置"));
            App.SendCommand(new LeaveShopCommand());

            var breathId = IdOf("能量体");
            App.SendCommand(new PlaceMonsterCommand(IdOf("回响嗓"), OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breathId, breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体", "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 5 }));
            Assert.That(landings.All(landing => landing.Factors.All(factor => factor.Label != "急急装置")), Is.True);
        }

        [Test]
        public void 急急装置不翻第一段执行带出的产能()
        {
            OpenAndReachShop(
                "能量体",
                "左能量体",
                "右能量体",
                "镜眼",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");
            App.SendCommand(new BuyToolCommand("急急装置"));
            App.SendCommand(new LeaveShopCommand());

            var hostId = IdOf("能量体");
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 2 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 10 }));
        }

        [Test]
        public void 劣胜装置把单词条怪物的产能量计分翻倍()
        {
            OpenAndReachShop(
                new FixedTools(new ToolDefinition("劣胜装置", 10, Rarity.Gold, ToolEffect.DoubleSingleAffix, 0)),
                "能量体",
                "左能量体",
                "右能量体",
                "换位手",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");
            App.SendCommand(new BuyToolCommand("劣胜装置"));
            App.SendCommand(new LeaveShopCommand());

            var hostId = IdOf("换位手");
            App.SendCommand(new DiscardMonsterCommand(IdOf("能量体")));
            InstallFromSlot(hostId);
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { hostId, hostId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体", "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 10, 10 }));
        }

        [Test]
        public void 同时带消灭和永久或者没有词条都不是单词条()
        {
            OpenAndReachShop(
                new FixedTools(new ToolDefinition("劣胜装置", 10, Rarity.Gold, ToolEffect.DoubleSingleAffix, 0)),
                TwoBreaths);
            App.SendCommand(new BuyToolCommand("劣胜装置"));
            App.SendCommand(new LeaveShopCommand());

            var devourId = IdOf("吞噬大嘴");
            var breathId = Breaths()[0].Id;
            App.SendCommand(new PlaceMonsterCommand(devourId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void 身上合起来有两个词条的怪物不是单词条()
        {
            OpenAndReachShop(
                new FixedTools(new ToolDefinition("劣胜装置", 10, Rarity.Gold, ToolEffect.DoubleSingleAffix, 0)),
                "能量体",
                "左能量体",
                "右能量体",
                "换位手",
                "换位手",
                "奇异香",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");
            App.SendCommand(new BuyToolCommand("劣胜装置"));
            App.SendCommand(new LeaveShopCommand());

            var hostId = IdOf("吞噬大嘴");
            App.SendCommand(new DiscardMonsterCommand(IdOf("能量体")));
            InstallFromSlot(hostId);
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 7 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void 急急和独孤叠乘进倍率且计分不改变工具次序()
        {
            OpenAndReachShop(
                new FixedTools(
                    new ToolDefinition("急急装置", 10, Rarity.White, ToolEffect.DoubleFirstEnergy, 0),
                    new ToolDefinition("劣胜装置", 10, Rarity.Gold, ToolEffect.DoubleSingleAffix, 0)),
                "能量体",
                "左能量体",
                "右能量体",
                "换位手",
                "能量体",
                "奇异香",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");
            App.SendCommand(new BuyToolCommand("急急装置"));
            App.SendCommand(new BuyToolCommand("劣胜装置"));
            App.SendCommand(new LeaveShopCommand());
            Assert.That(App.SendQuery(new RunLedgerQuery()).Tools, Is.EqualTo(new[] { "急急装置", "劣胜装置" }));

            var hostId = IdOf("换位手");
            var breaths = Breaths();
            App.SendCommand(new DiscardMonsterCommand(breaths[0].Id));
            InstallFromSlot(hostId);
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breaths[1].Id, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { hostId, hostId, breaths[1].Id }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体", "能量体", "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 5, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 4, 4, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 20, 20, 5 }));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Tools, Is.EqualTo(new[] { "急急装置", "劣胜装置" }));
        }

        [Test]
        public void 急急装置的翻倍乘进孤独心已有的倍率()
        {
            OpenAndReachShop(
                "孤独心",
                "能量体",
                "左能量体",
                "右能量体",
                "能量体",
                "奇异香",
                "汲取鼻",
                "换位手",
                "双头能量体");
            App.SendCommand(new BuyToolCommand("急急装置"));
            App.SendCommand(new LeaveShopCommand());

            var hostId = IdOf("孤独心");
            App.SendCommand(new DiscardMonsterCommand(IdOf("能量体")));
            InstallFromSlot(hostId);
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single();
            Assert.That(landing.MonsterId, Is.EqualTo(hostId));
            Assert.That(landing.SkillName, Is.EqualTo("能量体"));
            Assert.That(landing.Base, Is.EqualTo(5));
            Assert.That(landing.Multiplier, Is.EqualTo(4));
            Assert.That(landing.Energy, Is.EqualTo(20));
        }

        static readonly string[] TwoBreaths =
        {
            "能量体",
            "左能量体",
            "右能量体",
            "能量体",
            "奇异香",
            "汲取鼻",
            "孤独心",
            "吞噬大嘴",
            "双头能量体",
        };

        MonsterView[] Breaths() =>
            App.SendQuery(new MonsterCageQuery()).Where(monster => Holds(monster, "能量体")).ToArray();

        string IdOf(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, skillName)).Id;

        void OpenAndReachShop(params string[] openingDraws) => OpenAndReachShop(null, openingDraws);

        void OpenAndReachShop(IToolCatalog tools, params string[] openingDraws)
        {
            UseRules(new ScriptedDraw(openingDraws), new ScriptedLevelCatalog(5), tools);
            KeepOpened();
            var breathId = App.SendQuery(new MonsterCageQuery())
                .First(monster => Holds(monster, "能量体")).Id;
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
        }
    }
}
