using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class TechCatalog : IUtility
    {
        public const int Percent = 10;

        public const int ModifierAmount = 2;

        public static readonly string[] Names =
        {
            "基因实验",
            "槽位扩容",
            "变异学说",
            "大乱炖",
            "科学培育",
        };

        public bool Contains(string name) => Has(Names, name);

        public int SlotCount(IReadOnlyList<string> unlocked) => Has(unlocked, "槽位扩容") ? 2 : 1;

        public int ParentCapacity(IReadOnlyList<string> unlocked) => Has(unlocked, "大乱炖") ? 3 : 2;

        public bool AllowsBreedingSkill(IReadOnlyList<string> unlocked) => Has(unlocked, "基因实验");

        public bool GrantsExtraSkill(IReadOnlyList<string> unlocked) => Has(unlocked, "变异学说");

        public bool GrantsModifier(IReadOnlyList<string> unlocked) => Has(unlocked, "科学培育");

        static bool Has(IReadOnlyList<string> names, string name)
        {
            if (names == null || name == null)
                return false;

            for (var i = 0; i < names.Count; i++)
            {
                if (names[i] == name)
                    return true;
            }

            return false;
        }
    }
}
