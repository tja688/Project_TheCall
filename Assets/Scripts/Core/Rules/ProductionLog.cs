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
        public static string Format(IReadOnlyList<ProductionSubmission> submissions)
        {
            if (submissions == null || submissions.Count == 0)
                return "还没有结算。\n确认这一天的操作之后，每一次提交都会记在这里。";

            var buffer = new StringBuilder();
            for (var i = 0; i < submissions.Count; i++)
            {
                if (i > 0)
                    buffer.Append("\n\n");

                Append(buffer, submissions[i]);
            }

            return buffer.ToString();
        }

        static void Append(StringBuilder buffer, ProductionSubmission submission)
        {
            var kind = submission.Overtime ? "加班" : "结算";
            buffer.Append("—— 第").Append(submission.LevelNumber).Append("关 · ").Append(kind).Append(" ——\n");
            buffer.Append(Outcome(submission));

            var any = false;
            var entries = submission.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var landing = entries[i] as SettlementLanding;
                if (landing == null)
                    continue;

                any = true;
                buffer.Append("\n\n");
                AppendLanding(buffer, landing);
            }

            if (!any)
                buffer.Append("\n\n这次没有能量落地。");
        }

        static string Outcome(ProductionSubmission submission)
        {
            var payment = submission.Payment;
            if (payment.Failed)
                return "产出 " + submission.Produced + "    欠额 " + submission.Due + "    还差 " + payment.Shortfall + "    游戏失败";

            if (!submission.Overtime && payment.Shortfall > 0)
                return "产出 " + submission.Produced + "    应交 " + submission.Due + "    还差 " + payment.Shortfall + "    进入加班";

            if (submission.Overtime)
            {
                var overtime = "产出 " + submission.Produced + "    补上欠额 " + payment.Deducted;
                if (payment.Excess)
                    overtime += "    超额交款";

                return overtime + "    工资 " + payment.Wage;
            }

            var paid = "产出 " + submission.Produced + "    交上 " + payment.Deducted;
            if (payment.Excess)
                paid += "    超额交款";

            return paid + "    工资 " + payment.Wage;
        }

        static void AppendLanding(StringBuilder buffer, SettlementLanding landing)
        {
            if (string.IsNullOrEmpty(landing.MonsterName))
                buffer.Append(landing.SkillName);
            else
                buffer.Append(landing.MonsterName).Append(" · ").Append(landing.SkillName);

            if (landing.Cell >= 0)
                buffer.Append(" · 左起第 ").Append(landing.Cell + 1).Append(" 格");

            buffer.Append('\n');
            if (landing.SkillName == "毒跳伤")
            {
                buffer.Append("  落地 ").Append(landing.Energy);
                return;
            }

            buffer.Append("  ");
            if (landing.Side)
            {
                var side = string.IsNullOrEmpty(landing.CountedSideName) ? "这一侧" : landing.CountedSideName;
                buffer.Append(side).Append(' ').Append(landing.SideCount).Append(" 只 × 报价 ").Append(landing.Quote);
            }
            else
            {
                buffer.Append("报价 ").Append(landing.Quote);
            }

            for (var i = 0; i < landing.Adds.Count; i++)
                buffer.Append('，').Append(Term(landing.Adds[i], landing.MonsterName));

            if (landing.Side || landing.Adds.Count > 0)
                buffer.Append(" = 底数 ").Append(landing.Base);

            for (var i = 0; i < landing.Factors.Count; i++)
            {
                buffer.Append('\n').Append("  ").Append(Factor(landing.Factors[i], landing.MonsterName));
                buffer.Append(" ×").Append(landing.Factors[i].Factor);
            }

            buffer.Append("\n  落地 ").Append(landing.Energy);
            if (landing.Multiplier != 1)
                buffer.Append("（底数 ").Append(landing.Base).Append(" × 倍率 ").Append(landing.Multiplier).Append('）');

            if (landing.Writeback != 0)
                buffer.Append("\n  写回 ").Append(Signed(landing.Writeback));
        }

        static string Term(LandingAdd add, string scorerName)
        {
            var label = Speak(add.Label);
            if (!string.IsNullOrEmpty(add.SourceName) && add.SourceName != scorerName)
                return add.SourceName + " 的" + label + " " + Signed(add.Amount);

            return label + " " + Signed(add.Amount);
        }

        static string Factor(LandingFactor factor, string scorerName)
        {
            var label = Speak(factor.Label);
            if (!string.IsNullOrEmpty(factor.SourceName) && factor.SourceName != scorerName)
                return factor.SourceName + " 的" + label;

            return label;
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
