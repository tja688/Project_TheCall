using QFramework;

namespace TheCall
{
    public sealed class KeepOpeningMonsterCommand : AbstractCommand
    {
        readonly string _monsterId;

        public KeepOpeningMonsterCommand(string monsterId) => _monsterId = monsterId;

        protected override void OnExecute() => this.GetSystem<FlowSystem>().Keep(_monsterId);
    }
}
