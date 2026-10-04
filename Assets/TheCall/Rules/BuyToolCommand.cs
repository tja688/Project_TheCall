using QFramework;

namespace TheCall
{
    public sealed class BuyToolCommand : AbstractCommand
    {
        readonly string _toolName;

        public BuyToolCommand(string toolName) => _toolName = toolName;

        protected override void OnExecute() => this.GetSystem<ShopSystem>().BuyTool(_toolName);
    }
}
