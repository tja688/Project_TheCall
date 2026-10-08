using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class SettlementMarkTests : RulesFixture
    {
        [Test]
        public void 不稳定能量体没抽中时是未中且能量不增加()
        {
            var draw = ChanceDraw();
            draw.ChanceHits = false;
            Open(draw);
            var id = Place("不稳定能量体");
            App.SendCommand(new PlaceMonsterCommand(id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var first = App.SendQuery(new SettlementRecordQuery());
            var second = App.SendQuery(new SettlementRecordQuery());
            Assert.That(second.Count, Is.EqualTo(first.Count));
            Assert.That(second.OfType<SettlementMark>().Count(), Is.EqualTo(1));
            var miss = second.OfType<SettlementMark>().Single();
            Assert.That(miss.MonsterId, Is.EqualTo(id));
            Assert.That(miss.Label, Is.EqualTo("+0"));
            Assert.That(miss.Tone, Is.EqualTo(MarkTone.Miss));
            Assert.That(second.OfType<SettlementLanding>(), Is.Empty);
            Assert.That(App.SendQuery(new ProductionLogQuery()).Single().Produced, Is.EqualTo(0));
        }

        [Test]
        public void 不稳定能量体抽中时落地二十且没有未中()
        {
            var draw = ChanceDraw();
            draw.ChanceHits = true;
            Open(draw);
            var id = Place("不稳定能量体");
            App.SendCommand(new PlaceMonsterCommand(id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var entries = App.SendQuery(new SettlementRecordQuery());
            var landing = entries.OfType<SettlementLanding>().Single();
            Assert.That(landing.MonsterId, Is.EqualTo(id));
            Assert.That(landing.Energy, Is.EqualTo(20));
            Assert.That(entries.OfType<SettlementMark>(), Is.Empty);
            Assert.That(App.SendQuery(new ProductionLogQuery()).Single().Produced, Is.EqualTo(20));
        }

        [Test]
        public void 左复制腺体的再触发插在双头的两次普通弹出和两次额外弹出之间()
        {
            UseDraw(
                "双头能量体",
                "能量体",
                "左能量体",
                "左复制腺体",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "蜜能量体",
                "能量体");
            KeepOpened();
            var breathId = IdOf("双头能量体");
            var glandId = IdOf("左复制腺体");
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(glandId, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var shown = App.SendQuery(new SettlementRecordQuery())
                .Where(entry => entry is SettlementLanding || entry is SettlementMark)
                .ToArray();
            Assert.That(shown.Length, Is.EqualTo(5));
            Assert.That(((SettlementLanding)shown[0]).MonsterId, Is.EqualTo(breathId));
            Assert.That(((SettlementLanding)shown[0]).Energy, Is.EqualTo(2));
            Assert.That(((SettlementLanding)shown[1]).MonsterId, Is.EqualTo(breathId));
            Assert.That(((SettlementLanding)shown[1]).Energy, Is.EqualTo(2));
            var again = (SettlementMark)shown[2];
            Assert.That(again.MonsterId, Is.EqualTo(glandId));
            Assert.That(again.Label, Is.EqualTo("左复制腺体触发+1"));
            Assert.That(((SettlementLanding)shown[3]).MonsterId, Is.EqualTo(breathId));
            Assert.That(((SettlementLanding)shown[3]).Energy, Is.EqualTo(2));
            Assert.That(((SettlementLanding)shown[4]).MonsterId, Is.EqualTo(breathId));
            Assert.That(((SettlementLanding)shown[4]).Energy, Is.EqualTo(2));
        }

        [Test]
        public void 良好肉体只在正好两个技能时给产能加上三()
        {
            SettleBuilt("能量体", "良好肉体");
            var pairedLanding = App.SendQuery(new SettlementRecordQuery()).OfType<SettlementLanding>().Single();
            Assert.That(pairedLanding.Energy, Is.EqualTo(8));
            Assert.That(pairedLanding.Adds.Single().Label, Is.EqualTo("良好肉体"));
            Assert.That(pairedLanding.Adds.Single().Amount, Is.EqualTo(3));
            Assert.That(
                App.SendQuery(new SettlementRecordQuery()).OfType<SettlementMark>().Any(mark => mark.Label == "+0"),
                Is.False);

            var wide = SettleBuilt("能量体", "左能量体", "良好肉体");
            var entries = App.SendQuery(new SettlementRecordQuery());
            Assert.That(entries.OfType<SettlementLanding>().SelectMany(landing => landing.Adds).Any(add => add.Label == "良好肉体"), Is.False);
            var miss = entries.OfType<SettlementMark>().Single(mark => mark.Label == "+0");
            Assert.That(miss.MonsterId, Is.EqualTo(wide));
        }

        [Test]
        public void 产金管道记五金且本局金币增加五()
        {
            UseDraw(
                "产金管道",
                "能量体",
                "左能量体",
                "右能量体",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "双头能量体",
                "蜜能量体");
            KeepOpened();
            var id = IdOf("产金管道");
            var before = App.SendQuery(new RunLedgerQuery()).Gold;
            App.SendCommand(new PlaceMonsterCommand(id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold - before, Is.EqualTo(5));
            var gold = App.SendQuery(new SettlementRecordQuery()).OfType<SettlementMark>().Single();
            Assert.That(gold.MonsterId, Is.EqualTo(id));
            Assert.That(gold.Label, Is.EqualTo("+5金"));
            Assert.That(gold.Tone, Is.EqualTo(MarkTone.Gold));
        }

        string SettleBuilt(params string[] skills)
        {
            UseDraw(
                "能量体",
                "左能量体",
                "右能量体",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "双头能量体",
                "蜜能量体",
                "良好肉体");
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var monster = run.CreateMonster(skills);
            run.AddToCage(monster);
            App.SendCommand(new PlaceMonsterCommand(monster.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            return monster.Id;
        }

        string Place(string skillName)
        {
            var run = App.GetModel<RunModel>();
            var monster = run.CreateMonster(new[] { skillName });
            run.AddToCage(monster);
            return monster.Id;
        }

        void Open(PinnedDraw draw)
        {
            UseRules(draw, new ScriptedLevelCatalog(50));
            KeepOpened();
        }

        static PinnedDraw ChanceDraw() => new PinnedDraw(
            "不稳定能量体",
            "能量体",
            "左能量体",
            "右能量体",
            "奇异香",
            "汲取鼻",
            "孤独心",
            "双头能量体",
            "蜜能量体");

        string IdOf(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, skillName)).Id;
    }
}
