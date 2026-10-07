using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class OpeningTests : RulesFixture
    {
        [Test]
        public void 开局给出三只单技能候选且怪物笼为空()
        {
            var candidates = App.SendQuery(new OpeningCandidatesQuery());
            var cage = App.SendQuery(new MonsterCageQuery());

            Assert.That(candidates.Select(monster => monster.Id).Distinct().Count(), Is.EqualTo(3));
            Assert.That(
                candidates.Select(monster => monster.SkillNames.Single()).ToArray(),
                Is.EqualTo(new[] { "能量体", "左能量体", "右能量体" }));
            Assert.That(cage, Is.Empty);
        }

        [Test]
        public void 留下一只后落选不进笼并补到七只单技能()
        {
            var candidates = App.SendQuery(new OpeningCandidatesQuery());
            var keptId = candidates[0].Id;
            var rejectedIds = new[] { candidates[1].Id, candidates[2].Id };

            App.SendCommand(new KeepOpeningMonsterCommand(keptId));

            var cage = App.SendQuery(new MonsterCageQuery());
            Assert.That(cage.Count(), Is.EqualTo(7));
            Assert.That(cage.All(monster => monster.SkillNames.Count == 1), Is.True);
            var cageIds = cage.Select(monster => monster.Id).ToArray();
            Assert.That(cageIds, Does.Contain(keptId));
            Assert.That(cageIds, Does.Not.Contain(rejectedIds[0]));
            Assert.That(cageIds, Does.Not.Contain(rejectedIds[1]));
            Assert.That(
                cage.Select(monster => monster.SkillNames.Single()),
                Is.EquivalentTo(new[]
                {
                    "能量体",
                    "奇异香",
                    "怪异香",
                    "汲取鼻",
                    "孤独心",
                    "吞噬大嘴",
                    "双头能量体",
                }));
        }

        [Test]
        public void 开局时金币科技点工具技能槽和已解锁科技都是空的()
        {
            AssertLedgerEmpty(App.SendQuery(new RunLedgerQuery()));

            var keptId = App.SendQuery(new OpeningCandidatesQuery())[0].Id;
            App.SendCommand(new KeepOpeningMonsterCommand(keptId));

            AssertLedgerEmpty(App.SendQuery(new RunLedgerQuery()));
        }

        static void AssertLedgerEmpty(RunLedger ledger)
        {
            Assert.That(ledger.Gold, Is.EqualTo(0));
            Assert.That(ledger.TechPoints, Is.EqualTo(0));
            Assert.That(ledger.Tools, Is.Empty);
            Assert.That(ledger.SkillSlots, Is.Empty);
            Assert.That(ledger.UnlockedTech, Is.Empty);
        }

        [Test]
        public void 留下后进入第一关操作阶段且应交能量为50提取槽有五格()
        {
            var keptId = App.SendQuery(new OpeningCandidatesQuery())[0].Id;
            App.SendCommand(new KeepOpeningMonsterCommand(keptId));

            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Operation));
            var target = App.SendQuery(new LevelTargetQuery());
            Assert.That(target.LevelNumber, Is.EqualTo(1));
            Assert.That(target.EnergyDue, Is.EqualTo(50));

            var cells = App.SendQuery(new ExtractionSlotsQuery());
            Assert.That(cells.Count(), Is.EqualTo(5));
            Assert.That(cells.Select(cell => cell.Index).ToArray(), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(cells.All(cell => cell.MonsterId == null), Is.True);
        }
    }
}
