using System;
using System.Collections.Generic;

namespace TheCall.Scoring
{
    public sealed class ScoringTape
    {
        ScoringTape(ScoringCue[] cues, int produced)
        {
            Cues = cues;
            Produced = produced;
        }

        public IReadOnlyList<ScoringCue> Cues { get; }
        public int Produced { get; }

        public static ScoringTape Arrange(
            IReadOnlyList<SettlementEntry> entries,
            IReadOnlyList<string> slotIds)
        {
            if (entries == null || slotIds == null)
                return new ScoringTape(Array.Empty<ScoringCue>(), 0);

            var ids = new List<string>(slotIds.Count);
            for (var i = 0; i < slotIds.Count; i++)
                ids.Add(string.IsNullOrEmpty(slotIds[i]) ? null : slotIds[i]);

            var cues = new List<ScoringCue>();
            var head = -1;
            var produced = 0;
            for (var i = 0; i < entries.Count; i++)
                Append(entries[i], ids, cues, ref head, ref produced);

            return new ScoringTape(cues.ToArray(), produced);
        }

        static void Append(
            SettlementEntry entry,
            List<string> ids,
            List<ScoringCue> cues,
            ref int head,
            ref int produced)
        {
            if (entry is SettlementMark mark)
            {
                var slot = IndexOf(ids, mark.MonsterId);
                if (slot != head)
                {
                    cues.Add(new WalkCue(head, slot));
                    head = slot;
                }

                cues.Add(new MarkCue(slot, mark.Label, mark.Tone, mark.Motion));
                return;
            }

            if (entry is SettlementLanding landing)
            {
                var slot = IndexOf(ids, landing.MonsterId);
                if (slot != head)
                {
                    cues.Add(new WalkCue(head, slot));
                    head = slot;
                }

                cues.Add(new PopCue(
                    slot,
                    landing.SkillName,
                    landing.Energy,
                    landing.Multiplier,
                    landing.Writeback,
                    FiguresOf(landing),
                    BitsOf(landing),
                    BeatsOf(landing)));
                produced += landing.Energy;
                return;
            }

            if (entry is SettlementSwap swap)
            {
                var actor = IndexOf(ids, swap.ActorId);
                var target = IndexOf(ids, swap.TargetId);
                var moved = swap.Happened && actor >= 0 && target >= 0 && actor != target;
                if (actor >= 0 && actor != head)
                {
                    cues.Add(new WalkCue(head, actor));
                    head = actor;
                }

                cues.Add(new SwapCue(actor, target, moved));
                if (moved)
                {
                    var actorId = ids[actor];
                    ids[actor] = ids[target];
                    ids[target] = actorId;
                    head = IndexOf(ids, swap.ActorId);
                }

                return;
            }

            if (entry is SettlementRemoval removal)
            {
                var victim = IndexOf(ids, removal.MonsterId);
                var source = IndexOf(ids, removal.SourceId);
                var anchor = victim >= 0 ? victim : source >= 0 ? source : head;
                if (anchor >= 0 && anchor != head)
                {
                    cues.Add(new WalkCue(head, anchor));
                    head = anchor;
                }

                var happened = removal.Happened && victim >= 0;
                cues.Add(new RemovalCue(anchor, victim, happened));
                if (happened)
                    ids[victim] = null;

                return;
            }

            if (entry is SettlementPayment payment)
            {
                cues.Add(new PayCue(
                    KindOf(payment),
                    produced,
                    payment.Deducted,
                    payment.Shortfall,
                    payment.Wage,
                    payment.Excess,
                    PaymentLine(payment)));
                return;
            }

            var name = entry == null ? "null" : entry.GetType().Name;
            if (name == "SettlementHold" || name == "SettlementCause")
                throw new InvalidOperationException(name);

            throw new InvalidOperationException(name);
        }

        static Figure[] FiguresOf(SettlementLanding landing)
        {
            var figures = new List<Figure>();
            if (landing.Side || landing.Adds.Count > 0 || landing.Quote != landing.Base)
                figures.Add(new Figure(landing.Quote.ToString(), FigureRole.Quote));

            for (var i = 0; i < landing.Adds.Count; i++)
            {
                var add = landing.Adds[i];
                figures.Add(new Figure("+" + add.Amount + " " + add.Label, FigureRole.Add));
            }

            if (landing.Side)
                figures.Add(new Figure("×" + landing.SideCount, FigureRole.SideCount));

            figures.Add(new Figure("底 " + landing.Base, FigureRole.Base));
            for (var i = 0; i < landing.Factors.Count; i++)
            {
                var factor = landing.Factors[i];
                figures.Add(new Figure("×" + factor.Factor + " " + factor.Label, FigureRole.Factor));
            }

            figures.Add(new Figure("倍 " + landing.Multiplier, FigureRole.Multiplier));
            figures.Add(new Figure("+" + landing.Energy, FigureRole.Energy));
            if (landing.Writeback != 0)
                figures.Add(new Figure("写回 +" + landing.Writeback, FigureRole.Writeback));

            return figures.ToArray();
        }

        static ScoreBeat[] BeatsOf(SettlementLanding landing)
        {
            var beats = new List<ScoreBeat>();
            var formula = landing.Side || landing.Adds.Count > 0 || landing.Factors.Count > 0;
            if (formula)
            {
                if (landing.Quote != 0 || landing.Side)
                    beats.Add(new ScoreBeat(landing.Quote.ToString(), BeatRole.Quote, "", ""));

                if (landing.Side)
                    beats.Add(new ScoreBeat("×" + landing.SideCount, BeatRole.Side, "", landing.CountedSideName));

                for (var i = 0; i < landing.Adds.Count; i++)
                {
                    var add = landing.Adds[i];
                    beats.Add(new ScoreBeat(Signed(add.Amount), BeatRole.Add, Other(add.SourceName, landing.MonsterName), add.Label));
                }

                for (var i = 0; i < landing.Factors.Count; i++)
                {
                    var factor = landing.Factors[i];
                    beats.Add(new ScoreBeat("×" + factor.Factor, BeatRole.Factor, Other(factor.SourceName, landing.MonsterName), factor.Label));
                }
            }

            beats.Add(new ScoreBeat(Signed(landing.Energy), BeatRole.Energy, "", ""));
            if (landing.Writeback != 0)
                beats.Add(new ScoreBeat("写回 " + Signed(landing.Writeback), BeatRole.Writeback, "", ""));

            return beats.ToArray();
        }

        static string Other(string sourceName, string selfName)
        {
            if (string.IsNullOrEmpty(sourceName) || sourceName == selfName)
                return "";

            return sourceName;
        }

        static string Signed(int amount) => amount > 0 ? "+" + amount : amount.ToString();

        static int[] BitsOf(SettlementLanding landing)
        {
            var added = 0;
            for (var i = 0; i < landing.Adds.Count; i++)
                added += landing.Adds[i].Amount;

            var scale = landing.Side ? landing.SideCount : 1;
            var bits = new List<int>(landing.Adds.Count + 2);
            bits.Add(landing.Quote * scale);
            for (var i = 0; i < landing.Adds.Count; i++)
                bits.Add(landing.Adds[i].Amount);

            var extra = landing.Energy - (landing.Quote * scale + added);
            if (extra != 0)
                bits.Add(extra);

            var sum = 0;
            for (var i = 0; i < bits.Count; i++)
                sum += bits[i];
            if (sum != landing.Energy)
                throw new InvalidOperationException(landing.SkillName + " 的演出分数和能量不一致");

            return bits.ToArray();
        }

        static PayKind KindOf(SettlementPayment payment)
        {
            if (payment.Failed)
                return PayKind.Failed;
            if (payment.Deducted == 0 && payment.Shortfall > 0)
                return PayKind.Short;
            return PayKind.Paid;
        }

        static string PaymentLine(SettlementPayment payment)
        {
            if (payment.Failed)
                return "加班后仍未能达标";
            if (payment.Deducted == 0 && payment.Shortfall > 0)
                return "能量不足，欠额 " + payment.Shortfall;
            if (payment.Excess)
                return "结算完成，获得 1 科技点";
            return "结算完成，未能获得科技点";
        }

        static int IndexOf(List<string> ids, string id)
        {
            if (string.IsNullOrEmpty(id))
                return -1;

            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                    return i;
            }

            return -1;
        }
    }
}
