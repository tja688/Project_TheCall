using QFramework;

namespace TheCall
{
    public sealed class DiscardMonsterCommand : AbstractCommand
    {
        readonly string _monsterId;

        public DiscardMonsterCommand(string monsterId) => _monsterId = monsterId;

        protected override void OnExecute() => this.GetSystem<FlowSystem>().Discard(_monsterId);
    }
}
