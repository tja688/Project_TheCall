using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public sealed class ExtractionCellView
    {
        public ExtractionCellView(int index, string monsterId)
        {
            Index = index;
            MonsterId = monsterId;
        }

        public int Index { get; }

        public string MonsterId { get; }
    }

    public sealed class ExtractionSlotsQuery : AbstractQuery<IReadOnlyList<ExtractionCellView>>
    {
        protected override IReadOnlyList<ExtractionCellView> OnDo()
        {
            var cells = this.GetModel<LevelModel>().Extraction;
            var views = new ExtractionCellView[cells.Count];
            for (var i = 0; i < cells.Count; i++)
                views[i] = new ExtractionCellView(i, cells[i]);

            return views;
        }
    }
}
