namespace TheCall
{
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

        static int PositiveModulo(int value, int modulus)
        {
            var result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }
}
