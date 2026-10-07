using System;
using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class BreedingSeat
    {
        public BreedingSeat(int parentCapacity)
        {
            Parents = new string[parentCapacity];
        }

        public string[] Parents { get; }

        public string Skill { get; set; }

        public BreedingSeat Copy()
        {
            var copy = new BreedingSeat(Parents.Length);
            Array.Copy(Parents, copy.Parents, Parents.Length);
            copy.Skill = Skill;
            return copy;
        }
    }

    internal sealed class LevelModel : AbstractModel
    {
        string[] _extraction = Array.Empty<string>();
        BreedingSeat[] _slots = { new BreedingSeat(2) };
        BreedingSeat[] _locked;
        int _slotCount = 1;
        int _parentCapacity = 2;

        public int EnergyDue { get; private set; }

        public int ExcessEnergy { get; private set; }

        public int Shortfall { get; private set; }

        public bool InOvertime { get; private set; }

        public int Energy { get; private set; }

        public IReadOnlyList<string> Extraction => _extraction;

        public IReadOnlyList<string> Breeding
        {
            get
            {
                var flat = new string[_slotCount * _parentCapacity];
                var index = 0;
                for (var slot = 0; slot < _slots.Length; slot++)
                {
                    var parents = _slots[slot].Parents;
                    for (var seat = 0; seat < parents.Length; seat++)
                        flat[index++] = parents[seat];
                }

                return flat;
            }
        }

        public int BreedingSlotCount => _slotCount;

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
            _slotCount = 1;
            _parentCapacity = 2;
            _slots = EmptySeats(_slotCount, _parentCapacity);
            _locked = null;
        }

        public void FitExtraction(int cellCount)
        {
            if (cellCount == _extraction.Length)
                return;

            _extraction = new string[cellCount];
        }

        public void ApplyShape(int slotCount, int parentCapacity)
        {
            _slots = Rebuild(_slots, slotCount, parentCapacity);
            _slotCount = slotCount;
            _parentCapacity = parentCapacity;
        }

        public void LockBreeding()
        {
            _locked = new BreedingSeat[_slots.Length];
            for (var i = 0; i < _slots.Length; i++)
                _locked[i] = _slots[i].Copy();
        }

        public int LockedSlotCount => _locked == null ? 0 : _locked.Length;

        public int LockedParentCount(int slot) => _locked[slot].Parents.Length;

        public string LockedParent(int slot, int seat) => _locked[slot].Parents[seat];

        public string LockedSkill(int slot) => _locked[slot].Skill;

        public string ParentAt(int slot, int seat) => _slots[slot].Parents[seat];

        public int ParentCount(int slot) => _slots[slot].Parents.Length;

        public string SkillAt(int slot) => _slots[slot].Skill;

        public bool TryPutSkill(int slot, string skillName)
        {
            if (slot < 0 || slot >= _slots.Length || _slots[slot].Skill != null || string.IsNullOrEmpty(skillName))
                return false;

            _slots[slot].Skill = skillName;
            return true;
        }

        public bool TryTakeSkill(int slot, out string skillName)
        {
            skillName = null;
            if (slot < 0 || slot >= _slots.Length || _slots[slot].Skill == null)
                return false;

            skillName = _slots[slot].Skill;
            _slots[slot].Skill = null;
            return true;
        }

        public bool IsLockedParent(string monsterId)
        {
            if (_locked == null || string.IsNullOrEmpty(monsterId))
                return false;

            for (var slot = 0; slot < _locked.Length; slot++)
            {
                var parents = _locked[slot].Parents;
                for (var seat = 0; seat < parents.Length; seat++)
                {
                    if (parents[seat] == monsterId)
                        return true;
                }
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

        public void ClearDebt()
        {
            Shortfall = 0;
            InOvertime = false;
        }

        public void AddEnergy(int amount) => Energy += amount;

        public void Pay(int amount) => Energy -= amount;

        public bool Occupies(string monsterId) =>
            Holds(_extraction, monsterId) || HoldsBreeding(monsterId);

        public bool CanPlace(OperationArea area, int index)
        {
            if (area == OperationArea.Extraction)
                return CanPlace(_extraction, index);

            return TrySeat(index, out var slot, out var seat) && _slots[slot].Parents[seat] == null;
        }

        public void Put(OperationArea area, int index, string monsterId) =>
            Write(area, index, monsterId);

        internal void MoveCell(OperationArea fromArea, int fromCell, OperationArea toArea, int toCell)
        {
            var moving = Read(fromArea, fromCell);
            Write(fromArea, fromCell, null);
            Write(toArea, toCell, moving);
        }

        internal void ExchangeCells(
            OperationArea firstArea,
            int firstCell,
            OperationArea secondArea,
            int secondCell)
        {
            var firstId = Read(firstArea, firstCell);
            var secondId = Read(secondArea, secondCell);
            Write(firstArea, firstCell, secondId);
            Write(secondArea, secondCell, firstId);
        }

        internal string[] CaptureExtraction()
        {
            var copy = new string[_extraction.Length];
            Array.Copy(_extraction, copy, _extraction.Length);
            return copy;
        }

        internal void RestoreExtraction(string[] cells)
        {
            _extraction = new string[cells.Length];
            Array.Copy(cells, _extraction, cells.Length);
        }

        internal BreedingSeat[] CaptureBreeding()
        {
            var copy = new BreedingSeat[_slots.Length];
            for (var i = 0; i < copy.Length; i++)
                copy[i] = _slots[i].Copy();

            return copy;
        }

        internal void RestoreBreeding(BreedingSeat[] seats)
        {
            var copy = new BreedingSeat[seats.Length];
            for (var i = 0; i < copy.Length; i++)
                copy[i] = seats[i].Copy();

            _slots = copy;
        }

        public bool TryRemove(string monsterId)
        {
            if (TryClear(_extraction, monsterId))
                return true;

            for (var slot = 0; slot < _slots.Length; slot++)
            {
                var parents = _slots[slot].Parents;
                for (var seat = 0; seat < parents.Length; seat++)
                {
                    if (parents[seat] != monsterId)
                        continue;

                    parents[seat] = null;
                    return true;
                }
            }

            return false;
        }

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

        bool TrySeat(int index, out int slot, out int seat)
        {
            slot = 0;
            seat = 0;
            if (index < 0 || _parentCapacity == 0)
                return false;

            slot = index / _parentCapacity;
            seat = index % _parentCapacity;
            return slot < _slotCount;
        }

        bool HoldsBreeding(string monsterId)
        {
            if (monsterId == null)
                return false;

            for (var slot = 0; slot < _slots.Length; slot++)
            {
                var parents = _slots[slot].Parents;
                for (var seat = 0; seat < parents.Length; seat++)
                {
                    if (parents[seat] == monsterId)
                        return true;
                }
            }

            return false;
        }

        static BreedingSeat[] EmptySeats(int slotCount, int parentCapacity)
        {
            var seats = new BreedingSeat[slotCount];
            for (var i = 0; i < seats.Length; i++)
                seats[i] = new BreedingSeat(parentCapacity);

            return seats;
        }

        static BreedingSeat[] Rebuild(BreedingSeat[] current, int slotCount, int parentCapacity)
        {
            var next = EmptySeats(slotCount, parentCapacity);
            var slots = Math.Min(slotCount, current.Length);
            for (var slot = 0; slot < slots; slot++)
            {
                var copy = Math.Min(parentCapacity, current[slot].Parents.Length);
                for (var seat = 0; seat < copy; seat++)
                    next[slot].Parents[seat] = current[slot].Parents[seat];

                next[slot].Skill = current[slot].Skill;
            }

            return next;
        }

        string Read(OperationArea area, int index)
        {
            if (area == OperationArea.Extraction)
                return _extraction[index];

            TrySeat(index, out var slot, out var seat);
            return _slots[slot].Parents[seat];
        }

        void Write(OperationArea area, int index, string monsterId)
        {
            if (area == OperationArea.Extraction)
            {
                _extraction[index] = monsterId;
                return;
            }

            TrySeat(index, out var slot, out var seat);
            _slots[slot].Parents[seat] = monsterId;
        }

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
