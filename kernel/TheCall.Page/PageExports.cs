using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;

namespace TheCall
{
    public static partial class PageExports
    {
        [JSExport]
        public static string Boot(string bookJson)
        {
            try
            {
                TheCallApp.Reset();
                ContentGate.Use(ContentBook.Parse(bookJson));
                return "{}";
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }

        [JSExport]
        public static string Catalog()
        {
            try
            {
                var book = ContentGate.Current;
                if (book == null)
                    return Error("先装入内容。");

                var buffer = new StringBuilder();
                buffer.Append("{\"skills\":[");
                for (var i = 0; i < book.Skills.Count; i++)
                {
                    if (i > 0)
                        buffer.Append(',');

                    AppendSkill(buffer, book.Skills[i]);
                }

                buffer.Append("],\"tools\":[");
                for (var i = 0; i < book.Tools.Count; i++)
                {
                    if (i > 0)
                        buffer.Append(',');

                    buffer.Append("{\"name\":").Append(Quote(book.Tools[i].Name)).Append('}');
                }

                buffer.Append("],\"levels\":[");
                for (var i = 0; i < book.Levels.Count; i++)
                {
                    if (i > 0)
                        buffer.Append(',');

                    buffer.Append("{\"level\":").Append(i + 1);
                    buffer.Append(",\"due\":").Append(book.Levels[i].Due);
                    buffer.Append(",\"excess\":").Append(book.Levels[i].Excess);
                    buffer.Append('}');
                }

                buffer.Append("]}");
                return buffer.ToString();
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }

        [JSExport]
        public static string Cells(string toolsJson)
        {
            try
            {
                var book = ContentGate.Current;
                if (book == null)
                    return Error("先装入内容。");

                var names = ReadStrings(toolsJson);
                var count = new ToolCatalog(book).ExtractionCells(names);
                return "{\"cells\":" + count + "}";
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }

        [JSExport]
        public static string Score(string boardJson)
        {
            try
            {
                var board = ReadBoard(boardJson);
                var report = ExtractionBench.Run(board);
                return WriteReport(report);
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }

        static BenchBoard ReadBoard(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var level = root.GetProperty("level").GetInt32();
            var tools = new List<string>();
            foreach (var tool in root.GetProperty("tools").EnumerateArray())
                tools.Add(tool.GetString());

            var rowElement = root.GetProperty("row");
            var row = new BenchMonster?[rowElement.GetArrayLength()];
            var index = 0;
            foreach (var cell in rowElement.EnumerateArray())
            {
                if (cell.ValueKind == JsonValueKind.Null)
                {
                    row[index++] = null;
                    continue;
                }

                var names = new List<string>();
                foreach (var skill in cell.GetProperty("skills").EnumerateArray())
                    names.Add(skill.GetString());

                row[index++] = new BenchMonster(names);
            }

            return new BenchBoard(level, tools, row);
        }

        static List<string> ReadStrings(string json)
        {
            using var document = JsonDocument.Parse(json);
            var names = new List<string>();
            foreach (var item in document.RootElement.EnumerateArray())
                names.Add(item.GetString());

            return names;
        }

        static string WriteReport(BenchReport report)
        {
            var buffer = new StringBuilder();
            buffer.Append("{\"produced\":").Append(report.Produced);
            buffer.Append(",\"due\":").Append(report.Due);
            buffer.Append(",\"excessAt\":").Append(report.ExcessAt);
            buffer.Append(",\"meetsDue\":").Append(report.MeetsDue ? "true" : "false");
            buffer.Append(",\"meetsExcess\":").Append(report.MeetsExcess ? "true" : "false");
            buffer.Append(",\"landings\":[");
            var landingIndex = 0;
            var swapIndex = 0;
            var removalIndex = 0;
            buffer.Append(Join(report, entry => entry is SettlementLanding, (entry, first) =>
            {
                var landing = (SettlementLanding)entry;
                if (!first)
                    buffer.Append(',');

                buffer.Append("{\"monsterId\":").Append(Quote(landing.MonsterId));
                buffer.Append(",\"skillName\":").Append(Quote(landing.SkillName));
                buffer.Append(",\"quote\":").Append(landing.Quote);
                buffer.Append(",\"sideCount\":").Append(landing.SideCount);
                buffer.Append(",\"adds\":[");
                for (var i = 0; i < landing.Adds.Count; i++)
                {
                    if (i > 0)
                        buffer.Append(',');

                    buffer.Append("{\"label\":").Append(Quote(landing.Adds[i].Label));
                    buffer.Append(",\"amount\":").Append(landing.Adds[i].Amount).Append('}');
                }

                buffer.Append("],\"factors\":[");
                for (var i = 0; i < landing.Factors.Count; i++)
                {
                    if (i > 0)
                        buffer.Append(',');

                    buffer.Append("{\"label\":").Append(Quote(landing.Factors[i].Label));
                    buffer.Append(",\"factor\":").Append(landing.Factors[i].Factor).Append('}');
                }

                buffer.Append("],\"base\":").Append(landing.Base);
                buffer.Append(",\"multiplier\":").Append(landing.Multiplier);
                buffer.Append(",\"energy\":").Append(landing.Energy);
                buffer.Append(",\"writeback\":").Append(landing.Writeback).Append('}');
                landingIndex++;
            }));
            buffer.Append("],\"swaps\":[");
            var started = false;
            for (var i = 0; i < report.Entries.Count; i++)
            {
                var swap = report.Entries[i] as SettlementSwap;
                if (swap == null)
                    continue;

                if (started)
                    buffer.Append(',');

                started = true;
                buffer.Append("{\"actorId\":").Append(Quote(swap.ActorId));
                buffer.Append(",\"targetId\":").Append(Quote(swap.TargetId));
                buffer.Append(",\"happened\":").Append(swap.Happened ? "true" : "false").Append('}');
                swapIndex++;
            }

            buffer.Append("],\"removals\":[");
            started = false;
            for (var i = 0; i < report.Entries.Count; i++)
            {
                var removal = report.Entries[i] as SettlementRemoval;
                if (removal == null)
                    continue;

                if (started)
                    buffer.Append(',');

                started = true;
                buffer.Append("{\"monsterId\":").Append(Quote(removal.MonsterId));
                buffer.Append(",\"happened\":").Append(removal.Happened ? "true" : "false").Append('}');
                removalIndex++;
            }

            buffer.Append("]}");
            return buffer.ToString();
        }

        static string Join(BenchReport report, Func<SettlementEntry, bool> match, Action<SettlementEntry, bool> write)
        {
            var first = true;
            for (var i = 0; i < report.Entries.Count; i++)
            {
                if (!match(report.Entries[i]))
                    continue;

                write(report.Entries[i], first);
                first = false;
            }

            return "";
        }

        static void AppendSkill(StringBuilder buffer, SkillDef skill)
        {
            buffer.Append("{\"name\":").Append(Quote(skill.Name));
            buffer.Append(",\"rarity\":").Append(Quote(skill.Rarity.ToString()));
            buffer.Append(",\"use\":").Append(Quote(skill.Use.ToString()));
            buffer.Append(",\"affix\":");
            AppendFlags(buffer, skill.Affix);
            buffer.Append(",\"spans\":");
            AppendSpans(buffer, skill);
            buffer.Append(",\"effects\":[");
            for (var i = 0; i < skill.Effects.Count; i++)
            {
                if (i > 0)
                    buffer.Append(',');

                var effect = skill.Effects[i];
                buffer.Append("{\"kind\":").Append(Quote(effect.Kind.ToString()));
                buffer.Append(",\"a\":").Append(effect.A);
                buffer.Append(",\"b\":").Append(effect.B).Append('}');
            }

            buffer.Append("]}");
        }

        static void AppendFlags(StringBuilder buffer, SkillAffix affix)
        {
            buffer.Append('[');
            var first = true;
            WriteFlag(buffer, ref first, affix, SkillAffix.Destroy, "Destroy");
            WriteFlag(buffer, ref first, affix, SkillAffix.Permanent, "Permanent");
            WriteFlag(buffer, ref first, affix, SkillAffix.Capacity, "Capacity");
            WriteFlag(buffer, ref first, affix, SkillAffix.Immovable, "Immovable");
            buffer.Append(']');
        }

        static void WriteFlag(StringBuilder buffer, ref bool first, SkillAffix affix, SkillAffix flag, string name)
        {
            if ((affix & flag) == 0)
                return;

            if (!first)
                buffer.Append(',');

            first = false;
            buffer.Append(Quote(name));
        }

        static void AppendSpans(StringBuilder buffer, SkillDef skill)
        {
            buffer.Append('[');
            var pieces = SkillSentences.Pieces(skill);
            for (var i = 0; i < pieces.Length; i++)
            {
                if (i > 0)
                    buffer.Append(',');

                if (pieces[i].IsEnergy)
                    buffer.Append("{\"energy\":").Append(pieces[i].Energy).Append('}');
                else
                    buffer.Append("{\"text\":").Append(Quote(pieces[i].Text)).Append('}');
            }

            buffer.Append(']');
        }

        static string Error(string message) => "{\"error\":" + Quote(message) + "}";

        static string Quote(string text)
        {
            if (text == null)
                return "null";

            var buffer = new StringBuilder();
            buffer.Append('"');
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (ch == '\\' || ch == '"')
                    buffer.Append('\\');

                if (ch == '\n')
                    buffer.Append("\\n");
                else
                    buffer.Append(ch);
            }

            buffer.Append('"');
            return buffer.ToString();
        }
    }
}
