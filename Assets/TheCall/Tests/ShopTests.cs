using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class ShopTests : RulesFixture
    {
        static readonly string[] OpeningDraws =
        {
            "能量吐息",
            "左能量体",
            "右能量体",
            "能量吐息",
            "增量小手",
            "残留提取腺体",
            "孤独心",
            "吞噬大嘴",
            "双重吐息",
        };

        static readonly string[] FourSingles =
        {
            "1", "白", "能量吐息",
            "1", "蓝", "增量大手",
            "1", "金", "再回首头",
            "1", "白", "左能量体",
        };

        [Test]
        public void 商店开始时按注入结果摆出四只怪物并按稀有度标价()
        {
            ReachShop(And(FourSingles, "急急装置", "上级员工证"));

            var shelf = App.SendQuery(new ShelfQuery());

            Assert.That(
                shelf.Monsters.Select(monster => monster.SkillNames.Single()).ToArray(),
                Is.EqualTo(new[] { "能量吐息", "增量大手", "再回首头", "左能量体" }));
            Assert.That(shelf.Monsters.Select(monster => monster.Price).ToArray(), Is.EqualTo(new[] { 10, 20, 30, 10 }));
            var cageIds = App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id).ToArray();
            Assert.That(cageIds, Does.Not.Contain(shelf.Monsters[0].Id));
            Assert.That(cageIds, Does.Not.Contain(shelf.Monsters[1].Id));
            Assert.That(cageIds, Does.Not.Contain(shelf.Monsters[2].Id));
            Assert.That(cageIds, Does.Not.Contain(shelf.Monsters[3].Id));
        }

        [Test]
        public void 双技能商品的售价是两个稀有度相加()
        {
            ReachShop(
                "2", "白", "能量吐息", "蓝", "增量大手",
                "1", "金", "再回首头",
                "1", "白", "左能量体",
                "1", "白", "右能量体",
                "急急装置",
                "上级员工证");

            var shelf = App.SendQuery(new ShelfQuery());
            Assert.That(shelf.Monsters[0].SkillNames, Is.EqualTo(new[] { "能量吐息", "增量大手" }));
            Assert.That(shelf.Monsters[0].Price, Is.EqualTo(30));
            Assert.That(shelf.Monsters[1].Price, Is.EqualTo(30));
        }

        [Test]
        public void 同一只商品怪物不会抽到两个同名技能()
        {
            ReachShop(
                "2", "金", "再回首头", "金", "鼓励嘴",
                "1", "白", "能量吐息",
                "1", "白", "左能量体",
                "1", "白", "右能量体",
                "急急装置",
                "上级员工证");

            var skills = App.SendQuery(new ShelfQuery()).Monsters[0].SkillNames;
            Assert.That(skills, Is.EqualTo(new[] { "再回首头", "鼓励嘴" }));
            Assert.That(skills.Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void 商店按九成一只一成两只以及七成白两成蓝一成金抽取()
        {
            var draw = new RecordingDraw(Draws(
                "1", "白", "能量吐息",
                "1", "白", "左能量体",
                "1", "白", "右能量体",
                "1", "白", "增量小手",
                "急急装置",
                "上级员工证"));
            UseRules(draw, new ScriptedLevelCatalog(5));
            KeepAndPay();
            App.SendQuery(new ShelfQuery());

            var counts = draw.Lists.Where(list => list.All(item => item == "1" || item == "2")).ToArray();
            var rarities = draw.Lists.Where(list => list.All(item => item == "白" || item == "蓝" || item == "金")).ToArray();

            Assert.That(counts.Length, Is.EqualTo(4));
            Assert.That(counts[0], Is.EqualTo(new[] { "1", "1", "1", "1", "1", "1", "1", "1", "1", "2" }));
            Assert.That(rarities.Length, Is.EqualTo(4));
            Assert.That(rarities[0], Is.EqualTo(new[] { "白", "白", "白", "白", "白", "白", "白", "蓝", "蓝", "金" }));
        }

        [Test]
        public void 货架最多两件不同名工具并按稀有度权重和标价抽取()
        {
            var draw = new RecordingDraw(Draws(And(FourSingles, "急急装置", "上级员工证")));
            UseRules(draw, new ScriptedLevelCatalog(5));
            KeepAndPay();

            var shelf = App.SendQuery(new ShelfQuery());
            var toolLists = draw.Lists.Where(list => list.Contains("急急装置") || list.Contains("上级员工证")).ToArray();

            Assert.That(toolLists[0], Is.EqualTo(new[]
            {
                "急急装置", "急急装置", "急急装置", "急急装置", "急急装置", "急急装置", "急急装置",
                "上级员工证", "上级员工证",
                "独孤装置",
            }));
            Assert.That(toolLists[1], Is.EqualTo(new[] { "上级员工证", "上级员工证", "独孤装置" }));
            Assert.That(shelf.Tools.Select(tool => tool.Name).ToArray(), Is.EqualTo(new[] { "急急装置", "上级员工证" }));
            Assert.That(shelf.Tools.Select(tool => tool.Price).ToArray(), Is.EqualTo(new[] { 30, 40 }));
            Assert.That(shelf.Tools.Select(tool => tool.Name), Does.Not.Contain("独孤装置"));
        }

        [Test]
        public void 工具种类不足两件时货架少开位数()
        {
            ReachShop(new FixedTools(new ToolDefinition("上级员工证", 40, Rarity.Blue)), And(FourSingles, "上级员工证"));

            var tools = App.SendQuery(new ShelfQuery()).Tools;
            Assert.That(tools.Select(tool => tool.Name).ToArray(), Is.EqualTo(new[] { "上级员工证" }));
            Assert.That(tools[0].Price, Is.EqualTo(40));
        }

        [Test]
        public void 没有可卖的工具时货架不放工具()
        {
            ReachShop(new FixedTools(), FourSingles);

            Assert.That(App.SendQuery(new ShelfQuery()).Tools, Is.Empty);
        }

        [Test]
        public void 金币不足时购买不发生货物仍留在货架()
        {
            ReachShop(
                "2", "蓝", "增量大手", "金", "再回首头",
                "1", "白", "能量吐息",
                "1", "白", "左能量体",
                "1", "白", "右能量体",
                "独孤装置",
                "急急装置");
            var shelf = App.SendQuery(new ShelfQuery());
            var poor = shelf.Monsters[0];
            var before = App.SendQuery(new RunLedgerQuery()).Gold;

            App.SendCommand(new BuyToolCommand("独孤装置"));
            App.SendCommand(new BuyMonsterCommand(poor.Id));

            var after = App.SendQuery(new ShelfQuery());
            Assert.That(poor.Price, Is.EqualTo(50));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(before));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Tools, Is.Empty);
            Assert.That(after.Tools.Select(tool => tool.Name), Does.Contain("独孤装置"));
            Assert.That(after.Monsters.Select(monster => monster.Id), Does.Contain(poor.Id));
            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Not.Contain(poor.Id));
        }

        [Test]
        public void 买得起的怪物进入怪物笼并从货架拿走而且不补新货()
        {
            ReachShop(And(FourSingles, "急急装置", "上级员工证"));
            var shelf = App.SendQuery(new ShelfQuery());
            var bought = shelf.Monsters[0];
            var kept = shelf.Monsters.Skip(1).Select(monster => monster.Id).ToArray();
            var before = App.SendQuery(new RunLedgerQuery()).Gold;

            App.SendCommand(new BuyMonsterCommand(bought.Id));

            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(before - 10));
            var cage = App.SendQuery(new MonsterCageQuery()).Single(monster => monster.Id == bought.Id);
            Assert.That(cage.SkillNames, Is.EqualTo(new[] { "能量吐息" }));
            var again = App.SendQuery(new ShelfQuery());
            Assert.That(again.Monsters.Select(monster => monster.Id).ToArray(), Is.EqualTo(kept));
            Assert.That(again.Monsters.Select(monster => monster.Id), Does.Not.Contain(bought.Id));
        }

        [Test]
        public void 新买的工具按购买顺序排在工具次序末尾()
        {
            ReachShop(
                new FixedTools(
                    new ToolDefinition("急急装置", 10, Rarity.White),
                    new ToolDefinition("上级员工证", 10, Rarity.Blue),
                    new ToolDefinition("独孤装置", 10, Rarity.Gold)),
                And(FourSingles, "上级员工证", "急急装置"));
            var before = App.SendQuery(new RunLedgerQuery()).Gold;

            App.SendCommand(new BuyToolCommand("上级员工证"));
            App.SendCommand(new BuyToolCommand("急急装置"));

            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(before - 20));
            Assert.That(
                App.SendQuery(new RunLedgerQuery()).Tools,
                Is.EqualTo(new[] { "上级员工证", "急急装置" }));
            Assert.That(App.SendQuery(new ShelfQuery()).Tools, Is.Empty);
        }

        [Test]
        public void 本阶段不能更换货物离开时没买的消失()
        {
            ReachShop(And(FourSingles, "急急装置", "上级员工证"));
            var first = App.SendQuery(new ShelfQuery());
            var second = App.SendQuery(new ShelfQuery());
            var bought = first.Monsters[0];
            var unsold = first.Monsters.Skip(1).Select(monster => monster.Id).ToArray();
            App.SendCommand(new BuyMonsterCommand(bought.Id));
            App.SendCommand(new BuyToolCommand("急急装置"));

            Assert.That(second.Monsters.Select(monster => monster.Id).ToArray(), Is.EqualTo(first.Monsters.Select(monster => monster.Id).ToArray()));

            App.SendCommand(new LeaveShopCommand());

            var shelf = App.SendQuery(new ShelfQuery());
            Assert.That(shelf.Monsters, Is.Empty);
            Assert.That(shelf.Tools, Is.Empty);
            var cageIds = App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id).ToArray();
            Assert.That(cageIds, Does.Contain(bought.Id));
            Assert.That(cageIds, Does.Not.Contain(unsold[0]));
            Assert.That(cageIds, Does.Not.Contain(unsold[1]));
            Assert.That(cageIds, Does.Not.Contain(unsold[2]));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Tools, Is.EqualTo(new[] { "急急装置" }));
        }

        [Test]
        public void 出售怪物笼里的怪物按稀有度一半得金币()
        {
            ReachShop(And(FourSingles, "急急装置", "上级员工证"));
            var gland = App.SendQuery(new MonsterCageQuery()).Single(monster => monster.SkillNames.Single() == "残留提取腺体");
            var hand = App.SendQuery(new MonsterCageQuery()).Single(monster => monster.SkillNames.Single() == "增量小手");
            var before = App.SendQuery(new RunLedgerQuery()).Gold;

            App.SendCommand(new SellMonsterCommand(gland.Id));
            App.SendCommand(new SellMonsterCommand(hand.Id));

            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(before + 15));
            var cageIds = App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id).ToArray();
            Assert.That(cageIds, Does.Not.Contain(gland.Id));
            Assert.That(cageIds, Does.Not.Contain(hand.Id));
        }

        [Test]
        public void 工具货架怪物和提取槽里的怪物都不能出售()
        {
            ReachShop(And(FourSingles, "急急装置", "上级员工证"));
            var shelf = App.SendQuery(new ShelfQuery());
            var breath = App.SendQuery(new ExtractionSlotsQuery())[0].MonsterId;
            App.SendCommand(new BuyToolCommand("急急装置"));
            var goldAfterBuy = App.SendQuery(new RunLedgerQuery()).Gold;

            App.SendCommand(new SellMonsterCommand("急急装置"));
            App.SendCommand(new SellMonsterCommand(shelf.Monsters[0].Id));
            App.SendCommand(new SellMonsterCommand(breath));

            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(goldAfterBuy));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Tools, Is.EqualTo(new[] { "急急装置" }));
            Assert.That(App.SendQuery(new ShelfQuery()).Monsters.Select(monster => monster.Id), Does.Contain(shelf.Monsters[0].Id));
            Assert.That(App.SendQuery(new ExtractionSlotsQuery()).Select(cell => cell.MonsterId), Does.Contain(breath));
        }

        [Test]
        public void 商店之外不能买卖()
        {
            UseLevel(new ScriptedLevelCatalog(5), Draws(And(FourSingles, "急急装置", "上级员工证")));
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var hand = App.SendQuery(new MonsterCageQuery()).Single(monster => monster.SkillNames.Single() == "增量小手");

            App.SendCommand(new SellMonsterCommand(hand.Id));
            App.SendCommand(new BuyToolCommand("急急装置"));

            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(0));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Tools, Is.Empty);
            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Contain(hand.Id));
            Assert.That(App.SendQuery(new ShelfQuery()).Monsters, Is.Empty);
        }

        void ReachShop(params string[] shopDraws)
        {
            UseLevel(new ScriptedLevelCatalog(5), Draws(shopDraws));
            KeepAndPay();
        }

        void ReachShop(IToolCatalog tools, params string[] shopDraws)
        {
            UseRules(new ScriptedDraw(Draws(shopDraws)), new ScriptedLevelCatalog(5), tools);
            KeepAndPay();
        }

        static string[] And(string[] head, params string[] tail)
        {
            var names = new string[head.Length + tail.Length];
            head.CopyTo(names, 0);
            tail.CopyTo(names, head.Length);
            return names;
        }

        static string[] Draws(params string[] shopDraws)
        {
            var names = new string[OpeningDraws.Length + shopDraws.Length];
            OpeningDraws.CopyTo(names, 0);
            shopDraws.CopyTo(names, OpeningDraws.Length);
            return names;
        }

        void KeepAndPay()
        {
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "能量吐息")
                .ToArray();
            App.SendCommand(new PlaceMonsterCommand(breaths[0].Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breaths[1].Id, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());
        }
    }

    sealed class FixedTools : IToolCatalog
    {
        public FixedTools(params ToolDefinition[] tools) => Tools = tools;

        public IReadOnlyList<ToolDefinition> Tools { get; }
    }

    sealed class RecordingDraw : IDraw
    {
        readonly Queue<string> _names;

        public RecordingDraw(params string[] names) => _names = new Queue<string>(names);

        public List<string[]> Lists { get; } = new List<string[]>();

        public T Choose<T>(IReadOnlyList<T> options)
        {
            var copy = new string[options.Count];
            for (var i = 0; i < options.Count; i++)
                copy[i] = options[i].ToString();

            Lists.Add(copy);
            if (_names.Count == 0)
                throw new InvalidOperationException("没有更多预设抽取。");

            var name = _names.Dequeue();
            foreach (var option in options)
            {
                if (option is string text && text == name)
                    return option;
            }

            throw new InvalidOperationException("抽取名单里没有 " + name);
        }

        public bool Chance(int percent) => false;
    }
}
