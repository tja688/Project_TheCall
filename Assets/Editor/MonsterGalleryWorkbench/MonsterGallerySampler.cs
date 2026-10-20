#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TheCall.Editor
{
    /// <summary>
    /// Rolls monsters through the same content draws the game uses.
    /// Birth mood, later-day mood, shop shapes, opening cages, and breeding inheritance stay on those rules.
    /// </summary>
    static class MonsterGallerySampler
    {
        const int DuplicateToolChance = 15;
        const int GeneSlotChance = 40;
        const int ThreeParentChance = 30;
        const int FreshMoodChance = 40;

        internal static Specimen Roll(string source)
        {
            var book = Book();
            var draw = new RngDraw(new System.Random());
            var skills = new SkillCatalog(book);
            var personalities = new PersonalityCatalog(book);
            var races = new RaceCatalog(book);
            var moods = new MoodCatalog(book);
            var channel = Channel(source, draw);
            var duplicates = draw.Chance(DuplicateToolChance);
            var specimen = new Specimen
            {
                Channel = channel,
                Duplicates = duplicates,
                Seed = draw.NextSeed(),
            };

            if (channel == "breed")
                FillBreed(specimen, skills, personalities, races, draw, duplicates);
            else
                FillWild(specimen, channel, skills, personalities, races, draw, duplicates);

            FillMood(specimen, moods, draw);
            specimen.Summary = Summary(specimen);
            return specimen;
        }

        static void FillWild(Specimen specimen, string channel, SkillCatalog skills, PersonalityCatalog personalities,
            RaceCatalog races, RngDraw draw, bool duplicates)
        {
            specimen.Personality = personalities.Draw(draw);
            specimen.Race = races.Draw(draw);
            specimen.Skills = new List<string>(SkillsFor(channel, skills, draw, duplicates));
            var appearance = MonsterAppearance.FromSeed(specimen.Seed);
            CopyAppearance(specimen, appearance);
            specimen.ParentCount = 0;
            specimen.Detail = WildDetail(channel, specimen.Skills.Count, duplicates);
        }

        static void FillBreed(Specimen specimen, SkillCatalog skills, PersonalityCatalog personalities,
            RaceCatalog races, RngDraw draw, bool duplicates)
        {
            var parentCount = draw.Chance(ThreeParentChance) ? 3 : 2;
            var parents = new List<Parent>(parentCount);
            for (var i = 0; i < parentCount; i++)
            {
                var parentChannel = draw.Choose(new[] { "shop", "shop", "opening-candidate", "opening-produce", "opening-support" });
                var parent = new Parent
                {
                    Personality = personalities.Draw(draw),
                    Race = races.Draw(draw),
                    Skills = new List<string>(SkillsFor(parentChannel, skills, draw, duplicates)),
                    Appearance = MonsterAppearance.FromSeed(draw.NextSeed()),
                };
                parents.Add(parent);
            }

            var appearances = new MonsterAppearance[parents.Count];
            for (var i = 0; i < parents.Count; i++) appearances[i] = parents[i].Appearance;
            CopyAppearance(specimen, MonsterAppearance.Breed(appearances, specimen.Seed));

            var chosen = Inherit(parents, draw, duplicates);
            string gene = null;
            if (draw.Chance(GeneSlotChance))
            {
                var pool = Available(skills.Names(), chosen, duplicates);
                if (pool.Count > 0)
                {
                    gene = draw.Choose(pool);
                    chosen.Add(gene);
                }
            }

            string mutation = null;
            var techPercent = ContentGate.Current.Economy.TechPercent;
            if (draw.Chance(techPercent))
            {
                var pool = Available(skills.Names(), chosen, duplicates);
                if (pool.Count > 0)
                {
                    mutation = draw.Choose(pool);
                    chosen.Add(mutation);
                }
            }

            specimen.Personality = draw.Chance(40) ? draw.Choose(parents).Personality : personalities.Draw(draw);
            specimen.Race = draw.Choose(parents).Race;
            var maxSkills = specimen.Personality == "安于现状" ? 2 : 4;
            while (chosen.Count > maxSkills)
                chosen.RemoveAt(draw.ChooseIndex(chosen.Count));

            specimen.Skills = chosen;
            specimen.ParentCount = parentCount;
            specimen.Detail = BreedDetail(parentCount, gene, mutation, duplicates, specimen.Personality == "安于现状");
        }

        static void FillMood(Specimen specimen, MoodCatalog moods, RngDraw draw)
        {
            var locked = moods.FixedMood(specimen.Personality);
            if (draw.Chance(FreshMoodChance))
            {
                specimen.Mood = locked ?? "普通";
                specimen.MoodNote = locked != null
                    ? "刚生成。性格把心情固定为" + locked + "。"
                    : "刚生成，心情还是普通。";
                return;
            }

            var working = draw.Chance(50);
            var days = 1 + draw.ChooseIndex(6);
            var work = working ? days : 0;
            var rest = working ? 0 : draw.ChooseIndex(7);
            var cotton = draw.Chance(30);
            specimen.Mood = moods.CalculateDailyMood(specimen.Personality, work, rest, cotton);
            var daysText = working ? "连续工作 " + work + " 天" : rest == 0 ? "还没有连续工作或休息" : "连续休息 " + rest + " 天";
            specimen.MoodNote = daysText + (cotton ? "，持有隔音棉" : "") + "。";
            if (locked != null) specimen.MoodNote += "性格把心情固定为" + locked + "。";
        }

        static string[] SkillsFor(string channel, SkillCatalog skills, RngDraw draw, bool duplicates)
        {
            switch (channel)
            {
                case "opening-candidate": return StockDraw.OpeningPair(skills, draw);
                case "opening-produce": return new[] { StockDraw.One(skills, draw, DrawnPool.Produce, null) };
                case "opening-support": return new[] { StockDraw.One(skills, draw, DrawnPool.Support, null) };
                case "spawned": return new[] { StockDraw.One(skills, draw, DrawnPool.NoAmplifyBit, null) };
                case "shop": return StockDraw.ShopOffer(skills, draw, duplicates);
                default: throw new InvalidOperationException("未知的怪物来源。");
            }
        }

        static string Channel(string source, RngDraw draw)
        {
            switch (source)
            {
                case "any":
                    return draw.Choose(new[]
                    {
                        "opening-candidate",
                        "opening-produce", "opening-produce",
                        "opening-support", "opening-support",
                        "shop", "shop", "shop", "shop",
                        "breed", "breed", "breed",
                        "spawned",
                    });
                case "opening":
                    return draw.Choose(new[]
                    {
                        "opening-candidate",
                        "opening-produce", "opening-produce", "opening-produce", "opening-produce",
                        "opening-support", "opening-support", "opening-support", "opening-support",
                    });
                case "shop": return "shop";
                case "breed": return "breed";
                default: throw new InvalidOperationException("来源只能是 any、opening、shop 或 breed。");
            }
        }

        static List<string> Inherit(List<Parent> parents, RngDraw draw, bool duplicates)
        {
            var chosen = new List<string>();
            foreach (var parent in parents)
            {
                if (duplicates)
                {
                    chosen.Add(draw.Choose(parent.Skills));
                    continue;
                }

                var options = new List<string>();
                foreach (var name in parent.Skills)
                    if (!chosen.Contains(name) && !options.Contains(name)) options.Add(name);
                if (options.Count == 0) continue;
                chosen.Add(options.Count == 1 ? options[0] : draw.Choose(options));
            }

            return chosen;
        }

        static List<string> Available(IReadOnlyList<string> names, List<string> taken, bool duplicates)
        {
            var pool = new List<string>();
            for (var i = 0; i < names.Count; i++)
                if (duplicates || !taken.Contains(names[i])) pool.Add(names[i]);
            return pool;
        }

        static void CopyAppearance(Specimen specimen, MonsterAppearance appearance)
        {
            specimen.Recipe = appearance.Recipe;
            specimen.Palette = appearance.Palette;
            specimen.HeadAccessory = appearance.HeadAccessory;
            specimen.BackAccessory = appearance.BackAccessory;
            specimen.Variation = appearance.Variation;
        }

        static string WildDetail(string channel, int skillCount, bool duplicates)
        {
            string lead;
            switch (channel)
            {
                case "opening-candidate": lead = "开局三选一里留下的那只，带两个技能。"; break;
                case "opening-produce": lead = "开局留人后补进笼子的生产技能怪物。"; break;
                case "opening-support": lead = "开局留人后补进笼子的辅助技能怪物。"; break;
                case "spawned": lead = "技能效果当场生成的一只，只有一个技能。"; break;
                default:
                    lead = "商店货架上的怪物，这次抽出 " + skillCount + " 个技能。";
                    break;
            }

            if (duplicates && channel == "shop") lead += "持有复读腺，两个技能可以同名。";
            return lead;
        }

        static string BreedDetail(int parents, string gene, string mutation, bool duplicates, bool capped)
        {
            var text = parents == 3 ? "三亲培育。" : "两亲培育。";
            text += "每个亲本贡献一个技能，色板跟随第二亲本。";
            if (duplicates) text += "持有复读腺，同名技能可以留下，但不会因此多画一件。";
            if (gene != null) text += "这个槽放入了基因「" + gene + "」。";
            if (mutation != null) text += "额外技能科技掷中了突变「" + mutation + "」。";
            if (capped) text += "性格是安于现状，技能最多留两个。";
            else text += "技能超过四个时会随机裁掉。";
            return text;
        }

        static string Summary(Specimen specimen)
        {
            var catalog = MonsterArtCatalog.Runtime;
            var body = catalog != null && catalog.bodies != null && specimen.Recipe < catalog.bodies.Length
                ? catalog.Body(specimen.Recipe).name
                : "体型 " + specimen.Recipe;
            return body + " · " + specimen.Personality + " · " + specimen.Mood + " · " + Join(specimen.Skills);
        }

        static string Join(List<string> skills)
        {
            if (skills == null || skills.Count == 0) return "无技能";
            return string.Join("、", skills);
        }

        static ContentBook Book()
        {
            if (ContentGate.Current != null) return ContentGate.Current;
            if (TheCallApp.IsRunning) throw new InvalidOperationException("对局正在运行，但内容目录还没装入。");
            var path = Path.Combine(Application.streamingAssetsPath, "call-book.json");
            var book = ContentBook.Parse(File.ReadAllText(path));
            ContentGate.Use(book);
            return book;
        }

        sealed class Parent
        {
            public MonsterAppearance Appearance;
            public string Personality;
            public string Race;
            public List<string> Skills;
        }

        sealed class RngDraw : IDraw
        {
            readonly System.Random _random;

            public RngDraw(System.Random random) => _random = random;

            public int NextSeed() => _random.Next();

            public int ChooseIndex(int count) => _random.Next(count);

            public T Choose<T>(IReadOnlyList<T> options)
            {
                if (options == null || options.Count == 0) throw new InvalidOperationException("抽取名单是空的。");
                return options[_random.Next(options.Count)];
            }

            public bool Chance(int percent)
            {
                if (percent <= 0) return false;
                if (percent >= 100) return true;
                return _random.Next(100) < percent;
            }
        }
    }
}
#endif
