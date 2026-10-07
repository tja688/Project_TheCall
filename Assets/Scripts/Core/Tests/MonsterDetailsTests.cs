using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class MonsterDetailsTests : RulesFixture
    {
        [Test]
        public void 左能量体的详情是主动白字且句子来自效果说明()
        {
            var monster = App.SendQuery(new OpeningCandidatesQuery())
                .Single(candidate => candidate.SkillNames.Single() == "左能量体");

            var details = App.SendQuery(new MonsterDetailsQuery(monster.Id));

            Assert.That(details.Skills[0].Sentence, Is.EqualTo("右侧每有一个怪物产生2点能量，加减加在这次总产出上"));
            Assert.That(details.Skills[0].Kind, Is.EqualTo(SkillUse.Active));
            Assert.That(details.Skills[0].Rarity, Is.EqualTo(Rarity.White));
        }

        [Test]
        public void 未知怪物id的详情是空()
        {
            Assert.That(App.SendQuery(new MonsterDetailsQuery("missing")), Is.Null);
        }

        [Test]
        public void 文案表的名字与技能目录相同且能量体有句子()
        {
            var catalog = App.GetUtility<SkillCatalog>();
            var copy = App.GetUtility<SkillCopy>();

            Assert.That(copy.Names, Is.EqualTo(catalog.Names));
            Assert.That(copy.TryDescribe("能量体", out var kind, out var sentence), Is.True);
            Assert.That(kind, Is.EqualTo(SkillUse.Active));
            Assert.That(sentence, Is.EqualTo("产生5点能量"));
        }

        [Test]
        public void 句子片段拼回原文且只有产出能量的数单独成段()
        {
            foreach (var skill in ContentGate.Current.Skills)
            {
                var joined = string.Concat(SkillSentences.Pieces(skill).Select(piece =>
                    piece.IsEnergy ? piece.Energy.ToString() : piece.Text));
                Assert.That(joined, Is.EqualTo(SkillSentences.Format(skill)), skill.Name);
            }

            var breath = SkillSentences.Pieces(ContentGate.Current.FindSkill("能量体"));
            Assert.That(breath.Single(piece => piece.IsEnergy).Energy, Is.EqualTo(5));

            var small = SkillSentences.Pieces(ContentGate.Current.FindSkill("奇异香"));
            Assert.That(small.Any(piece => piece.IsEnergy), Is.False);

            var share = SkillSentences.Pieces(ContentGate.Current.FindSkill("蜜能量体"));
            Assert.That(share.Single(piece => piece.IsEnergy).Energy, Is.EqualTo(2));
        }

        [Test]
        public void 吞噬大嘴的句子没有星号()
        {
            App.SendCommand(new KeepOpeningMonsterCommand(App.SendQuery(new OpeningCandidatesQuery())[0].Id));
            var devourer = App.SendQuery(new MonsterCageQuery())
                .Single(monster => monster.SkillNames.Single() == "吞噬大嘴");

            var details = App.SendQuery(new MonsterDetailsQuery(devourer.Id));

            Assert.That(
                details.Skills[0].Sentence,
                Is.EqualTo("消灭随机一只相邻怪物，本怪物产能类型的技能数值永久+2"));
        }
    }
}
