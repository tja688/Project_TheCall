using System.Collections.Generic;

namespace TheCall
{
    internal static class ToolRules
    {
        public static int ExtractionCells(int baseCells, IReadOnlyList<ToolDefinition> tools, IReadOnlyList<string> held)
        {
            var tool = Find(tools, held, ToolEffect.ExtractionCells);
            return tool == null ? baseCells : tool.ExtractionCells;
        }

        public static bool DoublesFirstEnergy(IReadOnlyList<ToolDefinition> tools, IReadOnlyList<string> held) =>
            Find(tools, held, ToolEffect.DoubleFirstEnergy) != null;

        public static bool DoublesSingleAffix(IReadOnlyList<ToolDefinition> tools, IReadOnlyList<string> held) =>
            Find(tools, held, ToolEffect.DoubleSingleAffix) != null;

        static ToolDefinition Find(IReadOnlyList<ToolDefinition> tools, IReadOnlyList<string> held, ToolEffect effect)
        {
            if (tools == null || held == null)
                return null;

            for (var i = 0; i < held.Count; i++)
            {
                for (var tool = 0; tool < tools.Count; tool++)
                {
                    if (tools[tool].Name == held[i] && tools[tool].Effect == effect)
                        return tools[tool];
                }
            }

            return null;
        }
    }
}
