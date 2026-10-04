using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class SettlementRecordTests : RulesFixture
    {
        [Test]
        public void 没有相邻时吞噬大嘴的记录按落地消灭和支付的顺序写下写回与未发生的消灭()
        {
            UseDraw(
                "吞噬大嘴",
                "左能量体",
                "右能量体",
                "增量小手",
                "增量大手",
                "残留提取腺体",
                "孤独心",
                "双重吐息",
                "分享之手");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var devourerId = IdOf("吞噬大嘴");
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var record = App.SendQuery(new SettlementRecordQuery());
            Assert.That(record.Select(entry => entry.GetType().Name).ToArray(), Is.EqualTo(new[]
            {
                "SettlementLanding",
                "SettlementRemoval",
                "SettlementPayment",
            }));
            var landing = (SettlementLanding)record[0];
            Assert.That(landing.MonsterId, Is.EqualTo(devourerId));
            Assert.That(landing.SkillName, Is.EqualTo("吞噬大嘴"));
            Assert.That(landing.Base, Is.EqualTo(4));
            Assert.That(landing.Multiplier, Is.EqualTo(1));
            Assert.That(landing.Energy, Is.EqualTo(4));
            Assert.That(landing.Writeback, Is.EqualTo(2));
            var removal = (SettlementRemoval)record[1];
            Assert.That(removal.MonsterId, Is.Null);
            Assert.That(removal.Happened, Is.False);
            var payment = (SettlementPayment)record[2];
            Assert.That(payment.Deducted, Is.EqualTo(0));
            Assert.That(payment.Shortfall, Is.EqualTo(46));
            Assert.That(payment.Overtime, Is.True);
            Assert.That(payment.Failed, Is.False);
            Assert.That(payment.Excess, Is.False);
            Assert.That(payment.Wage, Is.EqualTo(0));
        }

        [Test]
        public void 没有左侧目标时换位记录为没有发生()
        {
            UseDraw(
                "换位手",
                "左能量体",
                "右能量体",
                "能量吐息",
                "增量小手",
                "增量大手",
                "孤独心",
                "双重吐息",
                "分享之手");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var swapperId = IdOf("换位手");
            App.SendCommand(new PlaceMonsterCommand(swapperId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var missed = App.SendQuery(new SettlementRecordQuery()).OfType<SettlementSwap>().Single();
            Assert.That(missed.ActorId, Is.EqualTo(swapperId));
            Assert.That(missed.TargetId, Is.Null);
            Assert.That(missed.Happened, Is.False);
        }

        [Test]
        public void 换位成功时记录发生且排在落地之后()
        {
            UseLevel(
                new ScriptedLevelCatalog(0),
                "能量吐息",
                "左能量体",
                "右能量体",
                "换位手",
                "增量小手",
                "增量大手",
                "孤独心",
                "双重吐息",
                "分享之手");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breathId = IdOf("能量吐息");
            var swapperId = IdOf("换位手");
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(swapperId, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var record = App.SendQuery(new SettlementRecordQuery());
            var swap = record.OfType<SettlementSwap>().Single();
            Assert.That(swap.ActorId, Is.EqualTo(swapperId));
            Assert.That(swap.TargetId, Is.EqualTo(breathId));
            Assert.That(swap.Happened, Is.True);
            Assert.That(
                App.SendQuery(new ExtractionSlotsQuery()).Select(cell => cell.MonsterId).ToArray(),
                Is.EqualTo(new[] { swapperId, breathId, null, null, null }));
            Assert.That(record[0], Is.InstanceOf<SettlementLanding>());
            Assert.That(record[1], Is.InstanceOf<SettlementSwap>());
            Assert.That(record[2], Is.InstanceOf<SettlementPayment>());
        }

        [Test]
        public void 立刻消灭写在该次落地之后并记下被拿走的怪物()
        {
            UseDraw(
                "吞噬大嘴",
                "左能量体",
                "右能量体",
                "能量吐息",
                "增量小手",
                "增量大手",
                "孤独心",
                "双重吐息",
                "分享之手");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var devourerId = IdOf("吞噬大嘴");
            var victimId = IdOf("能量吐息");
            App.SendCommand(new PlaceMonsterCommand(victimId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var record = App.SendQuery(new SettlementRecordQuery());
            Assert.That(record[0], Is.InstanceOf<SettlementLanding>());
            Assert.That(((SettlementLanding)record[0]).MonsterId, Is.EqualTo(victimId));
            Assert.That(record[1], Is.InstanceOf<SettlementLanding>());
            Assert.That(((SettlementLanding)record[1]).Writeback, Is.EqualTo(2));
            Assert.That(record[2], Is.InstanceOf<SettlementRemoval>());
            var removal = (SettlementRemoval)record[2];
            Assert.That(removal.MonsterId, Is.EqualTo(victimId));
            Assert.That(removal.Happened, Is.True);
        }

        [Test]
        public void 加班失败的支付记录留下欠额和失败且命令返回后仍能读到()
        {
            UseLevel(
                new ScriptedLevelCatalog(10),
                "能量吐息",
                "左能量体",
                "右能量体",
                "增量小手",
                "增量大手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breathId = IdOf("能量吐息");
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var shortfall = Payment();
            Assert.That(shortfall.Deducted, Is.EqualTo(0));
            Assert.That(shortfall.Shortfall, Is.EqualTo(5));
            Assert.That(shortfall.Overtime, Is.True);
            Assert.That(shortfall.Failed, Is.False);
            Assert.That(shortfall.Wage, Is.EqualTo(0));

            App.SendCommand(new ReturnMonsterCommand(breathId));
            App.SendCommand(new ConfirmSettlementCommand());

            var failed = Payment();
            Assert.That(failed.Deducted, Is.EqualTo(0));
            Assert.That(failed.Shortfall, Is.EqualTo(5));
            Assert.That(failed.Overtime, Is.True);
            Assert.That(failed.Failed, Is.True);
            Assert.That(failed.Excess, Is.False);
            Assert.That(failed.Wage, Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Failed));
            Assert.That(App.SendQuery(new LevelShortfallQuery()), Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(0));
        }

        [Test]
        public void 偿付成功且达到超额时记录扣除超额和工资()
        {
            UseLevel(
                new ScriptedLevelCatalog(5, 5),
                "能量吐息",
                "左能量体",
                "右能量体",
                "能量吐息",
                "增量小手",
                "增量大手",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breaths = Ids("能量吐息");
            App.SendCommand(new PlaceMonsterCommand(breaths[0], OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breaths[1], OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var payment = Payment();
            Assert.That(payment.Deducted, Is.EqualTo(5));
            Assert.That(payment.Shortfall, Is.EqualTo(0));
            Assert.That(payment.Overtime, Is.False);
            Assert.That(payment.Failed, Is.False);
            Assert.That(payment.Excess, Is.True);
            Assert.That(payment.Wage, Is.EqualTo(40));
            Assert.That(App.SendQuery(new RunLedgerQuery()).TechPoints, Is.EqualTo(1));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(40));
        }

        string IdOf(string skillName) => Ids(skillName).Single();

        string[] Ids(string skillName) =>
            App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Count == 1 && monster.SkillNames[0] == skillName)
                .Select(monster => monster.Id)
                .ToArray();
    }
}
