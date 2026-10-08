using System.Collections.Generic;

namespace TheCall
{
    internal enum MonsterWhere
    {
        Absent,
        Cage,
        Extraction,
        Breeding,
    }

    internal readonly struct MonsterFact
    {
        public MonsterFact(
            string id,
            MonsterWhere where,
            OperationArea area,
            int cell,
            IReadOnlyList<string> skillNames)
        {
            Id = id;
            Where = where;
            Area = area;
            Cell = cell;
            SkillNames = skillNames;
        }

        public string Id { get; }

        public MonsterWhere Where { get; }

        public OperationArea Area { get; }

        public int Cell { get; }

        public IReadOnlyList<string> SkillNames { get; }
    }

    internal readonly struct TransitionFacts
    {
        readonly MonsterFact[] _monsters;
        readonly string[] _skillNames;

        public TransitionFacts(
            bool inOperation,
            bool breedingSkillUnlocked,
            string[] skillNames,
            string[] extraction,
            string[] breeding,
            string[] breedingSkills,
            MonsterFact[] monsters)
        {
            InOperation = inOperation;
            BreedingSkillUnlocked = breedingSkillUnlocked;
            _skillNames = skillNames;
            SkillSlotCount = skillNames.Length;
            Extraction = extraction;
            Breeding = breeding;
            BreedingSkills = breedingSkills;
            _monsters = monsters;
        }

        public bool InOperation { get; }

        public bool BreedingSkillUnlocked { get; }

        public int SkillSlotCount { get; }

        public IReadOnlyList<string> Extraction { get; }

        public IReadOnlyList<string> Breeding { get; }

        public IReadOnlyList<string> BreedingSkills { get; }

        public bool TryMonster(string monsterId, out MonsterFact monster)
        {
            monster = default;
            if (string.IsNullOrEmpty(monsterId))
                return false;

            for (var i = 0; i < _monsters.Length; i++)
            {
                if (_monsters[i].Id != monsterId)
                    continue;

                monster = _monsters[i];
                return true;
            }

            return false;
        }

        public bool TrySkillName(int skillSlotIndex, out string skillName)
        {
            skillName = null;
            if (skillSlotIndex < 0 || skillSlotIndex >= _skillNames.Length)
                return false;

            skillName = _skillNames[skillSlotIndex];
            return true;
        }
    }

    internal enum TransitionEffect
    {
        Move,
        Replace,
        Exchange,
        Return,
        Discard,
        Invest,
        ReturnBreedingSkill,
    }

    internal readonly struct TransitionPlan
    {
        public TransitionPlan(
            TransitionEffect effect,
            MonsterWhere sourceWhere,
            string sourceMonsterId,
            string otherMonsterId,
            OperationArea fromArea,
            int fromCell,
            OperationArea toArea,
            int toCell,
            int skillSlotIndex,
            int breedingSlot)
        {
            Effect = effect;
            SourceWhere = sourceWhere;
            SourceMonsterId = sourceMonsterId;
            OtherMonsterId = otherMonsterId;
            FromArea = fromArea;
            FromCell = fromCell;
            ToArea = toArea;
            ToCell = toCell;
            SkillSlotIndex = skillSlotIndex;
            BreedingSlot = breedingSlot;
        }

        public TransitionEffect Effect { get; }

        public MonsterWhere SourceWhere { get; }

        public string SourceMonsterId { get; }

        public string OtherMonsterId { get; }

        public OperationArea FromArea { get; }

        public int FromCell { get; }

        public OperationArea ToArea { get; }

        public int ToCell { get; }

        public int SkillSlotIndex { get; }

        public int BreedingSlot { get; }

        public static TransitionPlan Move(MonsterFact source, OperationArea toArea, int toCell) =>
            new TransitionPlan(
                TransitionEffect.Move,
                source.Where,
                source.Id,
                null,
                source.Area,
                source.Cell,
                toArea,
                toCell,
                0,
                0);

        public static TransitionPlan Replace(MonsterFact source, string occupantId, OperationArea toArea, int toCell) =>
            new TransitionPlan(
                TransitionEffect.Replace,
                source.Where,
                source.Id,
                occupantId,
                source.Area,
                source.Cell,
                toArea,
                toCell,
                0,
                0);

        public static TransitionPlan Exchange(MonsterFact source, string occupantId, OperationArea toArea, int toCell) =>
            new TransitionPlan(
                TransitionEffect.Exchange,
                source.Where,
                source.Id,
                occupantId,
                source.Area,
                source.Cell,
                toArea,
                toCell,
                0,
                0);

        public static TransitionPlan Return(MonsterFact source) =>
            new TransitionPlan(
                TransitionEffect.Return,
                source.Where,
                source.Id,
                null,
                source.Area,
                source.Cell,
                source.Area,
                source.Cell,
                0,
                0);

        public static TransitionPlan Discard(MonsterFact source) =>
            new TransitionPlan(
                TransitionEffect.Discard,
                source.Where,
                source.Id,
                null,
                source.Area,
                source.Cell,
                source.Area,
                source.Cell,
                0,
                0);

        public static TransitionPlan Invest(int skillSlotIndex, int breedingSlot) =>
            new TransitionPlan(
                TransitionEffect.Invest,
                MonsterWhere.Absent,
                null,
                null,
                OperationArea.Extraction,
                0,
                OperationArea.Extraction,
                0,
                skillSlotIndex,
                breedingSlot);

        public static TransitionPlan ReturnBreedingSkill(int breedingSlot) =>
            new TransitionPlan(
                TransitionEffect.ReturnBreedingSkill,
                MonsterWhere.Absent,
                null,
                null,
                OperationArea.Extraction,
                0,
                OperationArea.Extraction,
                0,
                0,
                breedingSlot);
    }

    internal static class OperationTransition
    {
        public static bool TryPlan(TransitionFacts facts, OperationDrop drop, out TransitionPlan plan)
        {
            plan = default;
            if (!facts.InOperation)
                return false;

            switch (drop.Payload.Kind)
            {
                case PayloadKind.Monster:
                    return TryMonster(facts, drop, out plan);
                case PayloadKind.SkillChip:
                    return TrySkillChip(facts, drop, out plan);
                case PayloadKind.BreedingSkill:
                    return TryBreedingSkill(facts, drop, out plan);
                default:
                    return false;
            }
        }

        static bool TryMonster(TransitionFacts facts, OperationDrop drop, out TransitionPlan plan)
        {
            plan = default;
            if (!facts.TryMonster(drop.Payload.MonsterId, out var source))
                return false;

            switch (drop.Landing.Place)
            {
                case LandingPlace.Cage:
                    if (source.Where == MonsterWhere.Cage)
                        return false;

                    plan = TransitionPlan.Return(source);
                    return true;
                case LandingPlace.Extraction:
                case LandingPlace.BreedingSeat:
                    return TryBoard(facts, source, drop.Landing, out plan);
                case LandingPlace.Discard:
                    if (facts.SkillSlotCount >= 3 || source.SkillNames.Count == 0)
                        return false;

                    plan = TransitionPlan.Discard(source);
                    return true;
                default:
                    return false;
            }
        }

        static bool TryBoard(TransitionFacts facts, MonsterFact source, DropLanding landing, out TransitionPlan plan)
        {
            plan = default;
            var cells = landing.Place == LandingPlace.Extraction ? facts.Extraction : facts.Breeding;
            if (landing.Index < 0 || landing.Index >= cells.Count)
                return false;

            var toArea = landing.Place == LandingPlace.Extraction
                ? OperationArea.Extraction
                : OperationArea.Breeding;
            var occupantId = cells[landing.Index];
            if (occupantId == null)
            {
                plan = TransitionPlan.Move(source, toArea, landing.Index);
                return true;
            }

            if (occupantId == source.Id)
                return false;

            if (source.Where == MonsterWhere.Cage)
            {
                plan = TransitionPlan.Replace(source, occupantId, toArea, landing.Index);
                return true;
            }

            if (source.Where == MonsterWhere.Extraction || source.Where == MonsterWhere.Breeding)
            {
                plan = TransitionPlan.Exchange(source, occupantId, toArea, landing.Index);
                return true;
            }

            return false;
        }

        static bool TrySkillChip(TransitionFacts facts, OperationDrop drop, out TransitionPlan plan)
        {
            plan = default;
            var place = drop.Landing.Place;
            if (place == LandingPlace.Cage || place == LandingPlace.Extraction || place == LandingPlace.BreedingSeat)
                return false;

            if (place != LandingPlace.BreedingSocket)
                return false;

            var slot = drop.Landing.Index;
            if (!facts.BreedingSkillUnlocked || slot < 0 || slot >= facts.BreedingSkills.Count)
                return false;
            if (facts.BreedingSkills[slot] != null)
                return false;
            if (!facts.TrySkillName(drop.Payload.Index, out _))
                return false;

            plan = TransitionPlan.Invest(drop.Payload.Index, slot);
            return true;
        }

        static bool TryBreedingSkill(TransitionFacts facts, OperationDrop drop, out TransitionPlan plan)
        {
            plan = default;
            var slot = drop.Payload.Index;
            if (slot < 0 || slot >= facts.BreedingSkills.Count || facts.BreedingSkills[slot] == null)
                return false;
            if (drop.Landing.Place != LandingPlace.SkillSlot || facts.SkillSlotCount >= 3)
                return false;

            plan = TransitionPlan.ReturnBreedingSkill(slot);
            return true;
        }
    }
}
