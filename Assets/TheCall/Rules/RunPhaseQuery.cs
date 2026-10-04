using QFramework;

namespace TheCall
{
    public enum RunPhase
    {
        Opening,
        Operation,
        Shop,
    }

    public sealed class RunPhaseQuery : AbstractQuery<RunPhase>
    {
        protected override RunPhase OnDo() => this.GetModel<RunModel>().Phase;
    }
}
