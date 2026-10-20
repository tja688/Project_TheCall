using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TheCall
{
    public enum MonsterEyeFamily
    {
        Anxious,
        Indifferent,
        Manic,
        Slothful,
        Affectionate,
    }

    [Flags]
    public enum MonsterMutation
    {
        None = 0,
        StitchedMouth = 1,
        ExtraArms = 2,
        LargeHand = 4,
        SmallHand = 8,
        Tentacles = 16,
        Wings = 32,
        ExposedBrain = 64,
        ExtraHead = 128,
        Spikes = 256,
        DarkLimbs = 512,
        BodyStitches = 1024,
        Blush = 2048,
        Stars = 4096,
        Spots = 8192,
        LongTongue = 16384,
        ForkTongue = 32768,
        ButtonEye = 65536,
        HollowEyes = 131072,
        AsymmetricEyes = 262144,
    }

    /// <summary>
    /// 一次绘制所用的值快照。性格选择眼型，技能选择改造，心情只改变动态强度。
    /// 本表是对已交付美术的本次接入设计，不向规则添加性格或技能。
    /// </summary>
    public readonly struct MonsterVisualTraits
    {
        MonsterVisualTraits(MonsterAppearance appearance, MonsterEyeFamily eyeFamily, int moodTier, MonsterMutation mutations)
        {
            Appearance = appearance;
            EyeFamily = eyeFamily;
            MoodTier = moodTier;
            Mutations = mutations;
            Signature = string.Join("/", "monster-v1", Number(appearance.Recipe), Number(appearance.Palette),
                Number(appearance.Head), Number(appearance.Mouth), Number(appearance.Arms), Number(appearance.Legs),
                Number(appearance.Crest), Number(appearance.Pattern), Number(appearance.Variation),
                Number((int)eyeFamily), Number(moodTier), Number((int)mutations));
        }

        public MonsterAppearance Appearance { get; }
        public MonsterEyeFamily EyeFamily { get; }
        public int MoodTier { get; }
        public MonsterMutation Mutations { get; }
        public string Signature { get; }

        public string MutationDescription
        {
            get
            {
                if (Mutations == MonsterMutation.None) return "基础体态";
                var text = new StringBuilder();
                Describe(text, MonsterMutation.StitchedMouth, "缝合嘴");
                Describe(text, MonsterMutation.ExtraArms, "增生手臂");
                Describe(text, MonsterMutation.LargeHand, "肥大手");
                Describe(text, MonsterMutation.SmallHand, "短小手");
                Describe(text, MonsterMutation.Tentacles, "触须");
                Describe(text, MonsterMutation.Wings, "翅翼");
                Describe(text, MonsterMutation.ExposedBrain, "外露脑");
                Describe(text, MonsterMutation.ExtraHead, "副头");
                Describe(text, MonsterMutation.Spikes, "尖角");
                Describe(text, MonsterMutation.DarkLimbs, "暗色肢体");
                Describe(text, MonsterMutation.BodyStitches, "身体缝线");
                Describe(text, MonsterMutation.Blush, "腮红");
                Describe(text, MonsterMutation.Stars, "星形标记");
                Describe(text, MonsterMutation.Spots, "斑纹");
                Describe(text, MonsterMutation.LongTongue, "长舌");
                Describe(text, MonsterMutation.ForkTongue, "分叉舌");
                Describe(text, MonsterMutation.ButtonEye, "纽扣眼饰");
                Describe(text, MonsterMutation.HollowEyes, "空洞眼饰");
                Describe(text, MonsterMutation.AsymmetricEyes, "异形眼饰");
                return text.ToString();
            }
        }

        public static MonsterVisualTraits From(MonsterView monster)
        {
            if (monster == null) throw new ArgumentNullException(nameof(monster));
            return Create(monster.Appearance, monster.Personality, monster.Mood, monster.SkillNames);
        }

        public static MonsterVisualTraits From(MonsterAppearance appearance) =>
            Create(appearance, "稳定", "普通", Array.Empty<string>());

        public static MonsterVisualTraits Create(MonsterAppearance appearance, string personality, string mood, IReadOnlyList<string> skills)
        {
            var mutations = MonsterMutation.None;
            if (skills != null)
                for (var i = 0; i < skills.Count; i++) mutations |= SkillMutation(skills[i]);
            return new MonsterVisualTraits(appearance, Eyes(personality), Mood(mood), mutations);
        }

        // 原稿给出五种眼型；以下是当前 25 种规则性格的美术归类。
        static MonsterEyeFamily Eyes(string personality)
        {
            switch (personality)
            {
                case "稳定":
                case "孤僻":
                case "独立":
                case "稳重":
                case "安于现状": return MonsterEyeFamily.Indifferent;
                case "自律":
                case "依赖":
                case "好学":
                case "好奇":
                case "随心所欲": return MonsterEyeFamily.Anxious;
                case "自私":
                case "贪财":
                case "急躁":
                case "仇恨血脉": return MonsterEyeFamily.Manic;
                case "摸鱼派":
                case "厌恶工作":
                case "厌世":
                case "迷恋死亡": return MonsterEyeFamily.Slothful;
                case "重视血脉":
                case "合群":
                case "恋爱脑":
                case "乐天派":
                case "热爱工作":
                case "乐于助人":
                case "上进心": return MonsterEyeFamily.Affectionate;
                default: return MonsterEyeFamily.Indifferent;
            }
        }

        static int Mood(string mood)
        {
            switch (mood)
            {
                case "倦怠": return 0;
                case "疲倦": return 1;
                case "热情": return 3;
                case "热爱": return 4;
                default: return 2;
            }
        }

        // 每个正式技能都有确定的可见改造。左右同类技能共用部件语汇，具体挂点由画像处理。
        static MonsterMutation SkillMutation(string skill)
        {
            switch (skill)
            {
                case "能量体": return MonsterMutation.Spots;
                case "左能量体": return MonsterMutation.ExtraArms;
                case "右能量体": return MonsterMutation.ExtraArms;
                case "双头能量体": return MonsterMutation.ExtraHead;
                case "蜜能量体": return MonsterMutation.Blush | MonsterMutation.Wings;
                case "优秀能量体": return MonsterMutation.Stars;
                case "增生能量体": return MonsterMutation.ExtraArms | MonsterMutation.SmallHand;
                case "自爆体": return MonsterMutation.Spikes | MonsterMutation.Stars;
                case "奇异香": return MonsterMutation.Wings;
                case "传能耳": return MonsterMutation.Spikes;
                case "优质肉体": return MonsterMutation.LargeHand;
                case "良好肉体": return MonsterMutation.SmallHand;
                case "情绪收集器": return MonsterMutation.ExposedBrain;
                case "收尸腺": return MonsterMutation.DarkLimbs | MonsterMutation.HollowEyes;
                case "啃食体": return MonsterMutation.LongTongue;
                case "抽杀体": return MonsterMutation.Tentacles | MonsterMutation.Spikes;
                case "群能量体": return MonsterMutation.ExtraArms | MonsterMutation.ExtraHead;
                case "生长能量体": return MonsterMutation.Spots | MonsterMutation.Spikes;
                case "不稳定能量体": return MonsterMutation.BodyStitches | MonsterMutation.AsymmetricEyes;
                case "怪异香": return MonsterMutation.Wings | MonsterMutation.Spots;
                case "左复制腺体": return MonsterMutation.ExtraHead | MonsterMutation.ExposedBrain;
                case "右复制腺体": return MonsterMutation.ExtraHead | MonsterMutation.ExposedBrain;
                case "换位手": return MonsterMutation.LargeHand | MonsterMutation.ExtraArms;
                case "汲取鼻": return MonsterMutation.Tentacles | MonsterMutation.LongTongue;
                case "外接胚胎": return MonsterMutation.ExtraHead | MonsterMutation.Spots;
                case "产金管道": return MonsterMutation.Stars | MonsterMutation.Tentacles;
                case "不稳定肉体": return MonsterMutation.BodyStitches | MonsterMutation.AsymmetricEyes;
                case "祭品腺": return MonsterMutation.DarkLimbs | MonsterMutation.StitchedMouth;
                case "移植胚胎": return MonsterMutation.BodyStitches | MonsterMutation.ExtraHead;
                case "血祭体": return MonsterMutation.Tentacles | MonsterMutation.DarkLimbs;
                case "回响嗓": return MonsterMutation.ForkTongue;
                case "镜眼": return MonsterMutation.ButtonEye;
                case "无穷肌肉": return MonsterMutation.LargeHand | MonsterMutation.ExtraArms;
                case "血崩体": return MonsterMutation.Spikes | MonsterMutation.Wings | MonsterMutation.DarkLimbs;
                default: return MonsterMutation.None;
            }
        }

        void Describe(StringBuilder text, MonsterMutation mutation, string label)
        {
            if ((Mutations & mutation) == 0) return;
            if (text.Length > 0) text.Append("、");
            text.Append(label);
        }

        static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
