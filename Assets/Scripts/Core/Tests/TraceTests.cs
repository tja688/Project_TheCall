using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class TraceTests : RulesFixture
    {
        [Test]
        public void 工作台写下报价加项倍率并且达标来自结算记录()
        {
            var row = new BenchMonster?[5];
            row[0] = new BenchMonster(new[] { "增量小手" });
            row[1] = new BenchMonster(new[] { "能量吐息" });
            row[2] = new BenchMonster(new[] { "鼓励嘴" });
            var report = ExtractionBench.Run(new BenchBoard(1, new string[0], row));

            Assert.That(report.Produced, Is.EqualTo(12));
            Assert.That(report.Due, Is.EqualTo(50));
            Assert.That(report.ExcessAt, Is.EqualTo(60));
            Assert.That(report.MeetsDue, Is.False);
            Assert.That(report.MeetsExcess, Is.False);

            SettlementLanding landing = null;
            for (var i = 0; i < report.Entries.Count; i++)
            {
                var item = report.Entries[i] as SettlementLanding;
                if (item != null && item.SkillName == "能量吐息")
                    landing = item;
            }

            Assert.That(landing, Is.Not.Null);
            Assert.That(landing.Quote, Is.EqualTo(5));
            Assert.That(landing.SideCount, Is.EqualTo(0));
            Assert.That(landing.Adds.Count, Is.EqualTo(1));
            Assert.That(landing.Adds[0].Label, Is.EqualTo("增量小手"));
            Assert.That(landing.Adds[0].Amount, Is.EqualTo(1));
            Assert.That(landing.Factors.Count, Is.EqualTo(1));
            Assert.That(landing.Factors[0].Label, Is.EqualTo("鼓励嘴"));
            Assert.That(landing.Factors[0].Factor, Is.EqualTo(2));
            Assert.That(landing.Base, Is.EqualTo(6));
            Assert.That(landing.Multiplier, Is.EqualTo(2));
            Assert.That(landing.Energy, Is.EqualTo(12));
        }

        [Test]
        public void 左能量体的报价是每只基数且侧向人数单独记下()
        {
            var row = new BenchMonster?[5];
            row[0] = new BenchMonster(new[] { "能量吐息" });
            row[1] = new BenchMonster(new[] { "左能量体" });
            row[2] = new BenchMonster(new[] { "能量吐息" });
            var report = ExtractionBench.Run(new BenchBoard(1, new string[0], row));
            SettlementLanding side = null;
            for (var i = 0; i < report.Entries.Count; i++)
            {
                var item = report.Entries[i] as SettlementLanding;
                if (item != null && item.SkillName == "左能量体")
                    side = item;
            }

            Assert.That(side.Quote, Is.EqualTo(2));
            Assert.That(side.SideCount, Is.EqualTo(1));
            Assert.That(side.Base, Is.EqualTo(2));
            Assert.That(side.Energy, Is.EqualTo(2));
            Assert.That(side.MonsterId, Is.EqualTo(report.Placed[1]));
            Assert.That(report.Placed[0], Is.Not.EqualTo(report.Placed[1]));
            Assert.That(report.Placed[3], Is.Null);
        }
    }
}
