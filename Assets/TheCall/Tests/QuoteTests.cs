using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class QuoteTests : RulesFixture
    {
        [Test]
        public void 双重吐息在一次主动执行里报价两次且每次落地两点()
        {
            UseDraw(
                "双重吐息",
                "能量吐息",
                "左能量体",
                "右能量体",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴",
                "分享之手");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breathId = App.SendQuery(new MonsterCageQuery())
                .Single(monster => monster.SkillNames.Single() == "双重吐息").Id;
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breathId, breathId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "双重吐息", "双重吐息" }));
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
                "能量吐息",
                "太阳能头",
                "残留提取腺体",
                "孤独心",
                "双重吐息",
                "分享之手");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var devourerId = IdOf("吞噬大嘴");
            var victimId = IdOf("能量吐息");
            var stayingId = IdOf("太阳能头");
            App.SendCommand(new PlaceMonsterCommand(victimId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(stayingId, OperationArea.Extraction, 3));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { victimId, devourerId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息", "吞噬大嘴" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 4 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 4 }));

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
                "增量小手",
                "增量大手",
                "残留提取腺体",
                "孤独心",
                "双重吐息",
                "分享之手");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var devourerId = IdOf("吞噬大嘴");
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var first = App.SendQuery(new SettlementRecordQuery());
            Assert.That(first.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "吞噬大嘴" }));
            Assert.That(first.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 4 }));
            Assert.That(first.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(first.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 4 }));
            Assert.That(App.SendQuery(new ExtractionSlotsQuery())[2].MonsterId, Is.EqualTo(devourerId));

            App.SendCommand(new ConfirmSettlementCommand());

            var second = App.SendQuery(new SettlementRecordQuery());
            Assert.That(second.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "吞噬大嘴" }));
            Assert.That(second.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 6 }));
            Assert.That(second.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1 }));
            Assert.That(second.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 6 }));
        }

        [Test]
        public void 吞噬大嘴按抽取消灭指定的相邻而不是固定左侧()
        {
            var draw = new PinnedDraw(
                "吞噬大嘴",
                "左能量体",
                "右能量体",
                "能量吐息",
                "太阳能头",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "分享之手");
            UseRules(draw, new ScriptedLevelCatalog(50));

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var devourerId = IdOf("吞噬大嘴");
            var leftId = IdOf("能量吐息");
            var rightId = IdOf("太阳能头");
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
                App.SendQuery(new SettlementRecordQuery()).Single(landing => landing.MonsterId == leftId).Energy,
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
                "太阳能头",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "分享之手");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var cage = App.SendQuery(new MonsterCageQuery());
            var firstId = cage[0].Id;
            var secondId = cage[1].Id;
            App.SendCommand(new PlaceMonsterCommand(firstId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(secondId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { firstId, secondId }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 4, 4 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 4, 4 }));
        }

        [Test]
        public void 同一批里后面的技能不沿用吞噬大嘴刚落地的底数()
        {
            UseDraw(
                "吞噬大嘴",
                "左能量体",
                "右能量体",
                "双重吐息",
                "太阳能头",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "分享之手");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var devourerId = IdOf("吞噬大嘴");
            App.SendCommand(new DiscardMonsterCommand(IdOf("双重吐息")));
            App.SendCommand(new EquipSkillCommand(devourerId, 0));
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "吞噬大嘴", "双重吐息", "双重吐息" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 4, 2, 2 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 4, 2, 2 }));
        }

        [Test]
        public void 永久写回留到下一关且没有永久加成的技能报价不变()
        {
            UseLevel(
                new ScriptedLevelCatalog(new[] { 0, 0 }, new[] { 100, 100 }),
                "能量吐息",
                "左能量体",
                "右能量体",
                "吞噬大嘴",
                "太阳能头",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "双重吐息");

            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var breathId = IdOf("能量吐息");
            var devourerId = IdOf("吞噬大嘴");
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());
            App.SendCommand(new LeaveShopCommand());

            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(devourerId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = App.SendQuery(new SettlementRecordQuery());
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { breathId, devourerId }));
            Assert.That(landings.Select(landing => landing.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息", "吞噬大嘴" }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 5, 6 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 5, 6 }));
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
    }
}
