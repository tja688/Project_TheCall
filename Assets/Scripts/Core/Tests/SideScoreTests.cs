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
                "能量体",
                "能量体",
                "吞噬大嘴",
                "双头能量体",
                "汲取鼻",
                "左复制腺体",
                "换位手",
                "蜜能量体");

            var leftId = IdOf("左能量体");
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("吞噬大嘴"), OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(IdOf("双头能量体"), OperationArea.Extraction, 2));
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
                "能量体",
                "左能量体",
                "双头能量体",
                "右能量体",
                "吞噬大嘴",
                "双头能量体",
                "汲取鼻",
                "左复制腺体",
                "换位手");

            var rightId = IdOf("右能量体");
            App.SendCommand(new PlaceMonsterCommand(IdOf("汲取鼻"), OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("双头能量体"), OperationArea.Extraction, 1));
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
                "能量体",
                "能量体",
                "吞噬大嘴",
                "右能量体",
                "双头能量体",
                "汲取鼻",
                "左复制腺体",
                "换位手");

            var leftId = IdOf("左能量体");
            var rightId = IdOf("右能量体");
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("汲取鼻"), OperationArea.Extraction, 3));
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
        public void 有相邻怪物时孤独心不翻倍()
        {
            Open(
                "孤独心",
                "双头能量体",
                "蜜能量体",
                "能量体",
                "换位手",
                "汲取鼻",
                "左复制腺体",
                "能量体",
                "奇异香");

            var hostId = IdOf("孤独心");
            App.SendCommand(new DiscardMonsterCommand(FirstId("能量体")));
            InstallFromSlot(hostId);
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(IdOf("换位手"), OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single(item => item.MonsterId == hostId);
            Assert.That(landing.SkillName, Is.EqualTo("能量体"));
            Assert.That(landing.Base, Is.EqualTo(5));
            Assert.That(landing.Multiplier, Is.EqualTo(1));
            Assert.That(landing.Energy, Is.EqualTo(5));
        }

        [Test]
        public void 没有相邻怪物时孤独心这次能量翻倍()
        {
            Open(
                "孤独心",
                "双头能量体",
                "蜜能量体",
                "能量体",
                "换位手",
                "汲取鼻",
                "左复制腺体",
                "能量体",
                "奇异香");

            var hostId = IdOf("孤独心");
            App.SendCommand(new DiscardMonsterCommand(FirstId("能量体")));
            InstallFromSlot(hostId);
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single(item => item.MonsterId == hostId);
            Assert.That(landing.SkillName, Is.EqualTo("能量体"));
            Assert.That(landing.Base, Is.EqualTo(5));
            Assert.That(landing.Multiplier, Is.EqualTo(2));
            Assert.That(landing.Energy, Is.EqualTo(10));
        }

        [Test]
        public void 相邻只算格子挨着的怪物()
        {
            Open(
                "吞噬大嘴",
                "双头能量体",
                "蜜能量体",
                "能量体",
                "孤独心",
                "换位手",
                "汲取鼻",
                "左复制腺体",
                "能量体");

            var hostId = FirstId("能量体");
            App.SendCommand(new DiscardMonsterCommand(IdOf("孤独心")));
            InstallFromSlot(hostId);
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("换位手"), OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single(item => item.MonsterId == hostId);
            Assert.That(landing.SkillName, Is.EqualTo("能量体"));
            Assert.That(landing.Base, Is.EqualTo(5));
            Assert.That(landing.Multiplier, Is.EqualTo(2));
            Assert.That(landing.Energy, Is.EqualTo(10));
        }

        [Test]
        public void 奇异香给其他怪物的产能量计分加一且不加给自己() =>
            AssertAuraLeavesItsHostAtFive("奇异香", 6);

        [Test]
        public void 怪异香给其他怪物的产能量计分加二且不加给自己() =>
            AssertAuraLeavesItsHostAtFive("怪异香", 7);

        [Test]
        public void 外部加法进入底数外部乘法进入倍率()
        {
            Open(
                "双头能量体",
                "汲取鼻",
                "左复制腺体",
                "能量体",
                "孤独心",
                "奇异香",
                "怪异香",
                "吞噬大嘴",
                "蜜能量体");

            var hostId = FirstId("能量体");
            App.SendCommand(new DiscardMonsterCommand(IdOf("孤独心")));
            InstallFromSlot(hostId);
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("奇异香"), OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(IdOf("怪异香"), OperationArea.Extraction, 4));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single(item => item.MonsterId == hostId);
            Assert.That(landing.SkillName, Is.EqualTo("能量体"));
            Assert.That(landing.Base, Is.EqualTo(8));
            Assert.That(landing.Multiplier, Is.EqualTo(2));
            Assert.That(landing.Energy, Is.EqualTo(16));
        }

        [Test]
        public void 增量加进左右计数的底数()
        {
            Open(
                "左能量体",
                "能量体",
                "能量体",
                "奇异香",
                "吞噬大嘴",
                "双头能量体",
                "汲取鼻",
                "左复制腺体",
                "换位手");

            var leftId = IdOf("左能量体");
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("奇异香"), OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single(item => item.SkillName == "左能量体");
            Assert.That(landing.MonsterId, Is.EqualTo(leftId));
            Assert.That(landing.Base, Is.EqualTo(3));
            Assert.That(landing.Multiplier, Is.EqualTo(1));
            Assert.That(landing.Energy, Is.EqualTo(3));
        }

        [Test]
        public void 左右计数的加减加在总产出上且残留只按这一次发动插队()
        {
            Open(
                "左能量体",
                "能量体",
                "能量体",
                "奇异香",
                "汲取鼻",
                "蜜能量体",
                "右能量体",
                "孤独心",
                "吞噬大嘴");

            var leftId = IdOf("左能量体");
            var shareId = IdOf("蜜能量体");
            var glandId = IdOf("汲取鼻");
            App.SendCommand(new PlaceMonsterCommand(shareId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(leftId, OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(IdOf("奇异香"), OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(glandId, OperationArea.Extraction, 3));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(
                landings.Select(item => item.SkillName).ToArray(),
                Is.EqualTo(new[] { "蜜能量体", "汲取鼻", "左能量体", "汲取鼻" }));
            Assert.That(landings.Select(item => item.Base).ToArray(), Is.EqualTo(new[] { 3, 2, 8, 2 }));
            Assert.That(landings.Select(item => item.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1, 1, 1 }));
            Assert.That(landings.Select(item => item.Energy).ToArray(), Is.EqualTo(new[] { 3, 2, 8, 2 }));
            Assert.That(landings[2].MonsterId, Is.EqualTo(leftId));
        }

        [Test]
        public void 右能量体的加减加在左侧人数乘完后的总产出上()
        {
            Open(
                "右能量体",
                "孤独心",
                "吞噬大嘴",
                "能量体",
                "奇异香",
                "汲取鼻",
                "双头能量体",
                "蜜能量体",
                "左能量体");

            var rightId = IdOf("右能量体");
            App.SendCommand(new PlaceMonsterCommand(IdOf("能量体"), OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("奇异香"), OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(rightId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var right = Landings().Single(item => item.SkillName == "右能量体");
            Assert.That(right.MonsterId, Is.EqualTo(rightId));
            Assert.That(right.Base, Is.EqualTo(5));
            Assert.That(right.Multiplier, Is.EqualTo(1));
            Assert.That(right.Energy, Is.EqualTo(5));
        }

        [Test]
        public void 增量翻倍和额外次数结算为三十八且没有右侧时只剩外部加一()
        {
            Open(
                "左能量体",
                "能量体",
                "能量体",
                "左能量体",
                "镜眼",
                "左复制腺体",
                "奇异香",
                "汲取鼻",
                "孤独心");

            var bodies = App.SendQuery(new MonsterCageQuery())
                .Where(monster => Holds(monster, "左能量体"))
                .Select(monster => monster.Id)
                .ToArray();
            var handId = IdOf("奇异香");
            App.SendCommand(new PlaceMonsterCommand(bodies[0], OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("镜眼"), OperationArea.Extraction, 1));
            App.SendCommand(new PlaceMonsterCommand(bodies[1], OperationArea.Extraction, 2));
            App.SendCommand(new PlaceMonsterCommand(IdOf("左复制腺体"), OperationArea.Extraction, 3));
            App.SendCommand(new PlaceMonsterCommand(handId, OperationArea.Extraction, 4));
            App.SendCommand(new ConfirmSettlementCommand());

            var produced = Landings().Where(item => item.SkillName == "左能量体").ToArray();
            Assert.That(produced.Select(item => item.MonsterId).ToArray(), Is.EqualTo(new[] { bodies[0], bodies[1], bodies[1] }));
            Assert.That(produced.Select(item => item.Base).ToArray(), Is.EqualTo(new[] { 9, 5, 5 }));
            Assert.That(produced.Select(item => item.Multiplier).ToArray(), Is.EqualTo(new[] { 2, 2, 2 }));
            Assert.That(produced.Select(item => item.Energy).ToArray(), Is.EqualTo(new[] { 18, 10, 10 }));
            Assert.That(produced.Sum(item => item.Energy), Is.EqualTo(38));

            App.SendCommand(new ReturnMonsterCommand(bodies[0]));
            App.SendCommand(new ReturnMonsterCommand(handId));
            App.SendCommand(new PlaceMonsterCommand(handId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(bodies[0], OperationArea.Extraction, 4));
            App.SendCommand(new ConfirmSettlementCommand());

            var alone = Landings().Single(item => item.MonsterId == bodies[0]);
            Assert.That(alone.SkillName, Is.EqualTo("左能量体"));
            Assert.That(alone.Base, Is.EqualTo(1));
            Assert.That(alone.Energy, Is.EqualTo(1));
        }

        [Test]
        public void 培育槽上的增量光环不改提取计分()
        {
            Open(
                "能量体",
                "左能量体",
                "右能量体",
                "奇异香",
                "怪异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴",
                "双头能量体");

            var breathId = IdOf("能量体");
            App.SendCommand(new PlaceMonsterCommand(breathId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("奇异香"), OperationArea.Breeding, 0));
            App.SendCommand(new PlaceMonsterCommand(IdOf("怪异香"), OperationArea.Breeding, 1));
            App.SendCommand(new ConfirmSettlementCommand());

            var landing = Landings().Single();
            Assert.That(landing.MonsterId, Is.EqualTo(breathId));
            Assert.That(landing.SkillName, Is.EqualTo("能量体"));
            Assert.That(landing.Base, Is.EqualTo(5));
            Assert.That(landing.Multiplier, Is.EqualTo(1));
            Assert.That(landing.Energy, Is.EqualTo(5));
        }

        [Test]
        public void 孤独心翻倍只作用于这次计分()
        {
            Open(
                "能量体",
                "左能量体",
                "右能量体",
                "孤独心",
                "奇异香",
                "吞噬大嘴",
                "双头能量体",
                "汲取鼻",
                "左复制腺体");

            var cage = App.SendQuery(new MonsterCageQuery());
            var hostId = cage[0].Id;
            App.SendCommand(new DiscardMonsterCommand(cage[1].Id));
            InstallFromSlot(hostId);
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 3));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(item => item.SkillName).ToArray(), Is.EqualTo(new[] { "能量体" }));
            Assert.That(landings.Select(item => item.Base).ToArray(), Is.EqualTo(new[] { 5 }));
            Assert.That(landings.Select(item => item.Multiplier).ToArray(), Is.EqualTo(new[] { 2 }));
            Assert.That(landings.Select(item => item.Energy).ToArray(), Is.EqualTo(new[] { 10 }));
        }

        void AssertAuraLeavesItsHostAtFive(string aura, int otherBase)
        {
            Open(
                "能量体",
                "左能量体",
                "右能量体",
                aura,
                "能量体",
                "吞噬大嘴",
                "双头能量体",
                "汲取鼻",
                "孤独心");

            var cage = App.SendQuery(new MonsterCageQuery());
            var hostId = cage[0].Id;
            var donorId = cage[1].Id;
            var otherId = cage[2].Id;
            App.SendCommand(new DiscardMonsterCommand(donorId));
            InstallFromSlot(hostId);
            App.SendCommand(new PlaceMonsterCommand(hostId, OperationArea.Extraction, 0));
            App.SendCommand(new PlaceMonsterCommand(otherId, OperationArea.Extraction, 2));
            App.SendCommand(new ConfirmSettlementCommand());

            var landings = Landings();
            Assert.That(landings.Select(item => item.MonsterId).ToArray(), Is.EqualTo(new[] { hostId, otherId }));
            Assert.That(landings.Select(item => item.SkillName).ToArray(), Is.EqualTo(new[] { "能量体", "能量体" }));
            Assert.That(landings.Select(item => item.Base).ToArray(), Is.EqualTo(new[] { 5, otherBase }));
            Assert.That(landings.Select(item => item.Multiplier).ToArray(), Is.EqualTo(new[] { 1, 1 }));
            Assert.That(landings.Select(item => item.Energy).ToArray(), Is.EqualTo(new[] { 5, otherBase }));
        }

        [Test]
        public void 镜眼让这一刻相邻怪物的产能量计分翻倍且不翻自己和隔格()
        {
            Open(
                "镜眼",
                "左能量体",
                "右能量体",
                "能量体",
                "能量体",
                "奇异香",
                "汲取鼻",
                "孤独心",
                "吞噬大嘴");

            var mouthId = IdOf("镜眼");
            var breaths = App.SendQuery(new MonsterCageQuery())
                .Where(monster => Holds(monster, "能量体"))
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
            KeepOpened();
        }

        string IdOf(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).Single(monster => Holds(monster, skillName)).Id;

        string FirstId(string skillName) =>
            App.SendQuery(new MonsterCageQuery()).First(monster => Holds(monster, skillName)).Id;
    }
}
