using QFramework;

namespace TheCall
{
    public sealed class ReturnMonsterCommand : AbstractCommand
    {
        readonly string _monsterId;

        public ReturnMonsterCommand(string monsterId) => _monsterId = monsterId;

        protected override void OnExecute() => this.GetSystem<FlowSystem>().ReturnToCage(_monsterId);
    }
}
