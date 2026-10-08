using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class ProductionLogTests : RulesFixture
    {
        [Test]
        public void 结算把报价加项和倍率写成技能和收容编号()
        {
            Open(
                "镜眼",
                "左能量体",
                "右能量体",
                "能量体",
                "奇异香",
                "汲取鼻",
                "双头能量体",
                "孤独心",
                "吞噬大嘴");

            var incenseId = IdOf("奇异香");
            var breathId = IdOf("能量体");
            var eyeId = IdOf("镜眼");
            App.SendCommand(new PlaceMonsterCommand(incenseId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(eyeId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var incense = App.SendQuery(new MonsterQuery(incenseId)).DisplayName;
            var breath = App.SendQuery(new MonsterQuery(breathId)).DisplayName;
            var eye = App.SendQuery(new MonsterQuery(eyeId)).DisplayName;
            var text = ProductionLogText.Format(App.SendQuery(new ProductionLogQuery()));

            Assert.That(text, Does.Contain("—— 第1关 · 结算 ——"));
            Assert.That(text, Does.Contain("产出 12    应交 50    还差 38    进入加班"));
            Assert.That(text, Does.Contain(breath + " · 能量体 · 左起第 2 格"));
            Assert.That(text, Does.Contain("报价 5，" + incense + " 的奇异香 +1 = 底数 6"));
            Assert.That(text, Does.Contain(eye + " 的镜眼 ×2"));
            Assert.That(text, Does.Contain("落地 12（底数 6 × 倍率 2）"));
            Assert.That(text, Does.Not.Contain("宿主修正"));
            Assert.That(text, Does.Not.Contain("下家"));
        }

        [Test]
        public void 同一关先结算再加班记成两段()
        {
            Open(
                "能量体",
                "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "吞噬大嘴",
                "孤独心",
                "双头能量体");

            App.SendCommand(new PlaceMonsterCommand(IdOf("能量体"), OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new ConfirmSettlementCommand());

            var text = ProductionLogText.Format(App.SendQuery(new ProductionLogQuery()));
            var first = text.IndexOf("—— 第1关 · 结算 ——");
            var second = text.IndexOf("—— 第1关 · 加班 ——");
            Assert.That(first, Is.GreaterThanOrEqualTo(0));
            Assert.That(second, Is.GreaterThan(first));
            Assert.That(text, Does.Contain("进入加班"));
            Assert.That(text, Does.Contain("游戏失败"));
            Assert.That(App.SendQuery(new ProductionLogQuery()).Count, Is.EqualTo(2));
        }

        [Test]
        public void 左能量体按右侧还有几只来写()
        {
            Open(
                "左能量体",
                "吞噬大嘴",
                "孤独心",
                "能量体",
                "右能量体",
                "奇异香",
                "汲取鼻",
                "双头能量体",
                "怪异香");

            var leftId = IdOf("左能量体");
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("能量体"), OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var left = App.SendQuery(new MonsterQuery(leftId)).DisplayName;
            var text = ProductionLogText.Format(App.SendQuery(new ProductionLogQuery()));
            Assert.That(text, Does.Contain(left + " · 左能量体 · 左起第 1 格"));
            Assert.That(text, Does.Contain("右侧 1 只 × 报价 2 = 底数 2"));
            Assert.That(text, Does.Contain("落地 2"));
        }

        [Test]
        public void 内部叫法改写成怪物修正和下一个怪物的能量数值()
        {
            var landing = new SettlementLanding(
                "m1",
                "能量体",
                10,
                1,
                10,
                2,
                5,
                0,
                false,
                new[]
                {
                    new LandingAdd("宿主修正", 2),
                    new LandingAdd("下家", 3),
                },
                System.Array.Empty<LandingFactor>(),
                "SCP-173",
                1,
                null);
            var payment = new SettlementPayment(0, 40, true, false, false, 0);
            var text = ProductionLogText.Format(new[]
            {
                new ProductionSubmission(3, false, 10, 50, payment, new SettlementEntry[] { landing }),
            });

            Assert.That(text, Does.Contain("怪物修正 +2"));
            Assert.That(text, Does.Contain("下一个怪物的能量数值 +3"));
            Assert.That(text, Does.Contain("写回 +2"));
            Assert.That(text, Does.Not.Contain("宿主修正"));
            Assert.That(text, Does.Not.Contain("下家"));
        }

        [Test]
        public void 还没结算时说明每一次提交都会记下来()
        {
            Open(
                "能量体",
                "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "吞噬大嘴",
                "孤独心",
                "双头能量体");

            var text = ProductionLogText.Format(App.SendQuery(new ProductionLogQuery()));
            Assert.That(text, Does.Contain("还没有结算"));
            Assert.That(text, Does.Contain("每一次提交"));
        }

        void Open(params string[] names)
        {
            UseDraw(names);
            KeepOpened();
        }

        string IdOf(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, skillName)).Id;
    }
}
