using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public sealed class TechCatalog : IUtility
    {
        readonly ContentBook _book;

        public TechCatalog(ContentBook book) => _book = book;

        public bool Contains(string name) => Flag(name) != null;

        public int SlotCount(IReadOnlyList<string> unlocked) =>
            Has(unlocked, TechFlag.ExtraBreedingSlot) ? _book.Economy.ExpandedBreedingSlots : _book.Economy.BaseBreedingSlots;

        public int ParentCapacity(IReadOnlyList<string> unlocked) =>
            Has(unlocked, TechFlag.ExtraParent) ? _book.Economy.ExpandedParents : _book.Economy.BaseParents;

        public bool AllowsBreedingSkill(IReadOnlyList<string> unlocked) => Has(unlocked, TechFlag.BreedingSkill);

        public bool GrantsExtraSkill(IReadOnlyList<string> unlocked) => Has(unlocked, TechFlag.ExtraSkill);

        public bool GrantsModifier(IReadOnlyList<string> unlocked) => Has(unlocked, TechFlag.Modifier);

        bool Has(IReadOnlyList<string> unlocked, TechFlag flag)
        {
            if (unlocked == null)
                return false;

            for (var i = 0; i < unlocked.Count; i++)
            {
                var found = Flag(unlocked[i]);
                if (found == flag)
                    return true;
            }

            return false;
        }

        TechFlag? Flag(string name)
        {
            if (name == null)
                return null;

            for (var i = 0; i < _book.Techs.Count; i++)
            {
                if (_book.Techs[i].Name == name)
                    return _book.Techs[i].Flag;
            }

            return null;
        }
    }
}
