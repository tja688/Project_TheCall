using System;

namespace TheCall
{
    /// <summary>
    /// 怪物外观只保留配方和色板，供剪影选图。
    /// 旧拼接的部件 ID、模板和遗传已删除。新怪物系统替换本类型。
    /// 见 Assets/Notes/新怪物系统交接.md。
    /// </summary>
    public readonly struct MonsterAppearance
    {
        public const int RecipeCount = 6;
        public const int PaletteCount = 6;

        public MonsterAppearance(int recipe, int palette)
        {
            Recipe = PositiveModulo(recipe, RecipeCount);
            Palette = PositiveModulo(palette, PaletteCount);
        }

        public int Recipe { get; }

        public int Palette { get; }

        public static MonsterAppearance FromSeed(int seed)
        {
            var mixed = seed * 397 + 173;
            return new MonsterAppearance(seed, mixed);
        }

        public static MonsterAppearance Breed(MonsterAppearance first, MonsterAppearance second) =>
            Breed(first, second, 0);

        /// <summary>
        /// 旧实现按部件遗传头、眼、嘴、手脚和尾巴。那些字段已删除。
        /// 子代配方仍用原来的身体抽选，色板仍跟次代亲本，剪影因此保持稳定。
        /// 新怪物系统替换本方法。first 不再参与。
        /// </summary>
        public static MonsterAppearance Breed(MonsterAppearance first, MonsterAppearance second, int childSeed)
        {
            _ = first;
            var recipe = Choice(childSeed, 0x2D39, RecipeCount);
            return new MonsterAppearance(recipe, second.Palette);
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
