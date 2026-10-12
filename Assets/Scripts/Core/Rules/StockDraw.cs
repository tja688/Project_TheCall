using System;
using System.Collections.Generic;

namespace TheCall
{
    internal static class StockDraw
    {
        static readonly string[] Shapes =
        {
            "单", "单", "单", "单", "单", "单", "双", "幅", "幅", "幅",
        };

        public static string[][] OpeningCandidates(SkillCatalog catalog, IDraw draw)
        {
            var pairs = new string[3][];
            for (var i = 0; i < pairs.Length; i++) pairs[i] = OpeningPair(catalog, draw);
            return pairs;
        }

        internal static string[] OpeningPair(SkillCatalog catalog, IDraw draw)
        {
            var first = One(catalog, draw, DrawnPool.HasAmplify, null);
            return new[] { first, One(catalog, draw, DrawnPool.NotAmplifyOnly, first) };
        }

        public static string[] OpeningFillers(SkillCatalog catalog, IDraw draw)
        {
            var names = new string[8];
            for (var i = 0; i < 4; i++)
                names[i] = One(catalog, draw, DrawnPool.Produce, null);

            for (var i = 4; i < 8; i++)
                names[i] = One(catalog, draw, DrawnPool.Support, null);

            return names;
        }

        public static string[] ShopOffer(SkillCatalog catalog, IDraw draw, bool allowsDuplicates = false)
        {
            var shape = Choose(draw, Shapes, "形状");
            if (shape == "双")
                return Pair(catalog, draw, DrawnPool.NotAmplifyOnly, DrawnPool.NotAmplifyOnly, allowsDuplicates);

            if (shape == "幅")
                return Pair(catalog, draw, DrawnPool.AmplifyOnly, DrawnPool.NotAmplifyOnly, allowsDuplicates);

            return new[] { One(catalog, draw, DrawnPool.NoAmplifyBit, null) };
        }

        static string[] Pair(SkillCatalog catalog, IDraw draw, DrawnPool firstPool, DrawnPool secondPool, bool allowsDuplicates)
        {
            var first = One(catalog, draw, firstPool, null);
            var second = One(catalog, draw, secondPool, allowsDuplicates ? null : first);
            return new[] { first, second };
        }

        internal static string One(SkillCatalog catalog, IDraw draw, DrawnPool pool, string taken)
        {
            var words = RarityWords(catalog, pool, taken);
            var word = Choose(draw, words, pool.ToString());
            var names = catalog.Names(pool, RarityOf(word), taken);
            return Choose(draw, names, pool.ToString());
        }

        static string[] RarityWords(SkillCatalog catalog, DrawnPool pool, string taken)
        {
            var words = new List<string>();
            AddWords(words, catalog, pool, taken, Rarity.White, "白");
            AddWords(words, catalog, pool, taken, Rarity.Blue, "蓝");
            AddWords(words, catalog, pool, taken, Rarity.Gold, "金");
            return words.ToArray();
        }

        static void AddWords(
            List<string> words,
            SkillCatalog catalog,
            DrawnPool pool,
            string taken,
            Rarity rarity,
            string word)
        {
            if (catalog.Names(pool, rarity, taken).Count == 0)
                return;

            var weight = ContentGate.Current.Weight(rarity);
            for (var i = 0; i < weight; i++)
                words.Add(word);
        }

        static Rarity RarityOf(string word)
        {
            if (word == "蓝")
                return Rarity.Blue;
            if (word == "金")
                return Rarity.Gold;

            return Rarity.White;
        }

        static List<string> Except(IReadOnlyList<string> names, string taken)
        {
            var pool = new List<string>();
            for (var i = 0; i < names.Count; i++)
            {
                if (names[i] != taken)
                    pool.Add(names[i]);
            }

            return pool;
        }

        static string Choose(IDraw draw, IReadOnlyList<string> names, string pool)
        {
            if (names == null || names.Count == 0)
                throw new InvalidOperationException(pool + " 的抽取名单是空的。");

            return draw.Choose(names);
        }
    }
}
