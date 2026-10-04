using QFramework;

namespace TheCall
{
    public sealed class LevelShortfallQuery : AbstractQuery<int>
    {
        protected override int OnDo() => this.GetModel<LevelModel>().Shortfall;
    }
}
