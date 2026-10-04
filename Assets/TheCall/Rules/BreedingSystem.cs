using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal sealed class BreedingSystem : AbstractSystem
    {
        public void Spawn()
        {
            var parents = Parents();
            if (parents.Count == 0)
                return;

            var run = this.GetModel<RunModel>();
            run.AddToCage(run.CreateMonster(Inherit(parents, this.GetUtility<IDraw>())));
        }

        List<Monster> Parents()
        {
            var locked = this.GetModel<LevelModel>().LockedParents;
            if (locked == null)
                return new List<Monster>();

            var run = this.GetModel<RunModel>();
            var parents = new List<Monster>();
            for (var i = 0; i < locked.Count; i++)
            {
                if (locked[i] == null)
                    continue;

                var parent = run.Find(locked[i]);
                if (parent != null && parent.Skills.Count > 0)
                    parents.Add(parent);
            }

            return parents;
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
