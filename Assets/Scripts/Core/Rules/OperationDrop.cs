namespace TheCall
{
    public enum PayloadKind
    {
        Monster,
        SkillChip,
        BreedingSkill,
    }

    public enum LandingPlace
    {
        Cage,
        Extraction,
        BreedingSeat,
        Discard,
        BreedingSocket,
        SkillSlot,
        EmptySpace,
    }

    public readonly struct DropPayload
    {
        public PayloadKind Kind { get; }

        public string MonsterId { get; }

        public int Index { get; }

        DropPayload(PayloadKind kind, string monsterId, int index)
        {
            Kind = kind;
            MonsterId = monsterId;
            Index = index;
        }

        public static DropPayload Monster(string monsterId) =>
            new DropPayload(PayloadKind.Monster, monsterId, 0);

        public static DropPayload SkillChip(int skillSlotIndex) =>
            new DropPayload(PayloadKind.SkillChip, null, skillSlotIndex);

        public static DropPayload BreedingSkill(int breedingSlot) =>
            new DropPayload(PayloadKind.BreedingSkill, null, breedingSlot);
    }

    public readonly struct DropLanding
    {
        public LandingPlace Place { get; }

        public int Index { get; }

        public string MonsterId { get; }

        public DropLanding(LandingPlace place, int index, string monsterId)
        {
            Place = place;
            Index = index;
            MonsterId = monsterId;
        }
    }

    public readonly struct OperationDrop
    {
        public DropPayload Payload { get; }

        public DropLanding Landing { get; }

        public OperationDrop(DropPayload payload, DropLanding landing)
        {
            Payload = payload;
            Landing = landing;
        }
    }
}
