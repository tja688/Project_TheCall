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
    }

    public sealed class ToolDefinition
    {
        public ToolDefinition(string name, int price, Rarity rarity)
            : this(name, price, rarity, ToolEffect.None, 0)
        {
        }

        public ToolDefinition(string name, int price, Rarity rarity, ToolEffect effect, int extractionCells)
        {
            Name = name;
            Price = price;
            Rarity = rarity;
            Effect = effect;
            ExtractionCells = extractionCells;
        }

        public string Name { get; }

        public int Price { get; }

        public Rarity Rarity { get; }

        public ToolEffect Effect { get; }

        public int ExtractionCells { get; }
    }

    public interface IToolCatalog : IUtility
    {
        IReadOnlyList<ToolDefinition> Tools { get; }

        int ExtractionCells(IReadOnlyList<string> held) =>
            ToolRules.ExtractionCells(5, Tools, held);
    }

    internal sealed class ToolCatalog : IToolCatalog
    {
        readonly ContentBook _book;

        public ToolCatalog(ContentBook book) => _book = book;

        public IReadOnlyList<ToolDefinition> Tools => _book.Tools;

        public int ExtractionCells(IReadOnlyList<string> held) =>
            ToolRules.ExtractionCells(_book.Economy.BaseExtractionCells, Tools, held);
    }
}
