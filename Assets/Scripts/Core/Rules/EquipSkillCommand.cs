using QFramework;

namespace TheCall
{
    public sealed class EquipSkillCommand : AbstractCommand
    {
        readonly string _monsterId;
        readonly int _skillSlotIndex;

        public EquipSkillCommand(string monsterId, int skillSlotIndex)
        {
            _monsterId = monsterId;
            _skillSlotIndex = skillSlotIndex;
        }

        protected override void OnExecute() => this.GetSystem<FlowSystem>().Equip(_monsterId, _skillSlotIndex);
    }
}
