using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public sealed class ClockIntents : IUtility
    {
        readonly Dictionary<string, int> _poison = new Dictionary<string, int>();
        readonly Dictionary<string, int> _retriggers = new Dictionary<string, int>();
        readonly Dictionary<string, int> _secondPoints = new Dictionary<string, int>();
        readonly List<ForcedTrigger> _forces = new List<ForcedTrigger>();
        readonly List<LaterWrite> _writes = new List<LaterWrite>();
        readonly List<LaterRemoval> _endRemovals = new List<LaterRemoval>();
        readonly List<string> _permanentImmovable = new List<string>();

        internal bool WalksAgain { get; private set; }

        internal bool PlaysReverse { get; private set; }

        internal IReadOnlyList<ForcedTrigger> Forces => _forces;

        internal IReadOnlyList<LaterWrite> Writes => _writes;

        internal IReadOnlyList<LaterRemoval> EndRemovals => _endRemovals;

        internal IReadOnlyList<string> PermanentImmovable => _permanentImmovable;

        public void WalkAgain() => WalksAgain = true;

        public void PlayReverse() => PlaysReverse = true;

        public void Poison(string monsterId, int damage)
        {
            if (monsterId != null)
                _poison[monsterId] = damage;
        }

        public void Retrigger(string monsterId)
        {
            if (monsterId == null)
                return;

            _retriggers.TryGetValue(monsterId, out var count);
            _retriggers[monsterId] = count + 1;
        }

        public void SecondPoints(string monsterId, int count)
        {
            if (monsterId != null)
                _secondPoints[monsterId] = count;
        }

        public void ForceTrigger(string monsterId, double atTime)
        {
            if (monsterId != null)
                _forces.Add(new ForcedTrigger(monsterId, atTime));
        }

        public void WriteLater(string monsterId, string skillName, int amount, double atTime)
        {
            if (monsterId != null)
                _writes.Add(new LaterWrite(monsterId, skillName, amount, atTime));
        }

        public void DestroyAtEnd(string monsterId, double atTime)
        {
            if (monsterId != null)
                _endRemovals.Add(new LaterRemoval(monsterId, atTime));
        }

        public void MakePermanentlyImmovable(string monsterId)
        {
            if (monsterId != null)
                _permanentImmovable.Add(monsterId);
        }

        internal int PoisonOf(string monsterId)
        {
            if (monsterId != null && _poison.TryGetValue(monsterId, out var damage))
                return damage;

            return 0;
        }

        internal int RetriggerSources(string monsterId)
        {
            if (monsterId != null && _retriggers.TryGetValue(monsterId, out var count))
                return count;

            return 0;
        }

        internal int SecondPointsOf(string monsterId)
        {
            if (monsterId != null && _secondPoints.TryGetValue(monsterId, out var count) && count > 0)
                return count;

            return 1;
        }

        internal readonly struct ForcedTrigger
        {
            public ForcedTrigger(string monsterId, double atTime)
            {
                MonsterId = monsterId;
                AtTime = atTime;
            }

            public string MonsterId { get; }

            public double AtTime { get; }
        }

        internal readonly struct LaterWrite
        {
            public LaterWrite(string monsterId, string skillName, int amount, double atTime)
            {
                MonsterId = monsterId;
                SkillName = skillName;
                Amount = amount;
                AtTime = atTime;
            }

            public string MonsterId { get; }

            public string SkillName { get; }

            public int Amount { get; }

            public double AtTime { get; }
        }

        internal readonly struct LaterRemoval
        {
            public LaterRemoval(string monsterId, double atTime)
            {
                MonsterId = monsterId;
                AtTime = atTime;
            }

            public string MonsterId { get; }

            public double AtTime { get; }
        }
    }
}
