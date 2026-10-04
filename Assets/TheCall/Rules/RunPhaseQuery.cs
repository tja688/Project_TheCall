using QFramework;

namespace TheCall
{
    public enum RunPhase
    {
        Opening,
        Operation,
        Shop,
        Failed,
    }

    public sealed class RunPhaseQuery : AbstractQuery<RunPhase>
    {
        protected override RunPhase OnDo() => this.GetModel<RunModel>().Phase;
    }
}
