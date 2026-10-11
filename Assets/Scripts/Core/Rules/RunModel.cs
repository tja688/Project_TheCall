using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class SkillInstance
    {
        public SkillInstance(string name, int quote, bool isTemporary = false)
        {
            Name = name;
            Quote = quote;
            _permanentQuote = quote;
            IsTemporary = isTemporary;
        }

        public string Name { get; }

        public int Quote { get; private set; }

        public bool IsTemporary { get; }

        int _permanentQuote;

        public void Add(int amount, bool permanent)
        {
            Quote += amount;
            if (permanent)
                _permanentQuote += amount;
        }

        public void ClearTemporary() => Quote = _permanentQuote;
    }

    internal sealed class Monster
    {
        public Monster(string id, string displayName, SkillInstance skill)
            : this(id, displayName, new[] { skill })
        {
        }

        public Monster(
            string id,
            string displayName,
            IReadOnlyList<SkillInstance> skills,
            int modifier = 0,
            MonsterAppearance? appearance = null,
            IReadOnlyList<string> parentIds = null,
            string personality = "稳定",
            string race = "森林",
            string mood = "普通",
            int consecutiveWorkDays = 0,
            int consecutiveRestDays = 0)
        {
            Id = id;
            DisplayName = displayName;
            Skills = new List<SkillInstance>(skills);
            Modifier = modifier;
            Appearance = appearance ?? MonsterAppearance.FromSeed(StableSeed(id));
            ParentIds = parentIds == null ? System.Array.Empty<string>() : CopyParents(parentIds);
            Personality = string.IsNullOrEmpty(personality) ? "稳定" : personality;
            Race = string.IsNullOrEmpty(race) ? "森林" : race;
            Mood = string.IsNullOrEmpty(mood) ? "普通" : mood;
            ConsecutiveWorkDays = consecutiveWorkDays;
            ConsecutiveRestDays = consecutiveRestDays;
        }

        public IReadOnlyList<string> ParentIds { get; }

        public string Id { get; }

        public string DisplayName { get; }

        public List<SkillInstance> Skills { get; }

        public int Modifier { get; }
        public MonsterAppearance Appearance { get; }

        public bool Immovable { get; private set; }

        public string Personality { get; private set; }

        public string Race { get; private set; }

        public string Mood { get; private set; }

        public int ConsecutiveWorkDays { get; private set; }

        public int ConsecutiveRestDays { get; private set; }

        public void ChangePersonality(string personality)
        {
            if (!string.IsNullOrEmpty(personality))
                Personality = personality;
        }

        public void ChangeRace(string race)
        {
            if (!string.IsNullOrEmpty(race))
                Race = race;
        }

        public void SetMood(string mood)
        {
            if (!string.IsNullOrEmpty(mood))
                Mood = mood;
        }

        public void RecordWorkDay()
        {
            ConsecutiveWorkDays++;
            ConsecutiveRestDays = 0;
        }

        public void RecordRestDay()
        {
            ConsecutiveRestDays++;
            ConsecutiveWorkDays = 0;
        }

        public void ClearTemporarySkills()
        {
            Skills.RemoveAll(skill => skill.IsTemporary);
        }

        public int Capacity { get; private set; }

        public void AddCapacity(int layers) => Capacity += layers;

        public void ClearCapacity() => Capacity = 0;

        bool _immovableIsPermanent;

        public void MakeImmovable(bool permanent)
        {
            Immovable = true;
            if (permanent)
                _immovableIsPermanent = true;
        }

        public void ClearTemporaryImmovable()
        {
            if (_immovableIsPermanent)
                return;

            Immovable = false;
        }
        static string[] CopyParents(IReadOnlyList<string> parentIds)
        {
            var copy = new string[parentIds.Count];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = parentIds[index];

            return copy;
        }

        static int StableSeed(string id)
        {
            var hash = 17;
            for (var i = 0; i < id.Length; i++)
                hash = hash * 31 + id[i];
            return hash;
        }

    }

    internal readonly struct CageCapture
    {
        public CageCapture(Monster[] inCage, Monster[] living)
        {
            InCage = inCage;
            Living = living;
        }

        public Monster[] InCage { get; }

        public Monster[] Living { get; }
    }

    internal sealed class RunModel : AbstractModel
    {
        readonly List<Monster> _candidates = new List<Monster>();
        readonly List<Monster> _cage = new List<Monster>();
        readonly Dictionary<string, Monster> _byId = new Dictionary<string, Monster>();
        readonly List<string> _tools = new List<string>();
        readonly List<string> _shelfMonsters = new List<string>();
        readonly List<string> _shelfTools = new List<string>();
        readonly List<string> _skillSlots = new List<string>();
        readonly List<string> _unlockedTech = new List<string>();
        readonly List<ProductionSubmission> _productionLog = new List<ProductionSubmission>();
        int _nextId = 1;

        public RunPhase Phase { get; private set; } = RunPhase.Opening;

        public int LevelNumber { get; private set; }

        public int Gold { get; private set; }

        public int TechPoints { get; private set; }

        public IReadOnlyList<string> Tools => _tools;

        public IReadOnlyList<string> SkillSlots => _skillSlots;

        public IReadOnlyList<string> UnlockedTech => _unlockedTech;

        public IReadOnlyList<ProductionSubmission> ProductionLog => _productionLog;

        public void AppendProduction(ProductionSubmission submission) => _productionLog.Add(submission);

        public IReadOnlyList<Monster> Candidates => _candidates;

        public IReadOnlyList<Monster> Cage => _cage;

        public IReadOnlyList<string> ShelfMonsterIds => _shelfMonsters;

        public IReadOnlyList<string> ShelfToolNames => _shelfTools;

        public void EnterOperation(int levelNumber)
        {
            Phase = RunPhase.Operation;
            LevelNumber = levelNumber;
        }

        public int ShopVisit { get; private set; }

        int _stockedVisit;

        public bool NeedsShopStock => Phase == RunPhase.Shop && _stockedVisit != ShopVisit;

        public void EnterShop()
        {
            Phase = RunPhase.Shop;
            ShopVisit++;
        }

        public void MarkShopStocked() => _stockedVisit = ShopVisit;

        public void AddShelfMonster(string id) => _shelfMonsters.Add(id);

        public void AddShelfTool(string name) => _shelfTools.Add(name);

        public bool TryRemoveShelfMonster(string monsterId)
        {
            var index = _shelfMonsters.IndexOf(monsterId);
            if (index < 0)
                return false;

            _shelfMonsters.RemoveAt(index);
            return true;
        }

        public bool TryRemoveShelfTool(string toolName)
        {
            var index = _shelfTools.IndexOf(toolName);
            if (index < 0)
                return false;

            _shelfTools.RemoveAt(index);
            return true;
        }

        public void ClearShelf()
        {
            _shelfMonsters.Clear();
            _shelfTools.Clear();
        }

        public void EnterLevelStart(int levelNumber)
        {
            Phase = RunPhase.LevelStart;
            LevelNumber = levelNumber;
        }

        public int RetainedEnergy { get; private set; }

        public void SetRetainedEnergy(int amount) => RetainedEnergy = System.Math.Max(0, amount);

        public void Win()
        {
            Phase = RunPhase.Victory;
            Gold = 0;
            TechPoints = 0;
            RetainedEnergy = 0;
            _tools.Clear();
            _skillSlots.Clear();
            _unlockedTech.Clear();
            _cage.Clear();
            _candidates.Clear();
            _byId.Clear();
        }

        public void Lose()
        {
            Phase = RunPhase.Failed;
            Gold = 0;
            TechPoints = 0;
            RetainedEnergy = 0;
            _tools.Clear();
            _skillSlots.Clear();
            _unlockedTech.Clear();
            _cage.Clear();
            _candidates.Clear();
            _byId.Clear();
        }

        public void AddGold(int amount) => Gold += amount;

        public bool TrySpend(int amount)
        {
            if (amount < 0 || Gold < amount)
                return false;

            Gold -= amount;
            return true;
        }

        public void AddTool(string name) => _tools.Add(name);

        public void AddTechPoint() => TechPoints += 1;

        public bool TrySpendTechPoint(int amount)
        {
            if (amount < 0 || TechPoints < amount)
                return false;

            TechPoints -= amount;
            return true;
        }

        public bool HasTech(string name) => _unlockedTech.Contains(name);

        public void AddUnlockedTech(string name) => _unlockedTech.Add(name);

        public Monster AddCandidate(
            IReadOnlyList<string> skillNames,
            string personality = null,
            string race = null)
        {
            var monster = Create(skillNames, 0, null, null, personality, race);
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

        internal CageCapture CaptureCage()
        {
            var inCage = new Monster[_cage.Count];
            for (var i = 0; i < inCage.Length; i++)
                inCage[i] = _cage[i];

            var living = new Monster[_byId.Count];
            var index = 0;
            foreach (var monster in _byId.Values)
                living[index++] = monster;

            return new CageCapture(inCage, living);
        }

        internal void RestoreCage(CageCapture capture)
        {
            _cage.Clear();
            for (var i = 0; i < capture.InCage.Length; i++)
                _cage.Add(capture.InCage[i]);

            _byId.Clear();
            for (var i = 0; i < capture.Living.Length; i++)
            {
                var monster = capture.Living[i];
                _byId[monster.Id] = monster;
            }
        }

        internal string[] CaptureSkillSlots()
        {
            var copy = new string[_skillSlots.Count];
            for (var i = 0; i < copy.Length; i++)
                copy[i] = _skillSlots[i];

            return copy;
        }

        internal void RestoreSkillSlots(string[] slots)
        {
            _skillSlots.Clear();
            for (var i = 0; i < slots.Length; i++)
                _skillSlots.Add(slots[i]);
        }

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

        public bool TryTakeSkillAt(int index, out string skillName)
        {
            skillName = null;
            if (index < 0 || index >= _skillSlots.Count)
                return false;

            skillName = _skillSlots[index];
            _skillSlots.RemoveAt(index);
            return true;
        }

        public Monster CreateMonster(
            IReadOnlyList<string> skillNames,
            int modifier = 0,
            MonsterAppearance? appearance = null,
            IReadOnlyList<string> parentIds = null,
            string personality = null,
            string race = null,
            string mood = null,
            int consecutiveWorkDays = 0,
            int consecutiveRestDays = 0) =>
            Create(skillNames, modifier, appearance, parentIds, personality, race, mood, consecutiveWorkDays, consecutiveRestDays);

        public bool TryGainSkill(string monsterId, string skillName, bool isTemporary = false)
        {
            var monster = Find(monsterId);
            if (monster == null || monster.Skills.Count >= 4)
                return false;

            for (var index = 0; index < monster.Skills.Count; index++)
            {
                if (monster.Skills[index].Name == skillName)
                    return false;
            }

            monster.Skills.Add(MakeSkill(skillName, isTemporary));
            return true;
        }

        public void GrowAfterClear(SkillCatalog catalog)
        {
            foreach (var monster in _byId.Values)
            {
                for (var index = 0; index < monster.Skills.Count; index++)
                {
                    var amount = catalog.GrowAmount(monster.Skills[index].Name);
                    if (amount > 0)
                        monster.Skills[index].Add(amount, true);
                }
            }
        }

        public void ClearTemporaryQuotes()
        {
            foreach (var monster in _byId.Values)
            {
                for (var i = 0; i < monster.Skills.Count; i++)
                    monster.Skills[i].ClearTemporary();
            }
        }

        public void ClearTemporarySkills()
        {
            foreach (var monster in _byId.Values)
                monster.ClearTemporarySkills();
        }

        public void ClearTemporaryImmovable()
        {
            foreach (var monster in _byId.Values)
                monster.ClearTemporaryImmovable();
        }

        public void ClearCapacity()
        {
            foreach (var monster in _byId.Values)
                monster.ClearCapacity();
        }

        public Monster Find(string monsterId)
        {
            if (monsterId != null && _byId.TryGetValue(monsterId, out var monster))
                return monster;

            return null;
        }

        public MonsterView ToView(Monster monster) =>
            MonsterViews.From(monster, this.GetUtility<SkillCatalog>(), this.GetUtility<MoodCatalog>());

        public MonsterView FindView(string monsterId)
        {
            var monster = Find(monsterId);
            return monster == null ? null : ToView(monster);
        }

        protected override void OnInit()
        {
        }

        SkillInstance MakeSkill(string name, bool isTemporary = false) =>
            new SkillInstance(name, this.GetUtility<SkillCatalog>().StartingQuote(name), isTemporary);

        Monster Create(string skillName) => Create(new[] { skillName }, 0, null);

        Monster Create(
            IReadOnlyList<string> skillNames,
            int modifier,
            MonsterAppearance? appearance = null,
            IReadOnlyList<string> parentIds = null,
            string personality = null,
            string race = null,
            string mood = null,
            int consecutiveWorkDays = 0,
            int consecutiveRestDays = 0)
        {
            var skills = new SkillInstance[skillNames.Count];
            for (var i = 0; i < skills.Length; i++)
                skills[i] = MakeSkill(skillNames[i]);

            var serial = _nextId++;
            var id = "m" + serial;
            var monster = new Monster(
                id,
                MonsterCodenames.ForSerial(serial),
                skills,
                modifier,
                appearance,
                parentIds,
                personality ?? "稳定",
                race ?? "森林",
                mood ?? "普通",
                consecutiveWorkDays,
                consecutiveRestDays);
            _byId.Add(monster.Id, monster);
            return monster;
        }

    }
}
