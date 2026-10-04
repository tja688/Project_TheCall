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

        public int NextEnergyBonus(string skillName) => skillName == "分享之手" ? 3 : 0;

        public bool TryLandingResponse(string skillName, string causeSkillName, out int quote)
        {
            if (skillName == "残留提取腺体" && skillName != causeSkillName)
            {
                quote = 1;
                return true;
            }

            quote = 0;
            return false;
        }

        public int StartingQuote(string skillName)
        {
            if (skillName == "吞噬大嘴")
                return 4;
            if (skillName == "双重吐息" || skillName == "分享之手")
                return 2;
            if (TryEnergyQuote(skillName, out var quote))
                return quote;

            return 0;
        }

        public bool SwapsWithLeft(string skillName) => skillName == "换位手";

        public int ExtraWalksForRightNeighbor(string skillName) =>
            skillName == "再回首头" ? 1 : 0;

        public int CapacityExtraForLeftNeighbor(string skillName) =>
            skillName == "时间操控器官" ? 1 : 0;

        public bool ProducesEnergy(string skillName) =>
            TryEnergyQuote(skillName, out _) ||
            TrySideCount(skillName, out _, out _) ||
            TryDevour(skillName, out _, out _) ||
            TryRepeatedQuote(skillName, out _) ||
            NextEnergyBonus(skillName) > 0;

        public bool TryGainCapacity(string skillName, out int layers)
        {
            if (skillName == "太阳能头")
            {
                layers = 1;
                return true;
            }

            layers = 0;
            return false;
        }

        public bool TryDevour(string skillName, out int writeback, out bool permanent)
        {
            if (skillName == "吞噬大嘴")
            {
                writeback = 2;
                permanent = true;
                return true;
            }

            writeback = 0;
            permanent = false;
            return false;
        }

        public bool TryRepeatedQuote(string skillName, out int times)
        {
            if (skillName == "双重吐息")
            {
                times = 2;
                return true;
            }

            times = 0;
            return false;
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
