using QFramework;

namespace TheCall
{
    public enum RunPhase
    {
        Opening,
        Operation,
    }

    public sealed class RunPhaseQuery : AbstractQuery<RunPhase>
    {
        protected override RunPhase OnDo() => this.GetModel<RunModel>().Phase;
    }
}
