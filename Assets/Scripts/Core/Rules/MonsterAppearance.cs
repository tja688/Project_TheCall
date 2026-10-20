using System;
using System.Collections.Generic;

namespace TheCall
{
    /// <summary>
    /// 出生后固定的结构基因。编号由画像目录解释，不包含 Unity 资源或当日状态。
    /// </summary>
    public readonly struct MonsterAppearance
    {
        public const int RecipeCount = 6;
        public const int PaletteCount = 6;
        public const int HeadCount = 3;
        public const int MouthCount = 6;
        public const int ArmsCount = 4;
        public const int LegsCount = 4;
        public const int CrestCount = 4;
        public const int PatternCount = 4;
        public const int VariationCount = 32;

        public MonsterAppearance(int recipe, int palette)
            : this(recipe, palette,
                Choice(unchecked(recipe * 397 + palette * 173), 0x1907, HeadCount),
                Choice(unchecked(recipe * 397 + palette * 173), 0x31A9, MouthCount),
                Choice(unchecked(recipe * 397 + palette * 173), 0x4D21, ArmsCount),
                Choice(unchecked(recipe * 397 + palette * 173), 0x6C53, LegsCount),
                Choice(unchecked(recipe * 397 + palette * 173), 0x7F19, CrestCount),
                Choice(unchecked(recipe * 397 + palette * 173), 0x926B, PatternCount),
                Choice(unchecked(recipe * 397 + palette * 173), 0xB3D7, VariationCount))
        {
        }

        public MonsterAppearance(int recipe, int palette, int head, int mouth, int arms,
            int legs, int crest, int pattern, int variation)
        {
            Recipe = PositiveModulo(recipe, RecipeCount);
            Palette = PositiveModulo(palette, PaletteCount);
            Head = PositiveModulo(head, HeadCount);
            Mouth = PositiveModulo(mouth, MouthCount);
            Arms = PositiveModulo(arms, ArmsCount);
            Legs = PositiveModulo(legs, LegsCount);
            Crest = PositiveModulo(crest, CrestCount);
            Pattern = PositiveModulo(pattern, PatternCount);
            Variation = PositiveModulo(variation, VariationCount);
        }

        public int Recipe { get; }

        public int Palette { get; }
        public int Head { get; }
        public int Mouth { get; }
        public int Arms { get; }
        public int Legs { get; }
        public int Crest { get; }
        public int Pattern { get; }
        public int Variation { get; }

        public static MonsterAppearance FromSeed(int seed) => new MonsterAppearance(
            Choice(seed, 0x2D39, RecipeCount),
            Choice(seed, 0xAB17, PaletteCount),
            Choice(seed, 0x1907, HeadCount),
            Choice(seed, 0x31A9, MouthCount),
            Choice(seed, 0x4D21, ArmsCount),
            Choice(seed, 0x6C53, LegsCount),
            Choice(seed, 0x7F19, CrestCount),
            Choice(seed, 0x926B, PatternCount),
            Choice(seed, 0xB3D7, VariationCount));

        public static MonsterAppearance Breed(MonsterAppearance first, MonsterAppearance second) =>
            Breed(first, second, 0);

        /// <summary>
        /// 每个结构槽独立选取一名亲本，不消耗玩法随机数，也不凭空加入新基因。
        /// 色板沿用次亲本，性格、心情和技能仍由各自的玩法规则决定。
        /// </summary>
        public static MonsterAppearance Breed(MonsterAppearance first, MonsterAppearance second, int childSeed)
            => Breed(new[] { first, second }, childSeed);

        public static MonsterAppearance Breed(IReadOnlyList<MonsterAppearance> parents, int childSeed)
        {
            if (parents == null) throw new ArgumentNullException(nameof(parents));
            if (parents.Count < 2) throw new ArgumentException("外观遗传至少需要两名亲本。", nameof(parents));
            return new MonsterAppearance(
                parents[Choice(childSeed, 0x2D39, parents.Count)].Recipe,
                parents[1].Palette,
                parents[Choice(childSeed, 0x1907, parents.Count)].Head,
                parents[Choice(childSeed, 0x31A9, parents.Count)].Mouth,
                parents[Choice(childSeed, 0x4D21, parents.Count)].Arms,
                parents[Choice(childSeed, 0x6C53, parents.Count)].Legs,
                parents[Choice(childSeed, 0x7F19, parents.Count)].Crest,
                parents[Choice(childSeed, 0x926B, parents.Count)].Pattern,
                parents[Choice(childSeed, 0xB3D7, parents.Count)].Variation);
        }

        static int PositiveModulo(int value, int modulus)
        {
            var result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        static int Choice(int seed, int salt, int count)
        {
            unchecked
            {
                var value = seed ^ salt;
                value ^= value >> 16;
                value *= (int)0x7feb352d;
                value ^= value >> 15;
                value *= (int)0x846ca68b;
                value ^= value >> 16;
                return PositiveModulo(value, count);
            }
        }
    }
}
