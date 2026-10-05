using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class BreedingSystem : AbstractSystem
    {
        public void Spawn()
        {
            var level = this.GetModel<LevelModel>();
            var run = this.GetModel<RunModel>();
            var draw = this.GetUtility<IDraw>();
            for (var slot = 0; slot < level.LockedSlotCount; slot++)
            {
                var parents = Parents(level, run, slot);
                var skill = level.LockedSkill(slot);
                if (parents.Count == 0)
                {
                    ReturnSkill(run, skill);
                    continue;
                }

                var names = Inherit(parents, draw);
                var tech = this.GetUtility<TechCatalog>();
                if (!string.IsNullOrEmpty(skill))
                {
                    if (!names.Contains(skill) && names.Count < 4)
                        names.Add(skill);
                    else
                        ReturnSkill(run, skill);
                }

                if (tech.GrantsExtraSkill(run.UnlockedTech))
                {
                    var pool = Addable(this.GetUtility<SkillCatalog>(), names);
                    if (pool.Count > 0 && draw.Chance(TechCatalog.Percent))
                        names.Add(draw.Choose(pool));
                }

                var modifier = 0;
                if (tech.GrantsModifier(run.UnlockedTech) && draw.Chance(TechCatalog.Percent))
                    modifier += TechCatalog.ModifierAmount;

                run.AddToCage(run.CreateMonster(names, modifier));
            }
        }

        static List<Monster> Parents(LevelModel level, RunModel run, int slot)
        {
            var parents = new List<Monster>();
            var count = level.LockedParentCount(slot);
            for (var seat = 0; seat < count; seat++)
            {
                var id = level.LockedParent(slot, seat);
                if (id == null)
                    continue;

                var parent = run.Find(id);
                if (parent != null && parent.Skills.Count > 0)
                    parents.Add(parent);
            }

            return parents;
        }

        static void ReturnSkill(RunModel run, string skill)
        {
            if (string.IsNullOrEmpty(skill) || run.SkillSlots.Count >= 3)
                return;

            run.PutSkillInSlot(skill);
        }

        static List<string> Addable(SkillCatalog catalog, List<string> names)
        {
            var pool = new List<string>();
            if (names.Count >= 4)
                return pool;

            var all = catalog.Names;
            for (var i = 0; i < all.Count; i++)
            {
                if (!names.Contains(all[i]))
                    pool.Add(all[i]);
            }

            return pool;
        }

        static List<string> Inherit(List<Monster> parents, IDraw draw)
        {
            var allSingle = true;
            for (var i = 0; i < parents.Count; i++)
            {
                if (parents[i].Skills.Count != 1)
                    allSingle = false;
            }

            if (allSingle)
                return Distinct(parents);

            var chosen = new List<string>();
            for (var i = 0; i < parents.Count; i++)
            {
                var options = Except(parents[i], chosen);
                if (options.Count == 0)
                {
                    Release(parents, chosen, i, draw);
                    options = Except(parents[i], chosen);
                }

                if (options.Count == 0)
                    continue;

                chosen.Add(options.Count == 1 ? options[0] : draw.Choose(options));
            }

            return chosen;
        }

        static List<string> Distinct(List<Monster> parents)
        {
            var names = new List<string>();
            for (var i = 0; i < parents.Count; i++)
            {
                var name = parents[i].Skills[0].Name;
                if (!names.Contains(name))
                    names.Add(name);
            }

            return names;
        }

        static void Release(List<Monster> parents, List<string> chosen, int stuckIndex, IDraw draw)
        {
            var stuck = parents[stuckIndex];
            for (var i = 0; i < stuckIndex; i++)
            {
                if (!Has(stuck, chosen[i]))
                    continue;

                var alternatives = new List<string>();
                var skills = parents[i].Skills;
                for (var skill = 0; skill < skills.Count; skill++)
                {
                    var name = skills[skill].Name;
                    if (name != chosen[i] && !chosen.Contains(name))
                        alternatives.Add(name);
                }

                if (alternatives.Count == 0)
                    continue;

                chosen[i] = alternatives.Count == 1 ? alternatives[0] : draw.Choose(alternatives);
                return;
            }
        }

        static List<string> Except(Monster parent, List<string> chosen)
        {
            var options = new List<string>();
            for (var i = 0; i < parent.Skills.Count; i++)
            {
                var name = parent.Skills[i].Name;
                if (!chosen.Contains(name))
                    options.Add(name);
            }

            return options;
        }

        static bool Has(Monster parent, string name)
        {
            for (var i = 0; i < parent.Skills.Count; i++)
            {
                if (parent.Skills[i].Name == name)
                    return true;
            }

            return false;
        }

        protected override void OnInit()
        {
        }
    }
}
