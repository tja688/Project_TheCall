using System.Text;

namespace TheCall
{
    internal readonly struct SentencePiece
    {
        public SentencePiece(string text, int energy, bool isEnergy)
        {
            Text = text;
            Energy = energy;
            IsEnergy = isEnergy;
        }

        public string Text { get; }

        public int Energy { get; }

        public bool IsEnergy { get; }

        public static SentencePiece Literal(string text) => new SentencePiece(text, 0, false);

        // 上台后会随其他怪物的能量加成改写。次数、产能、光环自己的加值不要用这个。
        public static SentencePiece Points(int value) => new SentencePiece(null, value, true);
    }

    // 技能句子只写在这里。新的效果组合补一条；已有组合只改 call-book.json 就会进怪物库和技能库。
    internal static class SkillSentences
    {
        public static string Format(SkillDef skill)
        {
            var pieces = Pieces(skill);
            var buffer = new StringBuilder();
            for (var i = 0; i < pieces.Length; i++)
                buffer.Append(pieces[i].IsEnergy ? pieces[i].Energy.ToString() : pieces[i].Text);

            return buffer.ToString();
        }

        internal static SentencePiece[] Pieces(SkillDef skill)
        {
            var key = Key(skill);
            if (key == "EnergyQuote")
                return Produced(skill.Get(EffectKind.EnergyQuote).A);
            if (key == "SideCount")
            {
                var effect = skill.Get(EffectKind.SideCount);
                var side = (CountedSide)effect.B == CountedSide.Right ? "右侧" : "左侧";
                return new[]
                {
                    SentencePiece.Literal(side + "每有一个怪物产生"),
                    SentencePiece.Points(effect.A),
                    SentencePiece.Literal("点能量"),
                };
            }

            if (key == "AddToOthers")
                return new[] { SentencePiece.Literal("其他怪物产生的能量数值+" + skill.Get(EffectKind.AddToOthers).A) };
            if (key == "LandingResponse")
            {
                return new[]
                {
                    SentencePiece.Literal("其他怪物产生能量时，本怪物产生"),
                    SentencePiece.Points(skill.Get(EffectKind.LandingResponse).A),
                    SentencePiece.Literal("点能量（不会因为同名技能效果产生能量）"),
                };
            }

            if (key == "EnergyQuote+DoubleWhenIsolated")
            {
                return new[]
                {
                    SentencePiece.Literal("产生"),
                    SentencePiece.Points(skill.Get(EffectKind.EnergyQuote).A),
                    SentencePiece.Literal("点能量，当相邻没有其他怪物时，本怪物产生的能量翻倍"),
                };
            }

            if (key == "EnergyQuote+Devour")
            {
                return new[]
                {
                    SentencePiece.Literal("产生"),
                    SentencePiece.Points(skill.Get(EffectKind.EnergyQuote).A),
                    SentencePiece.Literal("点能量，消灭随机一只相邻怪物，本技能产生的能量数值永久+" + skill.Get(EffectKind.Devour).A),
                };
            }

            if (key == "EnergyQuote+RepeatQuote")
                return new[]
                {
                    SentencePiece.Literal("产生"),
                    SentencePiece.Points(skill.Get(EffectKind.EnergyQuote).A),
                    SentencePiece.Literal("点能量两次"),
                };
            if (key == "CapacityExtraForLeftNeighbor")
                return new[] { SentencePiece.Literal("左侧相邻怪物，产生能量的技能次数+" + skill.Get(EffectKind.CapacityExtraForLeftNeighbor).A) };
            if (key == "ExtraWalksForRightNeighbor")
                return new[] { SentencePiece.Literal("右侧相邻怪物的所有技能效果都触发" + Times(skill.Get(EffectKind.ExtraWalksForRightNeighbor).A + 1)) };
            if (key == "EnergyQuote+NextEnergyBonus")
            {
                return new[]
                {
                    SentencePiece.Literal("产生"),
                    SentencePiece.Points(skill.Get(EffectKind.EnergyQuote).A),
                    SentencePiece.Literal("点能量，下一个怪物产生的能量数值+" + skill.Get(EffectKind.NextEnergyBonus).A),
                };
            }

            if (key == "GainCapacity")
                return new[] { SentencePiece.Literal("获得" + skill.Get(EffectKind.GainCapacity).A + "点产能") };
            if (key == "SwapWithLeft")
                return new[] { SentencePiece.Literal("与左侧相邻怪物交换位置，之后获得不动") };
            if (key == "DoubleAdjacentEnergy")
                return new[] { SentencePiece.Literal("相邻怪物产生的能量翻倍") };

            throw new ContentBookException(skill.Name + " 的效果组合没有句子");
        }

        static SentencePiece[] Produced(int amount) => new[]
        {
            SentencePiece.Literal("产生"),
            SentencePiece.Points(amount),
            SentencePiece.Literal("点能量"),
        };

        static string Times(int count) => count == 2 ? "两次" : count + "次";

        static string Key(SkillDef skill)
        {
            var buffer = new StringBuilder();
            for (var kind = EffectKind.EnergyQuote; kind <= EffectKind.DoubleAdjacentEnergy; kind++)
            {
                if (!skill.Has(kind))
                    continue;

                if (buffer.Length > 0)
                    buffer.Append('+');

                buffer.Append(kind);
            }

            return buffer.ToString();
        }
    }
}
