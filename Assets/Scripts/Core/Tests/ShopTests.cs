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
            "良好肉体", "外接胚胎",
            "良好肉体", "产金管道",
            "优质肉体", "外接胚胎",
            "能量体", "能量体", "左能量体", "右能量体",
            "奇异香", "怪异香", "汲取鼻", "换位手",
        };

        static readonly string[] FourSingles =
        {
            "1", "白", "能量体",
            "1", "蓝", "怪异香",
            "1", "金", "回响嗓",
            "1", "白", "左能量体",
        };

        [Test]
        public void 商店开始时按注入结果摆出四只怪物并按稀有度标价()
        {
            ReachShop(And(FourSingles, "急急装置", "上级员工证"));

            var shelf = App.SendQuery(new ShelfQuery());

            Assert.That(
                shelf.Monsters.Select(monster => monster.SkillNames.Single()).ToArray(),
                Is.EqualTo(new[] { "能量体", "怪异香", "回响嗓", "左能量体" }));
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
                "2", "白", "能量体", "蓝", "怪异香",
                "1", "金", "回响嗓",
                "1", "白", "左能量体",
                "1", "白", "右能量体",
                "急急装置",
                "上级员工证");

            var shelf = App.SendQuery(new ShelfQuery());
            Assert.That(shelf.Monsters[0].SkillNames, Is.EqualTo(new[] { "能量体", "怪异香" }));
            Assert.That(shelf.Monsters[0].Price, Is.EqualTo(30));
            Assert.That(shelf.Monsters[1].Price, Is.EqualTo(30));
        }

        [Test]
        public void 同一只商品怪物不会抽到两个同名技能()
        {
            ReachShop(
                "2", "金", "回响嗓", "金", "镜眼",
                "1", "白", "能量体",
                "1", "白", "左能量体",
                "1", "白", "右能量体",
                "急急装置",
                "上级员工证");

            var skills = App.SendQuery(new ShelfQuery()).Monsters[0].SkillNames;
            Assert.That(skills, Is.EqualTo(new[] { "回响嗓", "镜眼" }));
            Assert.That(skills.Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void 商店按九成一只一成两只以及七成白两成蓝一成金抽取()
        {
            var draw = new RecordingDraw(Draws(
                "1", "白", "能量体",
                "1", "白", "左能量体",
                "1", "白", "右能量体",
                "1", "白", "奇异香",
                "急急装置",
                "上级员工证"));
            UseRules(draw, new ScriptedLevelCatalog(5));
            KeepAndPay();
            App.SendQuery(new ShelfQuery());

            var counts = draw.Lists.Where(list => list.Length > 0 && list.All(item => item == "单" || item == "双" || item == "幅")).ToArray();
            var rarities = draw.Lists.Where(list => list.All(item => item == "白" || item == "蓝" || item == "金")).Reverse().Take(4).Reverse().ToArray();

            Assert.That(counts.Length, Is.EqualTo(4));
            Assert.That(counts[0], Is.EqualTo(new[] { "单", "单", "单", "单", "单", "单", "双", "幅", "幅", "幅" }));
            Assert.That(rarities.Length, Is.EqualTo(4));
            Assert.That(rarities[0], Is.EqualTo(new[] { "白", "白", "白", "白", "白", "白", "白", "蓝", "蓝", "金" }));
        }

        [Test]
        public void 货架最多两件不同名工具并按稀有度权重和标价抽取()
        {
            var tools = new FixedTools(
                new ToolDefinition("急急装置", 30, Rarity.White),
                new ToolDefinition("上级员工证", 50, Rarity.Blue),
                new ToolDefinition("劣胜装置", 80, Rarity.Gold));
            var draw = new RecordingDraw(Draws(And(FourSingles, "急急装置", "上级员工证")));
            UseRules(draw, new ScriptedLevelCatalog(5), tools);
            KeepAndPay();

            var shelf = App.SendQuery(new ShelfQuery());
            var rarityLists = draw.Lists.Where(list => list.All(item => item == "White" || item == "Blue" || item == "Gold")).ToArray();
            Assert.That(rarityLists[0].Count(r => r == "White"), Is.EqualTo(7));
            Assert.That(rarityLists[0].Count(r => r == "Blue"), Is.EqualTo(2));
            Assert.That(rarityLists[0].Count(r => r == "Gold"), Is.EqualTo(1));
            // Second tool slot: White is out of stock, so White is 0 and pool renormalizes to Blue 2, Gold 1
            Assert.That(rarityLists[1].Count(r => r == "White"), Is.EqualTo(0));
            Assert.That(rarityLists[1].Count(r => r == "Blue"), Is.EqualTo(2));
            Assert.That(rarityLists[1].Count(r => r == "Gold"), Is.EqualTo(1));

            Assert.That(shelf.Tools.Select(tool => tool.Name).ToArray(), Is.EqualTo(new[] { "急急装置", "上级员工证" }));
            Assert.That(shelf.Tools.Select(tool => tool.Price).ToArray(), Is.EqualTo(new[] { 30, 50 }));
            Assert.That(shelf.Tools.Select(tool => tool.Rarity).ToArray(), Is.EqualTo(new[] { Rarity.White, Rarity.Blue }));
            Assert.That(shelf.Tools.Select(tool => tool.Name), Does.Not.Contain("劣胜装置"));
        }

        [Test]
        public void 工具种类不足两件时货架少开位数()
        {
            ReachShop(new FixedTools(new ToolDefinition("上级员工证", 50, Rarity.Blue)), And(FourSingles, "上级员工证"));

            var tools = App.SendQuery(new ShelfQuery()).Tools;
            Assert.That(tools.Select(tool => tool.Name).ToArray(), Is.EqualTo(new[] { "上级员工证" }));
            Assert.That(tools[0].Price, Is.EqualTo(50));
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
                "2", "蓝", "怪异香", "金", "回响嗓",
                "1", "白", "能量体",
                "1", "白", "左能量体",
                "1", "白", "右能量体",
                "劣胜装置",
                "急急装置");
            var shelf = App.SendQuery(new ShelfQuery());
            var poor = shelf.Monsters[0];
            App.SendCommand(new BuyToolCommand("急急装置"));
            var before = App.SendQuery(new RunLedgerQuery()).Gold;

            App.SendCommand(new BuyToolCommand("劣胜装置"));
            App.SendCommand(new BuyMonsterCommand(poor.Id));

            var after = App.SendQuery(new ShelfQuery());
            Assert.That(poor.Price, Is.EqualTo(50));
            Assert.That(before, Is.EqualTo(30));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(before));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Tools, Is.EqualTo(new[] { "急急装置" }));
            Assert.That(after.Tools.Select(tool => tool.Name), Does.Contain("劣胜装置"));
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
            Assert.That(cage.SkillNames, Is.EqualTo(new[] { "能量体" }));
            Assert.That(bought.Monster, Is.Not.Null);
            Assert.That(bought.Monster.Id, Is.EqualTo(cage.Id));
            Assert.That(bought.Monster.Appearance, Is.EqualTo(cage.Appearance));
            Assert.That(bought.Monster.Personality, Is.EqualTo(cage.Personality));
            Assert.That(bought.Monster.Mood, Is.EqualTo(cage.Mood));
            Assert.That(bought.Monster.Race, Is.EqualTo(cage.Race));
            Assert.That(bought.Monster.Skills.Select(skill => skill.InstanceId), Is.EqualTo(cage.Skills.Select(skill => skill.InstanceId)));
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
                    new ToolDefinition("劣胜装置", 10, Rarity.Gold)),
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
            var gland = App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, "汲取鼻"));
            var hand = App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, "奇异香"));
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
            KeepOpened();
            var hand = App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, "奇异香"));

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
            KeepOpened();
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => Holds(monster, "能量体"))
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

        public RecordingDraw(params string[] names) => _names = new Queue<string>(DrawAdapt.Adapt(names));

        public List<string[]> Lists { get; } = new List<string[]>();

        public T Choose<T>(IReadOnlyList<T> options)
        {
            var copy = new string[options.Count];
            for (var i = 0; i < options.Count; i++)
                copy[i] = options[i].ToString();

            Lists.Add(copy);
            if (_names.Count == 0)
                throw new InvalidOperationException("没有更多预设抽取。");

            var name = _names.Peek();
            if (ContentGate.Current != null && options.Count > 0 && options[0] is string rarityWord &&
                (rarityWord == "白" || rarityWord == "蓝" || rarityWord == "金"))
            {
                var skillDef = ContentGate.Current.FindSkill(name);
                if (skillDef != null)
                {
                    var word = skillDef.Rarity == Rarity.Gold ? "金" : skillDef.Rarity == Rarity.Blue ? "蓝" : "白";
                    for (var i = 0; i < options.Count; i++) if (options[i] is string text && text == word) return options[i];
                }
            }
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option is string text && text == name)
                {
                    _names.Dequeue();
                    return option;
                }

                if (option is Rarity r && (r.ToString() == name || (name == "白" && r == Rarity.White) || (name == "蓝" && r == Rarity.Blue) || (name == "金" && r == Rarity.Gold)))
                {
                    _names.Dequeue();
                    return option;
                }
            }

            if (ContentGate.Current != null)
            {
                var tool = ContentGate.Current.FindTool(name);
                if (tool != null)
                {
                    for (var i = 0; i < options.Count; i++)
                    {
                        var option = options[i];
                        if (option is Rarity r && r == tool.Rarity)
                            return option;
                        if (option is string s && (s == tool.Rarity.ToString() || (s == "白" && tool.Rarity == Rarity.White) || (s == "蓝" && tool.Rarity == Rarity.Blue) || (s == "金" && tool.Rarity == Rarity.Gold)))
                            return option;
                    }
                }
            }

            if (options.Count > 0 && options[0] is Rarity)
                return options[0];

            if (options.Count > 0 && (options[0] is RaceDef || options[0] is PersonalityDef))
                return options[0];

            if (ContentGate.Current != null && options.Count > 0 && options[0] is string optName)
            {
                if (ContentGate.Current.FindRace(optName) != null && ContentGate.Current.FindRace(name) == null)
                    return options[0];
                if (ContentGate.Current.FindPersonality(optName) != null && ContentGate.Current.FindPersonality(name) == null)
                    return options[0];
            }

            name = _names.Dequeue();
            throw new InvalidOperationException("抽取名单里没有 " + name);
        }

        public bool Chance(int percent) => false;
    }
}
