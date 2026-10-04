using System;
using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class LevelModel : AbstractModel
    {
        string[] _extraction = Array.Empty<string>();
        string[] _breeding = Array.Empty<string>();

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
