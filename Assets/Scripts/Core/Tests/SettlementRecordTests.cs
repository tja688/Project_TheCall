using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class SettlementRecordTests : RulesFixture
    {
        [Test]
        public void 没有左侧目标时换位记录为没有发生()
        {
            UseDraw(
                "换位手",
                "左能量体",
                "右能量体",
                "能量体",
                "奇异香",
                "怪异香",
                "良好肉体",
                "双头能量体",
                "蜜能量体");

            KeepOpened();
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
                "能量体",
                "左能量体",
                "右能量体",
                "换位手",
                "奇异香",
                "怪异香",
                "良好肉体",
                "双头能量体",
                "蜜能量体");

            KeepOpened();
            var breathId = IdOf("能量体");
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
            Assert.That(record[2], Is.InstanceOf<SettlementLanding>());
            Assert.That(record[3], Is.InstanceOf<SettlementPayment>());
        }

        [Test]
        public void 加班失败的支付记录留下欠额和失败且命令返回后仍能读到()
        {
            UseLevel(
                new ScriptedLevelCatalog(10),
                "能量体",
                "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "良好肉体",
                "优质肉体",
                "双头能量体");

            KeepOpened();
            var breathId = IdOf("能量体");
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
                "能量体",
                "左能量体",
                "右能量体",
                "能量体",
                "奇异香",
                "怪异香",
                "良好肉体",
                "优质肉体",
                "双头能量体");

            KeepOpened();
            var breaths = Ids("能量体");
            App.SendCommand(new PlaceMonsterCommand(breaths[0], OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breaths[1], OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var payment = Payment();
            Assert.That(payment.Deducted, Is.EqualTo(5));
            Assert.That(payment.Shortfall, Is.EqualTo(0));
            Assert.That(payment.Overtime, Is.False);
            Assert.That(payment.Failed, Is.False);
            Assert.That(payment.Excess, Is.True);
            Assert.That(payment.Wage, Is.EqualTo(60));
            Assert.That(App.SendQuery(new RunLedgerQuery()).TechPoints, Is.EqualTo(1));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(60));
        }

        string IdOf(string skillName) => Ids(skillName).Single();

        string[] Ids(string skillName) =>
            App.SendQuery(new MonsterCageQuery())
                .Where(monster => Holds(monster, skillName))
                .Select(monster => monster.Id)
                .ToArray();
    }
}
