using System;
using System.Collections;
using System.Collections.Generic;

namespace TheCall.Scoring
{
    public abstract class ScoringCue
    {
        internal abstract IEnumerator Accept(ICueSink sink);
    }

    internal interface ICueSink
    {
        IEnumerator Walk(WalkCue cue);
        IEnumerator Pop(PopCue cue);
        IEnumerator Swap(SwapCue cue);
        IEnumerator Remove(RemovalCue cue);
        IEnumerator Pay(PayCue cue);
        IEnumerator Mark(MarkCue cue);
    }

    public enum FigureRole
    {
        Quote,
        Add,
        SideCount,
        Base,
        Factor,
        Multiplier,
        Energy,
        Writeback,
    }

    public readonly struct Figure
    {
        public Figure(string text, FigureRole role)
        {
            Text = text;
            Role = role;
        }

        public string Text { get; }
        public FigureRole Role { get; }
    }

    public enum BeatRole
    {
        Again,
        Quote,
        Add,
        Side,
        Factor,
        Energy,
        Writeback,
    }

    public readonly struct ScoreBeat
    {
        public ScoreBeat(string text, BeatRole role, string sourceName, string caption)
        {
            Text = text;
            Role = role;
            SourceName = sourceName ?? "";
            Caption = caption ?? "";
        }

        public string Text { get; }
        public BeatRole Role { get; }
        public string SourceName { get; }
        public string Caption { get; }
    }

    public sealed class MarkCue : ScoringCue
    {
        internal MarkCue(int slotIndex, string label, MarkTone tone, MarkMotion motion)
        {
            SlotIndex = slotIndex;
            Label = label;
            Tone = tone;
            Motion = motion;
        }

        public int SlotIndex { get; }
        public string Label { get; }
        public MarkTone Tone { get; }
        public MarkMotion Motion { get; }

        internal override IEnumerator Accept(ICueSink sink) => sink.Mark(this);
    }

    public sealed class WalkCue : ScoringCue
    {
        internal WalkCue(int fromIndex, int toIndex)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
        }

        public int FromIndex { get; }
        public int ToIndex { get; }

        internal override IEnumerator Accept(ICueSink sink) => sink.Walk(this);
    }

    public sealed class PopCue : ScoringCue
    {
        internal PopCue(
            int slotIndex,
            string skillName,
            int energy,
            int multiplier,
            int writeback,
            Figure[] figures,
            int[] bits,
            ScoreBeat[] beats)
        {
            SlotIndex = slotIndex;
            SkillName = skillName;
            Energy = energy;
            Multiplier = multiplier;
            Writeback = writeback;
            Figures = figures ?? Array.Empty<Figure>();
            Bits = bits ?? Array.Empty<int>();
            Beats = beats ?? Array.Empty<ScoreBeat>();
        }

        public int SlotIndex { get; }
        public string SkillName { get; }
        public int Energy { get; }
        public int Multiplier { get; }
        public int Writeback { get; }
        public IReadOnlyList<Figure> Figures { get; }
        public IReadOnlyList<int> Bits { get; }
        public IReadOnlyList<ScoreBeat> Beats { get; }

        internal override IEnumerator Accept(ICueSink sink) => sink.Pop(this);
    }

    public sealed class SwapCue : ScoringCue
    {
        internal SwapCue(int actorIndex, int targetIndex, bool happened)
        {
            if (happened && (actorIndex < 0 || targetIndex < 0 || actorIndex == targetIndex))
                throw new ArgumentException("发生了的换位需要两个不同的下标");

            ActorIndex = actorIndex;
            TargetIndex = targetIndex;
            Happened = happened;
        }

        public int ActorIndex { get; }
        public int TargetIndex { get; }
        public bool Happened { get; }

        internal override IEnumerator Accept(ICueSink sink) => sink.Swap(this);
    }

    public sealed class RemovalCue : ScoringCue
    {
        internal RemovalCue(int anchorIndex, int victimIndex, bool happened)
        {
            if (happened && victimIndex < 0)
                throw new ArgumentException("发生了的消灭需要被拿走的下标");

            AnchorIndex = anchorIndex;
            VictimIndex = victimIndex;
            Happened = happened;
        }

        public int AnchorIndex { get; }
        public int VictimIndex { get; }
        public bool Happened { get; }

        internal override IEnumerator Accept(ICueSink sink) => sink.Remove(this);
    }

    public enum PayKind
    {
        Paid,
        Short,
        Failed,
    }

    public sealed class PayCue : ScoringCue
    {
        internal PayCue(
            PayKind kind,
            int produced,
            int deducted,
            int shortfall,
            int wage,
            bool excess,
            string line)
        {
            Kind = kind;
            Produced = produced;
            Deducted = deducted;
            Shortfall = shortfall;
            Wage = wage;
            Excess = excess;
            Line = line;
        }

        public PayKind Kind { get; }
        public int Produced { get; }
        public int Deducted { get; }
        public int Shortfall { get; }
        public int Wage { get; }
        public bool Excess { get; }
        public string Line { get; }

        internal override IEnumerator Accept(ICueSink sink) => sink.Pay(this);
    }
}
