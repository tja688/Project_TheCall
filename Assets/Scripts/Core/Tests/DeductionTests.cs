using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class DeductionTests : RulesFixture
    {
        [Test]
        public void 第一次能量不足时记下欠额并清掉已偿付能量回到操作阶段()
        {
            var keptId = App.SendQuery(new OpeningCandidatesQuery())[0].Id;
            App.SendCommand(new KeepOpeningMonsterCommand(keptId));

            var breathId = App.SendQuery(new MonsterCageQuery())
                .Single(monster => monster.SkillNames.Single() == "能量吐息").Id;
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(App.SendQuery(new LevelTargetQuery()).EnergyDue, Is.EqualTo(50));
            Assert.That(App.SendQuery(new LevelShortfallQuery()), Is.EqualTo(45));
            Assert.That(App.SendQuery(new LevelEnergyQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Operation));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(0));
        }

        [Test]
        public void 加班只扣除欠额不重新支付整关应交能量()
        {
            UseLevel(
                new ScriptedLevelCatalog(10),
                "能量吐息",
                "左能量体",
                "右能量体",
                "能量吐息",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breathId = App.SendQuery(new MonsterCageQuery())
                .First(monster => monster.SkillNames.Single() == "能量吐息").Id;
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(App.SendQuery(new LevelShortfallQuery()), Is.EqualTo(5));
            Assert.That(App.SendQuery(new LevelTargetQuery()).EnergyDue, Is.EqualTo(10));

            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(App.SendQuery(new LevelTargetQuery()).EnergyDue, Is.EqualTo(10));
            Assert.That(Payment().Deducted, Is.EqualTo(5));
            Assert.That(App.SendQuery(new LevelEnergyQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Shop));
        }

        [Test]
        public void 本关加过班时偿付成功的工资是二十金币()
        {
            UseLevel(
                new ScriptedLevelCatalog(10),
                "能量吐息",
                "左能量体",
                "右能量体",
                "能量吐息",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breathId = App.SendQuery(new MonsterCageQuery())
                .First(monster => monster.SkillNames.Single() == "能量吐息").Id;
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(20));
            Assert.That(App.SendQuery(new LevelEnergyQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new LevelShortfallQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Shop));
        }

        [Test]
        public void 加班后仍不足则在扣除处游戏失败并清空怪物笼金币科技点科技工具和技能槽()
        {
            UseLevel(
                new ScriptedLevelCatalog(20),
                "能量吐息",
                "左能量体",
                "右能量体",
                "增量小手",
                "增量大手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴",
                "双重吐息",
                "增量小手");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var cage = App.SendQuery(new MonsterCageQuery());
            var breathId = cage.First(monster => monster.SkillNames.Single() == "能量吐息").Id;
            var spareId = cage.First(monster => monster.SkillNames.Single() == "增量小手").Id;
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new DiscardMonsterCommand(spareId));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(App.SendQuery(new LevelShortfallQuery()), Is.EqualTo(15));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "增量小手" }));
            Assert.That(App.SendQuery(new MonsterCageQuery()).Count(), Is.EqualTo(5));

            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Failed));
            Assert.That(App.SendQuery(new MonsterCageQuery()), Is.Empty);
            Assert.That(
                App.SendQuery(new ExtractionSlotsQuery()).All(cell => cell.MonsterId == null),
                Is.True);
            var ledger = App.SendQuery(new RunLedgerQuery());
            Assert.That(ledger.Gold, Is.EqualTo(0));
            Assert.That(ledger.TechPoints, Is.EqualTo(0));
            Assert.That(ledger.UnlockedTech, Is.Empty);
            Assert.That(ledger.Tools, Is.Empty);
            Assert.That(ledger.SkillSlots, Is.Empty);
            Assert.That(App.SendQuery(new LevelEnergyQuery()), Is.EqualTo(0));
        }

        [Test]
        public void 第一关超额能量是六十()
        {
            var keptId = App.SendQuery(new OpeningCandidatesQuery())[0].Id;
            App.SendCommand(new KeepOpeningMonsterCommand(keptId));

            var target = App.SendQuery(new LevelTargetQuery());
            Assert.That(target.LevelNumber, Is.EqualTo(1));
            Assert.That(target.EnergyDue, Is.EqualTo(50));
            Assert.That(target.ExcessEnergy, Is.EqualTo(60));
        }

        [Test]
        public void 偿付成功且产出达到超额能量时获得一点科技点()
        {
            PayWithTwoBreaths(5, 5);

            Assert.That(App.SendQuery(new RunLedgerQuery()).TechPoints, Is.EqualTo(1));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(40));
            Assert.That(App.SendQuery(new LevelEnergyQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Shop));
        }

        [Test]
        public void 产出达到超额能量但扣款后剩余低于超额能量时获得一点科技点()
        {
            PayWithTwoBreaths(5, 10);

            Assert.That(App.SendQuery(new RunLedgerQuery()).TechPoints, Is.EqualTo(1));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(40));
            Assert.That(Payment().Excess, Is.True);
        }

        [Test]
        public void 偿付成功但产出能量未达到超额能量时没有科技点()
        {
            PayWithTwoBreaths(5, 11);

            Assert.That(App.SendQuery(new RunLedgerQuery()).TechPoints, Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(40));
            Assert.That(App.SendQuery(new LevelEnergyQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Shop));
        }

        [Test]
        public void 加班后本次产出达到超额能量时获得一点科技点并且工资是二十()
        {
            UseLevel(
                new ScriptedLevelCatalog(10, 5),
                "能量吐息",
                "左能量体",
                "右能量体",
                "能量吐息",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "能量吐息")
                .ToArray();
            App.SendCommand(new PlaceMonsterCommand(breaths[0].Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new PlaceMonsterCommand(breaths[1].Id, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(App.SendQuery(new LevelTargetQuery()).EnergyDue, Is.EqualTo(10));
            Assert.That(Payment().Deducted, Is.EqualTo(5));
            Assert.That(App.SendQuery(new RunLedgerQuery()).TechPoints, Is.EqualTo(1));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(20));
            Assert.That(App.SendQuery(new LevelEnergyQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new LevelShortfallQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Shop));
        }

        void PayWithTwoBreaths(int energyDue, int excessEnergy)
        {
            UseLevel(
                new ScriptedLevelCatalog(energyDue, excessEnergy),
                "能量吐息",
                "左能量体",
                "右能量体",
                "能量吐息",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "能量吐息")
                .ToArray();
            App.SendCommand(new PlaceMonsterCommand(breaths[0].Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breaths[1].Id, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());
        }
    }
}
