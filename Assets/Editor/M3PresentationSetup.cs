using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheCall.Editor
{
    public static class M3PresentationSetup
    {
        public static PresentationSpriteCatalog CreateSpriteCatalog()
        {
            var hearthwood = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Arts/Atlas/hearthwood.png"))
            {
                if (asset is Sprite s)
                    hearthwood[s.name] = s;
            }

            var icons = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Arts/Atlas/icons_full_32.png"))
            {
                if (asset is Sprite s)
                    icons[s.name] = s;
            }

            var uiSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Art/Ui" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Sprite s)
                        uiSprites[s.name] = s;
                }
            }

            Sprite S(string name)
            {
                if (uiSprites.TryGetValue(name, out var s1) && s1 != null) return s1;
                if (hearthwood.TryGetValue(name, out var s2) && s2 != null) return s2;
                if (icons.TryGetValue(name, out var s3) && s3 != null) return s3;
                return null;
            }

            Sprite IconAt(int index) => icons.TryGetValue("icons_full_32_" + index, out var s) ? s : null;

            const string catalogPath = "Assets/Resources/PresentationSpriteCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<PresentationSpriteCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<PresentationSpriteCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }

            catalog.tools.Clear();
            void AddTool(string name, Sprite icon, string desc, Rarity rarity, int price)
            {
                catalog.tools.Add(new ToolArtDef
                {
                    name = name,
                    icon = icon ?? S("potion") ?? S("IconBolt"),
                    description = desc,
                    rarity = rarity,
                    price = price
                });
            }

            AddTool("急急装置", S("IconRocket") ?? IconAt(100), "最左侧怪物最先产生能量的技能，能量值翻倍", Rarity.White, 30);
            AddTool("小喇叭", IconAt(54) ?? S("potion"), "最右侧怪物的能量值+3", Rarity.White, 30);
            AddTool("打卡机", IconAt(50) ?? S("coin"), "每个通关阶段后，获得 10 金币", Rarity.White, 30);
            AddTool("招工海报", S("paper_9slice") ?? IconAt(60), "商店多出现 1 只可购买的怪物", Rarity.White, 30);
            AddTool("批发合同", IconAt(62) ?? S("paper_9slice"), "商店中的工具价格-5 金币", Rarity.White, 30);
            AddTool("研磨机", IconAt(70) ?? S("axe"), "怪物通过废弃槽消灭时，获得 5 金币", Rarity.White, 30);
            AddTool("电化能机", S("IconBolt") ?? IconAt(80), "结算阶段时，产生 10 点能量", Rarity.White, 30);
            AddTool("精神操控仪", IconAt(90) ?? S("gem"), "性格提供的能量值加成额外+1", Rarity.White, 30);
            AddTool("怪物严选", S("gem") ?? IconAt(95), "开始阶段时，随机提升一个怪物的心情档位", Rarity.White, 30);
            AddTool("情绪收割机", IconAt(110) ?? S("IconBolt"), "心情为普通以上的怪物，能量值+2", Rarity.White, 30);
            AddTool("上级员工证", S("IconBadge") ?? IconAt(120), "提取槽额外获得一个格子", Rarity.Blue, 50);
            AddTool("隔音棉", IconAt(130) ?? S("shield"), "怪物不会因连续进入提取槽而降低心情", Rarity.Blue, 50);
            AddTool("五行罗盘", S("cursor") ?? IconAt(140), "提取槽中种族相生的相邻两只怪物，能量值各+3", Rarity.Blue, 50);
            AddTool("验货单", IconAt(150) ?? S("paper_9slice"), "商店多出现 1 件工具", Rarity.Blue, 50);
            AddTool("稀有度检测仪", IconAt(160) ?? S("gem"), "提取槽中每有 1 个金色技能，全场能量值+1", Rarity.Blue, 50);
            AddTool("劣胜装置", S("IconBolt") ?? IconAt(170), "单技能怪物产生的能量值翻倍，且产生能量的技能触发次数+1", Rarity.Gold, 80);
            AddTool("复读腺", IconAt(180) ?? S("potion"), "怪物可以重复拥有技能", Rarity.Gold, 80);
            AddTool("蓄热熔炉", S("iron_ore") ?? IconAt(190), "工资结算阶段时，将剩余能量的 20%保留到下一关卡，其余正常清零", Rarity.Gold, 80);

            catalog.races.Clear();
            void AddRace(string name, Sprite icon) => catalog.races.Add(new NamedSpriteDef { name = name, icon = icon });
            AddRace("山脉", S("coin") ?? IconAt(200));
            AddRace("森林", S("wood_log") ?? IconAt(205));
            AddRace("海生", S("watering_can") ?? S("potion") ?? IconAt(210));
            AddRace("天空", S("bar_fill_energy") ?? IconAt(215));
            AddRace("沙漠", S("stone") ?? IconAt(220));

            catalog.moods.Clear();
            void AddMood(string name, Sprite icon) => catalog.moods.Add(new NamedSpriteDef { name = name, icon = icon });
            AddMood("热爱", S("gem") ?? IconAt(250));
            AddMood("热情", S("apple") ?? IconAt(251));
            AddMood("普通", S("bread") ?? IconAt(252));
            AddMood("疲倦", S("turnip") ?? IconAt(253));
            AddMood("倦怠", S("mushroom") ?? IconAt(254));

            catalog.personalities.Clear();
            void AddPersonality(string name, Sprite icon) => catalog.personalities.Add(new NamedSpriteDef { name = name, icon = icon });
            var book = ContentBook.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "call-book.json")));
            var personalities = new string[book.Personalities.Count];
            for (var i = 0; i < personalities.Length; i++) personalities[i] = book.Personalities[i].Name;
            for (var i = 0; i < personalities.Length; i++)
            {
                var icon = IconAt(i + 1) ?? S("gem");
                AddPersonality(personalities[i], icon);
            }

            foreach (var tool in catalog.tools)
            {
                var definition = book.FindTool(tool.name);
                if (definition != null) { tool.description = definition.Description; tool.price = definition.Price; tool.rarity = definition.Rarity; }
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created PresentationSpriteCatalog with {catalog.tools.Count} tools, {catalog.races.Count} races, {catalog.moods.Count} moods, {catalog.personalities.Count} personalities.");
            return catalog;
        }

        public static void EnsureHoverPrefabHeader()
        {
            const string path = "Assets/Prefabs/UI/MonsterHoverPanel.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var window = root.transform.Find("Window");
                if (window == null)
                {
                    Debug.LogError("No Window in " + path);
                    return;
                }

                var header = window.Find("Header");
                if (header == null)
                {
                    var headerGo = new GameObject("Header", typeof(RectTransform));
                    headerGo.transform.SetParent(window, false);
                    var headerRect = headerGo.GetComponent<RectTransform>();
                    headerRect.anchorMin = new Vector2(0f, 1f);
                    headerRect.anchorMax = new Vector2(1f, 1f);
                    headerRect.pivot = new Vector2(0.5f, 1f);
                    headerRect.anchoredPosition = new Vector2(0f, 32f);
                    headerRect.sizeDelta = new Vector2(0f, 28f);

                    var img = headerGo.AddComponent<UnityEngine.UI.Image>();
                    img.color = new Color(0.08f, 0.12f, 0.16f, 0.95f);
                    img.raycastTarget = false;

                    var textGo = new GameObject("HeaderText", typeof(RectTransform));
                    textGo.transform.SetParent(headerGo.transform, false);
                    var textRect = textGo.GetComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero;
                    textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = new Vector2(8f, 2f);
                    textRect.offsetMax = new Vector2(-8f, -2f);

                    var tmp = textGo.AddComponent<TMPro.TextMeshProUGUI>();
                    var font = Resources.Load<TMPro.TMP_FontAsset>("SmileySans-Oblique-3 SDF");
                    if (font != null) tmp.font = font;
                    tmp.fontSize = 13;
                    tmp.color = new Color32(232, 244, 240, 255);
                    tmp.alignment = TMPro.TextAlignmentOptions.Center;
                    tmp.raycastTarget = false;
                    headerGo.transform.SetAsFirstSibling();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log("Added Header to MonsterHoverPanel prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
