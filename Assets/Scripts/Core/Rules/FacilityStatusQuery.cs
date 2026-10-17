using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public enum ResidentLocation { Resting, Extraction, Breeding }

    public sealed class ResidentStatus
    {
        public ResidentStatus(MonsterView monster, ResidentLocation location, int seat)
        { Monster = monster; Location = location; Seat = seat; }
        public MonsterView Monster { get; }
        public ResidentLocation Location { get; }
        public int Seat { get; }
    }

    // An owned-monster projection. Shop stock and opening candidates are deliberately outside it.
    public sealed class FacilityStatusQuery : AbstractQuery<IReadOnlyList<ResidentStatus>>
    {
        protected override IReadOnlyList<ResidentStatus> OnDo()
        {
            var run = this.GetModel<RunModel>();
            var level = this.GetModel<LevelModel>();
            var result = new List<ResidentStatus>();
            foreach (var monster in run.Cage)
                result.Add(new ResidentStatus(run.ToView(monster), ResidentLocation.Resting, -1));
            Add(level.Extraction, ResidentLocation.Extraction, run, result);
            Add(level.Breeding, ResidentLocation.Breeding, run, result);
            return result;
        }

        static void Add(IReadOnlyList<string> ids, ResidentLocation place, RunModel run, List<ResidentStatus> result)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                var monster = run.FindView(ids[i]);
                if (monster != null) result.Add(new ResidentStatus(monster, place, i));
            }
        }
    }
}
