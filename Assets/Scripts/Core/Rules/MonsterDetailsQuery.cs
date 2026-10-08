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
            var run = this.GetModel<RunModel>();
            var level = this.GetModel<LevelModel>();
            var catalog = this.GetArchitecture().GetUtility<SkillCatalog>();
            var tools = this.GetArchitecture().GetUtility<IToolCatalog>().Tools;
            var skills = new MonsterSkillDetail[view.Skills.Count];
            for (var i = 0; i < view.Skills.Count; i++)
            {
                var skill = view.Skills[i];
                if (skill == null || string.IsNullOrEmpty(skill.Name))
                    return null;
                if (!copy.TryDescribe(skill.Name, out var kind, out _, out var function))
                    return null;
                if (!copy.TrySkill(skill.Name, out var def))
                    return null;

                var spans = SkillReadout.Render(def, skill.Quote, view.Id, run, level, catalog, tools);
                skills[i] = new MonsterSkillDetail(skill.Name, kind, skill.Rarity, function, spans);
            }

            return new MonsterDetails(view.Id, view.DisplayName, skills, view.Modifier, view.Immovable, view.Capacity, view.Appearance);
        }
    }
}
