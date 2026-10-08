using System;
using System.Collections.Generic;
using System.Text;
using QFramework;

namespace TheCall
{
    public sealed class ProductionSubmission
    {
        public ProductionSubmission(
            int levelNumber,
            bool overtime,
            int produced,
            int due,
            SettlementPayment payment,
            IReadOnlyList<SettlementEntry> entries)
        {
            LevelNumber = levelNumber;
            Overtime = overtime;
            Produced = produced;
            Due = due;
            Payment = payment;
            Entries = entries ?? System.Array.Empty<SettlementEntry>();
        }

        public int LevelNumber { get; }

        public bool Overtime { get; }

        public int Produced { get; }

        public int Due { get; }

        public SettlementPayment Payment { get; }

        public IReadOnlyList<SettlementEntry> Entries { get; }
    }

    public sealed class ProductionLogQuery : AbstractQuery<IReadOnlyList<ProductionSubmission>>
    {
        protected override IReadOnlyList<ProductionSubmission> OnDo() =>
            this.GetModel<RunModel>().ProductionLog;
    }

    public static class ProductionLogText
    {
        public static string Format(IReadOnlyList<ProductionSubmission> submissions) =>
            Format(submissions, null);

        public static string Format(IReadOnlyList<ProductionSubmission> submissions, Func<string, string> nameOf)
        {
            if (submissions == null || submissions.Count == 0)
                return "还没有结算。\n确认这一天的操作之后，每一次提交都会记在这里。";

            var buffer = new StringBuilder();
            for (var i = 0; i < submissions.Count; i++)
            {
                if (i > 0)
                    buffer.Append("\n\n");

                Append(buffer, submissions[i], nameOf);
            }

            return buffer.ToString();
        }

        static void Append(StringBuilder buffer, ProductionSubmission submission, Func<string, string> nameOf)
        {
            var kind = submission.Overtime ? "加班" : "结算";
            buffer.Append("—— 第 ").Append(submission.LevelNumber).Append(" 关 · ").Append(kind).Append(" ——\n");
            buffer.Append(Outcome(submission));
            buffer.Append("\n\n下面按结算时的实际触发顺序写过程。右侧数字是计入本关总能量的点数。\n");
            buffer.Append("<align=\"right\"><size=90%>能量</size></align>\n");

            var names = CollectNames(submission.Entries);
            var energySum = 0;
            var anyStep = false;
            var entries = submission.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i] is SettlementPayment)
                    continue;

                anyStep = true;
                energySum += AppendStep(buffer, entries[i], names, nameOf);
            }

            if (!anyStep)
                buffer.Append("\n这次没有任何技能触发记录。");

            buffer.Append("\n<align=\"right\"><b>合计  ").Append(energySum).Append("</b></align>");
            if (energySum != submission.Produced)
                buffer.Append("\n（说明：过程里加总的 ").Append(energySum).Append(" 点与系统记录的产出 ")
                    .Append(submission.Produced).Append(" 点不一致，请以产出为准。）");
        }

        static string Outcome(ProductionSubmission submission)
        {
            var payment = submission.Payment;
            if (payment.Failed)
                return "本关最终产出 " + submission.Produced + " 点能量，应交 " + submission.Due +
                       " 点，仍差 " + payment.Shortfall + " 点，游戏失败。";

            if (!submission.Overtime && payment.Shortfall > 0)
                return "本关产出 " + submission.Produced + " 点能量，应交 " + submission.Due + " 点，还差 " +
                       payment.Shortfall + " 点，进入加班。";

            if (submission.Overtime)
            {
                var overtime = "加班阶段产出 " + submission.Produced + " 点能量，用来补上欠额 " + payment.Deducted + " 点。";
                if (payment.Excess)
                    overtime += "交款超过目标。";

                return overtime + "本段工资 " + payment.Wage + " 金币。";
            }

            var paid = "本关产出 " + submission.Produced + " 点能量，交上 " + payment.Deducted + " 点。";
            if (payment.Excess)
                paid += "交款超过目标。";

            return paid + "工资 " + payment.Wage + " 金币。";
        }

        static Dictionary<string, string> CollectNames(IReadOnlyList<SettlementEntry> entries)
        {
            var names = new Dictionary<string, string>();
            for (var i = 0; i < entries.Count; i++)
            {
                var landing = entries[i] as SettlementLanding;
                if (landing == null || string.IsNullOrEmpty(landing.MonsterName))
                    continue;

                names[landing.MonsterId] = landing.MonsterName;
            }

            return names;
        }

        static string DisplayName(string monsterId, Dictionary<string, string> names, Func<string, string> nameOf)
        {
            if (nameOf != null)
            {
                var resolved = nameOf(monsterId);
                if (!string.IsNullOrEmpty(resolved))
                    return resolved;
            }

            if (names.TryGetValue(monsterId, out var known) && !string.IsNullOrEmpty(known))
                return known;

            return string.IsNullOrEmpty(monsterId) ? "（未知怪物）" : monsterId;
        }

        static int AppendStep(
            StringBuilder buffer,
            SettlementEntry entry,
            Dictionary<string, string> names,
            Func<string, string> nameOf)
        {
            buffer.Append('\n');
            switch (entry)
            {
                case SettlementMark mark:
                    AppendMark(buffer, mark, names, nameOf);
                    return 0;
                case SettlementLanding landing:
                    return AppendLanding(buffer, landing, names, nameOf);
                case SettlementSwap swap:
                    AppendSwap(buffer, swap, names, nameOf);
                    return 0;
                case SettlementRemoval removal:
                    AppendRemoval(buffer, removal, names, nameOf);
                    return 0;
                default:
                    return 0;
            }
        }

        static void AppendMark(
            StringBuilder buffer,
            SettlementMark mark,
            Dictionary<string, string> names,
            Func<string, string> nameOf)
        {
            var who = DisplayName(mark.MonsterId, names, nameOf);
            switch (mark.Tone)
            {
                case MarkTone.Again:
                    var skill = AgainSkillName(mark.Label);
                    buffer.Append(who).Append(" 的「").Append(skill)
                        .Append("」生效，让前面已经走过的一条产能技能重新再触发一次。");
                    break;
                case MarkTone.Miss:
                    buffer.Append(who).Append(" 有一条效果在本轮结算，但没有带来能量、加成、倍率、金币或新技能。");
                    break;
                case MarkTone.Gold:
                    var gold = ParseGold(mark.Label);
                    buffer.Append(who).Append(" 的产金生效，获得 ").Append(gold)
                        .Append(" 金币（金币不计入右侧能量合计）。");
                    break;
                case MarkTone.Skills:
                    var count = ParseSkillCount(mark.Label);
                    buffer.Append(who).Append(" 获得了 ").Append(count)
                        .Append(" 个新技能（不计入右侧能量合计）。");
                    break;
            }
        }

        static int AppendLanding(
            StringBuilder buffer,
            SettlementLanding landing,
            Dictionary<string, string> names,
            Func<string, string> nameOf)
        {
            var who = string.IsNullOrEmpty(landing.MonsterName)
                ? DisplayName(landing.MonsterId, names, nameOf)
                : landing.MonsterName;

            if (landing.Cell >= 0)
                buffer.Append("左起第 ").Append(landing.Cell + 1).Append(" 格上的 ");

            buffer.Append(who).Append(" 的「").Append(landing.SkillName).Append("」触发。");

            if (landing.SkillName == "毒跳伤")
            {
                buffer.Append("毒伤落地，计入能量 ").Append(landing.Energy).Append(" 点。");
                AppendScore(buffer, landing.Energy);
                return landing.Energy;
            }

            if (landing.Side)
            {
                var side = string.IsNullOrEmpty(landing.CountedSideName) ? "一侧" : landing.CountedSideName;
                buffer.Append("这条技能按").Append(side).Append("还有 ").Append(landing.SideCount)
                    .Append(" 只怪物结算，每只基础报价 ").Append(landing.Quote).Append(" 点");
                if (landing.Adds.Count == 0)
                    buffer.Append("，底数 ").Append(landing.Base).Append(" 点。");
                else
                    buffer.Append('。');
            }
            else
            {
                buffer.Append("这条技能的基础报价是 ").Append(landing.Quote).Append(" 点");
                if (landing.Adds.Count == 0 && landing.Factors.Count == 0 && landing.Multiplier == 1)
                    buffer.Append('。');
                else
                    buffer.Append('。');
            }

            for (var i = 0; i < landing.Adds.Count; i++)
                buffer.Append(AddSentence(landing.Adds[i], who, names, nameOf));

            if (landing.Side || landing.Adds.Count > 0)
                buffer.Append("合计底数 ").Append(landing.Base).Append(" 点。");

            for (var i = 0; i < landing.Factors.Count; i++)
                buffer.Append(FactorSentence(landing.Factors[i], who, names, nameOf));

            if (landing.Multiplier != 1)
                buffer.Append("倍率乘积为 ").Append(landing.Multiplier).Append("。");

            buffer.Append("因此这次计入本关总能量 ").Append(landing.Energy).Append(" 点。");

            if (landing.Writeback != 0)
                buffer.Append("同时报价写回 ").Append(Signed(landing.Writeback))
                    .Append("（影响之后，不算进本关右侧合计）。");

            AppendScore(buffer, landing.Energy);
            return landing.Energy;
        }

        static void AppendSwap(
            StringBuilder buffer,
            SettlementSwap swap,
            Dictionary<string, string> names,
            Func<string, string> nameOf)
        {
            var actor = DisplayName(swap.ActorId, names, nameOf);
            var target = DisplayName(swap.TargetId, names, nameOf);
            if (swap.Happened)
                buffer.Append(actor).Append(" 与 ").Append(target).Append(" 换位成功。");
            else
                buffer.Append(actor).Append(" 尝试与 ").Append(target).Append(" 换位，但没有换成。");
        }

        static void AppendRemoval(
            StringBuilder buffer,
            SettlementRemoval removal,
            Dictionary<string, string> names,
            Func<string, string> nameOf)
        {
            var victim = DisplayName(removal.MonsterId, names, nameOf);
            if (removal.Happened)
            {
                buffer.Append(victim).Append(" 被吞噬，从提取槽移除。");
                return;
            }

            if (!string.IsNullOrEmpty(removal.SourceId))
            {
                var source = DisplayName(removal.SourceId, names, nameOf);
                buffer.Append(source).Append(" 的吞噬没有生效，").Append(victim).Append(" 仍在场上。");
                return;
            }

            buffer.Append("有一次吞噬判定，但没有怪物被移除。");
        }

        static string AddSentence(LandingAdd add, string scorerName, Dictionary<string, string> names, Func<string, string> nameOf)
        {
            var label = Speak(add.Label);
            var from = string.IsNullOrEmpty(add.SourceName)
                ? DisplayName(add.SourceId, names, nameOf)
                : add.SourceName;
            if (!string.IsNullOrEmpty(from) && from != scorerName)
                return from + " 的「" + label + "」为它加了 " + add.Amount + " 点。";

            return "「" + label + "」加成 " + Signed(add.Amount) + " 点。";
        }

        static string FactorSentence(LandingFactor factor, string scorerName, Dictionary<string, string> names, Func<string, string> nameOf)
        {
            var label = Speak(factor.Label);
            var from = string.IsNullOrEmpty(factor.SourceName)
                ? DisplayName(factor.SourceId, names, nameOf)
                : factor.SourceName;
            if (!string.IsNullOrEmpty(from) && from != scorerName)
                return from + " 的「" + label + "」让这次产出 ×" + factor.Factor + "。";

            return "「" + label + "」让这次产出 ×" + factor.Factor + "。";
        }

        static void AppendScore(StringBuilder buffer, int energy)
        {
            buffer.Append("\n<align=\"right\">+").Append(energy).Append("</align>");
        }

        static string AgainSkillName(string label)
        {
            if (string.IsNullOrEmpty(label))
                return "技能";

            const string suffix = "触发+1";
            if (label.EndsWith(suffix))
                return label.Substring(0, label.Length - suffix.Length);

            return label;
        }

        static int ParseGold(string label)
        {
            if (string.IsNullOrEmpty(label) || label.Length < 2 || label[0] != '+' || !label.EndsWith("金"))
                return 0;

            return int.TryParse(label.Substring(1, label.Length - 2), out var amount) ? amount : 0;
        }

        static int ParseSkillCount(string label)
        {
            const string prefix = "技能+";
            if (string.IsNullOrEmpty(label) || !label.StartsWith(prefix))
                return 0;

            return int.TryParse(label.Substring(prefix.Length), out var count) ? count : 0;
        }

        static string Speak(string label)
        {
            if (label == "宿主修正")
                return "怪物修正";
            if (label == "下家")
                return "下一个怪物的能量数值";

            return label;
        }

        static string Signed(int amount) => amount >= 0 ? "+" + amount : amount.ToString();
    }
}
