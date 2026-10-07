using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class SideScoreTests : RulesFixture
    {
        [Test]
        public void 左能量体按右侧现在还有的每只怪物落地两点()
        {
            Open(
                "左能量体",
                "能量吐息",
                "能量吐息",
                "吞噬大嘴",
                "双重吐息",
                "残留提取腺体",
                "时间操控器官",
                "换位手",
                "分享之手");

            var leftId = IdOf("左能量体");
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("吞噬大嘴"), OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(IdOf("双重吐息"), OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single(item => item.SkillName == "左能量体");
            Assert.That(landing.MonsterId, Is.EqualTo(leftId));
            Assert.That(landing.Base, Is.EqualTo(4));
            Assert.That(landing.Multiplier, Is.EqualTo(1));
            Assert.That(landing.Energy, Is.EqualTo(4));
        }

        [Test]
        public void 右能量体按左侧现在还有的每只怪物落地两点()
        {
            Open(
                "能量吐息",
                "左能量体",
                "能量吐息",
                "右能量体",
                "吞噬大嘴",
                "双重吐息",
                "残留提取腺体",
                "时间操控器官",
                "换位手");

            var rightId = IdOf("右能量体");
            App.SendCommand(new PlaceMonsterCommand(IdOf("残留提取腺体"), OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("双重吐息"), OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(rightId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single(item => item.SkillName == "右能量体");
            Assert.That(landing.MonsterId, Is.EqualTo(rightId));
            Assert.That(landing.Base, Is.EqualTo(4));
            Assert.That(landing.Multiplier, Is.EqualTo(1));
            Assert.That(landing.Energy, Is.EqualTo(4));
        }

        [Test]
        public void 中间空档不取消更远的怪物()
        {
            Open(
                "左能量体",
                "能量吐息",
                "能量吐息",
                "吞噬大嘴",
                "右能量体",
                "双重吐息",
                "残留提取腺体",
                "时间操控器官",
                "换位手");

            var leftId = IdOf("左能量体");
            var rightId = IdOf("右能量体");
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("残留提取腺体"), OperationArea.Extraction, 3));
            App.SendCommand(new PlaceMonsterCommand(rightId, OperationArea.Extraction, 4));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            var left = landings.Single(item => item.SkillName == "左能量体");
            var right = landings.Single(item => item.SkillName == "右能量体");
            Assert.That(left.MonsterId, Is.EqualTo(leftId));
            Assert.That(left.Base, Is.EqualTo(4));
            Assert.That(left.Multiplier, Is.EqualTo(1));
            Assert.That(left.Energy, Is.EqualTo(4));
            Assert.That(right.MonsterId, Is.EqualTo(rightId));
            Assert.That(right.Base, Is.EqualTo(4));
            Assert.That(right.Multiplier, Is.EqualTo(1));
            Assert.That(right.Energy, Is.EqualTo(4));
        }

        [Test]
        public void 有相邻怪物时孤独心落地四点()
        {
            Open(
                "孤独心",
                "能量吐息",
                "能量吐息",
                "吞噬大嘴",
                "双重吐息",
                "残留提取腺体",
                "时间操控器官",
                "换位手",
                "分享之手");

            var lonelyId = IdOf("孤独心");
            App.SendCommand(new PlaceMonsterCommand(lonelyId, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(IdOf("吞噬大嘴"), OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single(item => item.SkillName == "孤独心");
            Assert.That(landing.MonsterId, Is.EqualTo(lonelyId));
            Assert.That(landing.Base, Is.EqualTo(4));
            Assert.That(landing.Multiplier, Is.EqualTo(1));
            Assert.That(landing.Energy, Is.EqualTo(4));
        }

        [Test]
        public void 没有相邻怪物时孤独心这次能量翻倍()
        {
            Open(
                "孤独心",
                "能量吐息",
                "能量吐息",
                "吞噬大嘴",
                "双重吐息",
                "残留提取腺体",
                "时间操控器官",
                "换位手",
                "分享之手");

            var lonelyId = IdOf("孤独心");
            App.SendCommand(new PlaceMonsterCommand(lonelyId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single(item => item.SkillName == "孤独心");
            Assert.That(landing.MonsterId, Is.EqualTo(lonelyId));
            Assert.That(landing.Base, Is.EqualTo(4));
            Assert.That(landing.Multiplier, Is.EqualTo(2));
            Assert.That(landing.Energy, Is.EqualTo(8));
        }

        [Test]
        public void 相邻只算格子挨着的怪物()
        {
            Open(
                "孤独心",
                "能量吐息",
                "能量吐息",
                "吞噬大嘴",
                "双重吐息",
                "残留提取腺体",
                "时间操控器官",
                "换位手",
                "分享之手");

            var lonelyId = IdOf("孤独心");
            App.SendCommand(new PlaceMonsterCommand(lonelyId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("吞噬大嘴"), OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single(item => item.SkillName == "孤独心");
            Assert.That(landing.MonsterId, Is.EqualTo(lonelyId));
            Assert.That(landing.Base, Is.EqualTo(4));
            Assert.That(landing.Multiplier, Is.EqualTo(2));
            Assert.That(landing.Energy, Is.EqualTo(8));
        }

        [Test]
        public void 增量小手给其他怪物的产能量计分加一且不加给自己() =>
            AssertAuraLeavesItsHostAtFive("增量小手", 6);

        [Test]
        public void 增量大手给其他怪物的产能量计分加二且不加给自己() =>
            AssertAuraLeavesItsHostAtFive("增量大手", 7);

        [Test]
        public void 外部加法进入底数外部乘法进入倍率()
        {
            Open(
                "孤独心",
                "能量吐息",
                "能量吐息",
                "增量小手",
                "增量大手",
                "吞噬大嘴",
                "双重吐息",
                "残留提取腺体",
                "时间操控器官");

            var lonelyId = IdOf("孤独心");
            App.SendCommand(new PlaceMonsterCommand(lonelyId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("增量小手"), OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(IdOf("增量大手"), OperationArea.Extraction, 4));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single(item => item.SkillName == "孤独心");
            Assert.That(landing.MonsterId, Is.EqualTo(lonelyId));
            Assert.That(landing.Base, Is.EqualTo(7));
            Assert.That(landing.Multiplier, Is.EqualTo(2));
            Assert.That(landing.Energy, Is.EqualTo(14));
        }

        [Test]
        public void 增量加进左右计数的底数()
        {
            Open(
                "左能量体",
                "能量吐息",
                "能量吐息",
                "增量小手",
                "吞噬大嘴",
                "双重吐息",
                "残留提取腺体",
                "时间操控器官",
                "换位手");

            var leftId = IdOf("左能量体");
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("增量小手"), OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single(item => item.SkillName == "左能量体");
            Assert.That(landing.MonsterId, Is.EqualTo(leftId));
            Assert.That(landing.Base, Is.EqualTo(3));
            Assert.That(landing.Multiplier, Is.EqualTo(1));
            Assert.That(landing.Energy, Is.EqualTo(3));
        }

        [Test]
        public void 左右计数的加减先进入每只两点再乘只数且残留只按这一次发动插队()
        {
            Open(
                "左能量体",
                "能量吐息",
                "能量吐息",
                "增量小手",
                "残留提取腺体",
                "分享之手",
                "右能量体",
                "孤独心",
                "吞噬大嘴");

            var leftId = IdOf("左能量体");
            var shareId = IdOf("分享之手");
            var glandId = IdOf("残留提取腺体");
            App.SendCommand(new PlaceMonsterCommand(shareId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(IdOf("增量小手"), OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(glandId, OperationArea.Extraction, 3));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(
                landings.Select(item => item.SkillName).ToArray(),
                Is.EqualTo(new[] { "分享之手", "残留提取腺体", "左能量体", "残留提取腺体" }));
            Assert.That(landings.Select(item => item.Base).ToArray(), Is.EqualTo(new[] { 3, 2, 12, 2 }));
            Assert.That(landings.Select(item => item.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1, 1 }));
            Assert.That(landings.Select(item => item.Energy).ToArray(), Is.EqualTo(new[] { 3, 2, 12, 2 }));
            Assert.That(landings[2].MonsterId, Is.EqualTo(leftId));
        }

        [Test]
        public void 右能量体的加减也先进入每只两点再乘左侧只数()
        {
            Open(
                "右能量体",
                "孤独心",
                "吞噬大嘴",
                "能量吐息",
                "增量小手",
                "残留提取腺体",
                "双重吐息",
                "分享之手",
                "左能量体");

            var rightId = IdOf("右能量体");
            App.SendCommand(new PlaceMonsterCommand(IdOf("能量吐息"), OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("增量小手"), OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(rightId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var right = Landings().Single(item => item.SkillName == "右能量体");
            Assert.That(right.MonsterId, Is.EqualTo(rightId));
            Assert.That(right.Base, Is.EqualTo(6));
            Assert.That(right.Multiplier, Is.EqualTo(1));
            Assert.That(right.Energy, Is.EqualTo(6));
        }

        [Test]
        public void 增量翻倍和额外次数按每只两点结算为四十八且没有右侧时加成为零()
        {
            Open(
                "左能量体",
                "能量吐息",
                "能量吐息",
                "左能量体",
                "鼓励嘴",
                "时间操控器官",
                "增量小手",
                "残留提取腺体",
                "孤独心");

            var bodies = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "左能量体")
                .Select(monster => monster.Id)
                .ToArray();
            var handId = IdOf("增量小手");
            App.SendCommand(new PlaceMonsterCommand(bodies[0], OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("鼓励嘴"), OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(bodies[1], OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(IdOf("时间操控器官"), OperationArea.Extraction, 3));
            App.SendCommand(new PlaceMonsterCommand(handId, OperationArea.Extraction, 4));
            App.SendCommand(new ConfirmSettlementCommand());

            var produced = Landings().Where(item => item.SkillName == "左能量体").ToArray();
            Assert.That(produced.Select(item => item.MonsterId).ToArray(), Is.EqualTo(new[] { bodies[0], bodies[1], bodies[1] }));
            Assert.That(produced.Select(item => item.Base).ToArray(), Is.EqualTo(new[] { 12, 6, 6 }));
            Assert.That(produced.Select(item => item.Multiplier).ToArray(), Is.EqualTo(new[] { 2, 2, 2 }));
            Assert.That(produced.Select(item => item.Energy).ToArray(), Is.EqualTo(new[] { 24, 12, 12 }));
            Assert.That(produced.Sum(item => item.Energy), Is.EqualTo(48));

            App.SendCommand(new ReturnMonsterCommand(bodies[0]));
            App.SendCommand(new ReturnMonsterCommand(handId));
            App.SendCommand(new PlaceMonsterCommand(handId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(bodies[0], OperationArea.Extraction, 4));
            App.SendCommand(new ConfirmSettlementCommand());

            var alone = Landings().Single(item => item.MonsterId == bodies[0]);
            Assert.That(alone.SkillName, Is.EqualTo("左能量体"));
            Assert.That(alone.Base, Is.EqualTo(0));
            Assert.That(alone.Energy, Is.EqualTo(0));
        }

        [Test]
        public void 培育槽上的增量光环不改提取计分()
        {
            Open(
                "能量吐息",
                "左能量体",
                "右能量体",
                "增量小手",
                "增量大手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴",
                "双重吐息");

            var breathId = IdOf("能量吐息");
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("增量小手"), OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("增量大手"), OperationArea.Breeding, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single();
            Assert.That(landing.MonsterId, Is.EqualTo(breathId));
            Assert.That(landing.SkillName, Is.EqualTo("能量吐息"));
            Assert.That(landing.Base, Is.EqualTo(5));
            Assert.That(landing.Multiplier, Is.EqualTo(1));
            Assert.That(landing.Energy, Is.EqualTo(5));
        }

        [Test]
        public void 孤独心翻倍只作用于这次计分()
        {
            Open(
                "能量吐息",
                "左能量体",
                "右能量体",
                "孤独心",
                "增量小手",
                "吞噬大嘴",
                "双重吐息",
                "残留提取腺体",
                "时间操控器官");

            var cage = App.SendQuery(new MonsterCageQuery());
            var hostId = cage[0].Id;
            App.SendCommand(new DiscardMonsterCommand(cage[1].Id));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 3));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(item => item.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息", "孤独心" }));
            Assert.That(landings.Select(item => item.Base).ToArray(), Is.EqualTo(new[] { 5, 4 }));
            Assert.That(landings.Select(item => item.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(landings.Select(item => item.Energy).ToArray(), Is.EqualTo(new[] { 5, 8 }));
        }

        void AssertAuraLeavesItsHostAtFive(string aura, int otherBase)
        {
            Open(
                "能量吐息",
                "左能量体",
                "右能量体",
                aura,
                "能量吐息",
                "吞噬大嘴",
                "双重吐息",
                "残留提取腺体",
                "孤独心");

            var cage = App.SendQuery(new MonsterCageQuery());
            var hostId = cage[0].Id;
            var donorId = cage[1].Id;
            var otherId = cage[2].Id;
            App.SendCommand(new DiscardMonsterCommand(donorId));
            App.SendCommand(new EquipSkillCommand(hostId, 0));
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(otherId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(item => item.MonsterId).ToArray(), Is.EqualTo(new[] { hostId, otherId }));
            Assert.That(landings.Select(item => item.SkillName).ToArray(), Is.EqualTo(new[] { "能量吐息", "能量吐息" }));
            Assert.That(landings.Select(item => item.Base).ToArray(), Is.EqualTo(new[] { 5, otherBase }));
            Assert.That(landings.Select(item => item.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(item => item.Energy).ToArray(), Is.EqualTo(new[] { 5, otherBase }));
        }

        [Test]
        public void 鼓励嘴让这一刻相邻怪物的产能量计分翻倍且不翻自己和隔格()
        {
            Open(
                "鼓励嘴",
                "左能量体",
                "右能量体",
                "能量吐息",
                "能量吐息",
                "增量小手",
                "残留提取腺体",
                "孤独心",
                "吞噬大嘴");

            var mouthId = IdOf("鼓励嘴");
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => monster.SkillNames.Single() == "能量吐息")
                .Select(monster => monster.Id)
                .ToArray();
            App.SendCommand(new PlaceMonsterCommand(mouthId, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(breaths[0], OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(breaths[1], OperationArea.Extraction, 3));
            App.SendCommand(new ConfirmSettlementCommand());

            var adjacent = Landings().Single(landing => landing.MonsterId == breaths[0]);
            var distant = Landings().Single(landing => landing.MonsterId == breaths[1]);
            Assert.That(adjacent.Base, Is.EqualTo(5));
            Assert.That(adjacent.Multiplier, Is.EqualTo(2));
            Assert.That(adjacent.Energy, Is.EqualTo(10));
            Assert.That(distant.Base, Is.EqualTo(5));
            Assert.That(distant.Multiplier, Is.EqualTo(1));
            Assert.That(distant.Energy, Is.EqualTo(5));
            Assert.That(Landings().Any(landing => landing.MonsterId == mouthId), Is.False);
        }

        void Open(params string[] names)
        {
            UseDraw(names);
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
        }

        string IdOf(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => monster.SkillNames.Single() == skillName).Id;
    }
}
