using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class SkillCopy : IUtility
    {
        readonly struct Row
        {
            public readonly string Name;
            public readonly SkillUse Kind;
            public readonly string Sentence;

            public Row(string name, SkillUse kind, string sentence)
            {
                Name = name;
                Kind = kind;
                Sentence = sentence;
            }
        }

        static readonly Row[] Rows =
        {
            new Row("能量吐息", SkillUse.Active, "产生5点能量"),
            new Row("左能量体", SkillUse.Active, "右侧每有一个怪物产生2点能量"),
            new Row("右能量体", SkillUse.Active, "左侧每有一个怪物产生2点能量"),
            new Row("增量小手", SkillUse.Passive, "其他怪物产生的能量数值+1"),
            new Row("增量大手", SkillUse.Passive, "其他怪物产生的能量数值+2"),
            new Row("残留提取腺体", SkillUse.Passive, "其他怪物产生能量时，本怪物产生1点能量（不会因为同名技能效果产生能量）"),
            new Row("孤独心", SkillUse.Active, "产生4点能量，当相邻没有其他怪物时，本怪物产生的能量翻倍"),
            new Row("吞噬大嘴", SkillUse.Active, "产生4点能量，消灭随机一只相邻怪物，本技能产生的能量数值永久+2"),
            new Row("双重吐息", SkillUse.Active, "产生2点能量两次"),
            new Row("时间操控器官", SkillUse.Passive, "左侧相邻怪物，产生能量的技能次数+1"),
            new Row("再回首头", SkillUse.Passive, "右侧相邻怪物的所有技能效果都触发两次"),
            new Row("分享之手", SkillUse.Active, "产生2点能量，下一个怪物产生的能量数值+3"),
            new Row("太阳能头", SkillUse.Active, "获得1点产能"),
            new Row("换位手", SkillUse.Active, "与左侧相邻怪物交换位置，之后获得不动"),
            new Row("鼓励嘴", SkillUse.Passive, "相邻怪物产生的能量翻倍"),
        };

        public IReadOnlyList<string> Names { get; } = NamesOf(Rows);

        public bool TryDescribe(string skillName, out SkillUse kind, out string sentence)
        {
            for (var i = 0; i < Rows.Length; i++)
            {
                if (Rows[i].Name != skillName)
                    continue;

                kind = Rows[i].Kind;
                sentence = Rows[i].Sentence;
                return true;
            }

            kind = default;
            sentence = null;
            return false;
        }

        static string[] NamesOf(Row[] rows)
        {
            var names = new string[rows.Length];
            for (var i = 0; i < rows.Length; i++)
                names[i] = rows[i].Name;

            return names;
        }
    }
}
