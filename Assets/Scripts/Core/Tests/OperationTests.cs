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
    }
}