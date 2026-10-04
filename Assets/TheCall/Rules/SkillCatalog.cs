using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal enum CountedSide
    {
        Left = -1,
        Right = 1,
    }

    internal sealed class SkillCatalog : IUtility
    {
        public IReadOnlyList<string> Names { get; } = new[]
        {
            "能量吐息",
            "左能量体",
            "右能量体",
            "增量小手",
            "增量大手",
            "残留提取腺体",
            "孤独心",
            "吞噬大嘴",
            "双重吐息",
            "时间操控器官",
            "再回首头",
            "分享之手",
            "太阳能头",
            "换位手",
            "鼓励嘴",
        };

        public IReadOnlyList<string> NamesOf(Rarity rarity)
        {
            if (rarity == Rarity.Blue)
                return Blue;
            if (rarity == Rarity.Gold)
                return Gold;

            return White;
        }

        public Rarity RarityOf(string skillName)
        {
            if (Contains(Blue, skillName))
                return Rarity.Blue;
            if (Contains(Gold, skillName))
                return Rarity.Gold;

            return Rarity.White;
        }

        public bool TryEnergyQuote(string skillName, out int quote)
        {
            if (skillName == "能量吐息")
            {
                quote = 5;
                return true;
            }

            if (skillName == "孤独心")
            {
                quote = 4;
                return true;
            }

            quote = 0;
            return false;
        }

        public bool TrySideCount(string skillName, out CountedSide side, out int perMonster)
        {
            if (skillName == "左能量体")
            {
                side = CountedSide.Right;
                perMonster = 2;
                return true;
            }

            if (skillName == "右能量体")
            {
                side = CountedSide.Left;
                perMonster = 2;
                return true;
            }

            side = CountedSide.Right;
            perMonster = 0;
            return false;
        }

        public bool DoublesWhenIsolated(string skillName) => skillName == "孤独心";

        public int AddedToOthers(string skillName)
        {
            if (skillName == "增量小手")
                return 1;
            if (skillName == "增量大手")
                return 2;

            return 0;
        }

        static readonly string[] White =
        {
            "能量吐息",
            "左能量体",
            "右能量体",
            "增量小手",
            "吞噬大嘴",
            "双重吐息",
            "分享之手",
            "太阳能头",
        };

        static readonly string[] Blue =
        {
            "增量大手",
            "残留提取腺体",
            "孤独心",
            "时间操控器官",
            "换位手",
        };

        static readonly string[] Gold = { "再回首头", "鼓励嘴" };

        static bool Contains(string[] names, string skillName)
        {
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i] == skillName)
                    return true;
            }

            return false;
        }
    }
}
