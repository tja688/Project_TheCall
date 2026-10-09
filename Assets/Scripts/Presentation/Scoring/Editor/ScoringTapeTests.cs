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
                    new SettlementLanding("residue", "汲取鼻", 1, 1, 1, 0),
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
            Assert.That(pay.Line, Is.EqualTo("能量不足，欠额 45"));
            AssertRolesOnce(tape);
        }

        [Test]
        public void 换位之后消灭吐息落在下标1()
        {
            var slots = new[] { "breath", "swapper" };
            var tape = ScoringTape.Arrange(
                new SettlementEntry[]
                {
                    new SettlementLanding("breath", "能量体", 5, 1, 5, 0),
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
            Assert.That(pay.Line, Is.EqualTo("结算完成，未能获得科技点"));
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
                        8,
                        3,
                        24,
                        4,
                        3,
                        2,
                        true,
                        new[] { new LandingAdd("标签", 2, null, -1, null) },
                        new[] { new LandingFactor("暴击", 3, null, -1, null) }),
                },
                new[] { "sided" });

            var pop = tape.Cues.OfType<PopCue>().Single();
            Assert.That(pop.SlotIndex, Is.EqualTo(0));
            Assert.That(pop.Energy, Is.EqualTo(24));
            Assert.That(pop.Multiplier, Is.EqualTo(3));
            Assert.That(pop.Writeback, Is.EqualTo(4));
            Assert.That(tape.Produced, Is.EqualTo(24));
            Assert.That(
                pop.Figures.Select(figure => figure.Text).ToArray(),
                Is.EqualTo(new[] { "3", "+2 标签", "×2", "底 8", "×3 暴击", "倍 3", "+24", "写回 +4" }));
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
            Assert.That(failed.Line, Is.EqualTo("加班后仍未能达标"));
            Assert.That(failed.Produced, Is.EqualTo(0));

            var shortfall = PayOf(new SettlementPayment(0, 3, true, false, false, 0));
            Assert.That(shortfall.Kind, Is.EqualTo(PayKind.Short));
            Assert.That(shortfall.Line, Is.EqualTo("能量不足，欠额 3"));

            var excess = PayOf(new SettlementPayment(8, 0, false, false, true, 40));
            Assert.That(excess.Kind, Is.EqualTo(PayKind.Paid));
            Assert.That(excess.Excess, Is.True);
            Assert.That(excess.Line, Is.EqualTo("结算完成，获得 1 科技点"));

            var paid = PayOf(new SettlementPayment(8, 0, false, false, false, 0));
            Assert.That(paid.Kind, Is.EqualTo(PayKind.Paid));
            Assert.That(paid.Line, Is.EqualTo("结算完成，未能获得科技点"));
        }

        [Test]
        public void 同一怪物连续落地不插入行走()
        {
            var tape = ScoringTape.Arrange(
                new SettlementEntry[]
                {
                    new SettlementLanding("breath", "能量体", 5, 1, 5, 0),
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
                new SettlementEntry[] { new SettlementLanding("breath", "能量体", 5, 1, 5, 0) },
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
                new SettlementEntry[] { new SettlementLanding("breath", "能量体", 5, 1, 5, 0) },
                new[] { "breath" });
            var plainPop = plain.Cues.OfType<PopCue>().Single();
            Assert.That(plainPop.Bits, Is.EqualTo(new[] { 5 }));
            Assert.That(plainPop.Beats.Select(beat => beat.Text).ToArray(), Is.EqualTo(new[] { "+5" }));
            Assert.That(plainPop.Beats.Single().Role, Is.EqualTo(BeatRole.Energy));

            var added = ScoringTape.Arrange(
                new SettlementEntry[]
                {
                    new SettlementLanding(
                        "breath",
                        "能量体",
                        8,
                        2,
                        16,
                        0,
                        5,
                        0,
                        false,
                        new[] { new LandingAdd("奇异香", 1, null, -1, null), new LandingAdd("怪异香", 2, null, -1, null) },
                        new[] { new LandingFactor("镜眼", 2, null, -1, null) }),
                },
                new[] { "breath" });
            var addedPop = added.Cues.OfType<PopCue>().Single();
            Assert.That(addedPop.Bits, Is.EqualTo(new[] { 5, 1, 2, 8 }));
            Assert.That(
                addedPop.Beats.Select(beat => beat.Text).ToArray(),
                Is.EqualTo(new[] { "5", "+1", "+2", "×2", "+16" }));

            var sided = ScoringTape.Arrange(
                new SettlementEntry[]
                {
                    new SettlementLanding(
                        "sided",
                        "侧向",
                        8,
                        3,
                        24,
                        4,
                        3,
                        2,
                        true,
                        new[] { new LandingAdd("标签", 2, null, -1, null) },
                        new[] { new LandingFactor("暴击", 3, null, -1, null) }),
                },
                new[] { "sided" });
            var sidedPop = sided.Cues.OfType<PopCue>().Single();
            Assert.That(sidedPop.Bits, Is.EqualTo(new[] { 6, 2, 16 }));
            Assert.That(
                sidedPop.Beats.Select(beat => beat.Text).ToArray(),
                Is.EqualTo(new[] { "3", "×2", "+2", "×3", "+24", "写回 +4" }));
        }

        [Test]
        public void 外来加项和倍率从那只怪物抛向正在计分的怪物最后才落地()
        {
            var tape = ScoringTape.Arrange(
                new SettlementEntry[]
                {
                    new SettlementLanding(
                        "breath",
                        "能量体",
                        8,
                        2,
                        16,
                        0,
                        5,
                        0,
                        false,
                        new[]
                        {
                            new LandingAdd("奇异香", 1, null, -1, "SCP-096"),
                            new LandingAdd("良好肉体", 2, null, -1, "SCP-173"),
                        },
                        new[] { new LandingFactor("镜眼", 2, null, -1, "SCP-682") },
                        "SCP-173",
                        1,
                        null),
                },
                new[] { "incense", "breath", "eye" });

            var pop = tape.Cues.OfType<PopCue>().Single();
            Assert.That(
                pop.Beats.Select(beat => beat.Role).ToArray(),
                Is.EqualTo(new[]
                {
                    BeatRole.Quote,
                    BeatRole.Add,
                    BeatRole.Add,
                    BeatRole.Factor,
                    BeatRole.Energy,
                }));
            Assert.That(
                pop.Beats.Select(beat => beat.Text).ToArray(),
                Is.EqualTo(new[] { "5", "+1", "+2", "×2", "+16" }));
            Assert.That(
                pop.Beats.Select(beat => beat.SourceName).ToArray(),
                Is.EqualTo(new[] { "", "SCP-096", "", "SCP-682", "" }));
            Assert.That(pop.Beats.Any(beat => beat.Role == BeatRole.Again || beat.Text == "+1次"), Is.False);
            Assert.That(pop.Beats[1].Caption, Is.EqualTo("奇异香"));
            Assert.That(pop.Beats[2].Caption, Is.EqualTo("良好肉体"));
            Assert.That(pop.Beats[3].Caption, Is.EqualTo("镜眼"));
            Assert.That(pop.Bits.Sum(), Is.EqualTo(16));
            Assert.That(tape.Produced, Is.EqualTo(16));
        }

        [Test]
        public void 未中先走到那一格再亮红字且不计入产出()
        {
            var tape = ScoringTape.Arrange(
                new SettlementEntry[] { SettlementMark.Miss("breath") },
                new[] { "breath" });

            Assert.That(tape.Produced, Is.EqualTo(0));
            Assert.That(tape.Cues.Count, Is.EqualTo(2));
            var walk = (WalkCue)tape.Cues[0];
            Assert.That(walk.FromIndex, Is.EqualTo(-1));
            Assert.That(walk.ToIndex, Is.EqualTo(0));
            var mark = (MarkCue)tape.Cues[1];
            Assert.That(mark.Label, Is.EqualTo("+0"));
            Assert.That(mark.Tone, Is.EqualTo(MarkTone.Miss));
        }

        [Test]
        public void 再触发字在来源上且弹出里不再有加一次()
        {
            var tape = ScoringTape.Arrange(
                new SettlementEntry[]
                {
                    SettlementMark.Again("gland", "左复制腺体"),
                    new SettlementLanding("breath", "双头能量体", 2, 1, 2, 0),
                    new SettlementLanding("breath", "双头能量体", 2, 1, 2, 0),
                },
                new[] { "breath", "gland" });

            Assert.That(tape.Produced, Is.EqualTo(4));
            var mark = tape.Cues.OfType<MarkCue>().Single();
            Assert.That(mark.Label, Is.EqualTo("左复制腺体触发+1"));
            var pops = tape.Cues.OfType<PopCue>().ToArray();
            Assert.That(pops.Length, Is.EqualTo(2));
            Assert.That(pops[0].Energy, Is.EqualTo(2));
            Assert.That(pops[1].Energy, Is.EqualTo(2));
            Assert.That(pops[0].Beats.Any(beat => beat.Role == BeatRole.Again || beat.Text == "+1次"), Is.False);
            Assert.That(pops[1].Beats.Any(beat => beat.Role == BeatRole.Again || beat.Text == "+1次"), Is.False);
        }

        static void AssertRolesOnce(ScoringTape tape)
        {
            foreach (var pop in tape.Cues.OfType<PopCue>())
            {
                Assert.That(pop.Figures.Count(figure => figure.Role == FigureRole.Base), Is.EqualTo(1));
                Assert.That(pop.Figures.Count(figure => figure.Role == FigureRole.Multiplier), Is.EqualTo(1));
                Assert.That(pop.Figures.Count(figure => figure.Role == FigureRole.Energy), Is.EqualTo(1));
                Assert.That(pop.Bits.Sum(), Is.EqualTo(pop.Energy));
                Assert.That(pop.Beats.Count(beat => beat.Role == BeatRole.Energy), Is.EqualTo(1));
                var energy = pop.Beats.Single(beat => beat.Role == BeatRole.Energy);
                Assert.That(energy.Text, Is.EqualTo(pop.Energy > 0 ? "+" + pop.Energy : pop.Energy.ToString()));
                Assert.That(
                    pop.Beats.Count(beat => beat.Role == BeatRole.Writeback),
                    Is.EqualTo(pop.Writeback == 0 ? 0 : 1));
                var last = pop.Beats[pop.Beats.Count - 1].Role;
                Assert.That(last, Is.EqualTo(pop.Writeback == 0 ? BeatRole.Energy : BeatRole.Writeback));
            }
        }
    }
}
