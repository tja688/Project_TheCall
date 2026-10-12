using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal enum SkillSourceKind { Opening, Purchase, Parent, Gene, Mutation, Personality, SkillEffect, Fixture }

    internal readonly struct SkillAcquisitionResult
    {
        public SkillAcquisitionResult(SkillInstance instance, string failure = null) { Instance = instance; Failure = failure; }
        public bool Success => Instance != null;
        public SkillInstance Instance { get; }
        public string Failure { get; }
    }

    internal sealed class SkillInstance
    {
        public SkillInstance(string name, int quote, bool isTemporary = false, string id = null, SkillSourceKind sourceKind = SkillSourceKind.Fixture, string sourceId = null, string sourceMonsterId = null)
        {
            Id = id ?? System.Guid.NewGuid().ToString("N");
            SourceKind = sourceKind;
            SourceId = sourceId;
            SourceMonsterId = sourceMonsterId;
            Name = name;
            Quote = quote;
            _permanentQuote = quote;
            IsTemporary = isTemporary;
        }

        public string Id { get; }
        public string ContentId => Name;
        public SkillSourceKind SourceKind { get; }
        public string SourceId { get; }
        public string SourceMonsterId { get; }
        public int PermanentQuote => _permanentQuote;
        public string GrowthOwnerId => Id;

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
            DayBaseMood = Mood;
            ConsecutiveWorkDays = consecutiveWorkDays;
            ConsecutiveRestDays = consecutiveRestDays;
        }

        public IReadOnlyList<string> ParentIds { get; }

        public string Id { get; }

        public string DisplayName { get; }

        public List<SkillInstance> Skills { get; }

        public bool HasSkill(string skillName)
        {
            for (var i = 0; i < Skills.Count; i++)
            {
                if (Skills[i].Name == skillName)
                    return true;
            }
            return false;
        }

        public int Modifier { get; private set; }
        public void AddModifier(int amount) => Modifier += amount;
        public MonsterAppearance Appearance { get; }

        public bool Immovable { get; private set; }

        public bool HasClockedInToday { get; private set; }

        public bool TryClockIn()
        {
            if (HasClockedInToday)
                return false;

            HasClockedInToday = true;
            return true;
        }

        public void ResetDailyClockIn() => HasClockedInToday = false;

        public string Personality { get; private set; }

        public string Race { get; private set; }

        public string Mood { get; private set; }
        public string DayBaseMood { get; private set; }
        public int DayMoodBoost { get; private set; }
        public void BeginDailyMood(string baseMood, string mood) { DayBaseMood = baseMood; DayMoodBoost = 0; Mood = mood; }
        public void BoostDailyMood(string mood) { DayMoodBoost++; Mood = mood; }

        public int ConsecutiveWorkDays { get; private set; }

        public int ConsecutiveRestDays { get; private set; }

        public IReadOnlyList<SkillInstance> ChangePersonality(string personality, IDraw draw = null)
        {
            var removed = new List<SkillInstance>();
            if (string.IsNullOrEmpty(personality)) return removed;
            Personality = personality;
            if (personality != "安于现状" || Skills.Count <= 2) return removed;
            // Pick uniformly among valid pairs, rather than biasing toward the first permanent instance.
            var pairs = new List<int[]>();
            var hasPermanent = Skills.Exists(x => !x.IsTemporary);
            for (var i = 0; i < Skills.Count; i++)
                for (var j = i + 1; j < Skills.Count; j++)
                    if (!hasPermanent || !Skills[i].IsTemporary || !Skills[j].IsTemporary)
                        pairs.Add(new[] { i, j });
            var pair = (draw ?? new SystemDraw()).Choose(pairs);
            var kept = new[] { Skills[pair[0]], Skills[pair[1]] };
            for (var i = 0; i < Skills.Count; i++)
                if (i != pair[0] && i != pair[1]) removed.Add(Skills[i]);
            Skills.Clear();
            Skills.AddRange(kept);
            return removed;
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

    internal sealed class GeneSlot
    {
        public GeneSlot(string skillName, int? reservedBreedingSlot = null, string id = null)
        {
            SkillName = skillName;
            Id = id ?? System.Guid.NewGuid().ToString("N");
            ReservedBreedingSlot = reservedBreedingSlot;
        }

        public string Id { get; }
        public string SkillName { get; }

        public int? ReservedBreedingSlot { get; set; }

        public bool IsReserved => ReservedBreedingSlot.HasValue;
    }

    internal readonly struct GeneSlotCapture
    {
        public GeneSlotCapture(string skillName, int? reservedBreedingSlot, string id = null)
        {
            Id = id;
            SkillName = skillName;
            ReservedBreedingSlot = reservedBreedingSlot;
        }

        public string Id { get; }
        public string SkillName { get; }

        public int? ReservedBreedingSlot { get; }
    }

    internal sealed class RunModel : AbstractModel
    {
        readonly List<Monster> _candidates = new List<Monster>();
        readonly List<Monster> _cage = new List<Monster>();
        readonly Dictionary<string, Monster> _byId = new Dictionary<string, Monster>();
        readonly List<string> _tools = new List<string>();
        readonly HashSet<string> _workedToday = new HashSet<string>();
        readonly List<string> _shelfMonsters = new List<string>();
        readonly List<string> _shelfTools = new List<string>();
        readonly List<GeneSlot> _geneSlots = new List<GeneSlot>();
        readonly List<string> _unlockedTech = new List<string>();
        readonly List<ProductionSubmission> _productionLog = new List<ProductionSubmission>();
        int _nextId = 1;
        int _nextSkillId = 1;
        int _nextGeneId = 1;
        readonly Dictionary<string, List<LandingAdd>> _dailyAdds = new Dictionary<string, List<LandingAdd>>();
        readonly List<LandingAdd> _dailyLegacyAdds = new List<LandingAdd>();
        public IReadOnlyList<LandingAdd> DailyLegacyAdds => _dailyLegacyAdds;
        public void RememberDailyLegacy(LandingAdd add) => _dailyLegacyAdds.Add(add);
        public void RememberDailyAdd(string targetId, LandingAdd add)
        {
            if (!_dailyAdds.TryGetValue(targetId, out var adds)) _dailyAdds[targetId] = adds = new List<LandingAdd>();
            adds.Add(add);
        }
        public IReadOnlyList<LandingAdd> DailyAddsFor(string targetId) =>
            _dailyAdds.TryGetValue(targetId, out var adds) ? adds : System.Array.Empty<LandingAdd>();
        public void ClearDailyEffects() { _dailyAdds.Clear(); _dailyLegacyAdds.Clear(); }


        public string LastOperationFailure { get; private set; }
        public void SetOperationFailure(string reason) => LastOperationFailure = reason;
        public IReadOnlyList<SkillInstance> LastRemovedSkills { get; private set; } = System.Array.Empty<SkillInstance>();

        public RunPhase Phase { get; private set; } = RunPhase.Opening;

        public int LevelNumber { get; private set; }

        public int Gold { get; private set; }

        public int TechPoints { get; private set; }

        public IReadOnlyList<string> Tools => _tools;

        public bool HasTool(string name) => _tools.Contains(name);

        public IReadOnlyCollection<Monster> LivingMonsters => _byId.Values;

        public void MarkWorkedToday(string monsterId)
        {
            if (!string.IsNullOrEmpty(monsterId))
                _workedToday.Add(monsterId);
        }

        public bool DidWorkToday(string monsterId) =>
            !string.IsNullOrEmpty(monsterId) && _workedToday.Contains(monsterId);

        public void ClearWorkedToday() => _workedToday.Clear();

        public void ResetDailyClockIn()
        {
            foreach (var monster in _byId.Values)
                monster.ResetDailyClockIn();
        }

        public IReadOnlyList<GeneSlot> GeneSlots => _geneSlots;

        public IReadOnlyList<string> SkillSlots
        {
            get
            {
                var names = new string[_geneSlots.Count];
                for (var i = 0; i < names.Length; i++)
                    names[i] = _geneSlots[i].SkillName;
                return names;
            }
        }

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
            _workedToday.Clear();
            _geneSlots.Clear();
            _unlockedTech.Clear();
            _cage.Clear();
            _candidates.Clear();
            _byId.Clear();
            ClearDailyEffects();
        }

        public void Lose()
        {
            Phase = RunPhase.Failed;
            Gold = 0;
            TechPoints = 0;
            RetainedEnergy = 0;
            _tools.Clear();
            _workedToday.Clear();
            _geneSlots.Clear();
            _unlockedTech.Clear();
            _cage.Clear();
            _candidates.Clear();
            _byId.Clear();
            ClearDailyEffects();
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
            var monster = Create(skillNames, 0, null, null, personality, race, sourceKind: SkillSourceKind.Opening);
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

        public int GeneCapacity => HasTech("GeneStorage") || HasTech("基因仓储") ? 4 : 3;

        public bool TryReserveGeneSlot(int geneSlotIndex, int breedingSlot)
        {
            if (geneSlotIndex < 0 || geneSlotIndex >= _geneSlots.Count)
                return false;
            if (_geneSlots[geneSlotIndex].IsReserved)
                return false;
            _geneSlots[geneSlotIndex].ReservedBreedingSlot = breedingSlot;
            return true;
        }

        public void UnreserveGene(int breedingSlot)
        {
            for (var i = 0; i < _geneSlots.Count; i++)
            {
                if (_geneSlots[i].ReservedBreedingSlot == breedingSlot)
                    _geneSlots[i].ReservedBreedingSlot = null;
            }
        }

        public void ConsumeReservedGene(int breedingSlot)
        {
            for (var i = 0; i < _geneSlots.Count; i++)
            {
                if (_geneSlots[i].ReservedBreedingSlot == breedingSlot)
                {
                    _geneSlots.RemoveAt(i);
                    return;
                }
            }
        }

        internal GeneSlotCapture[] CaptureGeneSlots()
        {
            var copy = new GeneSlotCapture[_geneSlots.Count];
            for (var i = 0; i < copy.Length; i++)
                copy[i] = new GeneSlotCapture(_geneSlots[i].SkillName, _geneSlots[i].ReservedBreedingSlot, _geneSlots[i].Id);

            return copy;
        }

        internal void RestoreGeneSlots(GeneSlotCapture[] slots)
        {
            _geneSlots.Clear();
            for (var i = 0; i < slots.Length; i++)
                _geneSlots.Add(new GeneSlot(slots[i].SkillName, slots[i].ReservedBreedingSlot, slots[i].Id));
        }

        internal string[] CaptureSkillSlots()
        {
            var copy = new string[_geneSlots.Count];
            for (var i = 0; i < copy.Length; i++)
                copy[i] = _geneSlots[i].SkillName;

            return copy;
        }

        internal void RestoreSkillSlots(string[] slots)
        {
            _geneSlots.Clear();
            for (var i = 0; i < slots.Length; i++)
                _geneSlots.Add(new GeneSlot(slots[i]));
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

        public void PutSkillInSlot(string skillName) => _geneSlots.Add(new GeneSlot(skillName, id: "g" + _nextGeneId++));

        public bool TryTakeSkillAt(int index, out string skillName)
        {
            skillName = null;
            if (index < 0 || index >= _geneSlots.Count)
                return false;

            if (_geneSlots[index].IsReserved) return false;
            skillName = _geneSlots[index].SkillName;
            _geneSlots.RemoveAt(index);
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
            int consecutiveRestDays = 0, SkillSourceKind sourceKind = SkillSourceKind.Fixture) =>
            Create(skillNames, modifier, appearance, parentIds, personality, race, mood, consecutiveWorkDays, consecutiveRestDays, sourceKind);

        public string SkillGainFailure(string monsterId, string skillName)
        {
            var monster = Find(monsterId);
            if (monster == null) return "怪物不存在";
            if (string.IsNullOrEmpty(skillName) || !HasContentSkill(skillName)) return "技能不存在";
            if (monster.Skills.Count >= (monster.Personality == "安于现状" ? 2 : 4)) return "技能已达上限";
            if (!HasTool("复读腺") && monster.HasSkill(skillName)) return "重复技能需要复读腺";
            return null;
        }

        public SkillAcquisitionResult GainSkill(string monsterId, string skillName, bool isTemporary = false,
            SkillSourceKind sourceKind = SkillSourceKind.SkillEffect, string sourceId = null, string sourceMonsterId = null)
        {
            var failure = SkillGainFailure(monsterId, skillName);
            if (failure != null) return new SkillAcquisitionResult(null, failure);
            var instance = MakeSkill(skillName, isTemporary, sourceKind, sourceId, sourceMonsterId);
            Find(monsterId).Skills.Add(instance);
            return new SkillAcquisitionResult(instance);
        }

        public bool TryGainSkill(string monsterId, string skillName, bool isTemporary = false) =>
            GainSkill(monsterId, skillName, isTemporary).Success;

        public IReadOnlyList<SkillInstance> ChangePersonality(string monsterId, string personality)
        {
            var monster = Find(monsterId);
            if (monster == null) return System.Array.Empty<SkillInstance>();
            var removed = monster.ChangePersonality(personality, this.GetUtility<IDraw>());
            LastRemovedSkills = removed;
            var fixedMood = this.GetUtility<MoodCatalog>().FixedMood(personality);
            var restored = monster.DayBaseMood;
            for (var i = 0; i < monster.DayMoodBoost; i++) restored = this.GetUtility<MoodCatalog>().UpgradeMood(restored);
            monster.SetMood(fixedMood ?? restored);
            return removed;
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

        bool HasContentSkill(string name)
        {
            var names = this.GetUtility<SkillCatalog>().Names();
            for (var i = 0; i < names.Count; i++) if (names[i] == name) return true;
            return false;
        }
        internal SkillInstance MakeSkill(string name, bool isTemporary = false, SkillSourceKind sourceKind = SkillSourceKind.Fixture,
            string sourceId = null, string sourceMonsterId = null) =>
            new SkillInstance(name, this.GetUtility<SkillCatalog>().StartingQuote(name), isTemporary,
                "s" + _nextSkillId++, sourceKind, sourceId, sourceMonsterId);

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
            int consecutiveRestDays = 0, SkillSourceKind sourceKind = SkillSourceKind.Fixture)
        {
            var skills = new SkillInstance[skillNames.Count];
            for (var i = 0; i < skills.Length; i++)
                skills[i] = MakeSkill(skillNames[i], false, sourceKind);

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
                mood ?? this.GetUtility<MoodCatalog>().FixedMood(personality) ?? "普通",
                consecutiveWorkDays,
                consecutiveRestDays);
            monster.BeginDailyMood(mood ?? "普通", mood ?? this.GetUtility<MoodCatalog>().FixedMood(personality) ?? "普通");
            _byId.Add(monster.Id, monster);
            return monster;
        }

    }
}
