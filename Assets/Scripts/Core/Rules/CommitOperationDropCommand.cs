using QFramework;

namespace TheCall
{
    public sealed class CommitOperationDropCommand : AbstractCommand<bool>
    {
        readonly OperationDrop _drop;

        public CommitOperationDropCommand(OperationDrop drop) => _drop = drop;

        protected override bool OnExecute() => this.GetSystem<FlowSystem>().Commit(_drop);
    }
}
