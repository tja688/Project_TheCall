using System.Linq;
using NUnit.Framework;
using QFramework;

namespace TheCall.Tests
{
    public sealed class SettlementRemediationTests : RulesFixture
    {
        Monster Add(int cell, string personality, params string[] skills)
        {
            var run = App.GetModel<RunModel>();
            var monster = run.CreateMonster(skills, personality: personality);
            run.AddToCage(monster);
            App.SendCommand(new PlaceMonsterCommand(monster.Id, OperationArea.Extraction, cell));
            return monster;
        }
        SettlementLanding[] Lands() => App.GetSystem<SettlementSystem>().Entries.OfType<SettlementLanding>().ToArray();

        [Test] public void F02_ContentedLocksEachSkillBeforeSelfDestruct()
        {
            KeepOpened(); var host = Add(0, "安于现状", "能量体", "自爆体");
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Select(l => l.Energy), Is.EqualTo(new[] { 5, 5, 10 }));
            Assert.That(Lands().Sum(l => l.Energy), Is.EqualTo(20));
        }
        [Test] public void F02_EchoExecutesEachActiveInstanceCompletely()
        {
            KeepOpened(); Add(0, "稳定", "回响嗓"); Add(1, "稳定", "蜜能量体", "能量体");
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Select(l => l.SkillName), Is.EqualTo(new[] { "蜜能量体", "蜜能量体", "能量体", "能量体" }));
        }
        [Test] public void F06_DuplicateMirrorInstancesMultiplyIndependently()
        {
            KeepOpened(); App.GetModel<RunModel>().AddTool("复读腺");
            Add(0, "稳定", "镜眼", "镜眼"); var host = Add(1, "稳定", "能量体");
            App.SendCommand(new ConfirmSettlementCommand());
            var landing = Lands().Single(l => l.MonsterId == host.Id);
            Assert.That(landing.Energy, Is.EqualTo(20));
            Assert.That(landing.Factors.Select(f => f.SourceSkillInstanceId).Distinct().Count(), Is.EqualTo(2));
        }
        [Test] public void F06_DuplicateProliferationReceivesOneGrantPerInstance()
        {
            KeepOpened(); App.GetModel<RunModel>().AddTool("复读腺");
            Add(0, "稳定", "增生能量体"); var host = Add(1, "稳定", "增生能量体", "增生能量体");
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Count(l => l.MonsterId == host.Id), Is.EqualTo(4));
            Assert.That(Lands().Where(l => l.MonsterId == host.Id).GroupBy(l => l.SkillInstanceId).Select(g => g.Count()), Is.EqualTo(new[] { 2, 2 }));
        }
        [Test] public void F05_EchoRepeatsCorpseResponseForOneDeath()
        {
            KeepOpened(); Add(0, "稳定", "回响嗓"); var listener = Add(1, "稳定", "收尸腺"); Add(2, "稳定", "自爆体");
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Where(l => l.MonsterId == listener.Id).Select(l => l.Energy), Is.EqualTo(new[] { 3, 3 }));
        }
        [TestCase("重视血脉", 10)] [TestCase("仇恨血脉", 2)]
        public void F10_KinAffectsBothEndpointsAndExcludesStranger(string personality, int expected)
        {
            KeepOpened(); var run = App.GetModel<RunModel>();
            var parent = Add(0, personality, "能量体");
            var child = run.CreateMonster(new[] { "能量体" }, parentIds: new[] { parent.Id }); run.AddToCage(child);
            App.SendCommand(new PlaceMonsterCommand(child.Id, OperationArea.Extraction, 1));
            var stranger = Add(2, "稳定", "能量体"); App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Where(l => l.MonsterId == parent.Id).Sum(l => l.Energy), Is.EqualTo(expected));
            Assert.That(Lands().Where(l => l.MonsterId == child.Id).Sum(l => l.Energy), Is.EqualTo(expected));
            Assert.That(Lands().Where(l => l.MonsterId == stranger.Id).Sum(l => l.Energy), Is.EqualTo(5));
        }
        [Test] public void F11_MonsterTargetAuraDoesNotAddToUnownedTool()
        {
            KeepOpened(); App.GetModel<RunModel>().AddTool("电化能机"); Add(0, "稳定", "奇异香");
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Single(l => l.SkillName == "电化能机").Energy, Is.EqualTo(10));
        }
        [Test] public void F14_MindControlCorrectsNegativePersonalityAmount()
        {
            KeepOpened(); App.GetModel<RunModel>().AddTool("精神操控仪"); Add(0, "自私", "奇异香"); var host = Add(1, "稳定", "能量体");
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Single(l => l.MonsterId == host.Id).Energy, Is.EqualTo(4));
            Assert.That(Lands().Single(l => l.MonsterId == host.Id).Adds.Single(a => a.Label == "自私").Amount, Is.EqualTo(-2));
        }
        [Test] public void F15_NoTargetDoesNotProducePersonalityEnergy()
        {
            KeepOpened(); Add(0, "急躁", "啃食体"); App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Sum(l => l.Energy), Is.EqualTo(0));
        }
        [TestCase("急躁", "普通", 8)] [TestCase("稳定", "热爱", 7)] [TestCase("孤僻", "普通", 10)]
        public void F17_PreviewAndExecutionSharePersonalityAndMoodFormula(string personality, string mood, int expected)
        {
            KeepOpened(); var host = Add(0, personality, "能量体"); host.SetMood(mood);
            var preview = App.GetSystem<SettlementSystem>().Preview(host.Id, "能量体", 5);
            Assert.That(preview.Energy, Is.EqualTo(expected));
            Assert.That(App.GetModel<LevelModel>().Energy, Is.EqualTo(0));
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Single().Energy, Is.EqualTo(expected));
        }

        [Test] public void A11_NewActiveIsDeferredButNewAuraIsImmediate()
        {
            KeepOpened(); App.RegisterUtility<IDraw>(ScriptedDraw.Exact("奇异香", "能量体", "右能量体"));
            Add(0, "稳定", "回响嗓"); var muscle = Add(1, "稳定", "无穷肌肉"); var target = Add(2, "稳定", "能量体");
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(muscle.Skills.Any(s => s.Name == "奇异香" && s.IsTemporary), Is.True);
            Assert.That(muscle.Skills.Any(s => s.Name == "能量体" && s.IsTemporary), Is.True);
            Assert.That(Lands().Any(l => l.MonsterId == muscle.Id), Is.False);
            Assert.That(Lands().Single(l => l.MonsterId == target.Id).Energy, Is.EqualTo(6));
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Any(l => l.MonsterId == muscle.Id && l.SkillName == "能量体"), Is.True);
        }
        [Test] public void A28_NewDeathListenerStartsOnSecondDeathOnly()
        {
            KeepOpened(); App.RegisterUtility<IDraw>(ScriptedDraw.Exact("收尸腺", "自爆体"));
            var thief = Add(0, "稳定", "移植胚胎"); Add(1, "稳定", "自爆体", "收尸腺"); Add(2, "稳定", "自爆体");
            var run = App.GetModel<RunModel>(); var cage = run.CreateMonster(new[] { "收尸腺" }); run.AddToCage(cage);
            var breeder = run.CreateMonster(new[] { "收尸腺" }); run.AddToCage(breeder);
            App.SendCommand(new PlaceMonsterCommand(breeder.Id, OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(thief.Skills.Single(s => s.Name == "收尸腺").IsTemporary, Is.True);
            Assert.That(Lands().Where(l => l.MonsterId == thief.Id).Select(l => l.Energy), Is.EqualTo(new[] { 3 }));
            Assert.That(Lands().Any(l => l.MonsterId == cage.Id || l.MonsterId == breeder.Id), Is.False);
        }
        [Test] public void F03_MirrorRetainsOpeningTargetAfterSwap()
        {
            KeepOpened(); Add(0, "稳定", "镜眼"); var swapped = Add(1, "稳定", "能量体"); var mover = Add(2, "稳定", "换位手", "能量体");
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Where(l => l.MonsterId == swapped.Id).Select(l => l.Energy), Is.EqualTo(new[] { 10, 10 }));
            Assert.That(Lands().Single(l => l.MonsterId == mover.Id && l.SkillName == "能量体").Energy, Is.EqualTo(5));
        }
        [Test] public void F04_LeftListenerReactsBeforeRightVictimLegacy()
        {
            KeepOpened(); var listener = Add(0, "稳定", "收尸腺"); Add(1, "稳定", "血祭体"); Add(2, "稳定", "祭品腺");
            App.SendCommand(new ConfirmSettlementCommand());
            // Left listener is killed before right legacy, so it cannot listen to its own death or right death.
            Assert.That(Lands().Any(l => l.MonsterId == listener.Id), Is.False);
            Assert.That(Lands().Where(l => l.SkillName == "血祭体").Select(l => l.Energy), Is.EqualTo(new[] { 8, 12 }));
        }

        [Test] public void F04_DeathDispatchCombinesLeftListenerAndRightLegacyByPosition()
        {
            KeepOpened(); var listener = Add(0, "稳定", "收尸腺"); Add(1, "稳定", "抽杀体"); var victim = Add(2, "稳定", "祭品腺");
            App.RegisterUtility<IDraw>(ScriptedDraw.Exact(victim.Id)); App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Select(l => l.Energy), Is.EqualTo(new[] { 3, 14 }));
        }
        [Test] public void F03_QualityEligibilityUsesOpeningInstanceCountAfterCopy()
        {
            KeepOpened(); var host = Add(0, "稳定", "优质肉体", "不稳定肉体", "能量体"); var run = App.GetModel<RunModel>();
            var parent = run.CreateMonster(new[] { "左能量体" }); run.AddToCage(parent);
            App.SendCommand(new PlaceMonsterCommand(parent.Id, OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(host.Skills.Count, Is.EqualTo(4));
            Assert.That(host.Skills.Single(s => s.Name == "左能量体").IsTemporary, Is.True);
            Assert.That(Lands().Where(l => l.MonsterId == host.Id).Select(l => l.SkillName), Is.EqualTo(new[] { "能量体", "能量体" }));
        }
        [Test] public void F14_SociableEachNeighborIsOnePersonalityContribution()
        {
            KeepOpened(); App.GetModel<RunModel>().AddTool("精神操控仪"); Add(0, "稳定", "奇异香"); var host = Add(1, "合群", "能量体"); Add(2, "稳定", "回响嗓");
            App.SendCommand(new ConfirmSettlementCommand()); var landing = Lands().Single(l => l.MonsterId == host.Id);
            Assert.That(landing.Adds.Where(a => a.Label == "合群").Select(a => a.Amount), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(landing.Energy, Is.EqualTo(10));
        }
        [Test] public void F17_OvertimeLedgerSeparatesInitialNetAndCumulative()
        {
            KeepOpened(); var run = App.GetModel<RunModel>(); run.AddTool("蓄热熔炉"); var level = App.GetModel<LevelModel>(); level.AddEnergy(10);
            var host = Add(0, "稳定", "能量体"); host.AddModifier(20);
            App.SendCommand(new ConfirmSettlementCommand()); App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(run.ProductionLog.Select(p => p.Produced), Is.EqualTo(new[] { 25, 25 }));
            Assert.That(run.ProductionLog.Select(p => p.InitialEnergy), Is.EqualTo(new[] { 10, 35 }));
            Assert.That(run.ProductionLog.Select(p => p.AccumulatedEnergy), Is.EqualTo(new[] { 35, 60 }));
            Assert.That(run.ProductionLog.Last().Payment.Deducted, Is.EqualTo(50));
            Assert.That(run.ProductionLog.Last().Payment.Wage, Is.EqualTo(40));
            Assert.That(run.ProductionLog.Last().Payment.RetainedEnergy, Is.EqualTo(2));
            Assert.That(run.TechPoints, Is.EqualTo(1));
        }
        [Test] public void F17_NoExcessProducesZeroTechLogAndStableEventIds()
        {
            KeepOpened(); var run = App.GetModel<RunModel>(); var host = Add(0, "稳定", "能量体"); host.AddModifier(45);
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(run.TechPoints, Is.EqualTo(0));
            Assert.That(ProductionLogText.Format(run.ProductionLog), Does.Contain("获得 0 科技点"));
            Assert.That(run.ProductionLog.Single().Entries.Select(e => e.EventId).Distinct().Count(), Is.EqualTo(run.ProductionLog.Single().Entries.Count));
            Assert.That(run.ProductionLog.Single().Entries.Select(e => e.Sequence), Is.EqualTo(Enumerable.Range(0, run.ProductionLog.Single().Entries.Count)));
        }

        [Test] public void F06_FirstEnergyToolBindsOnlyFirstDuplicateInstance()
        {
            KeepOpened(); var run = App.GetModel<RunModel>(); run.AddTool("复读腺"); run.AddTool("急急装置"); var host = Add(0, "稳定", "能量体", "能量体");
            var first = App.GetSystem<SettlementSystem>().Preview(host.Id, "能量体", 5, skillInstanceId: host.Skills[0].Id);
            var second = App.GetSystem<SettlementSystem>().Preview(host.Id, "能量体", 5, skillInstanceId: host.Skills[1].Id);
            Assert.That(new[] { first.Energy, second.Energy }, Is.EqualTo(new[] { 10, 5 }));
            App.SendCommand(new ConfirmSettlementCommand()); Assert.That(Lands().Select(l => l.Energy), Is.EqualTo(new[] { 10, 5 }));
        }
        [Test] public void F10_TwoKinPersonalitySourcesStackIndependently()
        {
            KeepOpened(); var run = App.GetModel<RunModel>(); var parent = Add(0, "重视血脉", "能量体");
            var child = run.CreateMonster(new[] { "能量体" }, parentIds: new[] { parent.Id }, personality: "重视血脉"); run.AddToCage(child);
            App.SendCommand(new PlaceMonsterCommand(child.Id, OperationArea.Extraction, 1)); App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Where(l => l.MonsterId == parent.Id).Sum(l => l.Energy), Is.EqualTo(15));
            Assert.That(Lands().Where(l => l.MonsterId == child.Id).Sum(l => l.Energy), Is.EqualTo(15));
        }
        [Test] public void F17_PreviewDoesNotConsumeRandomOrChangeRuleState()
        {
            KeepOpened(); var draw = new CountingDraw(); App.RegisterUtility<IDraw>(draw); var host = Add(0, "随心所欲", "不稳定能量体");
            var level = App.GetModel<LevelModel>(); var run = App.GetModel<RunModel>();
            var quote = host.Skills[0].Quote; var gold = run.Gold; var attempts = host.HasClockedInToday;
            var preview = App.GetSystem<SettlementSystem>().Preview(host.Id, "不稳定能量体", quote, skillInstanceId: host.Skills[0].Id);
            Assert.That(preview.Energy, Is.EqualTo(20)); Assert.That(draw.Calls, Is.EqualTo(0));
            Assert.That(level.Energy, Is.EqualTo(0)); Assert.That(run.Gold, Is.EqualTo(gold));
            Assert.That(host.Skills[0].Quote, Is.EqualTo(quote)); Assert.That(host.HasClockedInToday, Is.EqualTo(attempts));
        }

        [Test] public void F02_SourceDeathPreservesLockedTimesButRemovesLaterGrants()
        {
            KeepOpened(); var run = App.GetModel<RunModel>(); run.AddTool("复读腺"); var source = Add(0, "稳定", "回响嗓");
            var host = Add(1, "稳定", "能量体", "抽杀体", "能量体"); Add(2, "稳定", "能量体");
            App.RegisterUtility<IDraw>(ScriptedDraw.Exact(source.Id)); App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Where(l => l.MonsterId == host.Id).Select(l => l.Energy), Is.EqualTo(new[] { 5, 5, 10, 10, 5 }));
        }
        [Test] public void F11_IndependentStillReceivesPersonalityToolAndExtraTriggers()
        {
            KeepOpened(); var run = App.GetModel<RunModel>(); run.AddTool("小喇叭");
            Add(0, "乐于助人", "奇异香", "回响嗓"); var host = Add(1, "独立", "能量体");
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Lands().Where(l => l.MonsterId == host.Id).Select(l => l.Energy), Is.EqualTo(new[] { 10, 10 }));
        }

        [Test] public void A28_NewTransplantInstanceStartsOnlyOnNextDeath()
        {
            KeepOpened(); App.GetModel<RunModel>().AddTool("复读腺"); App.RegisterUtility<IDraw>(ScriptedDraw.Exact("移植胚胎", "收尸腺", "收尸腺"));
            var thief = Add(0, "稳定", "移植胚胎"); Add(1, "稳定", "自爆体", "移植胚胎"); Add(2, "稳定", "自爆体", "收尸腺");
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(thief.Skills.Count(s => s.Name == "移植胚胎"), Is.EqualTo(2));
            Assert.That(thief.Skills.Count(s => s.Name == "收尸腺"), Is.EqualTo(2));
            Assert.That(thief.Skills.Count(s => s.IsTemporary), Is.EqualTo(3));
            Assert.That(Lands().Any(l => l.MonsterId == thief.Id), Is.False);
        }
        [Test] public void A10_ProliferationRoundGrantsResetBeforeActualOvertime()
        {
            KeepOpened(); var first = Add(0, "稳定", "增生能量体"); var second = Add(1, "稳定", "增生能量体"); var third = Add(2, "稳定", "增生能量体");
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(new[] { first, second, third }.Select(m => Lands().Count(l => l.MonsterId == m.Id)), Is.EqualTo(new[] { 1, 2, 4 }));
            Assert.That(App.GetModel<LevelModel>().InOvertime, Is.True);
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(new[] { first, second, third }.Select(m => Lands().Count(l => l.MonsterId == m.Id)), Is.EqualTo(new[] { 1, 2, 4 }));
        }
        sealed class CountingDraw : IDraw
        {
            public int Calls;
            public T Choose<T>(System.Collections.Generic.IReadOnlyList<T> options) { Calls++; return options[0]; }
            public bool Chance(int percent) { Calls++; return false; }
        }
    }
}
