using System;
using NUnit.Framework;
using QFramework;
namespace TheCall.Tests
{
    public sealed class BenchRemediationTests : RulesFixture
    {
        [Test]
        public void 工作台复读允许同名实例并各自产出()
        {
            var row = new BenchMonster?[5];
            row[0] = new BenchMonster(new[] { "能量体", "能量体" });
            var report = ExtractionBench.Run(new BenchBoard(1, new[] { "复读腺" }, row));
            Assert.That(report.Produced, Is.EqualTo(10));
        }
        [Test]
        public void 工作台无复读拒绝重复且安于现状使用领域上限()
        {
            var row = new BenchMonster?[5];
            row[0] = new BenchMonster(new[] { "能量体", "能量体" });
            Assert.Throws<BenchRejectedException>(() => ExtractionBench.Run(new BenchBoard(1, Array.Empty<string>(), row)));
            row[0] = new BenchMonster(new[] { "能量体", "左能量体", "蜜能量体" }, "安于现状");
            Assert.Throws<BenchRejectedException>(() => ExtractionBench.Run(new BenchBoard(1, Array.Empty<string>(), row)));
        }
        [TestCase("急躁", "普通", "8")]
        [TestCase("热爱工作", "热爱", "7")]
        [TestCase("孤僻", "普通", "10")]
        public void 工作台预览使用真实人格心情且没有结算写入(string personality, string mood, string expected)
        {
            var row = new BenchMonster?[5];
            row[0] = new BenchMonster(new[] { "能量体" }, personality, mood, "天空");
            var board = new BenchBoard(1, Array.Empty<string>(), row);
            var details = ExtractionBench.Preview(board);
            Assert.That(details[0].Skills[0].Sentence, Does.Contain(expected));
            Assert.That(TheCallApp.Interface.GetModel<LevelModel>().Energy, Is.Zero);
            Assert.That(TheCallApp.Interface.GetSystem<SettlementSystem>().Entries, Is.Empty);
            Assert.That(ExtractionBench.Run(board).Produced, Is.EqualTo(int.Parse(expected)));
        }
        [Test]
        public void 工作台重复实例成长临时性独立且累计包括带入()
        {
            var row = new BenchMonster?[5];
            row[0] = new BenchMonster(new[] { "能量体", "能量体" }, instances: new[] { new BenchSkill("能量体", 2), new BenchSkill("能量体", 4, true) });
            var board = new BenchBoard(1, new[] { "复读腺" }, row, 40, 90);
            var details = ExtractionBench.Preview(board);
            Assert.That(details[0].Skills[0].Sentence, Does.Contain("7"));
            Assert.That(details[0].Skills[1].Sentence, Does.Contain("9"));
            Assert.That(details[0].Skills[0].IsTemporary, Is.False);
            Assert.That(details[0].Skills[1].IsTemporary, Is.True);
            var report = ExtractionBench.Run(board);
            Assert.That(report.Produced, Is.EqualTo(16));
            Assert.That(report.InitialEnergy, Is.EqualTo(40));
            Assert.That(report.AccumulatedEnergy, Is.EqualTo(56));
            Assert.That(report.MeetsDue, Is.True);
            Assert.That(report.MeetsExcess, Is.False);
        }
        [Test]
        public void 工作台亲缘可引用后格并对父子双向生效()
        {
            var row = new BenchMonster?[5];
            row[0] = new BenchMonster(new[] { "能量体" }, parentCells: new[] { 2 });
            row[2] = new BenchMonster(new[] { "能量体" }, "重视血脉");
            Assert.That(ExtractionBench.Run(new BenchBoard(1, Array.Empty<string>(), row)).Produced, Is.EqualTo(20));
            row[2] = new BenchMonster(new[] { "能量体" }, parentCells: new[] { 0 });
            Assert.Throws<BenchRejectedException>(() => ExtractionBench.Run(new BenchBoard(1, Array.Empty<string>(), row)));
        }
    }
}
