using QFramework;

namespace TheCall
{
    public sealed class LevelEnergyQuery : AbstractQuery<int>
    {
        protected override int OnDo() => this.GetModel<LevelModel>().Energy;
    }
}
