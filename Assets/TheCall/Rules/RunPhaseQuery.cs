using QFramework;

namespace TheCall
{
    public enum RunPhase
    {
        Opening,
        LevelStart,
        Operation,
        Shop,
        Victory,
        Failed,
    }

    public sealed class RunPhaseQuery : AbstractQuery<RunPhase>
    {
        protected override RunPhase OnDo() => this.GetModel<RunModel>().Phase;
    }
}
