using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    // Text formatting consumes the same structured evaluator as settlement and never recomputes energy.
    internal static class SkillReadout
    {
        public static SentenceSpan[] Render(SkillDef skill, int quote, string monsterId, RunModel run,
            LevelModel level, SkillCatalog catalog, IReadOnlyList<ToolDefinition> tools,
            SettlementSight sight = null, string skillInstanceId = null)
        {
            var architecture = ((IBelongToArchitecture)run).GetArchitecture();
            var evaluated = architecture.GetSystem<SettlementSystem>().Preview(monsterId, skill.Name, quote, sight, skillInstanceId);
            var pieces = SkillSentences.Pieces(skill);
            var spans = new List<SentenceSpan>(pieces.Length + 2);
            foreach (var piece in pieces)
            {
                if (piece.Kind == FigureKind.None) { spans.Add(new SentenceSpan(piece.Text, false)); continue; }
                var shown = piece.Kind == FigureKind.PerHead ? evaluated.DisplayUnitEnergy : evaluated.Energy;
                var nominal = piece.Kind == FigureKind.Population || piece.Kind == FigureKind.SkillMultiple ? evaluated.NominalQuote : piece.Energy;
                if (piece.Kind == FigureKind.SkillMultiple) spans.Add(new SentenceSpan("（", false));
                spans.Add(new SentenceSpan(shown.ToString(), shown != nominal));
                if (piece.Kind == FigureKind.SkillMultiple) spans.Add(new SentenceSpan("）", false));
            }
            return spans.ToArray();
        }
    }
}
