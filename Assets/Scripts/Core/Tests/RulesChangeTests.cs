using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class RulesChangeTests : RulesFixture
    {
        static readonly string[] Opening =
        {
            "吞噬大嘴", "奇异香",
            "良好肉体", "产金管道",
            "优质肉体", "产金管道",
            "能量体", "能量体", "左能量体", "右能量体",
            "奇异香", "怪异香", "汲取鼻", "换位手",
        };

        [Test]
        public void 刷新依次花费5和10和20失败时货架与价格不变()
        {
            UseRules(ScriptedDraw.Exact(Opening), new ScriptedLevelCatalog(5));
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breaths = App.SendQuery(new MonsterCageQuery()).Where(monster => Holds(monster, "能量体")).ToArray();
            App.SendCommand(new PlaceMonsterCommand(breaths[0].Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breaths[1].Id, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(App.SendQuery(new ShelfQuery()).NextRefreshPrice, Is.EqualTo(5));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(60));

            var first = Ids();
            App.SendCommand(new RefreshShelfCommand());
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(55));
            Assert.That(App.SendQuery(new ShelfQuery()).NextRefreshPrice, Is.EqualTo(10));
            Assert.That(Ids(), Is.Not.EqualTo(first));

            App.SendCommand(new RefreshShelfCommand());
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(45));
            Assert.That(App.SendQuery(new ShelfQuery()).NextRefreshPrice, Is.EqualTo(20));

            App.SendCommand(new RefreshShelfCommand());
            var stuck = Ids();
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(25));
            Assert.That(App.SendQuery(new ShelfQuery()).NextRefreshPrice, Is.EqualTo(40));

            App.SendCommand(new RefreshShelfCommand());
            App.SendCommand(new RefreshShelfCommand());
            Assert.That(Ids(), Is.EqualTo(stuck));
            Assert.That(App.SendQuery(new ShelfQuery()).NextRefreshPrice, Is.EqualTo(40));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(25));
        }

        [Test]
        public void 最左怪物没有产能量技能时急急装置不翻右侧()
        {
            var tools = new FixedTools(new ToolDefinition("急急装置", 10, Rarity.White, ToolEffect.DoubleFirstEnergy, 0));
            UseRules(ScriptedDraw.Exact(Opening), new ScriptedLevelCatalog(5), tools);
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breath = App.SendQuery(new MonsterCageQuery()).First(monster => Holds(monster, "能量体")).Id;
            App.SendCommand(new PlaceMonsterCommand(breath, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new BuyToolCommand("急急装置"));
            App.SendCommand(new LeaveShopCommand());

            var scent = App.SendQuery(new MonsterCageQuery()).First(monster => Holds(monster, "奇异香")).Id;
            var right = App.SendQuery(new MonsterCageQuery()).First(monster => Holds(monster, "能量体")).Id;
            App.SendCommand(new PlaceMonsterCommand(scent, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(right, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single(item => item.SkillName == "能量体");
            Assert.That(landing.MonsterId, Is.EqualTo(right));
            Assert.That(landing.Multiplier, Is.EqualTo(1));
            Assert.That(landing.Factors.Select(factor => factor.Label), Does.Not.Contain("急急装置"));
        }

        [Test]
        public void 急急装置绑定的技能之后再走仍然翻倍()
        {
            var tools = new FixedTools(new ToolDefinition("急急装置", 10, Rarity.White, ToolEffect.DoubleFirstEnergy, 0));
            var intents = new ClockIntents();
            UseRules(ScriptedDraw.Exact(Opening), new ScriptedLevelCatalog(5), tools, intents);
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breath = App.SendQuery(new MonsterCageQuery()).First(monster => Holds(monster, "能量体")).Id;
            App.SendCommand(new PlaceMonsterCommand(breath, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new BuyToolCommand("急急装置"));
            App.SendCommand(new LeaveShopCommand());

            var bound = App.SendQuery(new MonsterCageQuery()).First(monster => Holds(monster, "能量体")).Id;
            App.SendCommand(new PlaceMonsterCommand(bound, OperationArea.Extraction, 0));
            intents.WalkAgain();
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings().Where(item => item.SkillName == "能量体").ToArray();
            Assert.That(landings.Length, Is.EqualTo(2));
            Assert.That(landings.All(item => item.MonsterId == bound), Is.True);
            Assert.That(landings.Select(item => item.Multiplier).ToArray(), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(landings[0].Factors.Single().Label, Is.EqualTo("急急装置"));
            Assert.That(landings[1].Factors.Single().Label, Is.EqualTo("急急装置"));
        }

        [Test]
        public void 劣胜装置给产能量技能多走一次并且因素名是劣胜装置()
        {
            var tools = new FixedTools(new ToolDefinition("劣胜装置", 10, Rarity.Gold, ToolEffect.DoubleSingleAffix, 0));
            var shop = new[]
            {
                "双", "白", "能量体", "蓝", "换位手",
                "单", "白", "左能量体",
                "单", "白", "右能量体",
                "单", "白", "双头能量体",
                "劣胜装置",
            };
            var names = new string[Opening.Length + shop.Length];
            Opening.CopyTo(names, 0);
            shop.CopyTo(names, Opening.Length);
            UseRules(ScriptedDraw.Exact(names), new ScriptedLevelCatalog(5), tools);
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breath = App.SendQuery(new MonsterCageQuery()).First(monster => Holds(monster, "能量体")).Id;
            App.SendCommand(new PlaceMonsterCommand(breath, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            var offer = App.SendQuery(new ShelfQuery()).Monsters[0];
            App.SendCommand(new BuyMonsterCommand(offer.Id));
            App.SendCommand(new BuyToolCommand("劣胜装置"));
            App.SendCommand(new LeaveShopCommand());
            App.SendCommand(new PlaceMonsterCommand(offer.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings().Where(item => item.SkillName == "能量体").ToArray();
            Assert.That(landings.Length, Is.EqualTo(2));
            Assert.That(landings.Select(item => item.Multiplier).ToArray(), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(landings[0].Factors.Single().Label, Is.EqualTo("劣胜装置"));
            Assert.That(landings[1].Factors.Single().Label, Is.EqualTo("劣胜装置"));
        }

        [Test]
        public void 幅的前一个技能没有金时稀有度名单省略金()
        {
            var draw = new RecordingDraw(ScriptedNames());
            UseRules(draw, new ScriptedLevelCatalog(5));
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breath = App.SendQuery(new MonsterCageQuery()).First(monster => Holds(monster, "能量体")).Id;
            App.SendCommand(new PlaceMonsterCommand(breath, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var rarities = draw.Lists.Where(list => list.Length > 0 && list.All(item => item == "白" || item == "蓝" || item == "金")).ToArray();
            Assert.That(rarities[0], Is.EqualTo(new[] { "白", "白", "白", "白", "白", "白", "白", "蓝", "蓝" }));
            Assert.That(rarities[0], Does.Not.Contain("金"));
        }

        static string[] ScriptedNames()
        {
            var shop = new[]
            {
                "幅", "白", "吞噬大嘴", "白", "能量体",
                "单", "白", "左能量体",
                "单", "白", "右能量体",
                "单", "白", "双头能量体",
                "急急装置", "上级员工证",
            };
            var names = new string[Opening.Length + shop.Length];
            Opening.CopyTo(names, 0);
            shop.CopyTo(names, Opening.Length);
            return names;
        }

        string[] Ids() =>
            App.SendQuery(new ShelfQuery()).Monsters.Select(monster => monster.Id).ToArray();
    }
}
