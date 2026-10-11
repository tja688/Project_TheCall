using System;

namespace TheCall
{
    internal enum BeatSite
    {
        Pop,
        Mark,
        Carry,
        Cause,
        Existing,
        Outside,
    }

    internal enum BeatProof
    {
        None,
        AddOnSelf,
        AddFromSelf,
        FactorFromSelf,
        OwnLanding,
        Cause,
    }

    internal readonly struct BeatSpec
    {
        public BeatSpec(BeatSite site, bool canMiss, BeatProof proof)
        {
            Site = site;
            CanMiss = canMiss;
            Proof = proof;
        }

        public BeatSite Site { get; }

        public bool CanMiss { get; }

        public BeatProof Proof { get; }
    }

    internal static class BeatBook
    {
        static BeatBook()
        {
            var kinds = (EffectKind[])Enum.GetValues(typeof(EffectKind));
            for (var i = 0; i < kinds.Length; i++)
                Spec(kinds[i]);
        }

        public static BeatSpec Spec(EffectKind kind)
        {
            switch (kind)
            {
                case EffectKind.EnergyQuote:
                case EffectKind.SideCount:
                case EffectKind.RepeatQuote:
                case EffectKind.OwnSkillMultiple:
                case EffectKind.PersonalityCountMultiple:
                case EffectKind.PopulationQuote:
                case EffectKind.GainCapacity:
                    return new BeatSpec(BeatSite.Pop, false, BeatProof.None);
                case EffectKind.ChanceQuote:
                    return new BeatSpec(BeatSite.Pop, true, BeatProof.None);
                case EffectKind.GainGold:
                    return new BeatSpec(BeatSite.Mark, false, BeatProof.None);
                case EffectKind.FillSkills:
                case EffectKind.CopyBreeding:
                case EffectKind.StealSkillOnOtherDestroyed:
                    return new BeatSpec(BeatSite.Mark, true, BeatProof.None);
                case EffectKind.SkillCountAdd:
                case EffectKind.EdgeBonus:
                    return new BeatSpec(BeatSite.Carry, true, BeatProof.AddOnSelf);
                case EffectKind.AddToOthers:
                case EffectKind.NextEnergyBonus:
                case EffectKind.RightRowOnLeftActive:
                case EffectKind.RightNeighborOnLeftActive:
                case EffectKind.LegacyAddToOthers:
                    return new BeatSpec(BeatSite.Carry, true, BeatProof.AddFromSelf);
                case EffectKind.DoubleAdjacentEnergy:
                case EffectKind.DoubleWhenIsolated:
                    return new BeatSpec(BeatSite.Carry, true, BeatProof.FactorFromSelf);
                case EffectKind.LandingResponse:
                case EffectKind.CorpseEnergyQuote:
                    return new BeatSpec(BeatSite.Carry, true, BeatProof.OwnLanding);
                case EffectKind.CapacityExtraForLeftNeighbor:
                case EffectKind.CapacityExtraForRightNeighbor:
                case EffectKind.ExtraWalksForRightNeighbor:
                case EffectKind.SkillCountExtra:
                case EffectKind.SameNameExtra:
                case EffectKind.KinExtra:
                    return new BeatSpec(BeatSite.Cause, true, BeatProof.Cause);
                case EffectKind.Devour:
                case EffectKind.SwapWithLeft:
                case EffectKind.SelfDestruct:
                case EffectKind.DevourLeftNeighborSkillMultiple:
                case EffectKind.KillRandomOther:
                case EffectKind.SacrificeNeighborsPerMonsterEnergy:
                case EffectKind.CullUntilAlonePerMonsterEnergy:
                    return new BeatSpec(BeatSite.Existing, false, BeatProof.None);
                case EffectKind.GrowOnClear:
                case EffectKind.LegacyOnDiscard:
                    return new BeatSpec(BeatSite.Outside, false, BeatProof.None);
                default:
                    throw new InvalidOperationException("BeatBook 缺少 " + kind);
            }
        }
    }
}
