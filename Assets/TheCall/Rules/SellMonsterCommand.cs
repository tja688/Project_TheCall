using QFramework;

namespace TheCall
{
    public sealed class SellMonsterCommand : AbstractCommand
    {
        readonly string _monsterId;

        public SellMonsterCommand(string monsterId) => _monsterId = monsterId;

        protected override void OnExecute() => this.GetSystem<ShopSystem>().SellMonster(_monsterId);
    }
}