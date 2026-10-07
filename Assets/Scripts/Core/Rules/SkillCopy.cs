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

        public bool TryDescribe(string skillName, out SkillUse kind, out string sentence)
        {
            var skill = _book.FindSkill(skillName);
            if (skill == null)
            {
                kind = default;
                sentence = null;
                return false;
            }

            kind = skill.Use;
            sentence = SkillSentences.Format(skill);
            return true;
        }
    }
}
