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
