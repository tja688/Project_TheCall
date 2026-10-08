using System;
using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    [Flags]
    internal enum SkillAffix
    {
        None = 0,
        Destroy = 1,
        Permanent = 2,
        Capacity = 4,
        Immovable = 8,
    }

    internal enum CountedSide
    {
        Left = -1,
        Right = 1,
    }

    internal enum DrawnPool
    {
        HasAmplify,
        AmplifyOnly,
        NotAmplifyOnly,
        NoAmplifyBit,
        Produce,
        Support,
    }

    internal sealed class SkillCatalog : IUtility
    {
        readonly ContentBook _book;

        public SkillCatalog(ContentBook book) => _book = book;

        public IReadOnlyList<Effect> Effects(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            return skill == null ? Array.Empty<Effect>() : skill.Effects;
        }

        public IReadOnlyList<string> Names()
        {
            var names = new string[_book.Skills.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = _book.Skills[i].Name;

            return names;
        }

        public IReadOnlyList<string> Names(DrawnPool pool)
        {
            var names = new List<string>();
            for (var i = 0; i < _book.Skills.Count; i++)
            {
                if (InPool(_book.Skills[i], pool))
                    names.Add(_book.Skills[i].Name);
            }

            return names;
        }

        public IReadOnlyList<string> Names(DrawnPool pool, Rarity rarity, string taken)
        {
            var matches = new List<SkillDef>();
            for (var i = 0; i < _book.Skills.Count; i++)
            {
                var skill = _book.Skills[i];
                if (skill.Rarity != rarity || !InPool(skill, pool))
                    continue;
                if (taken != null && skill.Name == taken)
                    continue;

                matches.Add(skill);
            }

            matches.Sort((left, right) => left.PoolIndex.CompareTo(right.PoolIndex));
            var names = new string[matches.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = matches[i].Name;

            return names;
        }

        public IReadOnlyList<string> NamesOf(Rarity rarity)
        {
            var matches = new List<SkillDef>();
            for (var i = 0; i < _book.Skills.Count; i++)
            {
                if (_book.Skills[i].Rarity == rarity)
                    matches.Add(_book.Skills[i]);
            }

            matches.Sort((left, right) => left.PoolIndex.CompareTo(right.PoolIndex));
            var names = new string[matches.Count];
            for (var i = 0; i < names.Length; i++)
                names[i] = matches[i].Name;

            return names;
        }

        public Rarity RarityOf(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            return skill == null ? Rarity.White : skill.Rarity;
        }

        public bool TryEnergyQuote(string skillName, out int quote)
        {
            var skill = _book.FindSkill(skillName);
            if (skill != null && skill.Has(EffectKind.EnergyQuote))
            {
                quote = skill.Get(EffectKind.EnergyQuote).A;
                return true;
            }

            quote = 0;
            return false;
        }

        public bool TrySideCount(string skillName, out CountedSide side, out int perMonster)
        {
            var skill = _book.FindSkill(skillName);
            if (skill != null && skill.Has(EffectKind.SideCount))
            {
                var effect = skill.Get(EffectKind.SideCount);
                side = (CountedSide)effect.B;
                perMonster = effect.A;
                return true;
            }

            side = CountedSide.Right;
            perMonster = 0;
            return false;
        }

        public bool DoublesWhenIsolated(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            return skill != null && skill.Has(EffectKind.DoubleWhenIsolated);
        }

        public bool DoublesAdjacentEnergy(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            return skill != null && skill.Has(EffectKind.DoubleAdjacentEnergy);
        }

        public int AddedToOthers(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            return skill != null && skill.Has(EffectKind.AddToOthers) ? skill.Get(EffectKind.AddToOthers).A : 0;
        }

        public int NextEnergyBonus(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            return skill != null && skill.Has(EffectKind.NextEnergyBonus) ? skill.Get(EffectKind.NextEnergyBonus).A : 0;
        }

        public bool TryLandingResponse(string skillName, string causeSkillName, out int quote)
        {
            var skill = _book.FindSkill(skillName);
            if (skill != null && skill.Has(EffectKind.LandingResponse) && skillName != causeSkillName)
            {
                quote = skill.Get(EffectKind.LandingResponse).A;
                return true;
            }

            quote = 0;
            return false;
        }

        public int StartingQuote(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            if (skill == null)
                return 0;
            if (skill.Has(EffectKind.EnergyQuote))
                return skill.Get(EffectKind.EnergyQuote).A;
            if (skill.Has(EffectKind.SideCount))
                return skill.Get(EffectKind.SideCount).A;
            if (skill.Has(EffectKind.LandingResponse))
                return skill.Get(EffectKind.LandingResponse).A;
            if (skill.Has(EffectKind.ChanceQuote))
                return skill.Get(EffectKind.ChanceQuote).A;

            return 0;
        }

        public bool IsActive(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            return skill != null && skill.Use == SkillUse.Active;
        }

        public bool IsProduce(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            return skill != null && (skill.Role & SkillRole.Produce) != 0;
        }

        public bool SwapsWithLeft(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            return skill != null && skill.Has(EffectKind.SwapWithLeft);
        }

        public int ExtraWalksForRightNeighbor(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            return skill != null && skill.Has(EffectKind.ExtraWalksForRightNeighbor)
                ? skill.Get(EffectKind.ExtraWalksForRightNeighbor).A
                : 0;
        }

        public int CapacityExtraForLeftNeighbor(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            return skill != null && skill.Has(EffectKind.CapacityExtraForLeftNeighbor)
                ? skill.Get(EffectKind.CapacityExtraForLeftNeighbor).A
                : 0;
        }

        public bool ProducesEnergy(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            if (skill == null)
                return false;

            return skill.Has(EffectKind.EnergyQuote) ||
                   skill.Has(EffectKind.SideCount) ||
                   skill.Has(EffectKind.RepeatQuote) ||
                   skill.Has(EffectKind.NextEnergyBonus) ||
                   skill.Has(EffectKind.ChanceQuote) ||
                   skill.Has(EffectKind.OwnSkillMultiple) ||
                   skill.Has(EffectKind.PopulationQuote);
        }

        public SkillAffix Affixes(string skillName)
        {
            var skill = _book.FindSkill(skillName);
            return skill == null ? SkillAffix.None : skill.Affix;
        }

        public bool TryGainCapacity(string skillName, out int layers)
        {
            var skill = _book.FindSkill(skillName);
            if (skill != null && skill.Has(EffectKind.GainCapacity))
            {
                layers = skill.Get(EffectKind.GainCapacity).A;
                return true;
            }

            layers = 0;
            return false;
        }

        public bool TryDevour(string skillName, out int writeback, out bool permanent)
        {
            var skill = _book.FindSkill(skillName);
            if (skill != null && skill.Has(EffectKind.Devour))
            {
                var effect = skill.Get(EffectKind.Devour);
                writeback = effect.A;
                permanent = effect.B == 1;
                return true;
            }

            writeback = 0;
            permanent = false;
            return false;
        }

        public bool TryRepeatedQuote(string skillName, out int times)
        {
            var skill = _book.FindSkill(skillName);
            if (skill != null && skill.Has(EffectKind.RepeatQuote))
            {
                times = skill.Get(EffectKind.RepeatQuote).A;
                return true;
            }

            times = 0;
            return false;
        }

        public int CapacityExtraForRightNeighbor(string skillName) =>
            Amount(skillName, EffectKind.CapacityExtraForRightNeighbor);

        public int RightRowBonus(string skillName) => Amount(skillName, EffectKind.RightRowOnLeftActive);

        public int GrowAmount(string skillName) => Amount(skillName, EffectKind.GrowOnClear);

        public int GoldOf(string skillName) => Amount(skillName, EffectKind.GainGold);

        public bool GrantsSameNameExtra(string skillName) => Has(skillName, EffectKind.SameNameExtra);

        public bool FillsSkills(string skillName) => Has(skillName, EffectKind.FillSkills);

        public bool CopiesBreeding(string skillName) => Has(skillName, EffectKind.CopyBreeding);

        public bool HasKin(string skillName) => Has(skillName, EffectKind.KinExtra);

        public bool HasLegacy(string skillName) => Has(skillName, EffectKind.LegacyOnDiscard);

        public bool IsPopulation(string skillName) => Has(skillName, EffectKind.PopulationQuote);

        public bool TryOwnMultiple(string skillName, out int factor)
        {
            var skill = _book.FindSkill(skillName);
            if (skill != null && skill.Has(EffectKind.OwnSkillMultiple))
            {
                factor = skill.Get(EffectKind.OwnSkillMultiple).A;
                return true;
            }

            factor = 0;
            return false;
        }

        public bool TryChance(string skillName, out int percent, out int quote)
        {
            var skill = _book.FindSkill(skillName);
            if (skill != null && skill.Has(EffectKind.ChanceQuote))
            {
                var effect = skill.Get(EffectKind.ChanceQuote);
                quote = effect.A;
                percent = effect.B;
                return true;
            }

            percent = 0;
            quote = 0;
            return false;
        }

        public bool TrySkillCountExtra(string skillName, out int when, out int walks)
        {
            var skill = _book.FindSkill(skillName);
            if (skill != null && skill.Has(EffectKind.SkillCountExtra))
            {
                var effect = skill.Get(EffectKind.SkillCountExtra);
                when = effect.A;
                walks = effect.B;
                return true;
            }

            when = 0;
            walks = 0;
            return false;
        }

        public bool TrySkillCountAdd(string skillName, out int when, out int amount)
        {
            var skill = _book.FindSkill(skillName);
            if (skill != null && skill.Has(EffectKind.SkillCountAdd))
            {
                var effect = skill.Get(EffectKind.SkillCountAdd);
                when = effect.A;
                amount = effect.B;
                return true;
            }

            when = 0;
            amount = 0;
            return false;
        }

        public bool TryEdge(string skillName, out CountedSide side, out int amount)
        {
            var skill = _book.FindSkill(skillName);
            if (skill != null && skill.Has(EffectKind.EdgeBonus))
            {
                var effect = skill.Get(EffectKind.EdgeBonus);
                amount = effect.A;
                side = (CountedSide)effect.B;
                return true;
            }

            side = CountedSide.Left;
            amount = 0;
            return false;
        }

        static bool InPool(SkillDef skill, DrawnPool pool)
        {
            var role = skill.Role;
            if (pool == DrawnPool.HasAmplify)
                return (role & SkillRole.Amplify) != 0;
            if (pool == DrawnPool.AmplifyOnly)
                return role == SkillRole.Amplify;
            if (pool == DrawnPool.NotAmplifyOnly)
                return role != SkillRole.None && role != SkillRole.Amplify;
            if (pool == DrawnPool.NoAmplifyBit)
                return role != SkillRole.None && (role & SkillRole.Amplify) == 0;
            if (pool == DrawnPool.Produce)
                return (role & SkillRole.Produce) != 0;

            return (role & SkillRole.Support) != 0;
        }

        bool Has(string skillName, EffectKind kind)
        {
            var skill = _book.FindSkill(skillName);
            return skill != null && skill.Has(kind);
        }

        int Amount(string skillName, EffectKind kind)
        {
            var skill = _book.FindSkill(skillName);
            return skill != null && skill.Has(kind) ? skill.Get(kind).A : 0;
        }
    }
}
