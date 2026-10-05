using QFramework;

namespace TheCall
{
    public sealed class PlaceBreedingSkillCommand : AbstractCommand
    {
        readonly int _slot;
        readonly int _skillSlotIndex;

        public PlaceBreedingSkillCommand(int slot, int skillSlotIndex)
        {
            _slot = slot;
            _skillSlotIndex = skillSlotIndex;
        }

        protected override void OnExecute() => this.GetSystem<FlowSystem>().PlaceBreedingSkill(_slot, _skillSlotIndex);
    }
}
