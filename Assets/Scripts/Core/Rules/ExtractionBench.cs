using System;
using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    internal readonly struct BenchMonster
    {
        public BenchMonster(IReadOnlyList<string> skillNames) => SkillNames = skillNames;

        public IReadOnlyList<string> SkillNames { get; }
    }

    internal sealed class BenchBoard
    {
        public BenchBoard(int levelNumber, IReadOnlyList<string> tools, BenchMonster?[] row)
        {
            LevelNumber = levelNumber;
            Tools = tools ?? Array.Empty<string>();
            Row = row ?? Array.Empty<BenchMonster?>();
        }

        public int LevelNumber { get; }

        public IReadOnlyList<string> Tools { get; }

        public BenchMonster?[] Row { get; }
    }

    internal sealed class BenchReport
    {
        public BenchReport(int produced, int due, int excessAt, bool meetsDue, bool meetsExcess, IReadOnlyList<SettlementEntry> entries, IReadOnlyList<string> placed)
        {
            Produced = produced;
            Due = due;
            ExcessAt = excessAt;
            MeetsDue = meetsDue;
            MeetsExcess = meetsExcess;
            Entries = entries;
            Placed = placed ?? Array.Empty<string>();
        }

        public int Produced { get; }

        public int Due { get; }

        public int ExcessAt { get; }

        public bool MeetsDue { get; }

        public bool MeetsExcess { get; }

        public IReadOnlyList<SettlementEntry> Entries { get; }

        public IReadOnlyList<string> Placed { get; }
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
            if (board == null)
                throw new BenchRejectedException("没有棋盘。");
            if (ContentGate.Current == null)
                throw new BenchRejectedException("先装入内容。");

            TheCallApp.Reset();
            TheCallApp.OnRegisterPatch = app => app.RegisterUtility<IDraw>(new FirstDraw());
            var architecture = TheCallApp.Interface;
            var skills = architecture.GetUtility<SkillCatalog>();
            var tools = architecture.GetUtility<IToolCatalog>();
            Check(board, skills, tools);
            architecture.GetSystem<FlowSystem>().Lay(board);
            var placed = architecture.GetModel<LevelModel>().CaptureExtraction();
            architecture.GetSystem<SettlementSystem>().Settle();
            return Report(architecture, placed);
        }

        static void Check(BenchBoard board, SkillCatalog skills, IToolCatalog tools)
        {
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

                var names = board.Row[cell].Value.SkillNames;
                if (names == null || names.Count < 1 || names.Count > 4)
                    throw new BenchRejectedException("每只怪物要有 1 到 4 个技能。");

                var seen = new HashSet<string>();
                for (var i = 0; i < names.Count; i++)
                {
                    if (!seen.Add(names[i]) || !Known(skills, names[i]))
                        throw new BenchRejectedException("技能不在目录里或重复：" + names[i]);
                }
            }
        }

        static BenchReport Report(IArchitecture architecture, IReadOnlyList<string> placed)
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
            return new BenchReport(produced, level.EnergyDue, level.ExcessEnergy, meetsDue, meetsExcess, entries, placed);
        }

        static bool Known(SkillCatalog skills, string name)
        {
            var names = skills.Names;
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

        sealed class FirstDraw : IDraw
        {
            public T Choose<T>(IReadOnlyList<T> options) => options[0];

            public bool Chance(int percent) => false;
        }
    }
}
