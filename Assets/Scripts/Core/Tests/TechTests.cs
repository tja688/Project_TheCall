using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class TechTests : RulesFixture
    {
        [Test]
        public void 解锁科技消耗一点科技点且不消耗金币和能量()
        {
            Open();
            EarnTechPoint();
            var gold = App.SendQuery(new RunLedgerQuery()).Gold;
            var energy = App.SendQuery(new LevelEnergyQuery());

            App.SendCommand(new UnlockTechCommand("基因实验"));

            var ledger = App.SendQuery(new RunLedgerQuery());
            Assert.That(ledger.TechPoints, Is.EqualTo(0));
            Assert.That(ledger.UnlockedTech, Is.EqualTo(new[] { "基因实验" }));
            Assert.That(ledger.Gold, Is.EqualTo(gold));
            Assert.That(App.SendQuery(new LevelEnergyQuery()), Is.EqualTo(energy));
        }

        [Test]
        public void 没有科技点时金币不能解锁科技()
        {
            UseLevel(new ScriptedLevelCatalog(0, 1), OpeningNames);
            KeepOpened();
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());
            var gold = App.SendQuery(new RunLedgerQuery()).Gold;

            App.SendCommand(new UnlockTechCommand("基因实验"));

            var ledger = App.SendQuery(new RunLedgerQuery());
            Assert.That(ledger.TechPoints, Is.EqualTo(0));
            Assert.That(ledger.UnlockedTech, Is.Empty);
            Assert.That(ledger.Gold, Is.EqualTo(gold));
            Assert.That(gold, Is.GreaterThan(0));
        }

        [Test]
        public void 同一科技不能再次解锁()
        {
            Open();
            EarnTechPoint();
            EarnTechPoint();

            App.SendCommand(new UnlockTechCommand("基因实验"));
            App.SendCommand(new UnlockTechCommand("基因实验"));

            var ledger = App.SendQuery(new RunLedgerQuery());
            Assert.That(ledger.TechPoints, Is.EqualTo(1));
            Assert.That(ledger.UnlockedTech, Is.EqualTo(new[] { "基因实验" }));
        }

        [Test]
        public void 不存在的科技不消耗科技点()
        {
            Open();
            EarnTechPoint();

            App.SendCommand(new UnlockTechCommand("炼金"));

            var ledger = App.SendQuery(new RunLedgerQuery());
            Assert.That(ledger.TechPoints, Is.EqualTo(1));
            Assert.That(ledger.UnlockedTech, Is.Empty);
        }

        [Test]
        public void 五项科技没有前置且各消耗一点()
        {
            Open();
            for (var i = 0; i < 5; i++)
                EarnTechPoint();

            foreach (var name in new[] { "科学培育", "大乱炖", "变异学说", "槽位扩容", "基因实验" })
                App.SendCommand(new UnlockTechCommand(name));

            var ledger = App.SendQuery(new RunLedgerQuery());
            Assert.That(ledger.TechPoints, Is.EqualTo(0));
            Assert.That(
                ledger.UnlockedTech,
                Is.EqualTo(new[] { "科学培育", "大乱炖", "变异学说", "槽位扩容", "基因实验" }));
        }

        [Test]
        public void 商店阶段不能解锁科技()
        {
            Open();
            App.SendCommand(new ConfirmSettlementCommand());

            App.SendCommand(new UnlockTechCommand("基因实验"));

            var ledger = App.SendQuery(new RunLedgerQuery());
            Assert.That(ledger.TechPoints, Is.EqualTo(1));
            Assert.That(ledger.UnlockedTech, Is.Empty);
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Shop));
        }

        [Test]
        public void 没有基因实验时培育槽不接收技能()
        {
            Open();
            App.SendCommand(new DiscardMonsterCommand(Parent("孤独心")));

            App.SendCommand(new PlaceBreedingSkillCommand(0, 0));

            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "孤独心" }));
            Assert.That(App.SendQuery(new BreedingPlansQuery())[0].SkillName, Is.Null);
        }

        [Test]
        public void 基因实验使每个培育槽放入一个培育技能()
        {
            Open();
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("基因实验"));
            App.SendCommand(new DiscardMonsterCommand(Parent("孤独心")));
            App.SendCommand(new DiscardMonsterCommand(Parent("奇异香")));

            App.SendCommand(new PlaceBreedingSkillCommand(0, 0));

            Assert.That(App.SendQuery(new BreedingPlansQuery())[0].SkillName, Is.EqualTo("孤独心"));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "奇异香" }));

            App.SendCommand(new PlaceBreedingSkillCommand(0, 0));

            Assert.That(App.SendQuery(new BreedingPlansQuery())[0].SkillName, Is.EqualTo("孤独心"));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "奇异香" }));
        }

        [Test]
        public void 培育技能与后代已有技能不同名时交给后代()
        {
            Open();
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("基因实验"));
            App.SendCommand(new DiscardMonsterCommand(Parent("怪异香")));
            App.SendCommand(new PlaceBreedingSkillCommand(0, 0));
            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(Parent("奇异香"), OperationArea.Breeding, 0));

            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            Assert.That(Offspring(before).SkillNames, Is.EqualTo(new[] { "奇异香", "怪异香" }));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.Empty);
        }

        [Test]
        public void 培育技能与后代已有技能同名时不交给后代()
        {
            UseLevel(
                new ScriptedLevelCatalog(0, 0),
                "能量体",
                "左能量体",
                "右能量体",
                "能量体",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");
            KeepOpened();
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("基因实验"));
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => Holds(monster, "能量体"))
                .ToArray();
            App.SendCommand(new DiscardMonsterCommand(breaths[1].Id));
            App.SendCommand(new PlaceBreedingSkillCommand(0, 0));
            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(breaths[0].Id, OperationArea.Breeding, 0));

            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            Assert.That(Offspring(before).SkillNames, Is.EqualTo(new[] { "能量体" }));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "能量体" }));
        }

        [Test]
        public void 锁定前可以把培育技能拿回技能槽()
        {
            Open();
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("基因实验"));
            App.SendCommand(new DiscardMonsterCommand(Parent("孤独心")));
            App.SendCommand(new PlaceBreedingSkillCommand(0, 0));

            App.SendCommand(new ReturnBreedingSkillCommand(0));

            Assert.That(App.SendQuery(new BreedingPlansQuery())[0].SkillName, Is.Null);
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "孤独心" }));
        }

        [Test]
        public void 技能槽已满时不能从培育槽拿回技能()
        {
            Open();
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("基因实验"));
            App.SendCommand(new DiscardMonsterCommand(Parent("孤独心")));
            App.SendCommand(new PlaceBreedingSkillCommand(0, 0));
            App.SendCommand(new DiscardMonsterCommand(Parent("奇异香")));
            App.SendCommand(new DiscardMonsterCommand(Parent("怪异香")));
            App.SendCommand(new DiscardMonsterCommand(Parent("汲取鼻")));

            App.SendCommand(new ReturnBreedingSkillCommand(0));

            Assert.That(App.SendQuery(new BreedingPlansQuery())[0].SkillName, Is.EqualTo("孤独心"));
            Assert.That(
                App.SendQuery(new RunLedgerQuery()).SkillSlots,
                Is.EqualTo(new[] { "奇异香", "怪异香", "汲取鼻" }));
        }

        [Test]
        public void 槽位扩容增加一个独立培育槽且各自锁定方案()
        {
            Open();
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("槽位扩容"));

            var plans = App.SendQuery(new BreedingPlansQuery());
            Assert.That(plans.Count, Is.EqualTo(2));
            Assert.That(plans[0].ParentIds.Count, Is.EqualTo(2));
            Assert.That(plans[1].ParentIds.Count, Is.EqualTo(2));

            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(Parent("奇异香"), OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(Parent("怪异香"), OperationArea.Breeding, 2));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            var offspring = App.SendQuery(new MonsterCageQuery()).Where(monster => !before.Contains(monster.Id)).ToArray();
            Assert.That(offspring.Length, Is.EqualTo(2));
            Assert.That(
                offspring.Select(monster => monster.SkillNames.Single()).ToArray(),
                Is.EquivalentTo(new[] { "奇异香", "怪异香" }));
        }

        [Test]
        public void 大乱炖使每个培育槽的亲本容量加一()
        {
            Open();
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("大乱炖"));

            var plans = App.SendQuery(new BreedingPlansQuery());
            Assert.That(plans.Count, Is.EqualTo(1));
            Assert.That(plans[0].ParentIds.Count, Is.EqualTo(3));

            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(Parent("奇异香"), OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(Parent("怪异香"), OperationArea.Breeding, 1));
            App.SendCommand(new PlaceMonsterCommand(Parent("汲取鼻"), OperationArea.Breeding, 2));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            Assert.That(
                Offspring(before).SkillNames,
                Is.EqualTo(new[] { "奇异香", "怪异香", "汲取鼻" }));
        }

        [Test]
        public void 扩容后的两个培育槽都能再放一只亲本并各自生成后代()
        {
            Open();
            EarnTechPoint();
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("槽位扩容"));
            App.SendCommand(new UnlockTechCommand("大乱炖"));

            var plans = App.SendQuery(new BreedingPlansQuery());
            Assert.That(plans.Count, Is.EqualTo(2));
            Assert.That(plans[0].ParentIds.Count, Is.EqualTo(3));
            Assert.That(plans[1].ParentIds.Count, Is.EqualTo(3));

            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(Parent("奇异香"), OperationArea.Breeding, 2));
            App.SendCommand(new PlaceMonsterCommand(Parent("怪异香"), OperationArea.Breeding, 3));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            var offspring = App.SendQuery(new MonsterCageQuery()).Where(monster => !before.Contains(monster.Id)).ToArray();
            Assert.That(offspring.Length, Is.EqualTo(2));
            Assert.That(
                offspring.Select(monster => monster.SkillNames.Single()).ToArray(),
                Is.EquivalentTo(new[] { "奇异香", "怪异香" }));
        }

        [Test]
        public void 每个培育槽的培育技能只交给该槽的后代()
        {
            Open();
            EarnTechPoint();
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("基因实验"));
            App.SendCommand(new UnlockTechCommand("槽位扩容"));
            App.SendCommand(new DiscardMonsterCommand(Parent("孤独心")));
            App.SendCommand(new DiscardMonsterCommand(Parent("吞噬大嘴")));
            App.SendCommand(new PlaceBreedingSkillCommand(0, 0));
            App.SendCommand(new PlaceBreedingSkillCommand(1, 0));
            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(Parent("奇异香"), OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(Parent("怪异香"), OperationArea.Breeding, 2));

            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            var offspring = App.SendQuery(new MonsterCageQuery()).Where(monster => !before.Contains(monster.Id)).ToArray();
            Assert.That(offspring.Length, Is.EqualTo(2));
            Assert.That(
                offspring.Select(monster => string.Join(",", monster.SkillNames)).ToArray(),
                Is.EquivalentTo(new[] { "奇异香,孤独心", "怪异香,吞噬大嘴" }));
        }

        [Test]
        public void 未解锁变异时抽中也不会改变后代()
        {
            var draw = new TechDraw(OpeningNames, true);
            Open(draw);
            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(Parent("奇异香"), OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            draw.Pinned = "吞噬大嘴";
            App.SendCommand(new LeaveShopCommand());

            Assert.That(Offspring(before).SkillNames, Is.EqualTo(new[] { "奇异香" }));
            Assert.That(draw.Percents, Is.Empty);
        }

        [Test]
        public void 变异学说抽中时后代再获得指定技能()
        {
            var draw = new TechDraw(OpeningNames, true);
            Open(draw);
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("变异学说"));
            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(Parent("奇异香"), OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            draw.Pinned = "吞噬大嘴";
            App.SendCommand(new LeaveShopCommand());

            Assert.That(Offspring(before).SkillNames, Is.EqualTo(new[] { "奇异香", "吞噬大嘴" }));
            Assert.That(draw.Percents, Is.EqualTo(new[] { 10 }));
        }

        [Test]
        public void 变异学说没抽中时后代不获得额外技能()
        {
            var draw = new TechDraw(OpeningNames, false);
            Open(draw);
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("变异学说"));
            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(Parent("奇异香"), OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            draw.Pinned = "吞噬大嘴";
            App.SendCommand(new LeaveShopCommand());

            Assert.That(Offspring(before).SkillNames, Is.EqualTo(new[] { "奇异香" }));
            Assert.That(draw.Percents, Is.EqualTo(new[] { 10 }));
        }

        [Test]
        public void 后代已经有四个技能时变异学说不再追加()
        {
            var draw = new TechDraw(OpeningNames, true);
            Open(draw);
            EarnTechPoint();
            EarnTechPoint();
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("大乱炖"));
            App.SendCommand(new UnlockTechCommand("基因实验"));
            App.SendCommand(new UnlockTechCommand("变异学说"));
            App.SendCommand(new DiscardMonsterCommand(Parent("孤独心")));
            App.SendCommand(new PlaceBreedingSkillCommand(0, 0));
            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(Parent("奇异香"), OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(Parent("怪异香"), OperationArea.Breeding, 1));
            App.SendCommand(new PlaceMonsterCommand(Parent("汲取鼻"), OperationArea.Breeding, 2));
            App.SendCommand(new ConfirmSettlementCommand());
            draw.Pinned = "左复制腺体";
            App.SendCommand(new LeaveShopCommand());

            Assert.That(
                Offspring(before).SkillNames,
                Is.EqualTo(new[] { "奇异香", "怪异香", "汲取鼻", "孤独心" }));
        }

        [Test]
        public void 科学培育抽中时后代获得加二修正且只参加自己的产能量计分()
        {
            var draw = new TechDraw(OpeningNames, true);
            Open(draw);
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("科学培育"));
            var parent = Parent("能量体");
            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(parent, OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            var child = Offspring(before);
            Assert.That(child.SkillNames, Is.EqualTo(new[] { "能量体" }));
            Assert.That(child.Modifier, Is.EqualTo(2));
            Assert.That(draw.Percents, Is.EqualTo(new[] { 10 }));

            App.SendCommand(new PlaceMonsterCommand(child.Id, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(parent, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var first = Landings();
            Assert.That(Landing(first, child.Id).Base, Is.EqualTo(7));
            Assert.That(Landing(first, child.Id).Energy, Is.EqualTo(7));
            Assert.That(Landing(first, parent).Base, Is.EqualTo(5));

            App.SendCommand(new LeaveShopCommand());
            Assert.That(
                App.SendQuery(new MonsterCageQuery()).Single(monster => monster.Id == child.Id).Modifier,
                Is.EqualTo(2));
            App.SendCommand(new PlaceMonsterCommand(child.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(Landing(Landings(), child.Id).Base, Is.EqualTo(7));
        }

        [Test]
        public void 科学培育的加二也加进产能另开的底数且不写进报价()
        {
            var names = new[]
            {
                "镜眼",
                "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "双头能量体",
            };
            var draw = new TechDraw(names, true);
            UseRules(draw, new ScriptedLevelCatalog(new[] { 0, 0, 3 }, new[] { 0, 0, 60 }));
            KeepOpened();
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("科学培育"));
            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(Parent("镜眼"), OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            var child = Offspring(before);
            Assert.That(child.Modifier, Is.EqualTo(2));
            App.SendCommand(new PlaceMonsterCommand(child.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(Landings(), Is.Empty);
            Assert.That(App.SendQuery(new MonsterQuery(child.Id)).Skills.Single().Quote, Is.EqualTo(0));
        }

        [Test]
        public void 科学培育没抽中时没有怪物修正()
        {
            var draw = new TechDraw(OpeningNames, false);
            Open(draw);
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("科学培育"));
            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(Parent("能量体"), OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            var child = Offspring(before);
            Assert.That(child.Modifier, Is.EqualTo(0));
            Assert.That(draw.Percents, Is.EqualTo(new[] { 10 }));

            App.SendCommand(new PlaceMonsterCommand(child.Id, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(Landing(Landings(), child.Id).Base, Is.EqualTo(5));
        }

        [Test]
        public void 两项变异都抽中时只追加一个技能和加二修正()
        {
            var draw = new TechDraw(OpeningNames, true, true);
            Open(draw);
            EarnTechPoint();
            EarnTechPoint();
            App.SendCommand(new UnlockTechCommand("变异学说"));
            App.SendCommand(new UnlockTechCommand("科学培育"));
            var before = CageIds();
            App.SendCommand(new PlaceMonsterCommand(Parent("奇异香"), OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            draw.Pinned = "吞噬大嘴";
            App.SendCommand(new LeaveShopCommand());

            var child = Offspring(before);
            Assert.That(child.SkillNames, Is.EqualTo(new[] { "奇异香", "吞噬大嘴" }));
            Assert.That(child.Modifier, Is.EqualTo(2));
            Assert.That(draw.Percents, Is.EqualTo(new[] { 10, 10 }));
        }

        static readonly string[] OpeningNames =
        {
            "能量体",
            "左能量体",
            "右能量体",
            "奇异香",
            "怪异香",
            "汲取鼻",
            "孤独心",
            "吞噬大嘴",
            "双头能量体",
        };

        void Open() => Open(new ScriptedDraw(OpeningNames));

        void Open(IDraw draw)
        {
            UseRules(draw, new ScriptedLevelCatalog(0, 0));
            KeepOpened();
        }

        string Parent(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, skillName)).Id;

        string[] CageIds() => App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id).ToArray();

        MonsterView Offspring(string[] before)
        {
            var offspring = App.SendQuery(new MonsterCageQuery()).Where(monster => !before.Contains(monster.Id)).ToArray();
            Assert.That(offspring.Length, Is.EqualTo(1));
            return offspring[0];
        }

        static SettlementLanding Landing(IReadOnlyList<SettlementLanding> landings, string monsterId)
        {
            var matches = landings.Where(landing => landing.MonsterId == monsterId).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1));
            return matches[0];
        }

        void EarnTechPoint()
        {
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());
        }
    }

    sealed class TechDraw : IDraw
    {
        readonly Queue<string> _names;
        readonly Queue<bool> _chances;

        public TechDraw(string[] opening, params bool[] chances)
        {
            _names = new Queue<string>(DrawAdapt.Adapt(opening));
            _chances = new Queue<bool>(chances);
        }

        public List<int> Percents { get; } = new List<int>();

        public string Pinned { get; set; }

        public bool Chance(int percent)
        {
            Percents.Add(percent);
            if (_chances.Count == 0)
                return false;

            return _chances.Dequeue();
        }

        public T Choose<T>(IReadOnlyList<T> options)
        {
            if (options == null || options.Count == 0)
                throw new InvalidOperationException("抽取名单是空的。");

            if (Pinned != null)
            {
                for (var i = 0; i < options.Count; i++)
                {
                    if (options[i] is string text && text == Pinned)
                        return options[i];
                }
            }

            if (_names.Count > 0)
            {
                var name = _names.Peek();
                for (var i = 0; i < options.Count; i++)
                {
                    if (options[i] is string text && text == name)
                    {
                        _names.Dequeue();
                        return options[i];
                    }
                }
            }

            return options[0];
        }
    }
}
