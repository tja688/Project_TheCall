using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class SwapAndClockTests : RulesFixture
    {
        [Test]
        public void 再走一遍仍按换位前的格子给左能量体报价()
        {
            var intents = new ClockIntents();
            intents.WalkAgain();
            var leftId = PlacePair(intents, "左能量体", "换位手");

            var landings = Landings()
                .Where(landing => landing.SkillName == "左能量体")
                .ToArray();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { leftId, leftId }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 2, 2 }));
        }

        [Test]
        public void 倒放仍按换位前的格子给左能量体报价()
        {
            var intents = new ClockIntents();
            intents.PlayReverse();
            var leftId = PlacePair(intents, "左能量体", "换位手");

            var landings = Landings()
                .Where(landing => landing.SkillName == "左能量体")
                .ToArray();
            Assert.That(landings.Select(landing => landing.MonsterId).ToArray(), Is.EqualTo(new[] { leftId, leftId }));
            Assert.That(landings.Select(landing => landing.Base).ToArray(), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(landings.Select(landing => landing.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(landing => landing.Energy).ToArray(), Is.EqualTo(new[] { 2, 2 }));
        }

        [Test]
        public void 全部计分结束后换位手才与左侧相邻怪物交换()
        {
            Begin(null, new ScriptedLevelCatalog(50), "左能量体", "换位手");
            var leftId = IdOf("左能量体");
            var swapId = IdOf("换位手");
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(swapId, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single();
            Assert.That(landing.MonsterId, Is.EqualTo(leftId));
            Assert.That(landing.SkillName, Is.EqualTo("左能量体"));
            Assert.That(landing.Base, Is.EqualTo(2));
            Assert.That(landing.Multiplier, Is.EqualTo(1));
            Assert.That(landing.Energy, Is.EqualTo(2));
            AssertCells(swapId, leftId, null, null, null);
        }

        [Test]
        public void 换位手不把空档对面的怪物当成左侧相邻()
        {
            Begin(null, new ScriptedLevelCatalog(50), "左能量体", "换位手");
            var leftId = IdOf("左能量体");
            var swapId = IdOf("换位手");
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(swapId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            AssertCells(leftId, null, swapId, null, null);
        }

        [Test]
        public void 没有左侧目标时换位不发生也不获得不动()
        {
            Begin(null, new ScriptedLevelCatalog(50), "换位手", "换位手");
            var alone = Ids("换位手")[0];
            var neighbor = Ids("换位手")[1];
            App.SendCommand(new PlaceMonsterCommand(alone, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(neighbor, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            AssertCells(neighbor, alone, null, null, null);
        }

        [Test]
        public void 目标带有不动时换位不发生也不获得不动()
        {
            var intents = new ClockIntents();
            Begin(intents, new ScriptedLevelCatalog(50), "能量体", "换位手", "换位手");
            var anchored = IdOf("能量体");
            var blocked = Ids("换位手")[0];
            var follower = Ids("换位手")[1];
            intents.MakePermanentlyImmovable(anchored);
            App.SendCommand(new PlaceMonsterCommand(anchored, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(blocked, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(follower, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            AssertCells(anchored, follower, blocked, null, null);
        }

        [Test]
        public void 执行时目标已被立刻消灭则换位不发生也不获得不动()
        {
            Begin(null, new ScriptedLevelCatalog(50), "换位手", "吞噬大嘴", "能量体", "换位手");
            var host = Ids("换位手")[0];
            var victim = IdOf("能量体");
            var right = Ids("换位手")[1];
            App.SendCommand(new DiscardMonsterCommand(IdOf("吞噬大嘴")));
            App.SendCommand(new EquipSkillCommand(host, 0));
            App.SendCommand(new PlaceMonsterCommand(victim, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(host, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(right, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            AssertCells(null, right, host, null, null);
            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Not.Contain(victim));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.Empty);
            Assert.That(
                Landings().Select(landing => landing.Energy).ToArray(),
                Is.EqualTo(new[] { 5 }));
        }

        [Test]
        public void 换位成功后获得的不动让后面的换位失败()
        {
            Begin(null, new ScriptedLevelCatalog(50), "换位手", "能量体", "换位手", "换位手");
            var first = Ids("换位手")[0];
            var left = IdOf("能量体");
            var second = Ids("换位手")[1];
            var third = Ids("换位手")[2];
            App.SendCommand(new PlaceMonsterCommand(left, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(first, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(second, OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(third, OperationArea.Extraction, 3));
            App.SendCommand(new ConfirmSettlementCommand());

            AssertCells(first, left, third, second, null);
        }

        [Test]
        public void 获得不动后操作阶段仍可把怪物改放到别的格子()
        {
            Begin(null, new ScriptedLevelCatalog(50), "换位手", "能量体");
            var swapper = IdOf("换位手");
            var left = IdOf("能量体");
            App.SendCommand(new PlaceMonsterCommand(left, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(swapper, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Operation));
            App.SendCommand(new ReturnMonsterCommand(swapper));
            App.SendCommand(new PlaceMonsterCommand(swapper, OperationArea.Extraction, 4));

            AssertCells(null, left, null, null, swapper);
        }

        [Test]
        public void 没有永久的不动在离开关卡后不再挡住换位()
        {
            Begin(null, new ScriptedLevelCatalog(0), "换位手", "能量体", "换位手");
            var first = Ids("换位手")[0];
            var left = IdOf("能量体");
            var second = Ids("换位手")[1];
            App.SendCommand(new PlaceMonsterCommand(left, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(first, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(second, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());
            AssertCells(first, left, second, null, null);

            App.SendCommand(new LeaveShopCommand());
            App.SendCommand(new PlaceMonsterCommand(first, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(second, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            AssertCells(second, first, null, null, null);
        }

        [Test]
        public void 永久不动离开关卡后仍然挡住换位()
        {
            var intents = new ClockIntents();
            Begin(intents, new ScriptedLevelCatalog(0), "能量体", "换位手");
            var anchored = IdOf("能量体");
            var swapper = IdOf("换位手");
            intents.MakePermanentlyImmovable(anchored);
            App.SendCommand(new PlaceMonsterCommand(anchored, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(swapper, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());
            AssertCells(anchored, swapper, null, null, null);

            App.SendCommand(new LeaveShopCommand());
            App.SendCommand(new PlaceMonsterCommand(anchored, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(swapper, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            AssertCells(anchored, swapper, null, null, null);
        }

        [Test]
        public void 结束消灭在换位之后拿走且扣除仍计入它落地的能量()
        {
            var intents = new ClockIntents();
            Begin(intents, new ScriptedLevelCatalog(5), "换位手", "能量体");
            var swapper = IdOf("换位手");
            var victim = IdOf("能量体");
            intents.DestroyAtEnd(victim, 0);
            App.SendCommand(new PlaceMonsterCommand(victim, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(swapper, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            AssertCells(swapper, null, null, null, null);
            Assert.That(App.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id), Does.Not.Contain(victim));
            Assert.That(App.SendQuery(new RunLedgerQuery()).SkillSlots, Is.Empty);
            Assert.That(Landings().Single().Energy, Is.EqualTo(5));
            Assert.That(App.SendQuery(new RunPhaseQuery()), Is.EqualTo(RunPhase.Shop));
            Assert.That(App.SendQuery(new RunLedgerQuery()).Gold, Is.EqualTo(40));
        }

        [Test]
        public void 同一时刻登记的结束消灭按从左到右拿走()
        {
            var intents = new ClockIntents();
            Begin(intents, new ScriptedLevelCatalog(50), "能量体", "能量体");
            var left = Ids("能量体")[0];
            var right = Ids("能量体")[1];
            intents.DestroyAtEnd(right, 0);
            intents.DestroyAtEnd(left, 0);
            App.SendCommand(new PlaceMonsterCommand(left, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(right, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(RemovedIds(), Is.EqualTo(new[] { left, right }));
        }

        [Test]
        public void 更早登记的结束消灭先于左边的怪物拿走()
        {
            var intents = new ClockIntents();
            Begin(intents, new ScriptedLevelCatalog(50), "能量体", "能量体");
            var left = Ids("能量体")[0];
            var right = Ids("能量体")[1];
            intents.DestroyAtEnd(right, 0);
            intents.DestroyAtEnd(left, 1);
            App.SendCommand(new PlaceMonsterCommand(left, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(right, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            Assert.That(RemovedIds(), Is.EqualTo(new[] { right, left }));
        }

        [Test]
        public void 重触发让每个秒点的毒跳伤再结算一遍且不重走技能()
        {
            var intents = new ClockIntents();
            Begin(intents, new ScriptedLevelCatalog(50), "能量体");
            var host = IdOf("能量体");
            intents.Poison(host, 3);
            intents.Retrigger(host);
            App.SendCommand(new PlaceMonsterCommand(host, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            AssertLanding("能量体", "毒跳伤", "毒跳伤");
            Assert.That(Bases(), Is.EqualTo(new[] { 5, 3, 3 }));
            Assert.That(Energies(), Is.EqualTo(new[] { 5, 3, 3 }));
        }

        [Test]
        public void 多个重触发来源也只把毒跳伤再结算一遍()
        {
            var intents = new ClockIntents();
            Begin(intents, new ScriptedLevelCatalog(50), "能量体");
            var host = IdOf("能量体");
            intents.Poison(host, 3);
            intents.Retrigger(host);
            intents.Retrigger(host);
            intents.Retrigger(host);
            App.SendCommand(new PlaceMonsterCommand(host, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            AssertLanding("能量体", "毒跳伤", "毒跳伤");
            Assert.That(Bases(), Is.EqualTo(new[] { 5, 3, 3 }));
        }

        [Test]
        public void 两个秒点各自把毒跳伤再结算一遍()
        {
            var intents = new ClockIntents();
            Begin(intents, new ScriptedLevelCatalog(50), "能量体");
            var host = IdOf("能量体");
            intents.SecondPoints(host, 2);
            intents.Poison(host, 3);
            intents.Retrigger(host);
            App.SendCommand(new PlaceMonsterCommand(host, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            AssertLanding("能量体", "毒跳伤", "毒跳伤", "能量体", "毒跳伤", "毒跳伤");
            Assert.That(Bases(), Is.EqualTo(new[] { 5, 3, 3, 5, 3, 3 }));
            Assert.That(Energies(), Is.EqualTo(new[] { 5, 3, 3, 5, 3, 3 }));
        }

        [Test]
        public void 同一只怪物要等零点一逻辑秒才能再次被强制触发()
        {
            var intents = new ClockIntents();
            Begin(intents, new ScriptedLevelCatalog(50), "能量体");
            var host = IdOf("能量体");
            intents.ForceTrigger(host, 0);
            intents.ForceTrigger(host, 0.05);
            intents.ForceTrigger(host, 0.1);
            App.SendCommand(new PlaceMonsterCommand(host, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            AssertLanding("能量体", "能量体", "能量体");
            Assert.That(Bases(), Is.EqualTo(new[] { 5, 5, 5 }));
            Assert.That(Energies(), Is.EqualTo(new[] { 5, 5, 5 }));
        }

        [Test]
        public void 稍后逻辑时刻才落下的写回在落下前的计分里读不到()
        {
            var intents = new ClockIntents();
            Begin(intents, new ScriptedLevelCatalog(50), "双头能量体");
            var host = IdOf("双头能量体");
            intents.WriteLater(host, "双头能量体", 4, 1);
            intents.ForceTrigger(host, 0.5);
            intents.ForceTrigger(host, 2);
            App.SendCommand(new PlaceMonsterCommand(host, OperationArea.Extraction, 0));
            App.SendCommand(new ConfirmSettlementCommand());

            AssertLanding("双头能量体", "双头能量体", "双头能量体", "双头能量体", "双头能量体", "双头能量体");
            Assert.That(Bases(), Is.EqualTo(new[] { 2, 2, 2, 2, 6, 6 }));
            Assert.That(Energies(), Is.EqualTo(new[] { 2, 2, 2, 2, 6, 6 }));
        }

        [Test]
        public void 能量体和孤独心的下一次计分读技能实例上的报价()
        {
            var intents = new ClockIntents();
            Begin(intents, new ScriptedLevelCatalog(50), "能量体", "孤独心");
            var breathId = IdOf("能量体");
            var lonelyId = Ids("孤独心")[0];
            intents.WriteLater(breathId, "能量体", 4, 1);
            intents.WriteLater(lonelyId, "孤独心", 3, 1);
            intents.ForceTrigger(breathId, 0.5);
            intents.ForceTrigger(lonelyId, 0.5);
            intents.ForceTrigger(breathId, 2);
            intents.ForceTrigger(lonelyId, 2);
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(lonelyId, OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var breath = Landings().Where(landing => landing.MonsterId == breathId).Select(landing => landing.Base).ToArray();
            var lonely = Landings().Where(landing => landing.MonsterId == lonelyId).Select(landing => landing.Base).ToArray();
            Assert.That(breath, Is.EqualTo(new[] { 5, 5, 9 }));
            Assert.That(lonely, Is.Empty);
        }

        string PlacePair(ClockIntents intents, string leftSkill, string rightSkill)
        {
            Begin(intents, new ScriptedLevelCatalog(50), leftSkill, rightSkill);
            var leftId = IdOf(leftSkill);
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf(rightSkill), OperationArea.Extraction, 1));
            App.SendCommand(new ConfirmSettlementCommand());
            return leftId;
        }

        void Begin(ClockIntents intents, ILevelCatalog levels, string kept, params string[] alsoInCage)
        {
            var names = new string[9];
            names[0] = kept;
            names[1] = "奇异香";
            names[2] = "怪异香";
            for (var i = 0; i < 6; i++)
                names[3 + i] = i < alsoInCage.Length ? alsoInCage[i] : "孤独心";

            UseRules(new ScriptedDraw(names), levels, intents: intents);
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
        }

        void AssertCells(params string[] monsterIds)
        {
            Assert.That(
                App.SendQuery(new ExtractionSlotsQuery()).Select(cell => cell.MonsterId).ToArray(),
                Is.EqualTo(monsterIds));
        }

        void AssertLanding(params string[] skillNames)
        {
            Assert.That(
                Landings().Select(landing => landing.SkillName).ToArray(),
                Is.EqualTo(skillNames));
        }

        int[] Bases() =>
            Landings().Select(landing => landing.Base).ToArray();

        int[] Energies() =>
            Landings().Select(landing => landing.Energy).ToArray();

        string IdOf(string skillName) => Ids(skillName).Single();

        string[] Ids(string skillName) =>
            App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Count == 1 && monster.SkillNames[0] == skillName)
                .Select(monster => monster.Id)
                .ToArray();
    }
}
