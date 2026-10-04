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
                "能量吐息",
                "左能量体",
                "右能量体",
                "能量吐息",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");

            Assert.That(App.SendQuery(new ExtractionSlotsQuery()).Count, Is.EqualTo(5));

            App.SendCommand(new BuyToolCommand("上级员工证"));
            App.SendCommand(new LeaveShopCommand());

            var breathId = App.SendQuery(new MonsterCageQuery())
                .First(monster => monster.SkillNames.Single() == "能量吐息").Id;
            Assert.That(App.SendQuery(new ExtractionSlotsQuery()).Count, Is.EqualTo(6));

            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 5));
            App.SendCommand(new ConfirmSettlementCommand());

            var cells = App.SendQuery(new ExtractionSlotsQuery());
            Assert.That(cells.Count, Is.EqualTo(6));
            Assert.That(cells[5].MonsterId, Is.EqualTo(breathId));
            var landing = App.SendQuery(new SettlementRecordQuery()).Single();
            Assert.That(landing.MonsterId, Is.EqualTo(breathId));
            Assert.That(landing.SkillName, Is.EqualTo("能量吐息"));
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

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breaths[0].Id, breaths[1].Id }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息", "能量吐息" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 2, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 10, 5 }));
        }

        [Test]
        public void 急急装置把第一段主动执行里的每次报价都翻倍()
        {
            OpenAndReachShop(
                "能量吐息",
                "左能量体",
                "右能量体",
                "双重吐息",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴",
                "太阳能头");
            App.SendCommand(new BuyToolCommand("急急装置"));
            App.SendCommand(new LeaveShopCommand());

            var doubleId = IdOf("双重吐息");
            var breathId = IdOf("能量吐息");
            App.SendCommand(new PlaceMonsterCommand(doubleId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { doubleId, doubleId, breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "双重吐息", "双重吐息", "能量吐息" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 2, 2, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 2, 2, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 4, 4, 5 }));
        }

        [Test]
        public void 再走是之后的主动执行急急装置不翻()
        {
            OpenAndReachShop(
                "能量吐息",
                "左能量体",
                "右能量体",
                "再回首头",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");
            App.SendCommand(new BuyToolCommand("急急装置"));
            App.SendCommand(new LeaveShopCommand());

            var breathId = IdOf("能量吐息");
            App.SendCommand(new PlaceMonsterCommand(IdOf("再回首头"), OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breathId, breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息", "能量吐息" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 2, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 10, 5 }));
        }

        [Test]
        public void 急急装置不翻第一段执行带出的产能()
        {
            OpenAndReachShop(
                "能量吐息",
                "左能量体",
                "右能量体",
                "太阳能头",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");
            App.SendCommand(new BuyToolCommand("急急装置"));
            App.SendCommand(new LeaveShopCommand());

            var hostId = IdOf("太阳能头");
            App.SendCommand(new DiscardMonsterCommand(IdOf("能量吐息")));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息", "产能" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 1 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 2, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 10, 1 }));
        }

        [Test]
        public void 独孤装置把单词条怪物的产能量计分翻倍()
        {
            OpenAndReachShop(
                new FixedTools(new ToolDefinition("独孤装置", 10, Rarity.Gold)),
                "能量吐息",
                "左能量体",
                "右能量体",
                "太阳能头",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");
            App.SendCommand(new BuyToolCommand("独孤装置"));
            App.SendCommand(new LeaveShopCommand());

            var hostId = IdOf("太阳能头");
            App.SendCommand(new DiscardMonsterCommand(IdOf("能量吐息")));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { hostId, hostId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息", "产能" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 1 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 10, 2 }));
        }

        [Test]
        public void 同时带消灭和永久或者没有词条都不是单词条()
        {
            OpenAndReachShop(
                new FixedTools(new ToolDefinition("独孤装置", 10, Rarity.Gold)),
                TwoBreaths);
            App.SendCommand(new BuyToolCommand("独孤装置"));
            App.SendCommand(new LeaveShopCommand());

            var devourId = IdOf("吞噬大嘴");
            var breathId = Breaths()[0].Id;
            App.SendCommand(new PlaceMonsterCommand(devourId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { devourId, breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "吞噬大嘴", "能量吐息" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 4, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 4, 5 }));
        }

        [Test]
        public void 身上合起来有两个词条的怪物不是单词条()
        {
            OpenAndReachShop(
                new FixedTools(new ToolDefinition("独孤装置", 10, Rarity.Gold)),
                "能量吐息",
                "左能量体",
                "右能量体",
                "太阳能头",
                "换位手",
                "增量小手",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");
            App.SendCommand(new BuyToolCommand("独孤装置"));
            App.SendCommand(new LeaveShopCommand());

            var hostId = IdOf("太阳能头");
            App.SendCommand(new DiscardMonsterCommand(IdOf("换位手")));
            App.SendCommand(new DiscardMonsterCommand(IdOf("能量吐息")));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "产能", "能量吐息", "产能" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 1, 5, 1 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 1, 5, 1 }));
        }

        [Test]
        public void 急急和独孤叠乘进倍率且计分不改变工具次序()
        {
            OpenAndReachShop(
                new FixedTools(
                    new ToolDefinition("急急装置", 10, Rarity.White),
                    new ToolDefinition("独孤装置", 10, Rarity.Gold)),
                "能量吐息",
                "左能量体",
                "右能量体",
                "太阳能头",
                "能量吐息",
                "增量小手",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");
            App.SendCommand(new BuyToolCommand("急急装置"));
            App.SendCommand(new BuyToolCommand("独孤装置"));
            App.SendCommand(new LeaveShopCommand());
            Assert.That(App.SendQuery(new RunLedgerQuery()).Tools, Is.EqualTo(new[] { "急急装置", "独孤装置" }));

            var hostId = IdOf("太阳能头");
            var breaths = Breaths();
            App.SendCommand(new DiscardMonsterCommand(breaths[0].Id));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breaths[1].Id, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { hostId, hostId, breaths[1].Id }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息", "产能", "能量吐息" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 1, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 4, 2, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 20, 2, 5 }));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Tools, Is.EqualTo(new[] { "急急装置", "独孤装置" }));
        }

        [Test]
        public void 急急装置的翻倍乘进孤独心已有的倍率()
        {
            OpenAndReachShop(TwoBreaths);
            App.SendCommand(new BuyToolCommand("急急装置"));
            App.SendCommand(new LeaveShopCommand());

            var lonelyId = IdOf("孤独心");
            App.SendCommand(new PlaceMonsterCommand(lonelyId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = App.SendQuery(new SettlementRecordQuery()).Single();
            Assert.That(landing.MonsterId, Is.EqualTo(lonelyId));
            Assert.That(landing.SkillName, Is.EqualTo("孤独心"));
            Assert.That(landing.Base, Is.EqualTo(4));
            Assert.That(landing.Multiplier, Is.EqualTo(4));
            Assert.That(landing.Energy, Is.EqualTo(16));
        }

        static readonly string[] TwoBreaths =
        {
            "能量吐息",
            "左能量体",
            "右能量体",
            "能量吐息",
            "增量小手",
            "残留提取腺体",
            "孤独心",
            "吞噬大嘴",
            "双重吐息",
        };

        MonsterView[] Breaths() =>
            App.SendQuery(new MonsterCageQuery()).Where(monster => monster.SkillNames.Single() == "能量吐息").ToArray();

        string IdOf(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => monster.SkillNames.Single() == skillName).Id;

        void OpenAndReachShop(params string[] openingDraws) => OpenAndReachShop(null, openingDraws);

        void OpenAndReachShop(IToolCatalog tools, params string[] openingDraws)
        {
            UseRules(new ScriptedDraw(openingDraws), new ScriptedLevelCatalog(5), tools);
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breathId = App.SendQuery(new MonsterCageQuery())
                .First(monster => monster.SkillNames.Single() == "能量吐息").Id;
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
        }
    }
}
