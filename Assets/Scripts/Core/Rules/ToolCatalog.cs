using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public enum ToolEffect
    {
        None = 0,
        DoubleFirstEnergy = 1,
        ExtractionCells = 2,
        DoubleSingleAffix = 3,
        RightmostEnergyBonus = 4,
        GoldOnClear = 5,
        ExtraShopMonster = 6,
        ToolDiscount = 7,
        GoldOnDiscard = 8,
        SettleEnergyQuote = 9,
        PersonalityEnergyBonus = 10,
        UpgradeRandomMood = 11,
        NormalOrHigherMoodBonus = 12,
        PreventMoodDrop = 13,
        ElementalAdjacentBonus = 14,
        ExtraShopTool = 15,
        GoldSkillGlobalBonus = 16,
        SingleSkillBonus = 17,
        AllowDuplicateSkills = 18,
        RetainEnergyPercent = 19,
    }

    public sealed class ToolDefinition
    {
        public ToolDefinition(string name, int price, Rarity rarity)
            : this(name, price, rarity, ToolEffect.None, 0, "")
        {
        }

        public ToolDefinition(string name, int price, Rarity rarity, ToolEffect effect, int extractionCells, string description = "")
        {
            Name = name;
            Price = price;
            Rarity = rarity;
            Effect = effect;
            ExtractionCells = extractionCells;
            Description = description ?? "";
        }

        public string Name { get; }

        public int Price { get; }

        public Rarity Rarity { get; }

        public ToolEffect Effect { get; }

        public int ExtractionCells { get; }

        public string Description { get; }
    }

    public interface IToolCatalog : IUtility
    {
        IReadOnlyList<ToolDefinition> Tools { get; }

        ToolDefinition Find(string name)
        {
            if (Tools == null) return null;
            for (var i = 0; i < Tools.Count; i++)
            {
                if (Tools[i].Name == name)
                    return Tools[i];
            }
            return null;
        }

        int ExtractionCells(IReadOnlyList<string> held) =>
            ToolRules.ExtractionCells(5, Tools, held);
    }

    internal sealed class ToolCatalog : IToolCatalog
    {
        readonly ContentBook _book;

        public ToolCatalog(ContentBook book) => _book = book;

        public IReadOnlyList<ToolDefinition> Tools => _book.Tools;

        public ToolDefinition Find(string name) => _book.FindTool(name);

        public int ExtractionCells(IReadOnlyList<string> held) =>
            ToolRules.ExtractionCells(_book.Economy.BaseExtractionCells, Tools, held);
    }
}
