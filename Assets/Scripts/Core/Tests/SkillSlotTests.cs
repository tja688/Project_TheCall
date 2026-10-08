using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class SkillSlotTests : RulesFixture
    {
        static readonly string[] Opening =
        {
            "能量体",
            "左能量体",
            "右能量体",
            "奇异香",
            "怪异香",
            "汲取鼻",
            "孤独心",
            "吞噬大嘴",
            "双头能量体",
        };

        [Test]
        public void 废弃立刻消灭怪物并把抽到的技能放入技能槽()
        {
            Begin("奇异香");

            var victim = App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, "奇异香"));
            Assert.That(victim.SkillNames, Is.EqualTo(new[] { "奇异香" }));

            App.SendCommand(new DiscardMonsterCommand(victim.Id));

            var cage = App.SendQuery(new MonsterCageQuery());
            Assert.That(cage.Select(monster => monster.Id), Does.Not.Contain(victim.Id));
            Assert.That(cage.Count(), Is.EqualTo(6));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "奇异香" }));
        }

        [Test]
        public void 废弃已经摆上提取槽的怪物后格子空出并且怪物消失()
        {
            Begin("奇异香");

            var victimId = App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, "奇异香")).Id;
            App.SendCommand(new PlaceMonsterCommand(victimId, OperationArea.Extraction, 2));
            App.SendCommand(new DiscardMonsterCommand(victimId));

            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[2].MonsterId, Is.Null);
            Assert.That(
                App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id),
                Does.Not.Contain(victimId));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "奇异香" }));
        }

        [Test]
        public void 技能槽能放下三份并且同名可以并存()
        {
            UseDraw(
                "能量体",
                "左能量体",
                "右能量体",
                "能量体",
                "怪异香",
                "左能量体",
                "孤独心",
                "吞噬大嘴",
                "双头能量体",
                "能量体",
                "左能量体",
                "怪异香");
            KeepFirst();

            var cage = App.SendQuery(new MonsterCageQuery());
            App.SendCommand(new DiscardMonsterCommand(cage.First(monster => Holds(monster, "能量体")).Id));
            App.SendCommand(new DiscardMonsterCommand(cage.Single(monster => Holds(monster, "左能量体")).Id));
            App.SendCommand(new DiscardMonsterCommand(cage.Single(monster => Holds(monster, "怪异香")).Id));

            Assert.That(
                App.SendQuery(new RunLedgerQuery()).SkillSlots,
                Is.EqualTo(new[] { "能量体", "左能量体", "怪异香" }));
        }

        [Test]
        public void 技能槽已满时废弃不发生怪物还在()
        {
            Begin("奇异香", "怪异香", "汲取鼻");
            var cage = App.SendQuery(new MonsterCageQuery());
            var stayingOnSlot = cage.Single(monster => Holds(monster, "能量体")).Id;
            var stayingInCage = cage.Single(monster => Holds(monster, "双头能量体")).Id;
            App.SendCommand(new DiscardMonsterCommand(cage.Single(monster => Holds(monster, "奇异香")).Id));
            App.SendCommand(new DiscardMonsterCommand(cage.Single(monster => Holds(monster, "怪异香")).Id));
            App.SendCommand(new DiscardMonsterCommand(cage.Single(monster => Holds(monster, "汲取鼻")).Id));
            App.SendCommand(new PlaceMonsterCommand(stayingOnSlot, OperationArea.Extraction, 1));

            App.SendCommand(new DiscardMonsterCommand(stayingOnSlot));
            App.SendCommand(new DiscardMonsterCommand(stayingInCage));

            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[1].MonsterId, Is.EqualTo(stayingOnSlot));
            Assert.That(
                App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id),
                Does.Contain(stayingInCage));
            Assert.That(
                App.SendQuery(new RunLedgerQuery()).SkillSlots,
                Is.EqualTo(new[] { "奇异香", "怪异香", "汲取鼻" }));
        }

        [Test]
        public void 开局阶段废弃候选不会消灭它也不会得到技能()
        {
            UseDraw("能量体", "左能量体", "右能量体");

            var candidateId = App.SendQuery(new OpeningCandidatesQuery())[0].Id;
            App.SendCommand(new DiscardMonsterCommand(candidateId));

            var candidates = App.SendQuery(new OpeningCandidatesQuery());
            Assert.That(candidates.Select(monster => monster.Id), Does.Contain(candidateId));
            Assert.That(candidates.Count(), Is.EqualTo(3));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.Empty);
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Opening));
        }

        [Test]
        public void 技能芯片落到怪物上不会装入()
        {
            Begin("奇异香");
            var host = App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, "能量体"));
            var donor = App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, "奇异香"));
            App.SendCommand(new PlaceMonsterCommand(host.Id, OperationArea.Extraction, 3));
            App.SendCommand(new DiscardMonsterCommand(donor.Id));
            var before = App.SendQuery(new MonsterQuery(host.Id)).SkillNames.ToArray();

            var onMonster = App.SendCommand(new CommitOperationDropCommand(new OperationDrop(
                DropPayload.SkillChip(0),
                new DropLanding(LandingPlace.Extraction, 3, host.Id))));
            var onCage = App.SendCommand(new CommitOperationDropCommand(new OperationDrop(
                DropPayload.SkillChip(0),
                new DropLanding(LandingPlace.Cage, 0, host.Id))));

            Assert.That(onMonster, Is.False);
            Assert.That(onCage, Is.False);
            Assert.That(App.SendQuery(new MonsterQuery(host.Id)).SkillNames, Is.EqualTo(before));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "奇异香" }));
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[3].MonsterId, Is.EqualTo(host.Id));
        }

        [Test]
        public void 废弃双技能怪物时放进技能槽的是抽到的那一个()
        {
            var keptId = Begin("能量体");
            App.SendCommand(new DiscardMonsterCommand(keptId));

            Assert.That(
                App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id),
                Does.Not.Contain(keptId));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "能量体" }));
        }

        string Begin(params string[] laterDraws)
        {
            var names = new string[Opening.Length + laterDraws.Length];
            Opening.CopyTo(names, 0);
            laterDraws.CopyTo(names, Opening.Length);
            UseDraw(names);
            return KeepFirst();
        }

        string KeepFirst()
        {
            var keptId = App.SendQuery(new OpeningCandidatesQuery())[0].Id;
            App.SendCommand(new KeepOpeningMonsterCommand(keptId));
            DrawAdapt.Restore(App);
            var cage = App.SendQuery(new MonsterCageQuery()).ToArray();
            return cage.Length == 0 ? keptId : cage[0].Id;
        }
    }
}
