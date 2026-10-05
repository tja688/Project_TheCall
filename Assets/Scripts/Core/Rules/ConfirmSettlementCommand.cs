using QFramework;

namespace TheCall
{
    public sealed class ConfirmSettlementCommand : AbstractCommand
    {
        protected override void OnExecute() => this.GetSystem<FlowSystem>().Confirm();
    }
}
