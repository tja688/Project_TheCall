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

    /// <summary>
    /// 一次绘制所用的值快照。性格选择眼型，心情改变动态强度，技能不参与外观。
    /// 本表是对已交付美术的本次接入设计，不向规则添加性格或技能。
    /// </summary>
    public readonly struct MonsterVisualTraits
    {
        MonsterVisualTraits(MonsterAppearance appearance, MonsterEyeFamily eyeFamily, int moodTier)
        {
            Appearance = appearance; EyeFamily = eyeFamily; MoodTier = moodTier;
            Signature = string.Join("/", "monster-template-v2", Number(appearance.Recipe), Number(appearance.Palette),
                Number(appearance.HeadAccessory), Number(appearance.BackAccessory), Number(appearance.Variation),
                Number((int)eyeFamily), Number(moodTier));
        }
        public MonsterAppearance Appearance { get; }
        public MonsterEyeFamily EyeFamily { get; }
        public int MoodTier { get; }
        public string Signature { get; }

        public static MonsterVisualTraits From(MonsterView monster)
        {
            if (monster == null) throw new ArgumentNullException(nameof(monster));
            return Create(monster.Appearance, monster.Personality, monster.Mood, monster.SkillNames);
        }

        public static MonsterVisualTraits From(MonsterAppearance appearance) =>
            Create(appearance, "稳定", "普通", Array.Empty<string>());

        public static MonsterVisualTraits Create(MonsterAppearance appearance, string personality, string mood, IReadOnlyList<string> skills)
        {
            return new MonsterVisualTraits(appearance, Eyes(personality), Mood(mood));
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

        static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
