using QFramework;

namespace TheCall
{
    public sealed class InstallGeneCommand : AbstractCommand<bool>
    {
        readonly string _monsterId;
        readonly int _geneSlotIndex;

        public InstallGeneCommand(string monsterId, int geneSlotIndex)
        {
            _monsterId = monsterId;
            _geneSlotIndex = geneSlotIndex;
        }

        protected override bool OnExecute() =>
            this.GetSystem<FlowSystem>().InstallGene(_monsterId, _geneSlotIndex);
    }
}
