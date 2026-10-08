using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class SkillCopy : IUtility
    {
        readonly ContentBook _book;

        public SkillCopy(ContentBook book) => _book = book;

        public IReadOnlyList<string> Names
        {
            get
            {
                var names = new string[_book.Skills.Count];
                for (var i = 0; i < names.Length; i++)
                    names[i] = _book.Skills[i].Name;

                return names;
            }
        }

        public bool TryDescribe(string skillName, out SkillUse kind, out string sentence, out string function)
        {
            var skill = _book.FindSkill(skillName);
            if (skill == null)
            {
                kind = default;
                sentence = null;
                function = null;
                return false;
            }

            kind = skill.Use;
            sentence = SkillSentences.Format(skill);
            function = FunctionText(skill.Role);
            return function.Length > 0;
        }

        internal bool TrySkill(string skillName, out SkillDef skill)
        {
            skill = _book.FindSkill(skillName);
            return skill != null;
        }

        static string FunctionText(SkillRole role)
        {
            var text = "";
            Append(SkillRole.Produce, "产能");
            Append(SkillRole.Support, "辅助");
            Append(SkillRole.Amplify, "增幅");
            Append(SkillRole.Economy, "经济");
            return text;

            void Append(SkillRole flag, string label)
            {
                if ((role & flag) == 0)
                    return;

                if (text.Length > 0)
                    text += "，";

                text += label;
            }
        }
    }
}
