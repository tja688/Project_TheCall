using System;
using System.Collections.Generic;
namespace TheCall
{
    // Stable source drawing IDs, independent of catalog/Inspector order.
    public enum MonsterTemplate { TwinCrawler = 0, BrainPatchwork = 1, TwinStars = 2, TwinTendrils = 3, StarCluster = 4, BrainRound = 5, ButtonCat = 6, WingSlug = 7, RivetBend = 8, HornBend = 9, FeelersBend = 10, Octopus = 11 }
    public enum MonsterPalette { Moss = 0, Berry = 1, Rose = 2, Cream = 3, Mist = 4, Bone = 5 }
    /// <summary>A complete drawing and gradient colorway, fixed at birth. No part genes.</summary>
    public readonly struct MonsterAppearance
    {
        public const int RecipeCount = 12, PaletteCount = 6, VariationCount = 32;
        static readonly int[] Basic = { 5, 6, 8, 9, 10, 11 };
        static readonly int[] Cultivated = { 0, 1, 2, 3, 4, 7 };
        public MonsterAppearance(int recipe, int palette, int variation = 0)
        { Recipe = Mod(recipe, RecipeCount); Palette = Mod(palette, PaletteCount); Variation = Mod(variation, VariationCount); }
        public int Recipe { get; }
        public MonsterTemplate Template => (MonsterTemplate)Recipe;
        public int Palette { get; }
        public int Variation { get; }
        public bool IsCultivated => Array.IndexOf(Cultivated, Recipe) >= 0;
        public static MonsterAppearance FromSeed(int seed) => Select(Basic, seed);
        public static MonsterAppearance Breed(MonsterAppearance first, MonsterAppearance second) => Breed(first, second, 0);
        public static MonsterAppearance Breed(MonsterAppearance first, MonsterAppearance second, int seed) => Breed(new[] { first, second }, seed);
        public static MonsterAppearance Breed(IReadOnlyList<MonsterAppearance> parents, int seed)
        {
            if (parents == null) throw new ArgumentNullException(nameof(parents));
            if (parents.Count < 2) throw new ArgumentException("培育至少需要两名亲本。", nameof(parents));
            // Gameplay owns inheritance. Art uses a separate seeded draw and consumes no gameplay RNG.
            return Select(Cultivated, seed);
        }
        static MonsterAppearance Select(int[] pool, int seed) => new MonsterAppearance(
            pool[Choice(seed, 0x2D39, pool.Length)], Choice(seed, 0xAB17, PaletteCount), Choice(seed, 0xB3D7, VariationCount));
        static int Mod(int value, int count) { var n = value % count; return n < 0 ? n + count : n; }
        static int Choice(int seed, int salt, int count)
        {
            unchecked
            {
                var value = seed ^ salt; value ^= value >> 16; value *= (int)0x7feb352d;
                value ^= value >> 15; value *= (int)0x846ca68b; value ^= value >> 16;
                return Mod(value, count);
            }
        }
    }
}
