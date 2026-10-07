using System;
using System.Collections.Generic;

namespace TheCall
{
    public sealed class ContentBookException : Exception
    {
        public ContentBookException(string message) : base(message)
        {
        }
    }

    internal enum EffectKind
    {
        EnergyQuote,
        SideCount,
        AddToOthers,
        LandingResponse,
        DoubleWhenIsolated,
        Devour,
        RepeatQuote,
        CapacityExtraForLeftNeighbor,
        ExtraWalksForRightNeighbor,
        NextEnergyBonus,
        GainCapacity,
        SwapWithLeft,
        DoubleAdjacentEnergy,
    }

    internal readonly struct Effect
    {
        public Effect(EffectKind kind, int a, int b)
        {
            Kind = kind;
            A = a;
            B = b;
        }

        public EffectKind Kind { get; }

        public int A { get; }

        public int B { get; }
    }

    internal sealed class SkillDef
    {
        public SkillDef(string name, Rarity rarity, int poolIndex, SkillUse use, SkillAffix affix, Effect[] effects)
        {
            Name = name;
            Rarity = rarity;
            PoolIndex = poolIndex;
            Use = use;
            Affix = affix;
            Effects = effects;
        }

        public string Name { get; }

        public Rarity Rarity { get; }

        public int PoolIndex { get; }

        public SkillUse Use { get; }

        public SkillAffix Affix { get; }

        public IReadOnlyList<Effect> Effects { get; }

        public bool Has(EffectKind kind)
        {
            for (var i = 0; i < Effects.Count; i++)
            {
                if (Effects[i].Kind == kind)
                    return true;
            }

            return false;
        }

        public Effect Get(EffectKind kind)
        {
            for (var i = 0; i < Effects.Count; i++)
            {
                if (Effects[i].Kind == kind)
                    return Effects[i];
            }

            throw new InvalidOperationException(Name + " 没有 " + kind);
        }
    }

    internal enum TechFlag
    {
        BreedingSkill,
        ExtraBreedingSlot,
        ExtraSkill,
        ExtraParent,
        Modifier,
    }

    internal sealed class TechDef
    {
        public TechDef(string name, TechFlag flag)
        {
            Name = name;
            Flag = flag;
        }

        public string Name { get; }

        public TechFlag Flag { get; }
    }

    internal sealed class Economy
    {
        public Economy(
            int wage,
            int overtimeWage,
            int techPercent,
            int modifierAmount,
            int baseExtractionCells,
            int baseBreedingSlots,
            int expandedBreedingSlots,
            int baseParents,
            int expandedParents)
        {
            Wage = wage;
            OvertimeWage = overtimeWage;
            TechPercent = techPercent;
            ModifierAmount = modifierAmount;
            BaseExtractionCells = baseExtractionCells;
            BaseBreedingSlots = baseBreedingSlots;
            ExpandedBreedingSlots = expandedBreedingSlots;
            BaseParents = baseParents;
            ExpandedParents = expandedParents;
        }

        public int Wage { get; }

        public int OvertimeWage { get; }

        public int TechPercent { get; }

        public int ModifierAmount { get; }

        public int BaseExtractionCells { get; }

        public int BaseBreedingSlots { get; }

        public int ExpandedBreedingSlots { get; }

        public int BaseParents { get; }

        public int ExpandedParents { get; }
    }

    internal readonly struct LevelBand
    {
        public LevelBand(int due, int excess)
        {
            Due = due;
            Excess = excess;
        }

        public int Due { get; }

        public int Excess { get; }
    }

    public sealed class ContentBook
    {
        ContentBook(
            Economy economy,
            LevelBand[] levels,
            ToolDefinition[] tools,
            TechDef[] techs,
            SkillDef[] skills,
            int whitePrice,
            int bluePrice,
            int goldPrice,
            int whiteWeight,
            int blueWeight,
            int goldWeight)
        {
            Economy = economy;
            Levels = levels;
            Tools = tools;
            Techs = techs;
            Skills = skills;
            _whitePrice = whitePrice;
            _bluePrice = bluePrice;
            _goldPrice = goldPrice;
            _whiteWeight = whiteWeight;
            _blueWeight = blueWeight;
            _goldWeight = goldWeight;
        }

        readonly int _whitePrice;
        readonly int _bluePrice;
        readonly int _goldPrice;
        readonly int _whiteWeight;
        readonly int _blueWeight;
        readonly int _goldWeight;

        internal Economy Economy { get; }

        internal IReadOnlyList<LevelBand> Levels { get; }

        internal IReadOnlyList<ToolDefinition> Tools { get; }

        internal IReadOnlyList<TechDef> Techs { get; }

        internal IReadOnlyList<SkillDef> Skills { get; }

        public static ContentBook Parse(string json)
        {
            var root = BookJson.Read(json) as JsonObject;
            if (root == null)
                throw new ContentBookException("$. 必须是对象");

            return Map(root);
        }

        internal int Wage(bool overtime) => overtime ? Economy.OvertimeWage : Economy.Wage;

        internal int Price(Rarity rarity)
        {
            if (rarity == Rarity.Blue)
                return _bluePrice;
            if (rarity == Rarity.Gold)
                return _goldPrice;

            return _whitePrice;
        }

        internal int Weight(Rarity rarity)
        {
            if (rarity == Rarity.Blue)
                return _blueWeight;
            if (rarity == Rarity.Gold)
                return _goldWeight;

            return _whiteWeight;
        }

        internal SkillDef FindSkill(string name)
        {
            for (var i = 0; i < Skills.Count; i++)
            {
                if (Skills[i].Name == name)
                    return Skills[i];
            }

            return null;
        }

        static ContentBook Map(JsonObject root)
        {
            var economyNode = Required(root, "economy", "$");
            var economyObject = AsObject(economyNode, "$.economy");
            var price = AsObject(Required(economyObject, "price", "$.economy"), "$.economy.price");
            var weight = AsObject(Required(economyObject, "weight", "$.economy"), "$.economy.weight");
            var economy = new Economy(
                Int(economyObject, "wage", "$.economy"),
                Int(economyObject, "overtimeWage", "$.economy"),
                Int(economyObject, "techPercent", "$.economy"),
                Int(economyObject, "modifierAmount", "$.economy"),
                Int(economyObject, "baseExtractionCells", "$.economy"),
                Int(economyObject, "baseBreedingSlots", "$.economy"),
                Int(economyObject, "expandedBreedingSlots", "$.economy"),
                Int(economyObject, "baseParents", "$.economy"),
                Int(economyObject, "expandedParents", "$.economy"));

            var levels = MapLevels(AsArray(Required(root, "levels", "$"), "$.levels"));
            var tools = MapTools(AsArray(Required(root, "tools", "$"), "$.tools"));
            var techs = MapTechs(AsArray(Required(root, "techs", "$"), "$.techs"));
            var skills = MapSkills(AsArray(Required(root, "skills", "$"), "$.skills"));
            return new ContentBook(
                economy,
                levels,
                tools,
                techs,
                skills,
                Int(price, "White", "$.economy.price"),
                Int(price, "Blue", "$.economy.price"),
                Int(price, "Gold", "$.economy.price"),
                Int(weight, "White", "$.economy.weight"),
                Int(weight, "Blue", "$.economy.weight"),
                Int(weight, "Gold", "$.economy.weight"));
        }

        static LevelBand[] MapLevels(JsonArray array)
        {
            if (array.Items.Count != 7)
                throw new ContentBookException("$.levels 必须是 7 关");

            var levels = new LevelBand[array.Items.Count];
            for (var i = 0; i < array.Items.Count; i++)
            {
                var path = "$.levels[" + i + "]";
                var band = AsObject(array.Items[i], path);
                levels[i] = new LevelBand(Int(band, "due", path), Int(band, "excess", path));
            }

            return levels;
        }

        static ToolDefinition[] MapTools(JsonArray array)
        {
            var tools = new ToolDefinition[array.Items.Count];
            var seen = new HashSet<string>();
            var effects = new HashSet<string>();
            for (var i = 0; i < array.Items.Count; i++)
            {
                var path = "$.tools[" + i + "]";
                var tool = AsObject(array.Items[i], path);
                var name = Text(tool, "name", path);
                if (!seen.Add(name))
                    throw new ContentBookException(path + ".name 重复");

                var effectName = Text(tool, "effect", path);
                if (!effects.Add(effectName))
                    throw new ContentBookException(path + ".effect 重复");

                ToolEffect effect;
                if (!Enum.TryParse(effectName, out effect) || effect == ToolEffect.None)
                    throw new ContentBookException(path + ".effect 未知");

                var cells = 0;
                if (effect == ToolEffect.ExtractionCells)
                    cells = Int(tool, "cells", path);

                tools[i] = new ToolDefinition(name, Int(tool, "price", path), RarityOf(Text(tool, "rarity", path), path), effect, cells);
            }

            return tools;
        }

        static TechDef[] MapTechs(JsonArray array)
        {
            var techs = new TechDef[array.Items.Count];
            var seen = new HashSet<string>();
            for (var i = 0; i < array.Items.Count; i++)
            {
                var path = "$.techs[" + i + "]";
                var tech = AsObject(array.Items[i], path);
                var name = Text(tech, "name", path);
                if (!seen.Add(name))
                    throw new ContentBookException(path + ".name 重复");

                TechFlag flag;
                if (!Enum.TryParse(Text(tech, "flag", path), out flag))
                    throw new ContentBookException(path + ".flag 未知");

                techs[i] = new TechDef(name, flag);
            }

            return techs;
        }

        static SkillDef[] MapSkills(JsonArray array)
        {
            var skills = new SkillDef[array.Items.Count];
            var names = new HashSet<string>();
            var pools = new HashSet<string>();
            for (var i = 0; i < array.Items.Count; i++)
            {
                var path = "$.skills[" + i + "]";
                var skill = AsObject(array.Items[i], path);
                var name = Text(skill, "name", path);
                if (!names.Add(name))
                    throw new ContentBookException(path + ".name 重复");

                var rarity = RarityOf(Text(skill, "rarity", path), path);
                var poolIndex = Int(skill, "poolIndex", path);
                if (!pools.Add(rarity + ":" + poolIndex))
                    throw new ContentBookException(path + ".poolIndex 在同一稀有度里重复");

                SkillUse use;
                if (!Enum.TryParse(Text(skill, "use", path), out use))
                    throw new ContentBookException(path + ".use 未知");

                var affix = Affix(AsArray(Required(skill, "affix", path), path + ".affix"), path);
                var effects = Effects(AsArray(Required(skill, "effects", path), path + ".effects"), path);
                var def = new SkillDef(name, rarity, poolIndex, use, affix, effects);
                CheckAffix(def, path);
                SkillSentences.Format(def);
                skills[i] = def;
            }

            return skills;
        }

        static void CheckAffix(SkillDef skill, string path)
        {
            if (skill.Has(EffectKind.SwapWithLeft) && skill.Affix != SkillAffix.Immovable)
                throw new ContentBookException(path + " 换位必须带不动");
            if (skill.Has(EffectKind.Devour) && skill.Affix != (SkillAffix.Destroy | SkillAffix.Permanent))
                throw new ContentBookException(path + " 吞噬必须带消灭和永久");
            if (skill.Has(EffectKind.GainCapacity) && skill.Affix != SkillAffix.Capacity)
                throw new ContentBookException(path + " 产能必须带产能词条");
            if (!skill.Has(EffectKind.SwapWithLeft) && !skill.Has(EffectKind.Devour) && !skill.Has(EffectKind.GainCapacity) && skill.Affix != SkillAffix.None)
                throw new ContentBookException(path + " 词条必须为空");
        }

        static Effect[] Effects(JsonArray array, string path)
        {
            var effects = new Effect[array.Items.Count];
            var seen = new HashSet<string>();
            for (var i = 0; i < array.Items.Count; i++)
            {
                var itemPath = path + ".effects[" + i + "]";
                var item = AsObject(array.Items[i], itemPath);
                var kindName = Text(item, "kind", itemPath);
                if (!seen.Add(kindName))
                    throw new ContentBookException(itemPath + ".kind 重复");

                EffectKind kind;
                if (!Enum.TryParse(kindName, out kind))
                    throw new ContentBookException(itemPath + ".kind 未知");

                effects[i] = EffectOf(kind, item, itemPath);
            }

            return effects;
        }

        static Effect EffectOf(EffectKind kind, JsonObject item, string path)
        {
            if (kind == EffectKind.EnergyQuote || kind == EffectKind.LandingResponse)
                return new Effect(kind, Int(item, "quote", path), 0);
            if (kind == EffectKind.AddToOthers || kind == EffectKind.NextEnergyBonus)
                return new Effect(kind, Int(item, "amount", path), 0);
            if (kind == EffectKind.SideCount)
            {
                CountedSide side;
                if (!Enum.TryParse(Text(item, "side", path), out side))
                    throw new ContentBookException(path + ".side 未知");

                return new Effect(kind, Int(item, "perMonster", path), (int)side);
            }

            if (kind == EffectKind.Devour)
            {
                var permanent = Bool(item, "permanent", path);
                return new Effect(kind, Int(item, "writeback", path), permanent ? 1 : 0);
            }

            if (kind == EffectKind.RepeatQuote)
            {
                var times = Int(item, "times", path);
                if (times != 2)
                    throw new ContentBookException(path + ".times 必须是 2");

                return new Effect(kind, times, 0);
            }

            if (kind == EffectKind.GainCapacity || kind == EffectKind.CapacityExtraForLeftNeighbor)
                return new Effect(kind, Int(item, "layers", path), 0);
            if (kind == EffectKind.ExtraWalksForRightNeighbor)
                return new Effect(kind, Int(item, "walks", path), 0);

            return new Effect(kind, 0, 0);
        }

        static SkillAffix Affix(JsonArray array, string path)
        {
            var affix = SkillAffix.None;
            for (var i = 0; i < array.Items.Count; i++)
            {
                var name = (array.Items[i] as JsonString)?.Text;
                SkillAffix flag;
                if (name == null || !Enum.TryParse(name, out flag) || flag == SkillAffix.None)
                    throw new ContentBookException(path + ".affix[" + i + "] 未知");

                affix |= flag;
            }

            return affix;
        }

        static Rarity RarityOf(string name, string path)
        {
            Rarity rarity;
            if (!Enum.TryParse(name, out rarity))
                throw new ContentBookException(path + " 稀有度未知");

            return rarity;
        }

        static JsonNode Required(JsonObject obj, string key, string path)
        {
            JsonNode node;
            if (!obj.Map.TryGetValue(key, out node))
                throw new ContentBookException(path + "." + key + " 缺失");

            return node;
        }

        static JsonObject AsObject(JsonNode node, string path)
        {
            var obj = node as JsonObject;
            if (obj == null)
                throw new ContentBookException(path + " 必须是对象");

            return obj;
        }

        static JsonArray AsArray(JsonNode node, string path)
        {
            var array = node as JsonArray;
            if (array == null)
                throw new ContentBookException(path + " 必须是数组");

            return array;
        }

        static string Text(JsonObject obj, string key, string path)
        {
            var node = Required(obj, key, path) as JsonString;
            if (node == null)
                throw new ContentBookException(path + "." + key + " 必须是字符串");

            return node.Text;
        }

        static int Int(JsonObject obj, string key, string path)
        {
            var node = Required(obj, key, path) as JsonNumber;
            if (node == null)
                throw new ContentBookException(path + "." + key + " 必须是整数");

            return node.Value;
        }

        static bool Bool(JsonObject obj, string key, string path)
        {
            var node = Required(obj, key, path) as JsonBool;
            if (node == null)
                throw new ContentBookException(path + "." + key + " 必须是布尔");

            return node.Value;
        }
    }

    public static class ContentGate
    {
        public static ContentBook Current { get; private set; }

        public static void Use(ContentBook book)
        {
            if (book == null || TheCallApp.IsRunning)
                throw new InvalidOperationException("先停掉架构，再装入内容。");

            Current = book;
        }
    }
}
