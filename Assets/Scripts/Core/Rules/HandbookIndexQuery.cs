using System;
using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public enum HandbookCategory { Guide, Skill, Personality, Race, Mood, Tool, Tech, Term }

    public sealed class HandbookEntry
    {
        public HandbookEntry(HandbookCategory category, string name, string metadata, string rule, string advice, Rarity? rarity = null, params string[] related)
        {
            Category = category; Name = name; Metadata = metadata; Rule = rule; Advice = advice;
            Rarity = rarity; Related = Array.AsReadOnly(related ?? Array.Empty<string>());
        }
        public HandbookCategory Category { get; }
        public string Name { get; }
        public string Id => Category + ":" + Name;
        public string Metadata { get; }
        public string Rule { get; }
        public string Advice { get; }
        public Rarity? Rarity { get; }
        public IReadOnlyList<string> Related { get; }
        public bool Matches(string search) => string.IsNullOrWhiteSpace(search) ||
            (Name + "\n" + Metadata + "\n" + Rule + "\n" + Advice).IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>只读条目投影。数值和效果来自游戏目录，不复制第二套技能数据。</summary>
    public sealed class HandbookIndexQuery : AbstractQuery<IReadOnlyList<HandbookEntry>>
    {
        protected override IReadOnlyList<HandbookEntry> OnDo()
        {
            var entries = new List<HandbookEntry>();
            var book = ContentGate.Current;
            void Add(HandbookCategory category, string name, string meta, string rule, string advice, params string[] related) =>
                entries.Add(new HandbookEntry(category, name, meta, rule, advice, null, related));
            Add(HandbookCategory.Guide, "第一天怎么开始", "入门 · 操作流程",
                "从三只候选怪物中留下一只。把怪物从怪物笼拖入提取槽，按下结算按钮开始产能。每只怪物都有性格、种族与技能；怪物详情中可以查看它们。",
                "先了解技能，再决定左右位置。手册说明目录基础效果，实际产能还会受心情、性格、工具和当日效果影响。", "Skill:能量体", "Guide:提取与结算", "Mood:普通");
            Add(HandbookCategory.Guide, "提取与结算", "操作 · 产能顺序",
                "每轮结算开始时固定怪物身份顺序，从左到右执行各怪物的主动技能。光环被动持续影响适用对象，触发被动响应匹配事件。换位后仍按固定身份完成正常执行，成功换位还可能插入额外执行。",
                "先看效果是主动、光环还是触发。结算后可在设置中打开结算记录，核对产能来源和效果执行过程。", "Term:主动与被动", "Term:能量值", "Term:不动");
            var targets = "";
            for (var day = 1; day <= book.Levels.Count; day++)
                targets += (day == 1 ? "" : "\n") + "第 " + day + " 天    需求 " + book.Levels[day - 1].Due + "    超额门槛 " + book.Levels[day - 1].Excess;
            Add(HandbookCategory.Guide, "七天目标与加班", "进度 · 工资 · 科技点",
                targets + "\n\n首次未达需求，保留本关能量并进入一次加班。加班继续累加，第二次仍未达需求则失败。达标后扣除完整需求；扣除前达到超额门槛，每天获得 1 科技点。",
                "首轮达标工资 " + book.Economy.Wage + " 金币，加班达标工资 " + book.Economy.OvertimeWage + " 金币。首次达标就进入商店，不能主动选择加班。第七天离开商店后通关。", "Tool:蓄热熔炉", "Tech:基因实验");
            Add(HandbookCategory.Guide, "工作、休息与心情", "每日状态 · 轮换阵容",
                "参加当天任一轮结算阵容就记为工作；存活且未参加结算则记为休息，另一种连续计数重置。只放入提取槽再取回、未参与结算，不算工作。加班不额外计一天。\n\n次日开始时按已完成的工作或休息天数计算心情。当天日常基础心情保持，拖动与加班不重新计算。",
                "连续工作会疲倦，休息可恢复热情。部分性格固定心情，部分工具会修正它；调整阵容前一起查阅。", "Mood:疲倦", "Mood:热爱", "Personality:稳定", "Tool:隔音棉");
            Add(HandbookCategory.Guide, "培育与继承", "培育槽 · 次日获得子代",
                "同一培育槽至少放入两只亲本，达标后锁定组合，下一天诞生子代。亲本保留，每只亲本随机贡献一个常驻技能；基因材料可额外贡献一个。子代随机继承一个亲本种族，性格有 40% 概率来自亲本、60% 来自性格池。",
                "先确定子代性格和技能上限，再处理重复与随机裁切。基因只有实际加入子代时才消耗，未使用的材料退回。第七天没有下一天培育。", "Tech:基因实验", "Tech:大乱炖", "Tool:复读腺", "Personality:安于现状");
            Add(HandbookCategory.Guide, "废弃与基因材料", "废弃槽 · 基因槽",
                "废弃会永久移除怪物，随机取出它的一个技能作为基因材料。基因槽初始上限 3，满仓时不能废弃。基因实验解锁后可把材料投入培育槽；只有迷恋死亡性格可以直接安装基因。",
                "先检查基因容量，再决定废弃。成功安装或继承才消耗材料；已预约给培育槽的基因不能再次使用。操作废弃不会触发遗赠。", "Tech:基因仓储", "Personality:迷恋死亡", "Term:遗赠");
            Add(HandbookCategory.Guide, "商店与工具", "金币 · 补充阵容",
                "关卡达标发工资后进入商店，可购买怪物、工具，出售可出售的怪物，或付费刷新货架。每种工具一局只能拥有一件，已持有的工具退出商品池。",
                "工具价格是目录基础价格，折扣会改变实际购买价。锁定为培育亲本的怪物不能出售。离开商店前检查培育组合和下一天目标。", "Tool:批发合同", "Guide:培育与继承");
            foreach (var skill in this.GetArchitecture().SendQuery(new SkillIndexQuery()))
            {
                var related = new List<string> { "Term:主动与被动", "Term:能量值" };
                foreach (var term in new[] { "永久", "遗赠", "消灭", "不动", "开工" })
                    if (skill.Sentence.Contains(term)) related.Add("Term:" + term);
                entries.Add(new HandbookEntry(HandbookCategory.Skill, skill.Name,
                    RarityName(skill.Rarity) + " · " + (skill.Kind == SkillUse.Active ? "主动" : "被动") + " · " + skill.Function,
                    skill.Sentence, "这里显示基础效果。怪物详情与结算记录显示当局的修正、成长及实际执行结果。", skill.Rarity,
                    related.ToArray()));
            }
            foreach (var p in this.GetArchitecture().GetUtility<IPersonalityCatalog>().Personalities)
                entries.Add(new HandbookEntry(HandbookCategory.Personality, p.Name, RarityName(p.Rarity) + " · " + KindName(p.Kind),
                    p.Description, "每只怪物只有一个性格，独立于技能。先检查适用时机和目标，再安排它的位置或工作。", p.Rarity,
                    "Guide:工作、休息与心情", "Term:开工"));
            var races = this.GetArchitecture().GetUtility<IRaceCatalog>().Races;
            foreach (var race in races)
            {
                var generatedBy = ""; var overcomeBy = "";
                foreach (var other in races) { if (other.Generates == race.Name) generatedBy = other.Name; if (other.Overcomes == race.Name) overcomeBy = other.Name; }
                Add(HandbookCategory.Race, race.Name, "种族 · " + race.Element + "属性",
                    "相生：" + race.Name + " → " + race.Generates + "\n相生：" + generatedBy + " → " + race.Name + "\n相克：" + race.Name + " → " + race.Overcomes + "\n被克：" + overcomeBy + " → " + race.Name,
                    "种族本身没有自动产能。明确提到种族或相生、相克的效果才使用这些关系；相生邻接按双向关系判断，相克按效果指定方向判断。",
                    "Race:" + race.Generates, "Race:" + race.Overcomes, "Tool:五行罗盘");
            }
            var moods = this.GetArchitecture().GetUtility<IMoodCatalog>();
            foreach (var mood in moods.Moods)
            {
                var history = new List<string>();
                for (var days = 0; days <= 4; days++)
                {
                    if (days == 0 && moods.CalculateBaseMood(0, 0) == mood.Name) history.Add("没有达到连续工作或休息阈值时");
                    if (days > 0 && moods.CalculateBaseMood(days, 0) == mood.Name && moods.CalculateBaseMood(days - 1, 0) != mood.Name) history.Add("已连续工作 " + days + " 天后");
                    if (days > 0 && moods.CalculateBaseMood(0, days) == mood.Name && moods.CalculateBaseMood(0, days - 1) != mood.Name) history.Add("已连续休息 " + days + " 天后");
                }
                Add(HandbookCategory.Mood, mood.Name, "心情 · 第 " + (mood.Tier + 1) + " 档 · 能量修正 " + Signed(mood.EnergyDelta),
                    string.Join("；", history) + "，次日基础心情为" + mood.Name + "。\n\n该怪物每次产能的能量值 " + Signed(mood.EnergyDelta) + "，单次产能最低为 0。",
                    "这里是通常情况下的工作与休息规则。性格固定心情、隔音棉等效果可能覆盖基础心情。", "Guide:工作、休息与心情", "Tool:隔音棉", "Personality:稳定");
            }
            foreach (var tool in this.GetArchitecture().GetUtility<IToolCatalog>().Tools)
                entries.Add(new HandbookEntry(HandbookCategory.Tool, tool.Name, RarityName(tool.Rarity) + " · 被动工具 · " + tool.Price + " 金币",
                    tool.Description, "购买后本局持续生效，每种工具只可拥有一件。这里显示基础价格，商店可能有折扣。", tool.Rarity, "Guide:商店与工具"));
            foreach (var tech in this.GetArchitecture().GetUtility<TechCatalog>().Techs)
                Add(HandbookCategory.Tech, tech.Name, "科技 · " + tech.Cost + " 科技点", tech.Description,
                    "每项科技一局解锁一次，效果持续本局。达到当日超额门槛可获得科技点。", "Guide:七天目标与加班", "Guide:培育与继承");
            Add(HandbookCategory.Term, "主动与被动", "技能类型", "主动技能在怪物执行时按顺序使用。光环被动持续影响适用对象；触发被动只响应匹配事件。", "被动技能不会因为轮到该怪物就自动执行一次。查看条目中的条件与目标。", "Guide:提取与结算");
            Add(HandbookCategory.Term, "开工", "事件词条", "怪物每天首次成功放入提取槽时触发。换格、收回重放、加班或改变性格都不会重置开工机会。", "开工与完成工作天数不同。工作天数取决于是否参加结算阵容。", "Guide:工作、休息与心情");
            Add(HandbookCategory.Term, "遗赠", "事件词条", "怪物在提取槽中被消灭时触发。操作阶段主动废弃怪物不会触发遗赠。", "注意技能要求的消灭位置和时机。", "Term:消灭", "Guide:废弃与基因材料");
            Add(HandbookCategory.Term, "消灭", "效果词条", "把怪物从游戏中移除，被移除的怪物不再执行后续技能。", "消灭亲本会影响培育。主动废弃和结算中的消灭可能触发不同事件。", "Term:遗赠");
            Add(HandbookCategory.Term, "不动", "位置词条", "怪物不能参与效果造成的交换位置。该状态不限制玩家在操作阶段摆放它。", "换位效果会检查双方的不动状态。", "Guide:提取与结算");
            Add(HandbookCategory.Term, "永久", "持续时间词条", "明确标注永久的增益与减益可以跨天保留。通常的临时技能及临时增减在当天通关后清理，加班仍属于同一天。", "临时技能的成长会随该实例移除，不转移到同名常驻技能。", "Term:技能实例与重复");
            Add(HandbookCategory.Term, "能量值", "产能词条", "一次产出标注的能量点数。修正作用于效果写明的来源；没有限定技能或性格时，作用于所有产能来源。单次产能最低为 0。", "手册给出基础效果，实际结果请结合怪物心情、性格、工具与结算记录查看。", "Mood:普通", "Guide:提取与结算");
            Add(HandbookCategory.Term, "技能实例与重复", "技能容量", "同名技能默认不能重复。持有复读腺后允许重复，每份技能独立记录顺序、次数、成长和临时状态。通常最多 4 个技能，安于现状最多 2 个。", "随机生成单技能怪物时不抽取增幅技能；继承或裁切到单技能时保留实际来源。", "Tool:复读腺", "Personality:安于现状");
            return entries.AsReadOnly();
        }

        static string Signed(int value) => value >= 0 ? "+" + value : value.ToString();
        public static string RarityName(Rarity rarity) => rarity switch { Rarity.White => "白色", Rarity.Blue => "蓝色", Rarity.Gold => "金色", Rarity.Negative => "负面", _ => throw new ArgumentOutOfRangeException(nameof(rarity)) };
        static string KindName(string kind) => kind switch { "Aura" => "光环被动", "Trigger" => "触发被动", _ => kind };
    }
}

