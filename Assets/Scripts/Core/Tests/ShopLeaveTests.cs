using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class ShopLeaveTests : RulesFixture
    {
        [Test]
        public void 七关应交能量和超额能量按关卡表()
        {
            var due = new[] { 50, 75, 100, 150, 200, 250, 300 };
            var excess = new[] { 60, 90, 120, 175, 230, 285, 350 };

            for (var level = 1; level <= 7; level++)
            {
                var target = App.SendQuery(new LevelTargetQuery(level));
                Assert.That(target.LevelNumber, Is.EqualTo(level));
                Assert.That(target.EnergyDue, Is.EqualTo(due[level - 1]));
                Assert.That(target.ExcessEnergy, Is.EqualTo(excess[level - 1]));
            }
        }

        [Test]
        public void 不是第七关时离开商店进入下一关开始阶段并清空本关()
        {
            UseLevel(
                new ScriptedLevelCatalog(new[] { 0, 75 }, new[] { 100, 90 }),
                "能量体",
                "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");

            KeepOpened();
            var cage = App.SendQuery(new MonsterCageQuery());
            var onExtraction = cage[0].Id;
            var onBreeding = cage[1].Id;
            App.SendCommand(new PlaceMonsterCommand(onExtraction, OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(onBreeding, OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Shop));
            App.SendCommand(new LeaveShopCommand());

            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.LevelStart));
            var target = App.SendQuery(new LevelTargetQuery());
            Assert.That(target.LevelNumber, Is.EqualTo(2));
            Assert.That(target.EnergyDue, Is.EqualTo(75));
            Assert.That(target.ExcessEnergy, Is.EqualTo(90));
            Assert.That(App.SendQuery(new LevelEnergyQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new LevelShortfallQuery()), Is.EqualTo(0));

            var extraction = App.SendQuery(new ExtractionSlotsQuery());
            Assert.That(extraction.Count(), Is.EqualTo(5));
            Assert.That(extraction.All(cell => cell.MonsterId == null), Is.True);
            var breeding = App.SendQuery(new BreedingSlotsQuery());
            Assert.That(breeding.Count(), Is.EqualTo(2));
            Assert.That(breeding.All(slot => slot.MonsterId == null), Is.True);

            var returned = App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id).ToArray();
            Assert.That(returned, Does.Contain(onExtraction));
            Assert.That(returned, Does.Contain(onBreeding));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(60));
        }

        [Test]
        public void 第七关离开商店后游戏胜利并清空()
        {
            UseLevel(
                new ScriptedLevelCatalog(0, 100),
                "能量体",
                "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "双头能量体",
                "奇异香");

            KeepOpened();
            var spareId = App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, "奇异香") && monster.SkillNames.Count == 1).Id;
            App.SendCommand(new DiscardMonsterCommand(spareId));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "奇异香" }));

            for (var level = 1; level <= 6; level++)
            {
                App.SendCommand(new ConfirmSettlementCommand());
                Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Shop));
                Assert.That(App.SendQuery(new LevelTargetQuery()).LevelNumber, Is.EqualTo(level));
                App.SendCommand(new LeaveShopCommand());
                Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.LevelStart));
                Assert.That(App.SendQuery(new LevelTargetQuery()).LevelNumber, Is.EqualTo(level + 1));
            }

            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Shop));
            Assert.That(App.SendQuery(new LevelTargetQuery()).LevelNumber, Is.EqualTo(7));
            Assert.That(App.SendQuery(new LevelTargetQuery()).EnergyDue, Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(420));
            Assert.That(App.SendQuery(new MonsterCageQuery()).Count(), Is.EqualTo(6));

            App.SendCommand(new LeaveShopCommand());

            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Victory));
            Assert.That(App.SendQuery(new MonsterCageQuery()), Is.Empty);
            var ledger = App.SendQuery(new RunLedgerQuery());
            Assert.That(ledger.Gold, Is.EqualTo(0));
            Assert.That(ledger.TechPoints, Is.EqualTo(0));
            Assert.That(ledger.Tools, Is.Empty);
            Assert.That(ledger.SkillSlots, Is.Empty);
            Assert.That(ledger.UnlockedTech, Is.Empty);
            Assert.That(App.SendQuery(new LevelEnergyQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new LevelShortfallQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new LevelTargetQuery()).EnergyDue, Is.EqualTo(0));
            Assert.That(App.SendQuery(new LevelTargetQuery()).ExcessEnergy, Is.EqualTo(0));
            Assert.That(App.SendQuery(new ExtractionSlotsQuery()).All(cell => cell.MonsterId == null), Is.True);
            Assert.That(App.SendQuery(new BreedingSlotsQuery()).All(slot => slot.MonsterId == null), Is.True);
        }

        [Test]
        public void 不在商店时离开不进入下一关()
        {
            var keptId = App.SendQuery(new OpeningCandidatesQuery())[0].Id;
            App.SendCommand(new KeepOpeningMonsterCommand(keptId));

            App.SendCommand(new LeaveShopCommand());

            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Operation));
            var target = App.SendQuery(new LevelTargetQuery());
            Assert.That(target.LevelNumber, Is.EqualTo(1));
            Assert.That(target.EnergyDue, Is.EqualTo(50));
            Assert.That(target.ExcessEnergy, Is.EqualTo(60));
        }
    }
}
