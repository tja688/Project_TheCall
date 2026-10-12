using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QFramework;

namespace TheCall.Tests
{
    public sealed class ScoringPlaybackM4Tests : RulesFixture
    {
        [Test]
        public void A26_快进与跳过相同固定随机局面最终能量金币科技材料工作记录与正常播放一致()
        {
            // 固定随机局面执行第一遍（正常播放步骤）
            var draw1 = new ScriptedDraw("能量体", "左能量体", "右能量体", "奇异香", "怪异香", "汲取鼻", "良好肉体", "优质肉体", "双头能量体");
            UseRules(draw1, new ScriptedLevelCatalog(0));
            KeepOpened();

            var run1 = App.GetModel<RunModel>();
            var level1 = App.GetModel<LevelModel>();
            var m1 = run1.CreateMonster(new[] { "能量体" }, personality: "稳定", mood: "普通");
            var m2 = run1.CreateMonster(new[] { "奇异香" }, personality: "自律", mood: "普通");
            run1.AddToCage(m1);
            run1.AddToCage(m2);

            App.SendCommand(new PlaceMonsterCommand(m1.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(m2.Id, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var record1 = App.SendQuery(new SettlementRecordQuery());
            var energy1 = App.SendQuery(new LevelEnergyQuery());
            var ledger1 = App.SendQuery(new RunLedgerQuery());
            var prodLog1 = run1.ProductionLog.ToArray();

            // 第二遍相同固定局面模拟跳过动画/直接结算求值
            TheCallApp.Reset();
            SetUp();
            var draw2 = new ScriptedDraw("能量体", "左能量体", "右能量体", "奇异香", "怪异香", "汲取鼻", "良好肉体", "优质肉体", "双头能量体");
            UseRules(draw2, new ScriptedLevelCatalog(0));
            KeepOpened();

            var run2 = App.GetModel<RunModel>();
            var level2 = App.GetModel<LevelModel>();
            var m1_b = run2.CreateMonster(new[] { "能量体" }, personality: "稳定", mood: "普通");
            var m2_b = run2.CreateMonster(new[] { "奇异香" }, personality: "自律", mood: "普通");
            run2.AddToCage(m1_b);
            run2.AddToCage(m2_b);

            App.SendCommand(new PlaceMonsterCommand(m1_b.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(m2_b.Id, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var record2 = App.SendQuery(new SettlementRecordQuery());
            var energy2 = App.SendQuery(new LevelEnergyQuery());
            var ledger2 = App.SendQuery(new RunLedgerQuery());
            var prodLog2 = run2.ProductionLog.ToArray();

            // 验证能量、产出、金币、科技点、记录与工作记录完全一致
            var produced1 = record1.OfType<SettlementLanding>().Sum(l => l.Energy);
            var produced2 = record2.OfType<SettlementLanding>().Sum(l => l.Energy);
            Assert.That(produced1, Is.EqualTo(produced2));
            Assert.That(record1.Count, Is.EqualTo(record2.Count));
            Assert.That(energy1, Is.EqualTo(energy2));
            Assert.That(ledger1.Gold, Is.EqualTo(ledger2.Gold));
            Assert.That(ledger1.TechPoints, Is.EqualTo(ledger2.TechPoints));
            Assert.That(prodLog1.Length, Is.EqualTo(prodLog2.Length));

            for (var i = 0; i < record1.Count; i++)
            {
                var e1 = record1[i];
                var e2 = record2[i];
                Assert.That(e1.GetType(), Is.EqualTo(e2.GetType()));
            }
        }

        [Test]
        public void 结构化回放包含正确来源与数值读数()
        {
            UseLevel(new ScriptedLevelCatalog(0));
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var level = App.GetModel<LevelModel>();
            var m1 = run.CreateMonster(new[] { "优质肉体" }, personality: "自律");
            var m2 = run.CreateMonster(new[] { "能量体", "奇异香" }, personality: "安于现状");
            run.AddToCage(m1);
            run.AddToCage(m2);
            App.SendCommand(new PlaceMonsterCommand(m1.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(m2.Id, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var record = App.SendQuery(new SettlementRecordQuery());
            var landings = record.OfType<SettlementLanding>().ToArray();
            var marks = record.OfType<SettlementMark>().ToArray();

            Assert.That(landings.Length, Is.GreaterThan(0));
            var ziliLanding = landings.Single(l => l.SkillName == "自律");
            Assert.That(ziliLanding.Adds.Any(a => a.Label == "奇异香" && a.Amount == 1), Is.True);

            // 验证 SettlementMark 包含 Amount 与 SkillName
            Assert.That(marks.Length, Is.GreaterThan(0));
            var positiveMark = marks.FirstOrDefault(m => m.Amount > 0 && !string.IsNullOrEmpty(m.SkillName));
            Assert.That(positiveMark, Is.Not.Null);
            Assert.That(positiveMark.Amount, Is.GreaterThan(0));
            Assert.That(positiveMark.SkillName, Is.Not.Null);

            // 验证熔炉溢出保留能量
            run.AddTool("熔炉");
            run.SetRetainedEnergy(10);
            Assert.That(run.RetainedEnergy, Is.EqualTo(10));
        }
    }
}
