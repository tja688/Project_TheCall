using System;
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

            if (key == "DoubleWhenIsolated")
                return new[] { SentencePiece.Literal("当相邻没有其他怪物时，本怪物本次结算产生的全部能量先加外部数值再翻倍") };
            if (key == "Devour")
                return new[] { SentencePiece.Literal("消灭随机一只相邻怪物，本怪物产能类型的技能数值永久+" + skill.Get(EffectKind.Devour).A) };

            if (key == "EnergyQuote+RepeatQuote")
                return new[]
                {
                    SentencePiece.Literal("产生"),
                    SentencePiece.Points(skill.Get(EffectKind.EnergyQuote).A),
                    SentencePiece.Literal("点能量两次"),
                };
            if (key == "CapacityExtraForLeftNeighbor")
                return new[] { SentencePiece.Literal("左侧相邻怪物，产生能量的触发次数+" + skill.Get(EffectKind.CapacityExtraForLeftNeighbor).A) };
            if (key == "CapacityExtraForRightNeighbor")
                return new[] { SentencePiece.Literal("右侧相邻怪物，产生能量的触发次数+" + skill.Get(EffectKind.CapacityExtraForRightNeighbor).A) };
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
            if (key == "OwnSkillMultiple")
                return new[] { SentencePiece.Literal("产生等同于怪物技能数数值两倍的能量") };
            if (key == "PopulationQuote")
                return new[] { SentencePiece.Literal("产生等同于怪物总量数值（培育槽+提取槽+怪物笼）的能量") };
            if (key == "EnergyQuote+GrowOnClear")
            {
                return new[]
                {
                    SentencePiece.Literal("产生"),
                    SentencePiece.Points(skill.Get(EffectKind.EnergyQuote).A),
                    SentencePiece.Literal("点能量，每通过一次关卡本，本技能能量数值永久+" + skill.Get(EffectKind.GrowOnClear).A),
                };
            }

            if (key == "EnergyQuote+SameNameExtra")
            {
                return new[]
                {
                    SentencePiece.Literal("产生"),
                    SentencePiece.Points(skill.Get(EffectKind.EnergyQuote).A),
                    SentencePiece.Literal("点能量，使用本技能后，其他怪物同名技能触发次数+1"),
                };
            }

            if (key == "ChanceQuote")
            {
                var effect = skill.Get(EffectKind.ChanceQuote);
                return new[]
                {
                    SentencePiece.Literal(effect.B + "%概率产生"),
                    SentencePiece.Points(effect.A),
                    SentencePiece.Literal("点能量"),
                };
            }

            if (key == "SkillCountExtra")
                return new[] { SentencePiece.Literal("当本怪物仅有" + CountWord(skill.Get(EffectKind.SkillCountExtra).A) + "个技能时，本怪物其他技能触发两次") };
            if (key == "LegacyOnDiscard")
                return new[] { SentencePiece.Literal("遗赠获得一个随机单技能怪物（通过废弃槽消灭，不会获得该技能，即使怪物只有这一个技能也是）") };
            if (key == "GainGold")
                return new[] { SentencePiece.Literal("获得" + skill.Get(EffectKind.GainGold).A + "点金币") };
            if (key == "FillSkills")
                return new[] { SentencePiece.Literal("随机获得技能，直到达到本怪物技能上限") };
            if (key == "EdgeBonus")
            {
                var effect = skill.Get(EffectKind.EdgeBonus);
                var edge = (CountedSide)effect.B == CountedSide.Left ? "最左侧" : "最右侧";
                return new[] { SentencePiece.Literal("当本怪物为提取槽" + edge + "怪物时，本怪物产生能量的技能能量数值+" + effect.A) };
            }

            if (key == "CopyBreeding")
                return new[] { SentencePiece.Literal("从每个培育笼中的怪物已有技能中随机获取一个技能") };
            if (key == "KinExtra")
                return new[] { SentencePiece.Literal("当本怪物与本怪物父本一起在提取槽时，提取槽里与父本技能同名的技能各触发次数+1") };
            if (key == "SkillCountAdd")
            {
                var effect = skill.Get(EffectKind.SkillCountAdd);
                return new[] { SentencePiece.Literal("当本怪物仅有" + CountWord(effect.A) + "个技能时，本怪物产生能量的技能能量数值+" + effect.B) };
            }

            if (key == "RightRowOnLeftActive")
                return new[] { SentencePiece.Literal("左侧相邻怪物每触发一个主动技能，右侧每一只怪物能量数值+" + skill.Get(EffectKind.RightRowOnLeftActive).A) };

            throw new ContentBookException(skill.Name + " 的效果组合没有句子");
        }

        static SentencePiece[] Produced(int amount) => new[]
        {
            SentencePiece.Literal("产生"),
            SentencePiece.Points(amount),
            SentencePiece.Literal("点能量"),
        };

        static string Times(int count) => count == 2 ? "两次" : count + "次";

        static string CountWord(int count) => count == 2 ? "两" : count == 3 ? "三" : count.ToString();

        static string Key(SkillDef skill)
        {
            var buffer = new StringBuilder();
            var kinds = (EffectKind[])Enum.GetValues(typeof(EffectKind));
            for (var index = 0; index < kinds.Length; index++)
            {
                var kind = kinds[index];
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
