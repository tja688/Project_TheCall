using QFramework;
using System.Linq;

namespace TheCall
{
    public sealed class OperationFeedbackQuery : AbstractQuery<string>
    {
        protected override string OnDo() => this.GetModel<RunModel>().LastOperationFailure;
    }
    public sealed class SkillTrimFeedbackQuery : AbstractQuery<string>
    {
        protected override string OnDo()
        {
            var removed = this.GetModel<RunModel>().LastRemovedSkills;
            return removed == null || removed.Count == 0 ? "" : string.Join("、", removed.Select(skill => skill.Name + (skill.IsTemporary ? "（临时）" : "（常驻）")));
        }
    }
}
