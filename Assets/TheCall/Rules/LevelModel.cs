using System;
using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class LevelModel : AbstractModel
    {
        string[] _extraction = Array.Empty<string>();
        string[] _breeding = Array.Empty<string>();
        string[] _lockedParents;

        public int EnergyDue { get; private set; }

        public int ExcessEnergy { get; private set; }

        public int Shortfall { get; private set; }

        public bool InOvertime { get; private set; }

        public int Energy { get; private set; }

        public IReadOnlyList<string> Extraction => _extraction;

        public IReadOnlyList<string> Breeding => _breeding;

        public void BeginLevel(int energyDue, int excessEnergy)
        {
            ClearProgress();
            EnergyDue = energyDue;
            ExcessEnergy = excessEnergy;
        }

        public void ClearProgress()
        {
            EnergyDue = 0;
            ExcessEnergy = 0;
            Energy = 0;
            Shortfall = 0;
            InOvertime = false;
            _extraction = new string[5];
            _breeding = new string[2];
            _lockedParents = null;
        }

        public void LockBreeding() => _lockedParents = (string[])_breeding.Clone();

        public IReadOnlyList<string> LockedParents => _lockedParents;

        public bool IsLockedParent(string monsterId)
        {
            if (_lockedParents == null || string.IsNullOrEmpty(monsterId))
                return false;

            for (var i = 0; i < _lockedParents.Length; i++)
            {
                if (_lockedParents[i] == monsterId)
                    return true;
            }

            return false;
        }

        public void ClearEnergy() => Energy = 0;

        public void RecordShortfall(int shortfall)
        {
            Shortfall = shortfall;
            InOvertime = true;
            Energy = 0;
        }

        public void AddEnergy(int amount) => Energy += amount;

        public void Pay(int amount) => Energy -= amount;

        public bool Occupies(string monsterId) =>
            Holds(_extraction, monsterId) || Holds(_breeding, monsterId);

        public bool CanPlace(OperationArea area, int index) => CanPlace(Slots(area), index);

        public void Put(OperationArea area, int index, string monsterId) =>
            Slots(area)[index] = monsterId;

        public bool TryRemove(string monsterId) =>
            TryClear(_extraction, monsterId) || TryClear(_breeding, monsterId);

        public bool TrySwapExtraction(string firstId, string secondId)
        {
            var first = IndexOf(_extraction, firstId);
            var second = IndexOf(_extraction, secondId);
            if (first < 0 || second < 0 || first == second)
                return false;

            var held = _extraction[first];
            _extraction[first] = _extraction[second];
            _extraction[second] = held;
            return true;
        }

        protected override void OnInit()
        {
        }

        string[] Slots(OperationArea area) =>
            area == OperationArea.Extraction ? _extraction : _breeding;

        static bool CanPlace(string[] slots, int index) =>
            index >= 0 && index < slots.Length && slots[index] == null;

        static bool Holds(string[] slots, string monsterId)
        {
            if (monsterId == null)
                return false;

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == monsterId)
                    return true;
            }

            return false;
        }

        static int IndexOf(string[] slots, string monsterId)
        {
            if (monsterId == null)
                return -1;

            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] == monsterId)
                    return i;
            }

            return -1;
        }

        static bool TryClear(string[] slots, string monsterId)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                if (slots[i] != monsterId)
                    continue;

                slots[i] = null;
                return true;
            }

            return false;
        }
    }
}
