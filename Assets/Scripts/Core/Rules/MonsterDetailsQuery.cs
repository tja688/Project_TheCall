using QFramework;

namespace TheCall
{
    public sealed class MonsterDetailsQuery : AbstractQuery<MonsterDetails>
    {
        readonly string _monsterId;

        public MonsterDetailsQuery(string monsterId) => _monsterId = monsterId;

        protected override MonsterDetails OnDo()
        {
            var view = this.GetModel<RunModel>().FindView(_monsterId);
            if (view?.Skills == null || view.Skills.Count < 1 || view.Skills.Count > 4)
                return null;

            var copy = this.GetArchitecture().GetUtility<SkillCopy>();
            var skills = new MonsterSkillDetail[view.Skills.Count];
            for (var i = 0; i < view.Skills.Count; i++)
            {
                var skill = view.Skills[i];
                if (skill == null || string.IsNullOrEmpty(skill.Name))
                    return null;
                if (!copy.TryDescribe(skill.Name, out var kind, out var sentence))
                    return null;

                skills[i] = new MonsterSkillDetail(skill.Name, sentence, kind, skill.Rarity);
            }

            return new MonsterDetails(view.Id, skills, view.Modifier, view.Immovable, view.Capacity, view.Appearance);
        }
    }
}
