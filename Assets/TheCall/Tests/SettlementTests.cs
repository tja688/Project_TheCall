using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class SettlementTests : RulesFixture
    {
        [Test]
        public void 能量吐息落地五点且不足时清掉本关能量()
        {
            var keptId = App.SendQuery(new OpeningCandidatesQuery())[0].Id;
            App.SendCommand(new KeepOpeningMonsterCommand(keptId));

            var breathId = App.SendQuery(new MonsterCageQuery())
                .Single(monster => monster.SkillNames.Single() == "能量吐息").Id;
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(App.SendQuery(new LevelShortfallQuery()), Is.EqualTo(45));
            Assert.That(App.SendQuery(new LevelEnergyQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Operation));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(0));
        }

        [Test]
        public void 播放头按这一刻的格子从左到右走且每只怪物只落地一次()
        {
            UseDraw(
                "能量吐息",
                "左能量体",
                "右能量体",
                "能量吐息",
                "增量小手",
                "能量吐息",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));

            var cage = App.SendQuery(new MonsterCageQuery());
            var atThree = cage[0].Id;
            var atZero = cage[1].Id;
            var between = cage[2].Id;
            var breeding = cage[3].Id;
            App.SendCommand(new PlaceMonsterCommand(atZero, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(between, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(atThree, OperationArea.Extraction, 3));
            App.SendCommand(new PlaceMonsterCommand(breeding, OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { atZero, atThree }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息", "能量吐息" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 5 }));
            Assert.That(App.SendQuery(new LevelShortfallQuery()), Is.EqualTo(40));
            Assert.That(App.SendQuery(new LevelEnergyQuery()), Is.EqualTo(0));
        }

        [Test]
        public void 能量超过应交时一次确认就清零剩余能量发放四十金币并停在商店()
        {
            ConfirmTwoBreaths(5);

            Assert.That(App.SendQuery(new LevelTargetQuery()).EnergyDue, Is.EqualTo(5));
            Assert.That(
                App.SendQuery(new SettlementRecordQuery()).Select(landing => landing.Energy).ToArray(),
                Is.EqualTo(new[] { 5, 5 }));
            AssertPaid();
        }

        [Test]
        public void 能量刚好等于应交时也发四十金币并停在商店()
        {
            ConfirmTwoBreaths(10);

            Assert.That(App.SendQuery(new LevelTargetQuery()).EnergyDue, Is.EqualTo(10));
            AssertPaid();
        }

        void ConfirmTwoBreaths(int energyDue)
        {
            UseLevel(
                new ScriptedLevelCatalog(energyDue),
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

        void AssertPaid()
        {
            Assert.That(App.SendQuery(new LevelEnergyQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(40));
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Shop));
        }
    }

    sealed class ScriptedLevelCatalog : ILevelCatalog
    {
        public ScriptedLevelCatalog(int energyDue, int excessEnergy = 60)
        {
            EnergyDue = energyDue;
            ExcessEnergy = excessEnergy;
        }

        public int EnergyDue { get; }

        public int ExcessEnergy { get; }
    }
}
