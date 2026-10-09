using System;
using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    /// <summary>
    /// 技能图鉴。读目录里的每一项，字段与悬停技能栏相同。
    /// 悬停会把当场报价写进句子；图鉴没有某一只怪物，句子用目录原文。
    /// </summary>
    public sealed class SkillIndexQuery : AbstractQuery<IReadOnlyList<MonsterSkillDetail>>
    {
        protected override IReadOnlyList<MonsterSkillDetail> OnDo()
        {
            var copy = this.GetArchitecture().GetUtility<SkillCopy>();
            var catalog = this.GetArchitecture().GetUtility<SkillCatalog>();
            var names = catalog.Names();
            var skills = new MonsterSkillDetail[names.Count];
            for (var i = 0; i < names.Count; i++)
            {
                var name = names[i];
                if (!copy.TryDescribe(name, out var kind, out var sentence, out var function))
                    throw new InvalidOperationException("技能目录缺了说明：" + name);

                skills[i] = new MonsterSkillDetail(
                    name,
                    kind,
                    catalog.RarityOf(name),
                    function,
                    new[] { new SentenceSpan(sentence, false) });
            }

            return skills;
        }
    }
}
