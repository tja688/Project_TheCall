using QFramework;

namespace TheCall
{
    public sealed class UnlockTechCommand : AbstractCommand
    {
        readonly string _name;

        public UnlockTechCommand(string name) => _name = name;

        protected override void OnExecute() => this.GetSystem<FlowSystem>().Unlock(_name);
    }
}
