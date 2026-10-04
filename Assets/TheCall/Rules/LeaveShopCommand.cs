using QFramework;

namespace TheCall
{
    public sealed class LeaveShopCommand : AbstractCommand
    {
        protected override void OnExecute() => this.GetSystem<FlowSystem>().LeaveShop();
    }
}
