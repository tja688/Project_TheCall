using QFramework;

namespace TheCall
{
    public enum OperationArea
    {
        Extraction,
        Breeding,
    }

    public sealed class PlaceMonsterCommand : AbstractCommand
    {
        readonly string _monsterId;
        readonly OperationArea _area;
        readonly int _cell;

        public PlaceMonsterCommand(string monsterId, OperationArea area, int cell)
        {
            _monsterId = monsterId;
            _area = area;
            _cell = cell;
        }

        protected override void OnExecute() => this.GetSystem<FlowSystem>().Place(_monsterId, _area, _cell);
    }
}
