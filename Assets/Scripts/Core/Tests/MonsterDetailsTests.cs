using System;
using System.Linq;
using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class MonsterDetailsTests : RulesFixture
    {
        [Test]
        public void 左能量体的详情是主动白字且句子来自效果说明()
        {
            KeepOpened();
            var run = App.GetModel<RunModel>();
            var created = run.CreateMonster(new[] { "左能量体" });
            run.AddToCage(created);
            var monster = App.SendQuery(new MonsterQuery(created.Id));

            var details = App.SendQuery(new MonsterDetailsQuery(monster.Id));

            Assert.That(details.DisplayName, Is.Not.EqualTo("左能量体"));
            Assert.That(details.DisplayName, Does.StartWith("SCP-"));
            Assert.That(details.Skills[0].Sentence, Is.EqualTo("右侧每有一个怪物产生2点能量"));
            Assert.That(details.Skills[0].Kind, Is.EqualTo(SkillUse.Active));
            Assert.That(details.Skills[0].Rarity, Is.EqualTo(Rarity.White));
            Assert.That(details.Skills[0].Function, Is.EqualTo("产能"));
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

            Assert.That(copy.Names, Is.EqualTo(catalog.Names()));
            Assert.That(copy.TryDescribe("能量体", out var kind, out var sentence, out var function), Is.True);
            Assert.That(kind, Is.EqualTo(SkillUse.Active));
            Assert.That(sentence, Is.EqualTo("产生5点能量"));
            Assert.That(function, Is.EqualTo("产能"));
            Assert.That(copy.TryDescribe("蜜能量体", out _, out _, out var both), Is.True);
            Assert.That(both, Is.EqualTo("产能，辅助"));
            Assert.That(copy.TryDescribe("吞噬大嘴", out _, out _, out var amplify), Is.True);
            Assert.That(amplify, Is.EqualTo("增幅"));
            Assert.That(copy.TryDescribe("外接胚胎", out _, out _, out var economy), Is.True);
            Assert.That(economy, Is.EqualTo("经济"));
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
            Assert.That(
                SkillSentences.Format(ContentGate.Current.FindSkill("左复制腺体")),
                Is.EqualTo("左侧相邻怪物，产生能量的触发次数+1"));
            Assert.That(
                SkillSentences.Format(ContentGate.Current.FindSkill("右复制腺体")),
                Is.EqualTo("右侧相邻怪物，产生能量的触发次数+1"));
        }

        [Test]
        public void 吞噬大嘴的句子没有星号()
        {
            KeepOpened();
            var devourer = App.SendQuery(new MonsterCageQuery())
                .Single(monster => Holds(monster, "吞噬大嘴"));

            var details = App.SendQuery(new MonsterDetailsQuery(devourer.Id));

            Assert.That(
                details.Skills[0].Sentence,
                Is.EqualTo("消灭随机一只相邻怪物，本怪物产能类型的技能数值永久+2"));
        }

        [Test]
        public void 没改过的能量数保持原文且不上色()
        {
            var breath = Skill(Fresh("能量体"), "能量体");

            Assert.That(breath.Sentence, Is.EqualTo("产生5点能量"));
            Assert.That(breath.DisplaySentence, Is.EqualTo(breath.Sentence));
            Assert.That(Colored(breath), Is.Empty);
        }

        [Test]
        public void 生长能量体过关后只把产出数改成橙色()
        {
            var id = Fresh("生长能量体");
            App.GetModel<RunModel>().GrowAfterClear(App.GetUtility<SkillCatalog>());

            var skill = Skill(id, "生长能量体");

            Assert.That(skill.Sentence, Is.EqualTo("产生5点能量，每通过一次关卡本，本技能能量数值永久+3"));
            Assert.That(Colored(skill), Is.EqualTo("5"));
            Assert.That(
                skill.DisplaySentence,
                Is.EqualTo("产生<color=#FF8C1A>5</color>点能量，每通过一次关卡本，本技能能量数值永久+3"));
        }

        [Test]
        public void 生长能量体把写回加项和倍率收成一个数()
        {
            KeepOpened();
            var grow = Fresh("生长能量体");
            var scent = Fresh("奇异香");
            var eye = Fresh("镜眼");
            App.GetModel<RunModel>().GrowAfterClear(App.GetUtility<SkillCatalog>());
            Place(scent, 0);
            Place(grow, 1);
            Place(eye, 2);

            var skill = Skill(grow, "生长能量体");

            Assert.That(skill.Sentence, Does.StartWith("产生12点能量"));
            Assert.That(Colored(skill), Is.EqualTo("12"));
            Assert.That(skill.Sentence, Does.Contain("永久+3"));
        }

        [Test]
        public void 不稳定能量体在奇异香离开提取槽后回到原文()
        {
            KeepOpened();
            var chance = Fresh("不稳定能量体");
            var scent = Fresh("奇异香");
            Place(chance, 0);
            Place(scent, 1);

            var boosted = Skill(chance, "不稳定能量体");
            Assert.That(boosted.Sentence, Is.EqualTo("50%概率产生21点能量"));
            Assert.That(Colored(boosted), Is.EqualTo("21"));

            App.SendCommand(new ReturnMonsterCommand(scent));

            var plain = Skill(chance, "不稳定能量体");
            Assert.That(plain.Sentence, Is.EqualTo("50%概率产生20点能量"));
            Assert.That(Colored(plain), Is.Empty);
        }

        [Test]
        public void 奇异香还没和目标一起放进提取槽时不加()
        {
            KeepOpened();
            var chance = Fresh("不稳定能量体");
            var scent = Fresh("奇异香");
            Place(scent, 0);

            var skill = Skill(chance, "不稳定能量体");

            Assert.That(skill.Sentence, Is.EqualTo("50%概率产生20点能量"));
            Assert.That(Colored(skill), Is.Empty);
        }

        [Test]
        public void 群能量体显示当前总量并在香离开后降回来()
        {
            KeepOpened();
            var swarm = Fresh("群能量体");
            var scent = Fresh("奇异香");
            var before = Lead(Skill(swarm, "群能量体").Sentence);
            Assert.That(Colored(Skill(swarm, "群能量体")), Is.Empty);

            Place(scent, 0);
            Assert.That(Lead(Skill(swarm, "群能量体").Sentence), Is.EqualTo(before));
            Assert.That(Colored(Skill(swarm, "群能量体")), Is.Empty);

            Place(swarm, 1);
            var boosted = Skill(swarm, "群能量体");
            Assert.That(Lead(boosted.Sentence), Is.EqualTo(before + 1));
            Assert.That(Colored(boosted), Is.EqualTo((before + 1).ToString()));
            Assert.That(boosted.Sentence, Does.Contain("（培育槽+提取槽+怪物笼）"));

            App.SendCommand(new ReturnMonsterCommand(scent));
            var dropped = Skill(swarm, "群能量体");
            Assert.That(Lead(dropped.Sentence), Is.EqualTo(before));
            Assert.That(Colored(dropped), Is.Empty);
        }

        [Test]
        public void 优秀能量体按技能数两倍再吃提取槽里的加项()
        {
            KeepOpened();
            var host = Fresh("优秀能量体");
            var scent = Fresh("奇异香");

            Assert.That(Skill(host, "优秀能量体").Sentence, Is.EqualTo("产生等同于怪物技能数数值两倍的能量（2）"));
            Assert.That(Colored(Skill(host, "优秀能量体")), Is.Empty);

            Place(host, 0);
            Place(scent, 1);
            var boosted = Skill(host, "优秀能量体");
            Assert.That(boosted.Sentence, Is.EqualTo("产生等同于怪物技能数数值两倍的能量（3）"));
            Assert.That(Colored(boosted), Is.EqualTo("3"));
        }

        [Test]
        public void 良好肉体只在技能数对上时加进能量()
        {
            var pair = Skill(Fresh("能量体", "良好肉体"), "能量体");
            var triple = Skill(Fresh("能量体", "良好肉体", "奇异香"), "能量体");

            Assert.That(pair.Sentence, Is.EqualTo("产生8点能量"));
            Assert.That(Colored(pair), Is.EqualTo("8"));
            Assert.That(triple.Sentence, Is.EqualTo("产生5点能量"));
            Assert.That(Colored(triple), Is.Empty);
        }

        [Test]
        public void 怪物修正直接加进能量数()
        {
            var run = App.GetModel<RunModel>();
            var monster = run.CreateMonster(new[] { "能量体" }, 2);
            run.AddToCage(monster);

            var skill = Skill(monster.Id, "能量体");

            Assert.That(skill.Sentence, Is.EqualTo("产生7点能量"));
            Assert.That(Colored(skill), Is.EqualTo("7"));
        }

        [Test]
        public void 贴边基因只在对应边上加()
        {
            KeepOpened();
            var left = Fresh("能量体", "争锋基因");
            var right = Fresh("能量体", "潜藏基因");
            var blocker = Fresh("能量体");
            Place(blocker, 0);
            Place(left, 2);
            Place(right, 4);

            Assert.That(Skill(left, "能量体").Sentence, Is.EqualTo("产生5点能量"));
            Assert.That(Skill(right, "能量体").Sentence, Is.EqualTo("产生10点能量"));

            App.SendCommand(new ReturnMonsterCommand(blocker));

            Assert.That(Skill(left, "能量体").Sentence, Is.EqualTo("产生10点能量"));
            Assert.That(Colored(Skill(left, "能量体")), Is.EqualTo("10"));
            Assert.That(Skill(right, "能量体").Sentence, Is.EqualTo("产生10点能量"));
        }

        [Test]
        public void 蜜能量体把加值写进下一个怪物而不是自己的赠送数字()
        {
            KeepOpened();
            var honey = Fresh("蜜能量体");
            var breath = Fresh("能量体");
            Place(honey, 0);
            Place(breath, 2);

            var gift = Skill(honey, "蜜能量体");
            var next = Skill(breath, "能量体");

            Assert.That(gift.Sentence, Is.EqualTo("产生2点能量，下一个怪物产生的能量数值+3"));
            Assert.That(Colored(gift), Is.Empty);
            Assert.That(next.Sentence, Is.EqualTo("产生8点能量"));
            Assert.That(Colored(next), Is.EqualTo("8"));
        }

        [Test]
        public void 传能耳按左侧主动技能加到更右侧的能量上()
        {
            KeepOpened();
            var left = Fresh("能量体");
            var ear = Fresh("传能耳");
            var right = Fresh("能量体");
            Place(left, 0);
            Place(ear, 1);
            Place(right, 2);

            Assert.That(Skill(left, "能量体").Sentence, Is.EqualTo("产生5点能量"));
            Assert.That(Skill(ear, "传能耳").Sentence, Is.EqualTo("左侧相邻怪物每触发一个主动技能，右侧每一只怪物能量数值+2"));
            Assert.That(Colored(Skill(ear, "传能耳")), Is.Empty);
            Assert.That(Skill(right, "能量体").Sentence, Is.EqualTo("产生7点能量"));
            Assert.That(Colored(Skill(right, "能量体")), Is.EqualTo("7"));
        }

        [Test]
        public void 回响嗓让传能耳左侧的主动技能再推一次()
        {
            KeepOpened();
            var echo = Fresh("回响嗓");
            var left = Fresh("能量体");
            var ear = Fresh("传能耳");
            var right = Fresh("能量体");
            Place(echo, 0);
            Place(left, 1);
            Place(ear, 2);
            Place(right, 3);

            Assert.That(Skill(right, "能量体").Sentence, Is.EqualTo("产生9点能量"));
            Assert.That(Colored(Skill(right, "能量体")), Is.EqualTo("9"));
        }

        [Test]
        public void 孤独心在不相邻时连同加项一起翻倍且和镜眼不叠两次()
        {
            KeepOpened();
            var lonely = Fresh("能量体", "孤独心");
            var scent = Fresh("奇异香");
            Place(lonely, 0);
            Place(scent, 2);

            Assert.That(Skill(lonely, "能量体").Sentence, Is.EqualTo("产生12点能量"));

            App.SendCommand(new ReturnMonsterCommand(scent));
            var eye = Fresh("镜眼");
            Place(eye, 1);

            Assert.That(Skill(lonely, "能量体").Sentence, Is.EqualTo("产生10点能量"));
            Assert.That(Colored(Skill(lonely, "能量体")), Is.EqualTo("10"));
        }

        [Test]
        public void 两侧镜眼连乘()
        {
            KeepOpened();
            var left = Fresh("镜眼");
            var breath = Fresh("能量体");
            var right = Fresh("镜眼");
            Place(left, 0);
            Place(breath, 1);
            Place(right, 2);

            Assert.That(Skill(breath, "能量体").Sentence, Is.EqualTo("产生20点能量"));
            Assert.That(Colored(Skill(breath, "能量体")), Is.EqualTo("20"));
        }

        [Test]
        public void 左能量体一侧只有一只时把平加并进单价一侧多只时不并()
        {
            KeepOpened();
            var left = Fresh("左能量体");
            var scent = Fresh("奇异香");
            Place(left, 0);
            Place(scent, 1);

            Assert.That(Skill(left, "左能量体").Sentence, Is.EqualTo("右侧每有一个怪物产生3点能量"));
            Assert.That(Colored(Skill(left, "左能量体")), Is.EqualTo("3"));

            var another = Fresh("能量体");
            Place(another, 2);

            Assert.That(Skill(left, "左能量体").Sentence, Is.EqualTo("右侧每有一个怪物产生2点能量"));
            Assert.That(Colored(Skill(left, "左能量体")), Is.Empty);
        }

        [Test]
        public void 左能量体的单价写回后变色()
        {
            var id = Fresh("左能量体");
            App.GetModel<RunModel>().Find(id).Skills[0].Add(3, true);

            var skill = Skill(id, "左能量体");

            Assert.That(skill.Sentence, Is.EqualTo("右侧每有一个怪物产生5点能量"));
            Assert.That(Colored(skill), Is.EqualTo("5"));
        }

        [Test]
        public void 汲取鼻吃到同槽的能量加项()
        {
            KeepOpened();
            var nose = Fresh("汲取鼻");
            var scent = Fresh("奇异香");
            Place(nose, 0);
            Place(scent, 1);

            var skill = Skill(nose, "汲取鼻");

            Assert.That(skill.Sentence, Does.Contain("产生2点能量"));
            Assert.That(Colored(skill), Is.EqualTo("2"));
            Assert.That(skill.Sentence, Does.Contain("不会因为同名技能效果产生能量"));
        }

        [Test]
        public void 急急装置只翻最左侧第一张产能技能()
        {
            KeepOpened();
            var left = Fresh("能量体");
            var right = Fresh("能量体");
            Place(left, 0);
            Place(right, 1);
            App.GetModel<RunModel>().AddTool("急急装置");

            Assert.That(Skill(left, "能量体").Sentence, Is.EqualTo("产生10点能量"));
            Assert.That(Skill(right, "能量体").Sentence, Is.EqualTo("产生5点能量"));

            App.SendCommand(new ReturnMonsterCommand(left));
            var scent = Fresh("奇异香");
            Place(scent, 0);

            Assert.That(Skill(right, "能量体").Sentence, Is.EqualTo("产生6点能量"));
        }

        [Test]
        public void 劣胜装置在提取槽里翻倍单词条怪物()
        {
            KeepOpened();
            var id = Fresh("能量体", "换位手");
            Place(id, 0);

            Assert.That(Skill(id, "能量体").Sentence, Is.EqualTo("产生5点能量"));

            App.GetModel<RunModel>().AddTool("劣胜装置");

            Assert.That(Skill(id, "能量体").Sentence, Is.EqualTo("产生10点能量"));
            Assert.That(Colored(Skill(id, "能量体")), Is.EqualTo("10"));
        }

        [Test]
        public void 光环自己的加值不改写()
        {
            KeepOpened();
            var scent = Fresh("奇异香");
            var breath = Fresh("能量体");
            Place(scent, 0);
            Place(breath, 1);

            var skill = Skill(scent, "奇异香");

            Assert.That(skill.Sentence, Is.EqualTo("其他怪物产生的能量数值+1"));
            Assert.That(skill.DisplaySentence, Is.EqualTo(skill.Sentence));
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

        MonsterSkillDetail Skill(string monsterId, string skillName)
        {
            var details = App.SendQuery(new MonsterDetailsQuery(monsterId));
            Assert.That(details, Is.Not.Null);
            return details.Skills.Single(skill => skill.Name == skillName);
        }

        static string Colored(MonsterSkillDetail skill)
        {
            var text = "";
            for (var i = 0; i < skill.Spans.Count; i++)
            {
                if (skill.Spans[i].Shifted)
                    text += skill.Spans[i].Text;
            }

            return text;
        }

        static int Lead(string sentence)
        {
            const string head = "产生等同于";
            var start = sentence.IndexOf(head, System.StringComparison.Ordinal) + head.Length;
            var end = sentence.IndexOf('（', start);
            return int.Parse(sentence.Substring(start, end - start));
        }
    }
}
