using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class SkillIndexTests : RulesFixture
    {
        [Test]
        public void 技能图鉴按目录顺序列出每一项且字段与悬停栏相同()
        {
            var index = App.SendQuery(new SkillIndexQuery());
            var names = App.GetUtility<SkillCatalog>().Names();
            Assert.That(index.Count, Is.EqualTo(names.Count));
            for (var i = 0; i < names.Count; i++)
                Assert.That(index[i].Name, Is.EqualTo(names[i]), names[i]);

            var breath = Find(index, "能量体");
            Assert.That(breath.Kind, Is.EqualTo(SkillUse.Active));
            Assert.That(breath.Rarity, Is.EqualTo(Rarity.White));
            Assert.That(breath.Function, Is.EqualTo("产能"));
            Assert.That(breath.Sentence, Is.EqualTo("产生5点能量"));
            Assert.That(breath.DisplaySentence, Is.EqualTo(breath.Sentence));

            var honey = Find(index, "蜜能量体");
            Assert.That(honey.Function, Is.EqualTo("产能，辅助"));
            Assert.That(honey.Sentence, Is.EqualTo(SkillSentences.Format(ContentGate.Current.FindSkill("蜜能量体"))));

            var echo = Find(index, "回响嗓");
            Assert.That(echo.Kind, Is.EqualTo(SkillUse.Passive));
            Assert.That(echo.Rarity, Is.EqualTo(Rarity.Gold));
            Assert.That(echo.Function, Is.EqualTo("辅助"));
            Assert.That(echo.Sentence, Is.EqualTo(SkillSentences.Format(ContentGate.Current.FindSkill("回响嗓"))));

            Assert.That(Has(index, Rarity.White), Is.True);
            Assert.That(Has(index, Rarity.Blue), Is.True);
            Assert.That(Has(index, Rarity.Gold), Is.True);
        }

        static MonsterSkillDetail Find(System.Collections.Generic.IReadOnlyList<MonsterSkillDetail> index, string name)
        {
            for (var i = 0; i < index.Count; i++)
                if (index[i].Name == name)
                    return index[i];

            Assert.Fail("图鉴里没有 " + name);
            return null;
        }

        static bool Has(System.Collections.Generic.IReadOnlyList<MonsterSkillDetail> index, Rarity rarity)
        {
            for (var i = 0; i < index.Count; i++)
                if (index[i].Rarity == rarity)
                    return true;

            return false;
        }
    }
}
