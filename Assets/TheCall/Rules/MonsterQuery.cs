using QFramework;

namespace TheCall
{
    public sealed class MonsterQuery : AbstractQuery<MonsterView>
    {
        readonly string _monsterId;

        public MonsterQuery(string monsterId) => _monsterId = monsterId;

        protected override MonsterView OnDo() => this.GetModel<RunModel>().FindView(_monsterId);
    }
}
