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
            var personalities = this.GetUtility<IPersonalityCatalog>();
            var races = this.GetUtility<IRaceCatalog>();
            for (var slot = 0; slot < level.LockedSlotCount; slot++)
            {
                var parents = Parents(level, run, slot);
                var skill = level.LockedSkill(slot);
                if (parents.Count < 2)
                {
                    run.UnreserveGene(slot);
                    continue;
                }

                var firstParent = parents[slot % parents.Count];
                var secondParent = parents[(slot + 1) % parents.Count];
                // 外观只剩配方和色板。旧部件遗传已删除，见 Assets/Notes/新怪物系统交接.md。
                var appearance = MonsterAppearance.Breed(
                    firstParent.Appearance,
                    secondParent.Appearance,
                    AppearanceSeed(firstParent.Id, secondParent.Id, slot));

                string personality;
                if (draw.Chance(40))
                {
                    var parentWithPersonality = draw.Choose(parents);
                    personality = parentWithPersonality.Personality ?? personalities.Draw(draw);
                }
                else
                {
                    personality = personalities.Draw(draw);
                }

                var parentWithRace = draw.Choose(parents);
                var race = parentWithRace.Race ?? races.Draw(draw);

                var maxSkills = personality == "安于现状" ? 2 : 4;
                var candidates = Inherit(parents, draw, run.HasTool("复读腺"));
                string geneId = null;
                foreach (var gene in run.GeneSlots) if (gene.ReservedBreedingSlot == slot) geneId = gene.Id;
                var geneCandidate = string.IsNullOrEmpty(skill) ? null :
                    new Candidate(skill, SkillSourceKind.Gene, geneId, null);
                if (geneCandidate != null && (run.HasTool("复读腺") || !Contains(candidates, skill)))
                    candidates.Add(geneCandidate);
                var tech = this.GetUtility<TechCatalog>();
                if (tech.GrantsExtraSkill(run.UnlockedTech))
                {
                    var pool = new List<string>();
                    foreach (var name in this.GetUtility<SkillCatalog>().Names())
                        if (run.HasTool("复读腺") || !Contains(candidates, name)) pool.Add(name);
                    // Mutation is rolled even if the eventual child is already at capacity.
                    if (pool.Count > 0 && draw.Chance(ContentGate.Current.Economy.TechPercent))
                        candidates.Add(new Candidate(draw.Choose(pool), SkillSourceKind.Mutation, "mutation:" + slot, null));
                }
                while (candidates.Count > maxSkills)
                {
                    var indices = new List<int>();
                    for (var i = 0; i < candidates.Count; i++) indices.Add(i);
                    candidates.RemoveAt(draw.Choose(indices));
                }
                if (geneCandidate != null && candidates.Contains(geneCandidate)) run.ConsumeReservedGene(slot);
                else run.UnreserveGene(slot);

                var modifier = 0;
                if (tech.GrantsModifier(run.UnlockedTech) && draw.Chance(ContentGate.Current.Economy.TechPercent))
                    modifier += ContentGate.Current.Economy.ModifierAmount;

                var parentIds = new string[parents.Count];
                for (var parent = 0; parent < parents.Count; parent++)
                    parentIds[parent] = parents[parent].Id;

                var names = new List<string>();
                foreach (var candidate in candidates) names.Add(candidate.Name);
                var child = run.CreateMonster(names, modifier, appearance, parentIds, personality: personality, race: race);
                for (var i = 0; i < candidates.Count; i++)
                    child.Skills[i] = run.MakeSkill(candidates[i].Name, false, candidates[i].Kind, candidates[i].SourceId, candidates[i].ParentId);
                run.AddToCage(child);
            }
        }

        static int AppearanceSeed(string firstParentId, string secondParentId, int slot)
        {
            unchecked
            {
                var seed = 17;
                var first = firstParentId ?? string.Empty;
                for (var i = 0; i < first.Length; i++)
                    seed = seed * 31 + first[i];
                var second = secondParentId ?? string.Empty;
                for (var i = 0; i < second.Length; i++)
                    seed = seed * 31 + second[i];
                return seed * 397 + slot;
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

        sealed class Candidate
        {
            public Candidate(string name, SkillSourceKind kind, string sourceId, string parentId)
            { Name = name; Kind = kind; SourceId = sourceId; ParentId = parentId; }
            public string Name { get; }
            public SkillSourceKind Kind { get; }
            public string SourceId { get; }
            public string ParentId { get; }
        }
        static bool Contains(List<Candidate> candidates, string name) => candidates.Exists(x => x.Name == name);
        static List<string> Options(Monster parent, List<Candidate> chosen)
        {
            var names = new List<string>();
            foreach (var skill in parent.Skills)
                if (!Contains(chosen, skill.Name) && !names.Contains(skill.Name)) names.Add(skill.Name);
            return names;
        }
        static Candidate FromParent(Monster parent, string name)
        {
            foreach (var skill in parent.Skills)
                if (skill.Name == name) return new Candidate(name, SkillSourceKind.Parent, skill.Id, parent.Id);
            throw new System.InvalidOperationException("亲本候选不存在");
        }
        static List<Candidate> Inherit(List<Monster> parents, IDraw draw, bool duplicates)
        {
            var chosen = new List<Candidate>();
            foreach (var parent in parents)
            {
                if (duplicates)
                {
                    var indices = new List<int>();
                    for (var i = 0; i < parent.Skills.Count; i++) indices.Add(i);
                    var instance = parent.Skills[indices.Count == 1 ? 0 : draw.Choose(indices)];
                    chosen.Add(new Candidate(instance.Name, SkillSourceKind.Parent, instance.Id, parent.Id));
                    continue;
                }
                var options = Options(parent, chosen);
                if (options.Count == 0)
                {
                    // Follow alternate-source chains, so three-parent collisions do not lose a
                    // contribution when a legal distinct assignment exists.
                    for (var i = 0; i < chosen.Count && options.Count == 0; i++)
                    {
                        if (!parent.HasSkill(chosen[i].Name)) continue;
                        if (MoveContribution(parents, chosen, i, new HashSet<int>(), draw))
                            options = Options(parent, chosen);
                    }
                }
                if (options.Count != 0)
                    chosen.Add(FromParent(parent, options.Count == 1 ? options[0] : draw.Choose(options)));
            }
            return chosen;
        }

        static bool MoveContribution(List<Monster> parents, List<Candidate> chosen, int index, HashSet<int> visited, IDraw draw)
        {
            if (!visited.Add(index)) return false;
            var source = parents.Find(x => x.Id == chosen[index].ParentId);
            var alternatives = new List<string>();
            foreach (var skill in source.Skills)
                if (skill.Name != chosen[index].Name && !alternatives.Contains(skill.Name)) alternatives.Add(skill.Name);
            while (alternatives.Count > 0)
            {
                var name = alternatives.Count == 1 ? alternatives[0] : draw.Choose(alternatives);
                alternatives.Remove(name);
                var conflict = chosen.FindIndex(x => x.Name == name);
                if (conflict < 0 || MoveContribution(parents, chosen, conflict, visited, draw))
                {
                    chosen[index] = FromParent(source, name);
                    return true;
                }
            }
            return false;
        }

        protected override void OnInit()
        {
        }
    }
}
