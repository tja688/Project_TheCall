using System;
using System.Collections.Generic;
namespace TheCall
{
    // Stable identifiers: append new entries, never derive identity from Inspector order.
    public enum MonsterTemplate { TwinCrawler = 0, BrainPatchwork = 1, TwinStars = 2, TwinTendrils = 3, StarCluster = 4, BrainRound = 5, ButtonCat = 6, WingSlug = 7, RivetBend = 8, HornBend = 9, FeelersBend = 10, Octopus = 11 }
    public enum MonsterPalette { Moss = 0, Berry = 1, Rose = 2, Cream = 3, Mist = 4, Bone = 5 }
    public enum MonsterHeadAccessory { None = 0, ShortHorn = 1, StripedHorn = 2, Bow = 3 }
    public enum MonsterBackAccessory { None = 0, BatWings = 1, FeatherWings = 2 }
    /// <summary>Birth identity, independent of skills, personality, mood and gameplay RNG.</summary>
    public readonly struct MonsterAppearance
    {
        public const int RecipeCount = 12, PaletteCount = 6, VariationCount = 32;
        public MonsterAppearance(int recipe, int palette) : this(recipe, palette, 0, 0, 0) { }
        public MonsterAppearance(int recipe, int palette, int headAccessory, int backAccessory, int variation)
        {
            Recipe = Mod(recipe, RecipeCount); Palette = Mod(palette, PaletteCount);
            HeadAccessory = Mod(headAccessory, 4); BackAccessory = Mod(backAccessory, 3);
            if (!Compatible(Recipe, HeadAccessory, BackAccessory)) throw new ArgumentException("饰品组合与成品模板不兼容。");
            Variation = Mod(variation, VariationCount);
        }
        public int Recipe { get; }
        public MonsterTemplate Template => (MonsterTemplate)Recipe;
        public int Palette { get; }
        public int HeadAccessory { get; }
        public int BackAccessory { get; }
        public int Variation { get; }
        // Single owner of compatibility; Unity resources must cover this exact set.
        public static bool Compatible(int template, int head, int back)
        {
            if (template < 0 || template >= RecipeCount || head < 0 || head > 3 || back < 0 || back > 2) return false;
            var headAllowed = head == 0 || ((template == 0 || template == 1 || template == 3 || template == 4 || template == 5 || template == 11) ? head == 3 :
                template == 6 || template == 7 ? head != 2 : true);
            var backAllowed = back == 0 || (template != 0 && template != 3 && template != 4 && template != 7 && template != 11);
            return headAllowed && backAllowed && !(head == 2 && back == 2);
        }
        public static MonsterAppearance FromSeed(int seed)
        {
            var template = Choice(seed, 0x2D39, RecipeCount); var pairs = Pairs(template, null);
            var pair = pairs[Choice(seed, 0x7F19, pairs.Count)];
            return new MonsterAppearance(template, Choice(seed, 0xAB17, PaletteCount), pair / 3, pair % 3, Choice(seed, 0xB3D7, VariationCount));
        }
        public static MonsterAppearance Breed(MonsterAppearance first, MonsterAppearance second) => Breed(first, second, 0);
        public static MonsterAppearance Breed(MonsterAppearance first, MonsterAppearance second, int seed) => Breed(new[] { first, second }, seed);
        public static MonsterAppearance Breed(IReadOnlyList<MonsterAppearance> parents, int seed)
        {
            if (parents == null) throw new ArgumentNullException(nameof(parents));
            if (parents.Count < 2) throw new ArgumentException("外观遗传至少需要两名亲本。", nameof(parents));
            var template = parents[Choice(seed, 0x2D39, parents.Count)].Recipe;
            if (Choice(seed, 0x6C53, 100) < 20)
            {
                var other = new List<int>();
                for (var id = 0; id < RecipeCount; id++)
                {
                    var present = false;
                    for (var p = 0; p < parents.Count; p++) present |= parents[p].Recipe == id;
                    if (!present) other.Add(id);
                }
                if (other.Count > 0) template = other[Choice(seed, 0x31A9, other.Count)];
            }
            var pairs = Pairs(template, parents); var pair = pairs.Count == 0 ? 0 : pairs[Choice(seed, 0x7F19, pairs.Count)];
            return new MonsterAppearance(template, parents[1].Palette, pair / 3, pair % 3, Choice(seed, 0xB3D7, VariationCount));
        }
        static List<int> Pairs(int template, IReadOnlyList<MonsterAppearance> parents)
        {
            var result = new List<int>();
            for (var head = 0; head < 4; head++) for (var back = 0; back < 3; back++)
            {
                if (!Compatible(template, head, back)) continue;
                var hasHead = parents == null; var hasBack = parents == null;
                if (parents != null) for (var p = 0; p < parents.Count; p++)
                { hasHead |= parents[p].HeadAccessory == head; hasBack |= parents[p].BackAccessory == back; }
                if (hasHead && hasBack) result.Add(head * 3 + back);
            }
            return result;
        }
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
