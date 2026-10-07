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

    internal sealed class SkillCatalog : IUtility
    {
        readonly ContentBook _book;

        public SkillCatalog(ContentBook book) => _book = book;

        public IReadOnlyList<string> Names
        {
            get
            {
                var names = new string[_book.Skills.Count];
                for (var i = 0; i < names.Length; i++)
                    names[i] = _book.Skills[i].Name;

                return names;
            }
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
            int quote;
            if (TryEnergyQuote(skillName, out quote))
                return quote;

            return 0;
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
                   skill.Has(EffectKind.Devour) ||
                   skill.Has(EffectKind.RepeatQuote) ||
                   skill.Has(EffectKind.NextEnergyBonus);
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
    }
}
