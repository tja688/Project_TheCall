using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public sealed class ToolDefinition
    {
        public ToolDefinition(string name, int price, Rarity rarity)
        {
            Name = name;
            Price = price;
            Rarity = rarity;
        }

        public string Name { get; }

        public int Price { get; }

        public Rarity Rarity { get; }
    }

    public interface IToolCatalog : IUtility
    {
        IReadOnlyList<ToolDefinition> Tools { get; }

        int ExtractionCells(IReadOnlyList<string> held) =>
            Holds(held, "上级员工证") ? 6 : 5;

        bool DoublesFirstEnergyExecution(IReadOnlyList<string> held) =>
            Holds(held, "急急装置");

        bool DoublesSingleAffixEnergy(IReadOnlyList<string> held) =>
            Holds(held, "独孤装置");

        private static bool Holds(IReadOnlyList<string> names, string name)
        {
            if (names == null || name == null)
                return false;

            for (var i = 0; i < names.Count; i++)
            {
                if (names[i] == name)
                    return true;
            }

            return false;
        }
    }

    internal sealed class ToolCatalog : IToolCatalog
    {
        public IReadOnlyList<ToolDefinition> Tools { get; } = new[]
        {
            new ToolDefinition("急急装置", 30, Rarity.White),
            new ToolDefinition("上级员工证", 40, Rarity.Blue),
            new ToolDefinition("独孤装置", 50, Rarity.Gold),
        };
    }
}
