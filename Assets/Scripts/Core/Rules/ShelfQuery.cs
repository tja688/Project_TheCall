using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public sealed class ShelfMonster
    {
        public ShelfMonster(string id, IReadOnlyList<string> skillNames, int price)
        {
            Id = id;
            SkillNames = skillNames;
            Price = price;
        }

        public string Id { get; }

        public IReadOnlyList<string> SkillNames { get; }

        public int Price { get; }
    }

    public sealed class ShelfTool
    {
        public ShelfTool(string name, int price, Rarity rarity)
        {
            Name = name;
            Price = price;
            Rarity = rarity;
        }

        public string Name { get; }

        public int Price { get; }

        public Rarity Rarity { get; }
    }

    public sealed class Shelf
    {
        public Shelf(IReadOnlyList<ShelfMonster> monsters, IReadOnlyList<ShelfTool> tools)
        {
            Monsters = monsters;
            Tools = tools;
        }

        public IReadOnlyList<ShelfMonster> Monsters { get; }

        public IReadOnlyList<ShelfTool> Tools { get; }
    }

    public sealed class ShelfQuery : AbstractQuery<Shelf>
    {
        protected override Shelf OnDo() => this.GetSystem<ShopSystem>().View();
    }
}
