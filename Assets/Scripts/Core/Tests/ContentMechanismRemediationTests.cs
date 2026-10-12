using System.Linq;
using NUnit.Framework;
using QFramework;

namespace TheCall.Tests
{
    // All setups here are isolated rule fixtures, never evidence of a natural player run.
    public sealed class ContentMechanismRemediationTests : RulesFixture
    {
        void Fixture()
        {
            var run = App.GetModel<RunModel>(); run.EnterOperation(1);
            App.GetModel<LevelModel>().BeginLevel(1000, 1100);
            App.RegisterUtility<IDraw>(new SuccessfulDraw());
        }
        Monster Put(int cell, string[] skills, string personality = "稳定", string race = "森林", string mood = "普通")
        {
            var run = App.GetModel<RunModel>(); var m = run.CreateMonster(skills, personality: personality, race: race, mood: mood);
            run.AddToCage(m); App.SendCommand(new PlaceMonsterCommand(m.Id, OperationArea.Extraction, cell)); return m;
        }
        SettlementLanding[] Lands() => App.GetSystem<SettlementSystem>().Entries.OfType<SettlementLanding>().ToArray();
        void Score() => App.SendCommand(new ConfirmSettlementCommand());
        [TestCase("能量体", 5)] [TestCase("左能量体", 2)] [TestCase("右能量体", 2)]
        [TestCase("双头能量体", 4)] [TestCase("蜜能量体", 3)] [TestCase("优秀能量体", 2)]
        [TestCase("增生能量体", 1)] [TestCase("自爆体", 10)] [TestCase("情绪收集器", 2)]
        [TestCase("啃食体", 4)] [TestCase("抽杀体", 10)] [TestCase("群能量体", 3)]
        [TestCase("生长能量体", 2)] [TestCase("不稳定能量体", 20)] [TestCase("血祭体", 16)] [TestCase("血崩体", 20)]
        public void SkillProductionHasIndependentBasicFixture(string skill, int expected)
        {
            Fixture(); Put(0, new[] { "能量体" }); var host = Put(1, new[] { skill }); var right = Put(2, new[] { "能量体" });
            Score(); Assert.That(Lands().Where(l => l.MonsterId == host.Id).Sum(l => l.Energy), Is.EqualTo(expected));
            if (skill == "蜜能量体") Assert.That(Lands().Single(l => l.MonsterId == right.Id).Energy, Is.EqualTo(8));
            if (skill == "双头能量体") Assert.That(Lands().Where(l => l.MonsterId == host.Id).Select(l => l.Energy), Is.EqualTo(new[] { 2, 2 }));
            if (skill == "血祭体") Assert.That(Lands().Where(l => l.MonsterId == host.Id).Select(l => l.Energy), Is.EqualTo(new[] { 8, 8 }));
            if (skill == "血崩体") Assert.That(Lands().Where(l => l.MonsterId == host.Id).Select(l => l.Energy), Is.EqualTo(new[] { 10, 10 }));
        }
        [TestCase("奇异香", 6)] [TestCase("怪异香", 7)] [TestCase("镜眼", 10)]
        public void SkillAuraHasIndependentBasicFixture(string skill, int expected)
        {
            Fixture(); var left = Put(0, new[] { "能量体" }); Put(1, new[] { skill }); var right = Put(2, new[] { "能量体" }); Score();
            Assert.That(Lands().Single(l => l.MonsterId == left.Id).Energy, Is.EqualTo(expected));
            Assert.That(Lands().Single(l => l.MonsterId == right.Id).Energy, Is.EqualTo(expected));
        }
        [TestCase("传能耳")] [TestCase("优质肉体")] [TestCase("良好肉体")] [TestCase("收尸腺")]
        [TestCase("左复制腺体")] [TestCase("右复制腺体")] [TestCase("换位手")] [TestCase("汲取鼻")]
        [TestCase("外接胚胎")] [TestCase("产金管道")] [TestCase("不稳定肉体")] [TestCase("祭品腺")]
        [TestCase("移植胚胎")] [TestCase("回响嗓")] [TestCase("无穷肌肉")]
        public void SkillSupportAndReactionHasIndependentBasicFixture(string skill)
        {
            Fixture(); var run = App.GetModel<RunModel>();
            switch (skill)
            {
                case "优质肉体":
                {
                    var host = Put(0, new[] { skill, "能量体", "产金管道" }); Score();
                    Assert.That(Lands().Where(l => l.MonsterId == host.Id).Select(l => l.Energy), Is.EqualTo(new[] { 5, 5 }));
                    Assert.That(run.Gold, Is.EqualTo(10)); break;
                }
                case "良好肉体":
                { var host = Put(0, new[] { skill, "能量体" }); Score(); Assert.That(Lands().Single().Energy, Is.EqualTo(8)); break; }
                case "传能耳":
                { Put(0, new[] { "能量体" }); Put(1, new[] { skill }); var right = Put(2, new[] { "能量体" }); Score(); Assert.That(Lands().Single(l => l.MonsterId == right.Id).Energy, Is.EqualTo(7)); break; }
                case "左复制腺体": case "右复制腺体": case "回响嗓":
                { var host = Put(1, new[] { skill }); var target = Put(skill == "左复制腺体" ? 0 : 2, new[] { "能量体" }); Score(); Assert.That(Lands().Where(l => l.MonsterId == target.Id).Select(l => l.Energy), Is.EqualTo(new[] { 5, 5 })); break; }
                case "收尸腺":
                { var host = Put(0, new[] { skill }); Put(1, new[] { "自爆体" }); Score(); Assert.That(Lands().Single(l => l.MonsterId == host.Id).Energy, Is.EqualTo(3)); break; }
                case "汲取鼻":
                { Put(0, new[] { "能量体" }); var host = Put(1, new[] { skill }); Put(2, new[] { "能量体" }); Score(); Assert.That(Lands().Where(l => l.MonsterId == host.Id).Select(l => l.Energy), Is.EqualTo(new[] { 1, 1 })); break; }
                case "换位手":
                { var left = Put(0, new[] { "能量体" }); var host = Put(1, new[] { skill }); Score(); Assert.That(App.GetModel<LevelModel>().Extraction[0], Is.EqualTo(host.Id)); Assert.That(host.Immovable, Is.True); Assert.That(Lands().Count(l => l.MonsterId == left.Id), Is.EqualTo(2)); break; }
                case "外接胚胎":
                { Put(0, new[] { skill }); Put(1, new[] { "啃食体" }); Score(); Assert.That(run.Cage.Count, Is.EqualTo(1)); Assert.That(run.Cage.Single().Skills.Count, Is.EqualTo(1)); break; }
                case "祭品腺":
                { Put(0, new[] { skill }); var host = Put(1, new[] { "啃食体" }); Score(); Assert.That(Lands().Single(l => l.MonsterId == host.Id).Energy, Is.EqualTo(8)); break; }
                case "移植胚胎":
                { var host = Put(0, new[] { skill }); Put(1, new[] { "自爆体" }); Score(); Assert.That(host.Skills.Single(s => s.Name == "自爆体").IsTemporary, Is.True); break; }
                case "产金管道":
                { Put(0, new[] { skill }); Score(); Assert.That(run.Gold, Is.EqualTo(5)); Assert.That(Lands(), Is.Empty); break; }
                case "无穷肌肉":
                { var host = Put(0, new[] { skill }); Score(); Assert.That(host.Skills.Count, Is.EqualTo(4)); Assert.That(host.Skills.Count(s => s.IsTemporary), Is.EqualTo(3)); Assert.That(Lands(), Is.Empty); break; }
                case "不稳定肉体":
                { var host = Put(0, new[] { skill }); var parent = run.CreateMonster(new[] { "能量体" }); run.AddToCage(parent); App.SendCommand(new PlaceMonsterCommand(parent.Id, OperationArea.Breeding, 0)); Score(); Assert.That(host.Skills.Single(s => s.Name == "能量体").IsTemporary, Is.True); Assert.That(Lands(), Is.Empty); break; }
                default: Assert.Fail("Missing mechanism fixture for " + skill); break;
            }
        }
        [TestCase("普通", 5)] [TestCase("热情", 6)] [TestCase("热爱", 7)] [TestCase("疲倦", 4)] [TestCase("倦怠", 3)]
        public void EachMoodChangesProductionByItsActualTier(string mood, int expected)
        { Fixture(); var host = Put(0, new[] { "能量体" }, mood: mood); Score(); Assert.That(Lands().Single().Energy, Is.EqualTo(expected)); }
        [TestCase("森林", "天空")] [TestCase("天空", "沙漠")] [TestCase("沙漠", "山脉")] [TestCase("山脉", "海生")] [TestCase("海生", "森林")]
        public void EachRaceHasActualGeneratingNeighborBonus(string race, string neighbor)
        { Fixture(); App.GetModel<RunModel>().AddTool("五行罗盘"); Put(0, new[] { "能量体" }, race: race); Put(1, new[] { "能量体" }, race: neighbor); Score(); Assert.That(Lands().Select(l => l.Energy), Is.EqualTo(new[] { 8, 8 })); }

        [TestCase("孤僻", 10)] [TestCase("自私", 10)] [TestCase("自律", 10)] [TestCase("安于现状", 10)]
        [TestCase("急躁", 8)] [TestCase("稳重", 8)]
        public void EachIsolatedSettlementPersonalityHasItsOwnEffect(string personality, int expected)
        { Fixture(); Put(0, new[] { "能量体" }, personality); Score(); Assert.That(Lands().Sum(l => l.Energy), Is.EqualTo(expected)); }
        [TestCase("合群", 8)] [TestCase("独立", 5)] [TestCase("依赖", 7)]
        public void EachNeighborPersonalityHasItsOwnEffect(string personality, int expected)
        {
            Fixture(); Put(0, new[] { "奇异香" }); var host = Put(1, new[] { "能量体" }, personality); Put(2, new[] { "回响嗓" }); Score();
            Assert.That(Lands().Single(l => l.MonsterId == host.Id).Energy, Is.EqualTo(expected));
        }
        [TestCase("恋爱脑", 7, 7)] [TestCase("乐于助人", 5, 7)] [TestCase("自私", 10, 2)]
        public void EachNeighborContributionChangesBothObservedEndpoints(string personality, int own, int other)
        {
            Fixture(); var host = Put(0, new[] { "能量体" }, personality); var neighbor = Put(1, new[] { "能量体" }); Score();
            Assert.That(Lands().Single(l => l.MonsterId == host.Id).Energy, Is.EqualTo(own));
            Assert.That(Lands().Single(l => l.MonsterId == neighbor.Id).Energy, Is.EqualTo(other));
        }
        [Test] public void GreedyReceivesGoldOnEveryActiveAttempt()
        { Fixture(); Put(0, new[] { "双头能量体", "能量体" }, "贪财"); Score(); Assert.That(App.GetModel<RunModel>().Gold, Is.EqualTo(2)); }
        [Test] public void FreeWillFailureStillNotifiesEnergyEar()
        {
            Fixture(); Put(0, new[] { "能量体" }, "随心所欲"); Put(1, new[] { "传能耳" }); var target = Put(2, new[] { "能量体" }); Score();
            Assert.That(Lands().Single(l => l.MonsterId == target.Id).Energy, Is.EqualTo(7));
            Assert.That(Lands().Count(), Is.EqualTo(1));
        }
        [TestCase("急急装置", 10)] [TestCase("小喇叭", 8)] [TestCase("情绪收割机", 7)]
        public void EachSimpleEnergyToolHasItsOwnActualSettlement(string tool, int expected)
        { Fixture(); App.GetModel<RunModel>().AddTool(tool); Put(0, new[] { "能量体" }); Score(); Assert.That(Lands().Single().Energy, Is.EqualTo(expected)); }
        [Test] public void MachineProducesUnownedEnergyInItsOwnEntry()
        { Fixture(); App.GetModel<RunModel>().AddTool("电化能机"); Score(); var landing = Lands().Single(); Assert.That(landing.SourceToolId, Is.EqualTo("电化能机")); Assert.That(landing.Energy, Is.EqualTo(10)); }
        [Test] public void MindControlChangesPersonalityAdditionOnly()
        { Fixture(); App.GetModel<RunModel>().AddTool("精神操控仪"); Put(0, new[] { "能量体" }, "急躁"); Score(); Assert.That(Lands().Single().Quote, Is.EqualTo(5)); Assert.That(Lands().Single().Energy, Is.EqualTo(9)); }
        [Test] public void GoldSkillDetectorCountsActualGoldSkillInstances()
        { Fixture(); App.GetModel<RunModel>().AddTool("稀有度检测仪"); var host = Put(0, new[] { "能量体" }); Put(2, new[] { "镜眼" }); Score(); Assert.That(Lands().Single(l => l.MonsterId == host.Id).Energy, Is.EqualTo(6)); }
        [Test] public void SingleSkillDeviceAddsOneTriggerAndDoublesEachOutput()
        { Fixture(); App.GetModel<RunModel>().AddTool("劣胜装置"); Put(0, new[] { "能量体" }); Score(); Assert.That(Lands().Select(l => l.Energy), Is.EqualTo(new[] { 10, 10 })); }
        sealed class SuccessfulDraw : IDraw
        {
            public T Choose<T>(System.Collections.Generic.IReadOnlyList<T> options) => options[0];
            public bool Chance(int percent) => true;
        }
    }
}
