using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class BreedingTests : RulesFixture
    {
        [Test]
        public void 一只亲本在下一关开始生成带有该技能的后代亲本仍留在怪物笼()
        {
            UseLevel(
                new ScriptedLevelCatalog(0),
                "能量体",
                "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var before = App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id).ToArray();
            var parent = App.SendQuery(new MonsterCageQuery())
                .Single(monster => monster.SkillNames.Single() == "奇异香");

            App.SendCommand(new PlaceMonsterCommand(parent.Id, OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.LevelStart));
            Assert.That(App.SendQuery(new BreedingSlotsQuery()).All(slot => slot.MonsterId == null), Is.True);
            var cage = App.SendQuery(new MonsterCageQuery()).ToArray();
            Assert.That(cage.Select(monster => monster.Id), Does.Contain(parent.Id));
            var offspring = cage.Where(monster => !before.Contains(monster.Id)).ToArray();
            Assert.That(offspring.Length, Is.EqualTo(1));
            Assert.That(offspring[0].SkillNames, Is.EqualTo(new[] { "奇异香" }));
        }

        [Test]
        public void 两只单技能亲本技能不同时后代按亲本顺序得到这两个技能()
        {
            OpenLevel();
            var first = Parent("奇异香");
            var second = Parent("怪异香");
            var before = CageIds();

            App.SendCommand(new PlaceMonsterCommand(first, OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(second, OperationArea.Breeding, 1));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            var offspring = Offspring(before);
            Assert.That(offspring.SkillNames, Is.EqualTo(new[] { "奇异香", "怪异香" }));
        }

        [Test]
        public void 两只单技能亲本技能相同时后代只有这一个技能()
        {
            OpenLevel(
                "能量体",
                "左能量体",
                "右能量体",
                "能量体",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");
            var parents = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "能量体")
                .ToArray();
            var before = CageIds();

            App.SendCommand(new PlaceMonsterCommand(parents[0].Id, OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(parents[1].Id, OperationArea.Breeding, 1));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            Assert.That(Offspring(before).SkillNames, Is.EqualTo(new[] { "能量体" }));
        }

        [Test]
        public void 亲本有多个技能时后代得到抽中的那一个()
        {
            var draw = new BreedingDraw(
                OpeningNames,
                "奇异香");
            UseRules(draw, new ScriptedLevelCatalog(0));
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var host = Parent("能量体");
            App.SendCommand(new DiscardMonsterCommand(Parent("奇异香")));
            App.SendCommand(new EquipSkillCommand(host, 0));
            var before = CageIds();

            App.SendCommand(new PlaceMonsterCommand(host, OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            Assert.That(Offspring(before).SkillNames, Is.EqualTo(new[] { "奇异香" }));
        }

        [Test]
        public void 亲本不全是单技能而又抽到同名时后代得到两个不同技能()
        {
            var draw = new BreedingDraw(
                new[]
                {
                    "能量体",
                    "左能量体",
                    "右能量体",
                    "能量体",
                    "奇异香",
                    "汲取鼻",
                    "孤独心",
                    "吞噬大嘴",
                    "双头能量体",
                },
                "能量体");
            UseRules(draw, new ScriptedLevelCatalog(0));
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "能量体")
                .ToArray();
            App.SendCommand(new DiscardMonsterCommand(Parent("奇异香")));
            App.SendCommand(new EquipSkillCommand(breaths[0].Id, 0));
            var before = CageIds();

            App.SendCommand(new PlaceMonsterCommand(breaths[0].Id, OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(breaths[1].Id, OperationArea.Breeding, 1));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            Assert.That(Offspring(before).SkillNames, Is.EqualTo(new[] { "奇异香", "能量体" }));
        }

        [Test]
        public void 抽到同名时不会把同一技能再随机一次()
        {
            var draw = new BreedingDraw(
                new[]
                {
                    "能量体",
                    "左能量体",
                    "右能量体",
                    "能量体",
                    "奇异香",
                    "孤独心",
                    "汲取鼻",
                    "吞噬大嘴",
                    "双头能量体",
                },
                "能量体",
                "能量体");
            UseRules(draw, new ScriptedLevelCatalog(0));
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "能量体")
                .ToArray();
            var hand = Parent("奇异香");
            var heart = Parent("孤独心");
            App.SendCommand(new DiscardMonsterCommand(hand));
            App.SendCommand(new EquipSkillCommand(breaths[0].Id, 0));
            App.SendCommand(new DiscardMonsterCommand(heart));
            App.SendCommand(new EquipSkillCommand(breaths[1].Id, 0));
            var before = CageIds();

            App.SendCommand(new PlaceMonsterCommand(breaths[0].Id, OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(breaths[1].Id, OperationArea.Breeding, 1));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            Assert.That(Offspring(before).SkillNames, Is.EqualTo(new[] { "能量体", "孤独心" }));
        }

        [Test]
        public void 空的培育槽不会生成后代()
        {
            OpenLevel();
            var before = CageIds();

            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            Assert.That(CageIds(), Is.EqualTo(before));
            Assert.That(App.SendQuery(new BreedingSlotsQuery()).All(slot => slot.MonsterId == null), Is.True);
        }

        [Test]
        public void 培育槽只有两个亲本位放不进第三只也不吃技能槽()
        {
            OpenLevel();
            var first = Parent("奇异香");
            var second = Parent("怪异香");
            var third = Parent("汲取鼻");
            var donor = Parent("孤独心");
            App.SendCommand(new DiscardMonsterCommand(donor));
            var before = CageIds();

            App.SendCommand(new PlaceMonsterCommand(first, OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(second, OperationArea.Breeding, 1));
            App.SendCommand(new PlaceMonsterCommand(third, OperationArea.Breeding, 2));

            var slots = App.SendQuery(new BreedingSlotsQuery());
            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(slots[0].MonsterId, Is.EqualTo(first));
            Assert.That(slots[1].MonsterId, Is.EqualTo(second));
            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Contain(third));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "孤独心" }));

            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            Assert.That(Offspring(before).SkillNames, Is.EqualTo(new[] { "奇异香", "怪异香" }));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.EqualTo(new[] { "孤独心" }));
        }

        [Test]
        public void 加班可以改掉尚未最终锁定的方案锁定后的亲本不能出售()
        {
            UseLevel(
                new ScriptedLevelCatalog(10),
                "能量体",
                "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breath = Parent("能量体");
            var dropped = Parent("奇异香");
            var kept = Parent("怪异香");
            App.SendCommand(new PlaceMonsterCommand(breath, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(dropped, OperationArea.Breeding, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Operation));
            App.SendCommand(new ReturnMonsterCommand(dropped));
            App.SendCommand(new PlaceMonsterCommand(kept, OperationArea.Breeding, 0));
            Assert.That(App.SendQuery(new BreedingSlotsQuery())[0].MonsterId, Is.EqualTo(kept));
            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Contain(dropped));

            App.SendCommand(new ConfirmSettlementCommand());
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Shop));
            var wage = App.SendQuery(new RunLedgerQuery()).Gold;
            App.SendCommand(new ReturnMonsterCommand(kept));
            App.SendCommand(new SellMonsterCommand(kept));
            Assert.That(App.SendQuery(new BreedingSlotsQuery())[0].MonsterId, Is.EqualTo(kept));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(wage));

            App.SendCommand(new SellMonsterCommand(dropped));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(wage + 5));
            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Not.Contain(dropped));

            var known = CageIds().Concat(new[] { kept, breath }).ToArray();
            App.SendCommand(new LeaveShopCommand());
            Assert.That(Offspring(known).SkillNames, Is.EqualTo(new[] { "怪异香" }));
            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Contain(kept));
            Assert.That(App.SendQuery(new BreedingSlotsQuery()).All(slot => slot.MonsterId == null), Is.True);
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

        void OpenLevel(params string[] names)
        {
            if (names.Length == 0)
                names = OpeningNames;

            UseLevel(new ScriptedLevelCatalog(0), names);
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
        }

        string Parent(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => monster.SkillNames.Single() == skillName).Id;

        string[] CageIds() => App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id).ToArray();

        MonsterView Offspring(string[] before)
        {
            var offspring = App.SendQuery(new MonsterCageQuery()).Where(monster => !before.Contains(monster.Id)).ToArray();
            Assert.That(offspring.Length, Is.EqualTo(1));
            return offspring[0];
        }
    }

    sealed class BreedingDraw : IDraw
    {
        readonly Queue<string> _opening;
        readonly Queue<string> _picks;

        public BreedingDraw(string[] opening, params string[] picks)
        {
            _opening = new Queue<string>(opening);
            _picks = new Queue<string>(picks);
        }

        public T Choose<T>(IReadOnlyList<T> options)
        {
            if (options == null || options.Count == 0)
                throw new InvalidOperationException("抽取名单是空的。");

            if (options.Count > 4)
                return Take(_opening, options);

            if (options.Count > 1)
                return Take(_picks, options);

            return options[0];
        }

        static T Take<T>(Queue<string> queue, IReadOnlyList<T> options)
        {
            if (queue.Count == 0)
                return options[0];

            var name = queue.Peek();
            for (var i = 0; i < options.Count; i++)
            {
                if (options[i] is string text && text == name)
                {
                    queue.Dequeue();
                    return options[i];
                }
            }

            return options[0];
        }

        public bool Chance(int percent) => false;
    }
}
