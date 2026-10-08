using QFramework;

namespace TheCall
{
    public sealed class RefreshShelfCommand : AbstractCommand
    {
        protected override void OnExecute() => this.GetSystem<ShopSystem>().Refresh();
    }
}
