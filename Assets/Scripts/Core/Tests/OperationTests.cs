using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class OperationTests : RulesFixture
    {
        [Test]
        public void 操作阶段可以把怪物摆上提取槽并在确认前拿回()
        {
            var keptId = App.SendQuery(new OpeningCandidatesQuery())[0].Id;
            App.SendCommand(new KeepOpeningMonsterCommand(keptId));

            var monsterId = App.SendQuery(new MonsterCageQuery())[0].Id;
            App.SendCommand(new PlaceMonsterCommand(monsterId, OperationArea.Extraction, 2));

            var cells = App.SendQuery(new ExtractionSlotsQuery());
            Assert.That(cells[2].MonsterId, Is.EqualTo(monsterId));
            Assert.That(cells.Where(cell => cell.Index != 2).All(cell => cell.MonsterId == null), Is.True);
            var placedCage = App.SendQuery(new MonsterCageQuery());
            Assert.That(placedCage.Count(), Is.EqualTo(6));
            Assert.That(placedCage.Select(monster => monster.Id), Does.Not.Contain(monsterId));

            App.SendCommand(new ReturnMonsterCommand(monsterId));

            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[2].MonsterId, Is.Null);
            var returned = App.SendQuery(new MonsterCageQuery());
            Assert.That(returned.Count(), Is.EqualTo(7));
            Assert.That(returned.Select(monster => monster.Id), Does.Contain(monsterId));
        }

        [Test]
        public void 同一只怪物不能同时处于提取槽和培育槽()
        {
            var keptId = App.SendQuery(new OpeningCandidatesQuery())[0].Id;
            App.SendCommand(new KeepOpeningMonsterCommand(keptId));

            var cage = App.SendQuery(new MonsterCageQuery());
            var onBreeding = cage[0].Id;
            var onExtraction = cage[1].Id;

            App.SendCommand(new PlaceMonsterCommand(onBreeding, OperationArea.Breeding, 0));

            Assert.That(App.SendQuery(new BreedingSlotsQuery())[0].MonsterId, Is.EqualTo(onBreeding));
            Assert.That(App.SendQuery(new ExtractionSlotsQuery()).All(cell => cell.MonsterId == null), Is.True);

            App.SendCommand(new PlaceMonsterCommand(onExtraction, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(onExtraction, OperationArea.Breeding, 1));
            App.SendCommand(new PlaceMonsterCommand(onBreeding, OperationArea.Extraction, 0));

            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[1].MonsterId, Is.EqualTo(onExtraction));
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[0].MonsterId, Is.Null);
            Assert.That(App.SendQuery(new BreedingSlotsQuery())[0].MonsterId, Is.EqualTo(onBreeding));
            Assert.That(App.SendQuery(new BreedingSlotsQuery())[1].MonsterId, Is.Null);

            var occupied = App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id).ToArray();
            Assert.That(occupied, Does.Not.Contain(onExtraction));
            Assert.That(occupied, Does.Not.Contain(onBreeding));

            App.SendCommand(new ReturnMonsterCommand(onBreeding));

            Assert.That(App.SendQuery(new BreedingSlotsQuery())[0].MonsterId, Is.Null);
            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Contain(onBreeding));
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[1].MonsterId, Is.EqualTo(onExtraction));
        }

        [Test]
        public void 笼里的怪物落到占用格时替换并把原占用者送回笼()
        {
            EnterOperation();
            var cage = CageIds();
            var incoming = cage[0];
            var occupant = cage[1];
            App.SendCommand(new PlaceMonsterCommand(occupant, OperationArea.Extraction, 2));

            var applied = App.SendCommand(new CommitOperationDropCommand(new OperationDrop(
                DropPayload.Monster(incoming),
                new DropLanding(LandingPlace.Extraction, 2, occupant))));

            Assert.That(applied, Is.True);
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[2].MonsterId, Is.EqualTo(incoming));
            Assert.That(CageIds(), Does.Contain(occupant));
            Assert.That(CageIds(), Does.Not.Contain(incoming));
            Assert.That(App.SendQuery(new MonsterQuery(occupant)), Is.Not.Null);
            Assert.That(App.SendQuery(new ExtractionSlotsQuery()).Count(cell => cell.MonsterId != null), Is.EqualTo(1));
        }

        [Test]
        public void 棋盘上的怪物交换提取格和培育位()
        {
            EnterOperation();
            var cage = CageIds();
            var onExtraction = cage[0];
            var onBreeding = cage[1];
            App.SendCommand(new PlaceMonsterCommand(onExtraction, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(onBreeding, OperationArea.Breeding, 0));
            var cageBefore = CageIds();

            var applied = App.SendCommand(new CommitOperationDropCommand(new OperationDrop(
                DropPayload.Monster(onExtraction),
                new DropLanding(LandingPlace.BreedingSeat, 0, onBreeding))));

            Assert.That(applied, Is.True);
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[1].MonsterId, Is.EqualTo(onBreeding));
            Assert.That(App.SendQuery(new BreedingSlotsQuery())[0].MonsterId, Is.EqualTo(onExtraction));
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[0].MonsterId, Is.Null);
            Assert.That(CageIds(), Is.EqualTo(cageBefore));
        }

        [Test]
        public void 棋盘上的怪物移入空格且仍不在笼里()
        {
            EnterOperation();
            var moving = CageIds()[0];
            App.SendCommand(new PlaceMonsterCommand(moving, OperationArea.Extraction, 0));
            var cageBefore = CageIds();

            var applied = App.SendCommand(new CommitOperationDropCommand(new OperationDrop(
                DropPayload.Monster(moving),
                new DropLanding(LandingPlace.Extraction, 4, null))));

            Assert.That(applied, Is.True);
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[0].MonsterId, Is.Null);
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[4].MonsterId, Is.EqualTo(moving));
            Assert.That(CageIds(), Is.EqualTo(cageBefore));
            Assert.That(CageIds(), Does.Not.Contain(moving));
        }

        [Test]
        public void 从提取格废弃怪物后格子空出并且技能进槽()
        {
            EnterOperation();
            var victim = CageIds()[1];
            App.SendCommand(new PlaceMonsterCommand(victim, OperationArea.Extraction, 2));

            var applied = App.SendCommand(new CommitOperationDropCommand(new OperationDrop(
                DropPayload.Monster(victim),
                new DropLanding(LandingPlace.Discard, 0, null))));

            Assert.That(applied, Is.True);
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[2].MonsterId, Is.Null);
            Assert.That(CageIds(), Does.Not.Contain(victim));
            Assert.That(App.SendQuery(new MonsterQuery(victim)), Is.Null);
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "奇异香" }));
        }

        [Test]
        public void 技能槽已有三份时从棋盘废弃不会发生()
        {
            EnterOperation();
            var cage = CageIds();
            App.SendCommand(new DiscardMonsterCommand(cage[1]));
            App.SendCommand(new DiscardMonsterCommand(cage[2]));
            App.SendCommand(new DiscardMonsterCommand(cage[3]));
            App.SendCommand(new PlaceMonsterCommand(cage[4], OperationArea.Extraction, 1));

            var applied = App.SendCommand(new CommitOperationDropCommand(new OperationDrop(
                DropPayload.Monster(cage[4]),
                new DropLanding(LandingPlace.Discard, 0, null))));

            Assert.That(applied, Is.False);
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[1].MonsterId, Is.EqualTo(cage[4]));
            Assert.That(App.SendQuery(new MonsterQuery(cage[4])).SkillNames, Is.EqualTo(new[] { "孤独心" }));
            Assert.That(
                App.SendQuery(new RunLedgerQuery()).SkillSlots,
                Is.EqualTo(new[] { "奇异香", "怪异香", "汲取鼻" }));
        }

        [Test]
        public void 技能芯片落到棋盘怪物上时装入()
        {
            EnterOperation();
            var host = CageIds()[0];
            var donor = CageIds()[1];
            App.SendCommand(new PlaceMonsterCommand(host, OperationArea.Extraction, 3));
            App.SendCommand(new DiscardMonsterCommand(donor));

            var applied = App.SendCommand(new CommitOperationDropCommand(new OperationDrop(
                DropPayload.SkillChip(0),
                new DropLanding(LandingPlace.Extraction, 3, host))));

            Assert.That(applied, Is.True);
            Assert.That(
                App.SendQuery(new MonsterQuery(host)).SkillNames,
                Is.EqualTo(new[] { "能量体", "奇异香" }));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.Empty);
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[3].MonsterId, Is.EqualTo(host));
        }

        [Test]
        public void 技能槽已满时培育技能不能拿回()
        {
            UseLevel(
                new ScriptedLevelCatalog(0, 0),
                "能量体",
                "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());
            App.SendCommand(new UnlockTechCommand("基因实验"));

            var cage = CageIds();
            string IdOf(string skillName)
            {
                for (var i = 0; i < cage.Length; i++)
                {
                    var monster = App.SendQuery(new MonsterQuery(cage[i]));
                    if (monster != null && monster.SkillNames.Count == 1 && monster.SkillNames[0] == skillName)
                        return cage[i];
                }

                Assert.Fail("笼里没有 " + skillName);
                return null;
            }

            App.SendCommand(new DiscardMonsterCommand(IdOf("孤独心")));
            App.SendCommand(new PlaceBreedingSkillCommand(0, 0));
            App.SendCommand(new DiscardMonsterCommand(IdOf("奇异香")));
            App.SendCommand(new DiscardMonsterCommand(IdOf("怪异香")));
            App.SendCommand(new DiscardMonsterCommand(IdOf("汲取鼻")));

            var applied = App.SendCommand(new CommitOperationDropCommand(new OperationDrop(
                DropPayload.BreedingSkill(0),
                new DropLanding(LandingPlace.SkillSlot, 0, null))));

            Assert.That(applied, Is.False);
            Assert.That(App.SendQuery(new BreedingPlansQuery())[0].SkillName, Is.EqualTo("孤独心"));
            Assert.That(
                App.SendQuery(new RunLedgerQuery()).SkillSlots,
                Is.EqualTo(new[] { "奇异香", "怪异香", "汲取鼻" }));
        }

        [Test]
        public void 非法落点不改变笼子和棋盘()
        {
            EnterOperation();
            var cage = CageIds();
            var skills = App.SendQuery(new RunLedgerQuery()).SkillSlots.ToArray();

            var applied = App.SendCommand(new CommitOperationDropCommand(new OperationDrop(
                DropPayload.Monster(cage[0]),
                new DropLanding(LandingPlace.EmptySpace, 0, null))));

            Assert.That(applied, Is.False);
            Assert.That(CageIds(), Is.EqualTo(cage));
            Assert.That(App.SendQuery(new ExtractionSlotsQuery()).All(cell => cell.MonsterId == null), Is.True);
            Assert.That(App.SendQuery(new BreedingSlotsQuery()).All(seat => seat.MonsterId == null), Is.True);
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(skills));
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Operation));
        }

        [Test]
        public void 同一次落子再提交一次返回失败且棋盘不变()
        {
            EnterOperation();
            var moving = CageIds()[0];
            App.SendCommand(new PlaceMonsterCommand(moving, OperationArea.Extraction, 0));
            var drop = new OperationDrop(
                DropPayload.Monster(moving),
                new DropLanding(LandingPlace.Extraction, 4, null));

            Assert.That(App.SendCommand(new CommitOperationDropCommand(drop)), Is.True);
            var cage = CageIds();

            Assert.That(App.SendCommand(new CommitOperationDropCommand(drop)), Is.False);
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[0].MonsterId, Is.Null);
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[4].MonsterId, Is.EqualTo(moving));
            Assert.That(CageIds(), Is.EqualTo(cage));
        }

        void EnterOperation()
        {
            var keptId = App.SendQuery(new OpeningCandidatesQuery())[0].Id;
            App.SendCommand(new KeepOpeningMonsterCommand(keptId));
        }

        string[] CageIds() => App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id).ToArray();
    }
}