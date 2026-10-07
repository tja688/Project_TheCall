using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class QuoteTests : RulesFixture
    {
        [Test]
        public void 双头能量体在一次主动执行里报价两次且每次落地两点()
        {
            UseDraw(
                "双头能量体",
                "能量体",
                "左能量体",
                "右能量体",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "蜜能量体");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breathId = App.SendQuery(new MonsterCageQuery())
                .Single(monster => monster.SkillNames.Single() == "双头能量体").Id;
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breathId, breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "双头能量体", "双头能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 2, 2 }));
        }

        [Test]
        public void 吞噬大嘴落地四点并立刻消灭相邻怪物且格子不挪动()
        {
            UseDraw(
                "吞噬大嘴",
                "左能量体",
                "右能量体",
                "能量体",
                "镜眼",
                "汲取鼻",
                "孤独心",
                "双头能量体",
                "蜜能量体");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var devourerId = IdOf("吞噬大嘴");
            var victimId = IdOf("能量体");
            var stayingId = IdOf("镜眼");
            App.SendCommand(new PlaceMonsterCommand(victimId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(stayingId, OperationArea.Extraction, 3));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { victimId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5 }));

            var cells = App.SendQuery(new ExtractionSlotsQuery());
            Assert.That(cells.Select(cell => cell.MonsterId).ToArray(), Is.EqualTo(new[]
            {
                null,
                devourerId,
                null,
                stayingId,
                null,
            }));
            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Not.Contain(victimId));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.Empty);
        }

        [Test]
        public void 没有相邻时吞噬大嘴仍然落地且下一次计分使用写回后的报价()
        {
            UseDraw(
                "吞噬大嘴",
                "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心",
                "双头能量体",
                "蜜能量体");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var devourerId = IdOf("吞噬大嘴");
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(Landings(), Is.Empty);
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[2].MonsterId, Is.EqualTo(devourerId));
            Assert.That(App.SendQuery(new MonsterQuery(devourerId)).Skills.Single().Quote, Is.EqualTo(0));

            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(Landings(), Is.Empty);
        }

        [Test]
        public void 吞噬大嘴按抽取消灭指定的相邻而不是固定左侧()
        {
            var draw = new PinnedDraw(
                "吞噬大嘴",
                "左能量体",
                "右能量体",
                "能量体",
                "镜眼",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "蜜能量体");
            UseRules(draw, new ScriptedLevelCatalog(50));

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var devourerId = IdOf("吞噬大嘴");
            var leftId = IdOf("能量体");
            var rightId = IdOf("镜眼");
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(rightId, OperationArea.Extraction, 2));
            draw.Pinned = rightId;
            App.SendCommand(new ConfirmSettlementCommand());

            var cells = App.SendQuery(new ExtractionSlotsQuery());
            Assert.That(cells[0].MonsterId, Is.EqualTo(leftId));
            Assert.That(cells[1].MonsterId, Is.EqualTo(devourerId));
            Assert.That(cells[2].MonsterId, Is.Null);
            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Not.Contain(rightId));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.Empty);
            Assert.That(
                Landings().Single(landing => landing.MonsterId == leftId).Energy,
                Is.EqualTo(5));
        }

        [Test]
        public void 同一批里另一只吞噬大嘴仍用自己的报价()
        {
            UseDraw(
                "吞噬大嘴",
                "左能量体",
                "右能量体",
                "吞噬大嘴",
                "镜眼",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "蜜能量体");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var cage = App.SendQuery(new MonsterCageQuery());
            var firstId = cage[0].Id;
            var secondId = cage[1].Id;
            App.SendCommand(new PlaceMonsterCommand(firstId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(secondId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(Landings(), Is.Empty);
            Assert.That(App.SendQuery(new MonsterQuery(firstId)).Skills.Single().Quote, Is.EqualTo(0));
            Assert.That(App.SendQuery(new MonsterQuery(secondId)).Skills.Single().Quote, Is.EqualTo(0));
        }

        [Test]
        public void 同一批里后面的技能不沿用吞噬大嘴刚落地的底数()
        {
            UseDraw(
                "吞噬大嘴",
                "左能量体",
                "右能量体",
                "双头能量体",
                "镜眼",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "蜜能量体");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var devourerId = IdOf("吞噬大嘴");
            App.SendCommand(new DiscardMonsterCommand(IdOf("双头能量体")));
            App.SendCommand(new EquipSkillCommand(devourerId, 0));
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "双头能量体", "双头能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 4, 4 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 4, 4 }));
        }

        [Test]
        public void 永久写回留到下一关且没有永久加成的技能报价不变()
        {
            UseLevel(
                new ScriptedLevelCatalog(new[] { 0, 0 }, new[] { 100, 100 }),
                "能量体",
                "左能量体",
                "右能量体",
                "吞噬大嘴",
                "镜眼",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "双头能量体");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breathId = IdOf("能量体");
            var devourerId = IdOf("吞噬大嘴");
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量体" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5 }));
        }

        string IdOf(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => monster.SkillNames.Single() == skillName).Id;
    }

    sealed class PinnedDraw : IDraw
    {
        readonly System.Collections.Generic.Queue<string> _names;

        public PinnedDraw(params string[] names) =>
            _names = new System.Collections.Generic.Queue<string>(names);

        public string Pinned { get; set; }

        public T Choose<T>(System.Collections.Generic.IReadOnlyList<T> options)
        {
            if (Pinned != null)
            {
                foreach (var option in options)
                {
                    if (option is string text && text == Pinned)
                        return option;
                }
            }

            if (_names.Count == 0)
            {
                if (options == null || options.Count == 0)
                    throw new System.InvalidOperationException("抽取名单是空的。");

                return options[0];
            }

            var name = _names.Dequeue();
            foreach (var option in options)
            {
                if (option is string text && text == name)
                    return option;
            }

            throw new System.InvalidOperationException("抽取名单里没有 " + name);
        }

        public bool Chance(int percent) => false;
    }
}
