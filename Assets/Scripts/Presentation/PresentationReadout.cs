using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TheCall
{
    // Shared monster artwork has the same consumers in opening, cage and work seats.
    internal static class PresentationReadout
    {
        public static void Monster(MonsterSlotView slot, MonsterView monster, bool selected, PresentationSpriteCatalog catalog)
        {
            if (slot == null) return;
            slot.monsterId = monster?.Id;
            var occupied = monster != null;
            if (slot.portrait != null)
            {
                slot.portrait.gameObject.SetActive(occupied);
                if (occupied) slot.portrait.Show(monster.Appearance);
                else slot.portrait.Clear();
            }
            if (slot.emptyMark != null) slot.emptyMark.SetActive(!occupied);
            Text(slot.title, occupied ? $"{monster.DisplayName} [{monster.Race}]" : "");
            Text(slot.subtitle, occupied ? $"{monster.Personality} · {monster.Mood} | {SkillLine(monster)}" : "");
            Badge(slot.personalityIcon, occupied ? catalog?.GetPersonalityIcon(monster.Personality) : null);
            Badge(slot.raceIcon, occupied ? catalog?.GetRaceIcon(monster.Race) : null);
            Badge(slot.moodIcon, occupied ? catalog?.GetMoodIcon(monster.Mood) : null);
            if (slot.selection != null) slot.selection.SetActive(selected);
        }

        public static void Text(TMP_Text label, string text)
        {
            if (label != null) label.text = text;
        }

        public static void Label(Transform root, string name, string value) => Text(Find(root, name)?.GetComponent<TMP_Text>(), value);

        public static void Shown(Transform root, string name, bool shown)
        {
            var child = Find(root, name);
            if (child != null) child.gameObject.SetActive(shown);
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            var direct = root.Find(name);
            if (direct != null) return direct;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static void Badge(UnityEngine.UI.Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
        }

        static string SkillLine(MonsterView monster)
        {
            if (monster.Skills.Count == 0) return "";
            var skill = monster.Skills[0];
            var quote = skill.Quote > 0 ? "报价 " + skill.Quote : "效果";
            var rarity = skill.Rarity == Rarity.Gold ? "金" : skill.Rarity == Rarity.Blue ? "蓝" : "白";
            return monster.Skills.Count == 1 ? quote + " · " + rarity : quote + " · " + monster.Skills.Count + "项";
        }

        public static string Join(IReadOnlyList<string> values) => values == null ? "" : string.Join("\n", values);

        public static bool Contains(IReadOnlyList<string> values, string value)
        {
            if (values == null) return false;
            for (var i = 0; i < values.Count; i++) if (values[i] == value) return true;
            return false;
        }
    }
}
