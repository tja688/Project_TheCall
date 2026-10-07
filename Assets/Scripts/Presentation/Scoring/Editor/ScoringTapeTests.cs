using System.Linq;
using NUnit.Framework;

namespace TheCall.Scoring
{
    public sealed class ScoringTapeTests
    {
        [Test]
        public void 残留之后空消灭从残留走回吞噬者()
        {
            var tape = ScoringTape.Arrange(
                new SettlementEntry[]
                {
                    new SettlementLanding("devourer", "吞噬大嘴", 4, 1, 4, 2),
                    new SettlementLanding("residue", "残留提取腺体", 1, 1, 1, 0),
                    new SettlementRemoval(null, false, "devourer"),
                    new SettlementPayment(0, 45, true, false, false, 0),
                },
                new[] { "residue", null, "devourer" });

            Assert.That(tape.Produced, Is.EqualTo(5));
            Assert.That(tape.Cues.OfType<PopCue>().Sum(pop => pop.Energy), Is.EqualTo(5));
            Assert.That(tape.Cues[4], Is.InstanceOf<WalkCue>());
            Assert.That(((WalkCue)tape.Cues[4]).FromIndex, Is.EqualTo(0));
            Assert.That(((WalkCue)tape.Cues[4]).ToIndex, Is.EqualTo(2));
            Assert.That(((RemovalCue)tape.Cues[5]).AnchorIndex, Is.EqualTo(2));
            Assert.That(((RemovalCue)tape.Cues[5]).VictimIndex, Is.EqualTo(-1));
            Assert.That(((RemovalCue)tape.Cues[5]).Happened, Is.False);
            Assert.That(tape.Cues[tape.Cues.Count - 1], Is.InstanceOf<PayCue>());

            var pay = (PayCue)tape.Cues[tape.Cues.Count - 1];
            Assert.That(pay.Kind, Is.EqualTo(PayKind.Short));
            Assert.That(pay.Produced, Is.EqualTo(5));
            Assert.That(pay.Deducted, Is.EqualTo(0));
            Assert.That(pay.Shortfall, Is.EqualTo(45));
            Assert.That(pay.Line, Is.EqualTo("能量不足，欠额 45。这是加班，不是重开。"));
            AssertRolesOnce(tape);
        }

        [Test]
        public void 换位之后消灭吐息落在下标1()
        {
            var slots = new[] { "breath", "swapper" };
            var tape = ScoringTape.Arrange(
                new SettlementEntry[]
                {
                    new SettlementLanding("breath", "能量吐息", 5, 1, 5, 0),
                    new SettlementLanding("swapper", "产能", 1, 1, 1, 0),
                    new SettlementSwap("swapper", "breath", true),
                    new SettlementRemoval("breath", true),
                    new SettlementPayment(6, 0, false, false, false, 40),
                },
                slots);

            var removal = tape.Cues.OfType<RemovalCue>().Single();
            Assert.That(removal.VictimIndex, Is.EqualTo(1));
            Assert.That(removal.AnchorIndex, Is.EqualTo(1));
            Assert.That(removal.Happened, Is.True);
            Assert.That(tape.Produced, Is.EqualTo(6));
            Assert.That(tape.Cues.OfType<PopCue>().Sum(pop => pop.Energy), Is.EqualTo(6));

            var swap = tape.Cues.OfType<SwapCue>().Single();
            Assert.That(swap.ActorIndex, Is.EqualTo(1));
            Assert.That(swap.TargetIndex, Is.EqualTo(0));
            Assert.That(swap.Happened, Is.True);

            var walks = tape.Cues.OfType<WalkCue>().ToArray();
            Assert.That(walks[walks.Length - 1].FromIndex, Is.EqualTo(0));
            Assert.That(walks[walks.Length - 1].ToIndex, Is.EqualTo(1));

            var pay = (PayCue)tape.Cues[tape.Cues.Count - 1];
            Assert.That(pay.Kind, Is.EqualTo(PayKind.Paid));
            Assert.That(pay.Produced, Is.EqualTo(6));
            Assert.That(pay.Wage, Is.EqualTo(40));
            Assert.That(pay.Line, Is.EqualTo("结算完成，商店开了。"));
            Assert.That(slots, Is.EqualTo(new[] { "breath", "swapper" }));
            AssertRolesOnce(tape);
        }

        [Test]
        public void 侧向落地按报价加项只数底因子倍能量写回出字()
        {
            var tape = ScoringTape.Arrange(
                new SettlementEntry[]
                {
                    new SettlementLanding(
                        "sided",
                        "侧向",
                        10,
                        3,
                        30,
                        4,
                        3,
                        2,
                        true,
                        new[] { new LandingAdd("标签", 2) },
                        new[] { new LandingFactor("暴击", 3) }),
                },
                new[] { "sided" });

            var pop = tape.Cues.OfType<PopCue>().Single();
            Assert.That(pop.SlotIndex, Is.EqualTo(0));
            Assert.That(pop.Energy, Is.EqualTo(30));
            Assert.That(pop.Multiplier, Is.EqualTo(3));
            Assert.That(pop.Writeback, Is.EqualTo(4));
            Assert.That(tape.Produced, Is.EqualTo(30));
            Assert.That(
                pop.Figures.Select(figure => figure.Text).ToArray(),
                Is.EqualTo(new[] { "3", "+2 标签", "×2", "底 10", "×3 暴击", "倍 3", "+30", "写回 +4" }));
            Assert.That(
                pop.Figures.Select(figure => figure.Role).ToArray(),
                Is.EqualTo(new[]
                {
                    FigureRole.Quote,
                    FigureRole.Add,
                    FigureRole.SideCount,
                    FigureRole.Base,
                    FigureRole.Factor,
                    FigureRole.Multiplier,
                    FigureRole.Energy,
                    FigureRole.Writeback,
                }));
            AssertRolesOnce(tape);

            var emptySide = ScoringTape.Arrange(
                new SettlementEntry[]
                {
                    new SettlementLanding(
                        "empty-side",
                        "侧向",
                        0,
                        1,
                        0,
                        0,
                        4,
                        0,
                        true,
                        System.Array.Empty<LandingAdd>(),
                        System.Array.Empty<LandingFactor>()),
                },
                new[] { "empty-side" });
            var zero = emptySide.Cues.OfType<PopCue>().Single();
            Assert.That(
                zero.Figures.Select(figure => figure.Text).ToArray(),
                Is.EqualTo(new[] { "4", "×0", "底 0", "倍 1", "+0" }));
            Assert.That(emptySide.Produced, Is.EqualTo(0));
        }

        [Test]
        public void 支付句子沿用结算的四句且失败优先()
        {
            var failed = PayOf(new SettlementPayment(0, 9, true, true, true, 0));
            Assert.That(failed.Kind, Is.EqualTo(PayKind.Failed));
            Assert.That(failed.Line, Is.EqualTo("加班后仍未补足。"));
            Assert.That(failed.Produced, Is.EqualTo(0));

            var shortfall = PayOf(new SettlementPayment(0, 3, true, false, false, 0));
            Assert.That(shortfall.Kind, Is.EqualTo(PayKind.Short));
            Assert.That(shortfall.Line, Is.EqualTo("能量不足，欠额 3。这是加班，不是重开。"));

            var excess = PayOf(new SettlementPayment(8, 0, false, false, true, 40));
            Assert.That(excess.Kind, Is.EqualTo(PayKind.Paid));
            Assert.That(excess.Excess, Is.True);
            Assert.That(excess.Line, Is.EqualTo("结算完成，获得 1 科技点。商店开了。"));

            var paid = PayOf(new SettlementPayment(8, 0, false, false, false, 0));
            Assert.That(paid.Kind, Is.EqualTo(PayKind.Paid));
            Assert.That(paid.Line, Is.EqualTo("结算完成，商店开了。"));
        }

        [Test]
        public void 同一怪物连续落地不插入行走()
        {
            var tape = ScoringTape.Arrange(
                new SettlementEntry[]
                {
                    new SettlementLanding("breath", "能量吐息", 5, 1, 5, 0),
                    new SettlementLanding("breath", "毒跳伤", 2, 1, 2, 0),
                },
                new[] { "breath" });

            Assert.That(tape.Cues.Count, Is.EqualTo(3));
            Assert.That(((WalkCue)tape.Cues[0]).ToIndex, Is.EqualTo(0));
            Assert.That(tape.Cues[1], Is.InstanceOf<PopCue>());
            Assert.That(tape.Cues[2], Is.InstanceOf<PopCue>());
            Assert.That(tape.Produced, Is.EqualTo(7));
            AssertRolesOnce(tape);
        }

        [Test]
        public void 记录或槽位缺失时步骤为空()
        {
            var missingEntries = ScoringTape.Arrange(null, new[] { "breath" });
            Assert.That(missingEntries.Cues.Count, Is.EqualTo(0));
            Assert.That(missingEntries.Produced, Is.EqualTo(0));

            var missingSlots = ScoringTape.Arrange(
                new SettlementEntry[] { new SettlementLanding("breath", "能量吐息", 5, 1, 5, 0) },
                null);
            Assert.That(missingSlots.Cues.Count, Is.EqualTo(0));
            Assert.That(missingSlots.Produced, Is.EqualTo(0));
        }

        static PayCue PayOf(SettlementPayment payment)
        {
            var tape = ScoringTape.Arrange(new SettlementEntry[] { payment }, new[] { "breath" });
            return (PayCue)tape.Cues.Single();
        }

        [Test]
        public void 落地先加上自身底分再逐个加上别人的分数和倍率差额()
        {
            var plain = ScoringTape.Arrange(
                new SettlementEntry[] { new SettlementLanding("breath", "能量吐息", 5, 1, 5, 0) },
                new[] { "breath" });
            Assert.That(plain.Cues.OfType<PopCue>().Single().Bits, Is.EqualTo(new[] { 5 }));

            var added = ScoringTape.Arrange(
                new SettlementEntry[]
                {
                    new SettlementLanding(
                        "breath",
                        "能量吐息",
                        8,
                        2,
                        16,
                        0,
                        5,
                        0,
                        false,
                        new[] { new LandingAdd("增量小手", 1), new LandingAdd("增量大手", 2) },
                        new[] { new LandingFactor("鼓励嘴", 2) }),
                },
                new[] { "breath" });
            Assert.That(added.Cues.OfType<PopCue>().Single().Bits, Is.EqualTo(new[] { 5, 1, 2, 8 }));

            var sided = ScoringTape.Arrange(
                new SettlementEntry[]
                {
                    new SettlementLanding(
                        "sided",
                        "侧向",
                        10,
                        3,
                        30,
                        4,
                        3,
                        2,
                        true,
                        new[] { new LandingAdd("标签", 2) },
                        new[] { new LandingFactor("暴击", 3) }),
                },
                new[] { "sided" });
            Assert.That(sided.Cues.OfType<PopCue>().Single().Bits, Is.EqualTo(new[] { 6, 4, 20 }));
        }

        static void AssertRolesOnce(ScoringTape tape)
        {
            foreach (var pop in tape.Cues.OfType<PopCue>())
            {
                Assert.That(pop.Figures.Count(figure => figure.Role == FigureRole.Base), Is.EqualTo(1));
                Assert.That(pop.Figures.Count(figure => figure.Role == FigureRole.Multiplier), Is.EqualTo(1));
                Assert.That(pop.Figures.Count(figure => figure.Role == FigureRole.Energy), Is.EqualTo(1));
                Assert.That(pop.Bits.Sum(), Is.EqualTo(pop.Energy));
            }
        }
    }
}
