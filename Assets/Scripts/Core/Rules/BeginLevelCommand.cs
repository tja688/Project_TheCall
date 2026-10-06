using QFramework;

namespace TheCall
{
    public sealed class BeginLevelCommand : AbstractCommand
    {
        protected override void OnExecute() => this.GetSystem<FlowSystem>().BeginLevel();
    }
}
