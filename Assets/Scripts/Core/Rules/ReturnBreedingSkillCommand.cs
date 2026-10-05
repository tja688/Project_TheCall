using QFramework;

namespace TheCall
{
    public sealed class ReturnBreedingSkillCommand : AbstractCommand
    {
        readonly int _slot;

        public ReturnBreedingSkillCommand(int slot) => _slot = slot;

        protected override void OnExecute() => this.GetSystem<FlowSystem>().ReturnBreedingSkill(_slot);
    }
}
