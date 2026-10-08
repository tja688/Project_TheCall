using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class ShopSystem : AbstractSystem
    {
        public Shelf View()
        {
            var run = this.GetModel<RunModel>();
            if (run.Phase != RunPhase.Shop)
                return Empty();

            return new Shelf(MonsterViews(run), ToolViews(run), this.GetModel<LevelModel>().RefreshPrice);
        }

        public void Refresh()
        {
            var run = this.GetModel<RunModel>();
            if (run.Phase != RunPhase.Shop)
                return;

            var level = this.GetModel<LevelModel>();
            if (!run.TrySpend(level.RefreshPrice))
                return;

            DiscardUnsold(run);
            StockMonsters(run);
            StockTools(run);
            level.NotePaidRefresh();
        }

        public void Open()
        {
            var run = this.GetModel<RunModel>();
            if (!run.NeedsShopStock)
                return;

            run.MarkShopStocked();
            DiscardUnsold(run);
            StockMonsters(run);
            StockTools(run);
        }

        public void BuyMonster(string monsterId)
        {
            var run = this.GetModel<RunModel>();
            if (run.Phase != RunPhase.Shop || !Holds(run.ShelfMonsterIds, monsterId))
                return;

            var monster = run.Find(monsterId);
            if (!run.TrySpend(MonsterPrice(monster)))
                return;

            run.TryRemoveShelfMonster(monsterId);
            run.AddToCage(monster);
        }

        public void BuyTool(string toolName)
        {
            var run = this.GetModel<RunModel>();
            if (run.Phase != RunPhase.Shop || !Holds(run.ShelfToolNames, toolName))
                return;
            if (!TryTool(toolName, out var tool) || !run.TrySpend(tool.Price))
                return;

            run.TryRemoveShelfTool(toolName);
            run.AddTool(tool.Name);
        }

        public void SellMonster(string monsterId)
        {
            var run = this.GetModel<RunModel>();
            if (run.Phase != RunPhase.Shop)
                return;
            if (this.GetModel<LevelModel>().IsLockedParent(monsterId))
                return;
            if (!run.TryRemoveFromCage(monsterId, out var monster))
                return;

            run.AddGold(Proceeds(monster));
            run.TryDestroy(monsterId);
        }

        public void Close()
        {
            var run = this.GetModel<RunModel>();
            if (run.Phase != RunPhase.Shop)
                return;

            DiscardUnsold(run);
            run.MarkShopStocked();
        }

        protected override void OnInit()
        {
        }

        void StockMonsters(RunModel run)
        {
            var draw = this.GetUtility<IDraw>();
            var catalog = this.GetUtility<SkillCatalog>();
            for (var i = 0; i < 4; i++)
                run.AddShelfMonster(run.CreateMonster(StockDraw.ShopOffer(catalog, draw)).Id);
        }

        void StockTools(RunModel run)
        {
            var remaining = UnheldTools(run);
            var draw = this.GetUtility<IDraw>();
            while (run.ShelfToolNames.Count < 2 && remaining.Count > 0)
            {
                var weighted = new List<string>();
                for (var i = 0; i < remaining.Count; i++)
                {
                    var copies = Weight(remaining[i].Rarity);
                    for (var copy = 0; copy < copies; copy++)
                        weighted.Add(remaining[i].Name);
                }

                var name = draw.Choose(weighted);
                run.AddShelfTool(name);
                remaining.RemoveAll(tool => tool.Name == name);
            }
        }

        List<ToolDefinition> UnheldTools(RunModel run)
        {
            var held = new HashSet<string>();
            for (var i = 0; i < run.Tools.Count; i++)
                held.Add(run.Tools[i]);

            var remaining = new List<ToolDefinition>();
            var tools = this.GetUtility<IToolCatalog>().Tools;
            for (var i = 0; i < tools.Count; i++)
            {
                if (!held.Contains(tools[i].Name) && !Holds(run.ShelfToolNames, tools[i].Name))
                    remaining.Add(tools[i]);
            }

            return remaining;
        }

        void DiscardUnsold(RunModel run)
        {
            var ids = run.ShelfMonsterIds;
            for (var i = 0; i < ids.Count; i++)
                run.TryDestroy(ids[i]);

            run.ClearShelf();
        }

        ShelfMonster[] MonsterViews(RunModel run)
        {
            var ids = run.ShelfMonsterIds;
            var monsters = new ShelfMonster[ids.Count];
            for (var i = 0; i < ids.Count; i++)
            {
                var monster = run.Find(ids[i]);
                var names = new string[monster.Skills.Count];
                for (var skill = 0; skill < names.Length; skill++)
                    names[skill] = monster.Skills[skill].Name;

                monsters[i] = new ShelfMonster(monster.Id, names, MonsterPrice(monster), monster.Appearance);
            }

            return monsters;
        }

        ShelfTool[] ToolViews(RunModel run)
        {
            var names = run.ShelfToolNames;
            var tools = new ShelfTool[names.Count];
            for (var i = 0; i < names.Count; i++)
            {
                TryTool(names[i], out var tool);
                tools[i] = new ShelfTool(tool.Name, tool.Price, tool.Rarity);
            }

            return tools;
        }

        int MonsterPrice(Monster monster) => Sum(monster, PriceOf);

        int Proceeds(Monster monster) => Sum(monster, ProceedsOf);

        int Sum(Monster monster, System.Func<Rarity, int> price)
        {
            var catalog = this.GetUtility<SkillCatalog>();
            var total = 0;
            for (var i = 0; i < monster.Skills.Count; i++)
                total += price(catalog.RarityOf(monster.Skills[i].Name));

            return total;
        }

        bool TryTool(string name, out ToolDefinition tool)
        {
            var tools = this.GetUtility<IToolCatalog>().Tools;
            for (var i = 0; i < tools.Count; i++)
            {
                if (tools[i].Name != name)
                    continue;

                tool = tools[i];
                return true;
            }

            tool = null;
            return false;
        }

        static bool Holds(IReadOnlyList<string> names, string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            for (var i = 0; i < names.Count; i++)
            {
                if (names[i] == name)
                    return true;
            }

            return false;
        }

        static int Weight(Rarity rarity) => ContentGate.Current.Weight(rarity);

        static int PriceOf(Rarity rarity) => ContentGate.Current.Price(rarity);

        static int ProceedsOf(Rarity rarity) => PriceOf(rarity) / 2;

        static Shelf Empty() => new Shelf(System.Array.Empty<ShelfMonster>(), System.Array.Empty<ShelfTool>(), 0);
    }
}
