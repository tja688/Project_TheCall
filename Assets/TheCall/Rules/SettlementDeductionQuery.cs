using QFramework;

namespace TheCall
{
    public sealed class SettlementDeductionQuery : AbstractQuery<int>
    {
        protected override int OnDo() => this.GetSystem<SettlementSystem>().Deducted;
    }
}
