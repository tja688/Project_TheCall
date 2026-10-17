using System.Linq;
using NUnit.Framework;
using QFramework;

namespace TheCall.Tests
{
    public sealed class FacilityStatusTests : RulesFixture
    {
        [Test]
        public void MonitorTracksOwnershipAcrossRestWorkAndBreeding()
        {
            KeepOpened();
            var before = App.SendQuery(new MonsterCageQuery()).ToArray();
            var first = before[0].Id;
            App.SendCommand(new PlaceMonsterCommand(first, OperationArea.Extraction, 2));
            var statuses = App.SendQuery(new FacilityStatusQuery());
            Assert.That(statuses.Select(x => x.Monster.Id), Is.EquivalentTo(before.Select(x => x.Id)));
            var worker = statuses.Single(x => x.Monster.Id == first);
            Assert.That(worker.Location, Is.EqualTo(ResidentLocation.Extraction));
            Assert.That(worker.Seat, Is.EqualTo(2));
            Assert.That(worker.Monster.Mood, Is.EqualTo(App.SendQuery(new MonsterQuery(first)).Mood));
            App.SendCommand(new ReturnMonsterCommand(first));
            Assert.That(App.SendQuery(new FacilityStatusQuery()).Single(x => x.Monster.Id == first).Location, Is.EqualTo(ResidentLocation.Resting));
            App.SendCommand(new PlaceMonsterCommand(first, OperationArea.Breeding, 0));
            Assert.That(App.SendQuery(new FacilityStatusQuery()).Single(x => x.Monster.Id == first).Location, Is.EqualTo(ResidentLocation.Breeding));
        }

        [Test]
        public void UnownedCandidatesNeverAppearInMonitor()
        {
            Assert.That(App.SendQuery(new OpeningCandidatesQuery()).Count, Is.GreaterThan(0));
            Assert.That(App.SendQuery(new FacilityStatusQuery()), Is.Empty);
        }
    }
}
