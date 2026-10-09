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
            var text = Format();

            Assert.That(text, Does.Contain("—— 第 1 关 · 结算 ——"));
            Assert.That(text, Does.Contain("获得 12 点能量"));
            Assert.That(text, Does.Contain("进入加班"));
            Assert.That(text, Does.Contain(breath + " 的「能量体」触发"));
            Assert.That(text, Does.Contain("左起第 2 格"));
            Assert.That(text, Does.Contain(incense + " 的「奇异香」为它加了 1 点"));
            Assert.That(text, Does.Contain(eye + " 的「镜眼」让这次产出 ×2"));
            Assert.That(text, Does.Contain("计入本关总能量 12 点"));
            Assert.That(text, Does.Contain("\n+12"));
            Assert.That(text, Does.Not.Contain("<align="));
            Assert.That(text, Does.Contain("<b>合计  12</b>"));
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

            var text = Format();
            var first = text.IndexOf("—— 第 1 关 · 结算 ——");
            var second = text.IndexOf("—— 第 1 关 · 加班 ——");
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
            var text = Format();
            Assert.That(text, Does.Contain(left + " 的「左能量体」触发"));
            Assert.That(text, Does.Contain("右侧还有 1 只怪物"));
            Assert.That(text, Does.Contain("计入本关总能量 2 点"));
            Assert.That(text, Does.Contain("<b>合计  7</b>"));
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
                    new LandingAdd("宿主修正", 2, null, -1, null),
                    new LandingAdd("下家", 3, null, -1, null),
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

            Assert.That(text, Does.Contain("「怪物修正」加成 +2 点"));
            Assert.That(text, Does.Contain("「下一个怪物的能量数值」加成 +3 点"));
            Assert.That(text, Does.Contain("报价写回 +2"));
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

        [Test]
        public void 再触发写在对应落地之前且能量可逐项加总()
        {
            Open(
                "能量体",
                "左能量体",
                "右能量体",
                "回响嗓",
                "能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心");

            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => Holds(monster, "能量体"))
                .ToArray();
            var leftId = breaths[0].Id;
            var rightId = breaths[1].Id;
            var echoId = IdOf("回响嗓");
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(echoId, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(rightId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var echo = App.SendQuery(new MonsterQuery(echoId)).DisplayName;
            var text = Format();
            var firstLanding = text.IndexOf("「能量体」触发");
            var again = text.IndexOf(echo + " 的「回响嗓」生效");
            var thirdLanding = text.LastIndexOf("计入本关总能量 5 点");
            Assert.That(firstLanding, Is.GreaterThanOrEqualTo(0));
            Assert.That(again, Is.GreaterThan(firstLanding));
            Assert.That(thirdLanding, Is.GreaterThan(again));
            Assert.That(text, Does.Contain("<b>合计  15</b>"));
        }

        void Open(params string[] names)
        {
            UseDraw(names);
            KeepOpened();
        }

        string Format() =>
            ProductionLogText.Format(
                App.SendQuery(new ProductionLogQuery()),
                id => App.SendQuery(new MonsterQuery(id))?.DisplayName);

        string IdOf(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, skillName)).Id;
    }
}
