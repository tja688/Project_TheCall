using QFramework;

namespace TheCall
{
    public sealed class BuyMonsterCommand : AbstractCommand
    {
        readonly string _monsterId;

        public BuyMonsterCommand(string monsterId) => _monsterId = monsterId;

        protected override void OnExecute() => this.GetSystem<ShopSystem>().BuyMonster(_monsterId);
    }
}
