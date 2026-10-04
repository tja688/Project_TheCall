using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class SkillInstance
    {
        public SkillInstance(string name) => Name = name;

        public string Name { get; }
    }

    internal sealed class Monster
    {
        public Monster(string id, SkillInstance skill)
        {
            Id = id;
            Skills = new List<SkillInstance> { skill };
        }

        public string Id { get; }

        public List<SkillInstance> Skills { get; }
    }

    internal sealed class RunModel : AbstractModel
    {
        readonly List<Monster> _candidates = new List<Monster>();
        readonly List<Monster> _cage = new List<Monster>();
        readonly Dictionary<string, Monster> _byId = new Dictionary<string, Monster>();
        readonly List<string> _tools = new List<string>();
        readonly List<string> _skillSlots = new List<string>();
        readonly List<string> _unlockedTech = new List<string>();
        int _nextId = 1;

        public RunPhase Phase { get; private set; } = RunPhase.Opening;

        public int LevelNumber { get; private set; }

        public int Gold { get; private set; }

        public int TechPoints { get; private set; }

        public IReadOnlyList<string> Tools => _tools;

        public IReadOnlyList<string> SkillSlots => _skillSlots;

        public IReadOnlyList<string> UnlockedTech => _unlockedTech;

        public IReadOnlyList<Monster> Candidates => _candidates;

        public IReadOnlyList<Monster> Cage => _cage;

        public void EnterOperation(int levelNumber)
        {
            Phase = RunPhase.Operation;
            LevelNumber = levelNumber;
        }

        public void EnterShop() => Phase = RunPhase.Shop;

        public void Lose()
        {
            Phase = RunPhase.Failed;
            Gold = 0;
            TechPoints = 0;
            _tools.Clear();
            _skillSlots.Clear();
            _unlockedTech.Clear();
            _cage.Clear();
            _candidates.Clear();
            _byId.Clear();
        }

        public void AddGold(int amount) => Gold += amount;

        public void AddTechPoint() => TechPoints += 1;

        public Monster AddCandidate(string skillName)
        {
            var monster = Create(skillName);
            _candidates.Add(monster);
            return monster;
        }

        public bool TryTakeCandidate(string monsterId, out Monster kept)
        {
            kept = null;
            if (string.IsNullOrEmpty(monsterId))
                return false;

            var index = _candidates.FindIndex(monster => monster.Id == monsterId);
            if (index < 0)
                return false;

            kept = _candidates[index];
            for (var i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i].Id != kept.Id)
                    _byId.Remove(_candidates[i].Id);
            }

            _candidates.Clear();
            return true;
        }

        public void AddToCage(Monster monster) => _cage.Add(monster);

        public void AddNewToCage(string skillName) => _cage.Add(Create(skillName));

        public bool TryRemoveFromCage(string monsterId, out Monster monster)
        {
            monster = null;
            if (string.IsNullOrEmpty(monsterId))
                return false;

            var index = _cage.FindIndex(item => item.Id == monsterId);
            if (index < 0)
                return false;

            monster = _cage[index];
            _cage.RemoveAt(index);
            return true;
        }

        public bool TryDestroy(string monsterId)
        {
            if (string.IsNullOrEmpty(monsterId) || !_byId.ContainsKey(monsterId))
                return false;

            TryRemoveFromCage(monsterId, out _);
            _byId.Remove(monsterId);
            return true;
        }

        public void PutSkillInSlot(string skillName) => _skillSlots.Add(skillName);

        public bool TryEquip(string monsterId, int skillSlotIndex)
        {
            if (skillSlotIndex < 0 || skillSlotIndex >= _skillSlots.Count)
                return false;

            var monster = Find(monsterId);
            if (monster == null || monster.Skills.Count >= 4)
                return false;

            var skillName = _skillSlots[skillSlotIndex];
            for (var i = 0; i < monster.Skills.Count; i++)
            {
                if (monster.Skills[i].Name == skillName)
                    return false;
            }

            _skillSlots.RemoveAt(skillSlotIndex);
            monster.Skills.Add(new SkillInstance(skillName));
            return true;
        }

        public Monster Find(string monsterId)
        {
            if (monsterId != null && _byId.TryGetValue(monsterId, out var monster))
                return monster;

            return null;
        }

        protected override void OnInit()
        {
        }

        Monster Create(string skillName)
        {
            var monster = new Monster("m" + _nextId++, new SkillInstance(skillName));
            _byId.Add(monster.Id, monster);
            return monster;
        }
    }
}
