using System;
using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal readonly struct BenchSkill
    {
        public BenchSkill(string name, int growth = 0, bool temporary = false) { Name = name; Growth = growth; Temporary = temporary; }
        public string Name { get; }
        public int Growth { get; }
        public bool Temporary { get; }
    }

    internal readonly struct BenchMonster
    {
        public BenchMonster(IReadOnlyList<string> skillNames, string personality = null, string mood = null, string race = null,
            IReadOnlyList<BenchSkill> instances = null, int modifier = 0, int workDays = 0, int restDays = 0, IReadOnlyList<int> parentCells = null)
        {
            SkillNames = skillNames;
            Personality = personality;
            Mood = mood;
            Race = race;
            Instances = instances; Modifier = modifier; WorkDays = workDays; RestDays = restDays; ParentCells = parentCells;
        }

        public IReadOnlyList<string> SkillNames { get; }

        public string Personality { get; }

        public string Mood { get; }

        public string Race { get; }
        public IReadOnlyList<BenchSkill> Instances { get; }
        public int Modifier { get; }
        public int WorkDays { get; }
        public int RestDays { get; }
        public IReadOnlyList<int> ParentCells { get; }
    }

    internal sealed class BenchBoard
    {
        public BenchBoard(int levelNumber, IReadOnlyList<string> tools, BenchMonster?[] row, int initialEnergy = 0, int seed = 1)
        {
            LevelNumber = levelNumber;
            Tools = tools ?? Array.Empty<string>();
            Row = row ?? Array.Empty<BenchMonster?>();
            InitialEnergy = initialEnergy; Seed = seed;
        }

        public int LevelNumber { get; }

        public IReadOnlyList<string> Tools { get; }

        public BenchMonster?[] Row { get; }
        public int InitialEnergy { get; }
        public int Seed { get; }
    }

    internal sealed class BenchReport
    {
        public BenchReport(int produced, int due, int excessAt, bool meetsDue, bool meetsExcess, IReadOnlyList<SettlementEntry> entries, IReadOnlyList<string> placed, int initialEnergy = 0)
        {
            Produced = produced;
            Due = due;
            ExcessAt = excessAt;
            MeetsDue = meetsDue;
            MeetsExcess = meetsExcess;
            Entries = entries;
            Placed = placed ?? Array.Empty<string>();
            InitialEnergy = initialEnergy;
        }

        public int Produced { get; }

        public int Due { get; }

        public int ExcessAt { get; }

        public bool MeetsDue { get; }

        public bool MeetsExcess { get; }

        public IReadOnlyList<SettlementEntry> Entries { get; }

        public IReadOnlyList<string> Placed { get; }
        public int InitialEnergy { get; }
        public int AccumulatedEnergy => InitialEnergy + Produced;
    }

    internal sealed class BenchRejectedException : Exception
    {
        public BenchRejectedException(string message) : base(message)
        {
        }
    }

    internal static class ExtractionBench
    {
        public static BenchReport Run(BenchBoard board)
        {
            var architecture = Prepare(board);
            var placed = architecture.GetModel<LevelModel>().CaptureExtraction();
            architecture.GetSystem<SettlementSystem>().Settle();
            return Report(architecture, placed, board.InitialEnergy);
        }

        // The workbench uses an isolated architecture. Reading details never settles it
        // and never consumes the random stream of a player's Unity run.
        public static IReadOnlyList<MonsterDetails> Preview(BenchBoard board)
        {
            var architecture = Prepare(board);
            var row = architecture.GetModel<LevelModel>().CaptureExtraction();
            var details = new List<MonsterDetails>();
            for (var i = 0; i < row.Length; i++)
                details.Add(string.IsNullOrEmpty(row[i]) ? null : architecture.SendQuery(new MonsterDetailsQuery(row[i])));
            return details;
        }

        static IArchitecture Prepare(BenchBoard board)
        {
            if (board == null)
                throw new BenchRejectedException("没有棋盘。");
            if (ContentGate.Current == null)
                throw new BenchRejectedException("先装入内容。");

            TheCallApp.Reset();
            TheCallApp.OnRegisterPatch = app => app.RegisterUtility<IDraw>(new BenchDraw(board.Seed));
            var architecture = TheCallApp.Interface;
            var skills = architecture.GetUtility<SkillCatalog>();
            var tools = architecture.GetUtility<IToolCatalog>();
            Check(board, skills, tools);
            architecture.GetSystem<FlowSystem>().Lay(board);
            return architecture;
        }

        static void Check(BenchBoard board, SkillCatalog skills, IToolCatalog tools)
        {
            if (board.InitialEnergy < 0) throw new BenchRejectedException("带入能量不能小于0。");
            if (board.LevelNumber < 1 || board.LevelNumber > 7)
                throw new BenchRejectedException("关卡要在 1 到 7。");

            var held = new HashSet<string>();
            for (var i = 0; i < board.Tools.Count; i++)
            {
                if (!held.Add(board.Tools[i]) || FindTool(tools, board.Tools[i]) == null)
                    throw new BenchRejectedException("工具不在目录里或重复：" + board.Tools[i]);
            }

            if (board.Row.Length != tools.ExtractionCells(board.Tools))
                throw new BenchRejectedException("格子数和工具对不上。");

            for (var cell = 0; cell < board.Row.Length; cell++)
            {
                if (board.Row[cell] == null)
                    continue;

                var bm = board.Row[cell].Value;
                var names = bm.SkillNames;
                var cap = bm.Personality == "安于现状" ? 2 : 4;
                if (names == null || names.Count < 1 || names.Count > cap)
                    throw new BenchRejectedException("当前性格最多允许 " + cap + " 个技能。");

                var seen = new HashSet<string>();
                for (var i = 0; i < names.Count; i++)
                {
                    if ((!seen.Add(names[i]) && !held.Contains("复读腺")) || !Known(skills, names[i]))
                        throw new BenchRejectedException("技能不在目录里或重复：" + names[i]);
                }

                if (!string.IsNullOrEmpty(bm.Personality) && !KnownPersonality(bm.Personality))
                    throw new BenchRejectedException("性格不在目录里：" + bm.Personality);
                if (!string.IsNullOrEmpty(bm.Race) && !KnownRace(bm.Race))
                    throw new BenchRejectedException("种族不在目录里：" + bm.Race);
                if (!string.IsNullOrEmpty(bm.Mood) && !KnownMood(bm.Mood))
                    throw new BenchRejectedException("心情不在目录里：" + bm.Mood);
            }
        }

        static BenchReport Report(IArchitecture architecture, IReadOnlyList<string> placed, int initialEnergy)
        {
            var entries = architecture.GetSystem<SettlementSystem>().Entries;
            var produced = 0;
            SettlementPayment payment = null;
            for (var i = 0; i < entries.Count; i++)
            {
                var landing = entries[i] as SettlementLanding;
                if (landing != null)
                    produced += landing.Energy;

                var pay = entries[i] as SettlementPayment;
                if (pay != null)
                    payment = pay;
            }

            var level = architecture.GetModel<LevelModel>();
            var meetsDue = payment != null && payment.Shortfall == 0 && !payment.Failed;
            var meetsExcess = payment != null && payment.Excess;
            return new BenchReport(produced, level.EnergyDue, level.ExcessEnergy, meetsDue, meetsExcess, entries, placed, initialEnergy);
        }

        static bool Known(SkillCatalog skills, string name)
        {
            var names = skills.Names();
            for (var i = 0; i < names.Count; i++)
            {
                if (names[i] == name)
                    return true;
            }

            return false;
        }

        static ToolDefinition FindTool(IToolCatalog tools, string name)
        {
            for (var i = 0; i < tools.Tools.Count; i++)
            {
                if (tools.Tools[i].Name == name)
                    return tools.Tools[i];
            }

            return null;
        }

        static bool KnownPersonality(string name)
        {
            var book = ContentGate.Current;
            if (book == null || book.Personalities == null) return false;
            for (var i = 0; i < book.Personalities.Count; i++)
                if (book.Personalities[i].Name == name) return true;
            return false;
        }

        static bool KnownRace(string name)
        {
            var book = ContentGate.Current;
            if (book == null || book.Races == null) return false;
            for (var i = 0; i < book.Races.Count; i++)
                if (book.Races[i].Name == name) return true;
            return false;
        }

        static bool KnownMood(string name)
        {
            var book = ContentGate.Current;
            if (book == null || book.Moods == null) return false;
            for (var i = 0; i < book.Moods.Count; i++)
                if (book.Moods[i].Name == name) return true;
            return false;
        }

        sealed class BenchDraw : IDraw
        {
            readonly Random _random;
            public BenchDraw(int seed) { _random = new Random(seed); }
            public T Choose<T>(IReadOnlyList<T> options) => options[_random.Next(options.Count)];

            public bool Chance(int percent) => _random.Next(100) < percent;
        }
    }
}
