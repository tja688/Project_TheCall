using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class SettlementSightTests : RulesFixture
    {
        [Test]
        public void 演出换位后悬停句子按新格子而不是终局棋盘()
        {
            KeepOpened();
            var left = Fresh("左能量体");
            var scent = Fresh("奇异香");
            Place(left, 0);
            Place(scent, 1);
            Assert.That(Sentence(left, "左能量体"), Is.EqualTo("右侧每有一个怪物产生3点能量"));

            App.GetModel<SettlementSight>().Capture(App.GetModel<LevelModel>(), App.GetModel<RunModel>());
            App.SendCommand(new ShowSettlementSightCommand());
            App.SendCommand(new MoveSettlementSightCommand(0, 1));

            Assert.That(Sentence(left, "左能量体"), Is.EqualTo("右侧每有一个怪物产生2点能量"));

            App.SendCommand(new HideSettlementSightCommand());
            Assert.That(Sentence(left, "左能量体"), Is.EqualTo("右侧每有一个怪物产生3点能量"));
        }

        [Test]
        public void 结算删掉的怪物在演出格子清空前仍能读出技能()
        {
            KeepOpened();
            var left = Fresh("左能量体");
            var scent = Fresh("奇异香");
            Place(left, 0);
            Place(scent, 1);
            App.GetModel<SettlementSight>().Capture(App.GetModel<LevelModel>(), App.GetModel<RunModel>());
            App.GetModel<LevelModel>().TryRemove(scent);
            Assert.That(App.GetModel<RunModel>().TryDestroy(scent), Is.True);

            Assert.That(App.SendQuery(new MonsterDetailsQuery(scent)), Is.Null);
            Assert.That(Sentence(left, "左能量体"), Is.EqualTo("右侧每有一个怪物产生2点能量"));

            App.SendCommand(new ShowSettlementSightCommand());
            var gone = App.SendQuery(new MonsterDetailsQuery(scent));
            Assert.That(gone, Is.Not.Null);
            Assert.That(gone.Skills.Single().Name, Is.EqualTo("奇异香"));
            Assert.That(Sentence(left, "左能量体"), Is.EqualTo("右侧每有一个怪物产生3点能量"));

            App.SendCommand(new ClearSettlementSightCommand(1));
            Assert.That(Sentence(left, "左能量体"), Is.EqualTo("右侧每有一个怪物产生2点能量"));

            App.SendCommand(new HideSettlementSightCommand());
            Assert.That(App.SendQuery(new MonsterDetailsQuery(scent)), Is.Null);
        }

        string Fresh(params string[] names)
        {
            var run = App.GetModel<RunModel>();
            var monster = run.CreateMonster(names);
            run.AddToCage(monster);
            return monster.Id;
        }

        void Place(string monsterId, int cell) =>
            App.SendCommand(new PlaceMonsterCommand(monsterId, OperationArea.Extraction, cell));

        string Sentence(string monsterId, string skillName)
        {
            var details = App.SendQuery(new MonsterDetailsQuery(monsterId));
            Assert.That(details, Is.Not.Null);
            return details.Skills.Single(skill => skill.Name == skillName).Sentence;
        }
    }
}
