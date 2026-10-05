using QFramework;

namespace TheCall
{
    public sealed class LeaveShopCommand : AbstractCommand
    {
        protected override void OnExecute()
        {
            this.GetSystem<ShopSystem>().Close();
            this.GetSystem<FlowSystem>().LeaveShop();
        }
    }
}
