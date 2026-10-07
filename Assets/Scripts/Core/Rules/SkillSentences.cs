using System.Text;

namespace TheCall
{
    internal static class SkillSentences
    {
        public static string Format(SkillDef skill)
        {
            var key = Key(skill);
            if (key == "EnergyQuote")
                return "产生" + skill.Get(EffectKind.EnergyQuote).A + "点能量";
            if (key == "SideCount")
            {
                var effect = skill.Get(EffectKind.SideCount);
                var side = (CountedSide)effect.B == CountedSide.Right ? "右侧" : "左侧";
                return side + "每有一个怪物产生" + effect.A + "点能量";
            }

            if (key == "AddToOthers")
                return "其他怪物产生的能量数值+" + skill.Get(EffectKind.AddToOthers).A;
            if (key == "LandingResponse")
                return "其他怪物产生能量时，本怪物产生" + skill.Get(EffectKind.LandingResponse).A + "点能量（不会因为同名技能效果产生能量）";
            if (key == "EnergyQuote+DoubleWhenIsolated")
                return "产生" + skill.Get(EffectKind.EnergyQuote).A + "点能量，当相邻没有其他怪物时，本怪物产生的能量翻倍";
            if (key == "EnergyQuote+Devour")
                return "产生" + skill.Get(EffectKind.EnergyQuote).A + "点能量，消灭随机一只相邻怪物，本技能产生的能量数值永久+" + skill.Get(EffectKind.Devour).A;
            if (key == "EnergyQuote+RepeatQuote")
                return "产生" + skill.Get(EffectKind.EnergyQuote).A + "点能量两次";
            if (key == "CapacityExtraForLeftNeighbor")
                return "左侧相邻怪物，产生能量的技能次数+" + skill.Get(EffectKind.CapacityExtraForLeftNeighbor).A;
            if (key == "ExtraWalksForRightNeighbor")
                return "右侧相邻怪物的所有技能效果都触发" + Times(skill.Get(EffectKind.ExtraWalksForRightNeighbor).A + 1);
            if (key == "EnergyQuote+NextEnergyBonus")
                return "产生" + skill.Get(EffectKind.EnergyQuote).A + "点能量，下一个怪物产生的能量数值+" + skill.Get(EffectKind.NextEnergyBonus).A;
            if (key == "GainCapacity")
                return "获得" + skill.Get(EffectKind.GainCapacity).A + "点产能";
            if (key == "SwapWithLeft")
                return "与左侧相邻怪物交换位置，之后获得不动";
            if (key == "DoubleAdjacentEnergy")
                return "相邻怪物产生的能量翻倍";

            throw new ContentBookException(skill.Name + " 的效果组合没有句子");
        }

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
