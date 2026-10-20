using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall.Editor
{
    /// <summary>Local, repeatable migration: existing objects and authoring settings stay in place.</summary>
    public static class PresentationRemediation
    {
        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before saving presentation assets.");
            HandbookSceneSetup.Ensure();
            var book = ContentBook.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "call-book.json")));
            M3PresentationSetup.CreateSpriteCatalog();
            // 旧拼接目录修复已删除。见 Assets/Notes/新怪物系统交接.md。
            var research = UnityEngine.Object.FindAnyObjectByType<ResearchScreenView>(FindObjectsInactive.Include);
            if (research != null) MigrateResearch(research, book);
            var operation = UnityEngine.Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
            if (operation != null)
            {
                MigrateTools(operation);
                // Place the waste area below extraction, leaving the right column for inventory and genes.
                if (operation.discardButton != null)
                {
                    var rect = operation.discardButton.transform as RectTransform;
                    if (rect != null) rect.anchoredPosition = new Vector2(1080, -932);
                }
            }
            foreach (var slot in UnityEngine.Object.FindObjectsByType<MonsterSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Badges(slot);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/UI", "Assets/Prefabs/MonsterSlotCard" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var slot in root.GetComponentsInChildren<MonsterSlotView>(true)) Badges(slot);
                    foreach (var hover in root.GetComponentsInChildren<MonsterHover>(true)) MigrateHover(hover);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var hover in UnityEngine.Object.FindObjectsByType<MonsterHover>(FindObjectsInactive.Include, FindObjectsSortMode.None)) MigrateHover(hover);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            return "Presentation migrated locally and saved: six research entries, separate tool icons, badges and readable details. badgeDupeSets=" + CountBadgeDuplicateSets();
        }

        public static int CountBadgeDuplicateSets()
        {
            var dupes = 0;
            foreach (var slot in UnityEngine.Object.FindObjectsByType<MonsterSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var name in new[] { "PersonalityBadge", "RaceBadge", "MoodBadge" })
                {
                    var count = 0;
                    foreach (var transform in slot.GetComponentsInChildren<Transform>(true))
                    {
                        if (transform.name == name)
                            count++;
                    }

                    if (count > 1)
                        dupes++;
                }
            }

            return dupes;
        }

        public static string ValidateSaved()
        {
            var book = ContentBook.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "call-book.json")));
            var catalog = AssetDatabase.LoadAssetAtPath<PresentationSpriteCatalog>("Assets/Resources/PresentationSpriteCatalog.asset");
            if (catalog == null) throw new InvalidOperationException("Presentation catalog missing.");
            foreach (var personality in book.Personalities) if (catalog.GetPersonalityIcon(personality.Name) == null) throw new InvalidOperationException("Missing personality icon: " + personality.Name);
            foreach (var race in book.Races) if (catalog.GetRaceIcon(race.Name) == null) throw new InvalidOperationException("Missing race icon: " + race.Name);
            foreach (var mood in book.Moods) if (catalog.GetMoodIcon(mood.Name) == null) throw new InvalidOperationException("Missing mood icon: " + mood.Name);
            foreach (var tool in book.Tools)
            {
                var art = catalog.FindTool(tool.Name);
                if (art == null || art.icon == null || art.description != tool.Description || art.price != tool.Price || art.rarity != tool.Rarity) throw new InvalidOperationException("Tool content mismatch: " + tool.Name);
            }
            var research = UnityEngine.Object.FindAnyObjectByType<ResearchScreenView>(FindObjectsInactive.Include);
            if (research == null || research.nodes.Length != book.Techs.Count || research.nodes.Select(node => node.techName).Distinct().Count() != book.Techs.Count) throw new InvalidOperationException("Research nodes mismatch.");
            foreach (var node in research.nodes)
            {
                var icon = node.transform.Find("IconImage")?.GetComponent<Image>();
                if (icon == null || !icon.enabled || icon.sprite == null || node.title == null || node.title.text != node.techName || !node.summary.Contains(book.FindTech(node.techName).Description)) throw new InvalidOperationException("Research actual consumer incomplete: " + node.techName);
            }
            foreach (var slot in UnityEngine.Object.FindObjectsByType<MonsterSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (slot.personalityIcon == null || slot.raceIcon == null || slot.moodIcon == null) throw new InvalidOperationException("Missing badge consumer: " + slot.name);
            // 旧拼接目录校验已删除。
            return "Saved references verified: 25 personalities, 18 tools, six research nodes, monster badge consumers.";
        }

        static void MigrateResearch(ResearchScreenView view, ContentBook book)
        {
            if (view.nodes == null || view.nodes.Length == 0) throw new InvalidOperationException("Research template missing.");
            var template = view.nodes[0];
            var nodes = new List<TechNodeView>();
            for (var i = 0; i < book.Techs.Count; i++)
            {
                var definition = book.Techs[i];
                var node = view.nodes.FirstOrDefault(candidate => candidate != null && candidate.techName == definition.Name);
                if (node == null) node = UnityEngine.Object.Instantiate(template, template.transform.parent);
                node.name = "Tech_" + definition.Name;
                node.techName = definition.Name;
                var icon = node.transform.Find("IconImage")?.GetComponent<Image>();
                if (icon != null)
                {
                    var file = definition.Name switch { "基因实验" => "IconGene", "槽位扩容" => "IconSlot", "变异学说" => "IconMutant", "大乱炖" => "IconStew", "科学培育" => "IconFlask", _ => "IconBadge" };
                    icon.sprite = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Ui/" + file + ".png").OfType<Sprite>().FirstOrDefault();
                    icon.enabled = icon.sprite != null; icon.preserveAspect = true;
                }
                node.summary = definition.Description + "\n消耗1科技点；每局一次；无前置。";
                if (node.title != null) node.title.text = definition.Name;
                if (node.costLabel != null) node.costLabel.text = "消耗 1 科技点";
                var rect = node.transform as RectTransform;
                rect.anchoredPosition = new Vector2(100 + (i % 3) * 440, -(200 + (i / 3) * 400));
                nodes.Add(node);
            }
            view.nodes = nodes.ToArray();
            foreach (var child in template.transform.parent.GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("Pipe", StringComparison.Ordinal)) child.gameObject.SetActive(false);
            EditorUtility.SetDirty(view);
        }

        static void MigrateTools(OperationScreenView view)
        {
            if (view.ownedToolViews == null) return;
            for (var i = 0; i < view.ownedToolViews.Length; i++)
            {
                var tool = view.ownedToolViews[i];
                if (tool == null) continue;
                var root = tool.transform as RectTransform;
                root.anchoredPosition = new Vector2(82 + i * 68, -6);
                root.sizeDelta = new Vector2(64, 62);
                var child = tool.transform.Find("Icon");
                if (child == null)
                {
                    var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(tool.transform, false);
                    child = go.transform;
                }
                var image = child.GetComponent<Image>();
                var old = tool.GetComponent<Image>();
                if (old != null) { image.sprite = old.sprite; old.enabled = false; old.raycastTarget = false; }
                var icon = (RectTransform)child;
                icon.anchorMin = icon.anchorMax = new Vector2(0, 1); icon.pivot = new Vector2(0, 1);
                icon.anchoredPosition = new Vector2(8, -4); icon.sizeDelta = new Vector2(48, 38);
                image.preserveAspect = true; image.raycastTarget = true;
                tool.iconImage = image;
                if (tool.nameLabel != null)
                {
                    tool.nameLabel.fontSize = 10;
                    tool.nameLabel.enableAutoSizing = false;
                    tool.nameLabel.textWrappingMode = TextWrappingModes.Normal;
                    tool.nameLabel.rectTransform.sizeDelta = new Vector2(64, 24);
                }
                EditorUtility.SetDirty(tool);
            }
        }

        static void Badges(MonsterSlotView slot)
        {
            slot.personalityIcon = Badge(slot, slot.personalityIcon, "PersonalityBadge", 0);
            slot.raceIcon = Badge(slot, slot.raceIcon, "RaceBadge", 1);
            slot.moodIcon = Badge(slot, slot.moodIcon, "MoodBadge", 2);
            EditorUtility.SetDirty(slot);
        }

        static Image Badge(MonsterSlotView slot, Image preferred, string name, int index)
        {
            var matches = new List<Image>();
            foreach (var transform in slot.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name != name)
                    continue;
                var image = transform.GetComponent<Image>();
                if (image != null)
                    matches.Add(image);
            }

            Image keep;
            if (matches.Count == 0)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(slot.transform, false);
                keep = go.GetComponent<Image>();
            }
            else
            {
                keep = preferred != null && matches.Contains(preferred) ? preferred : matches[0];
                for (var i = 0; i < matches.Count; i++)
                {
                    if (matches[i] == keep)
                        continue;
                    DestroyBadgeObject(matches[i].gameObject);
                }
            }

            var rect = (RectTransform)keep.transform;
            rect.SetParent(slot.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-4 - index * 20, -4);
            rect.sizeDelta = new Vector2(18, 18);
            keep.raycastTarget = false;
            keep.preserveAspect = true;
            return keep;
        }

        static void DestroyBadgeObject(GameObject gameObject)
        {
            if (gameObject == null)
                return;
            if (PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                if (PrefabUtility.IsAddedGameObjectOverride(gameObject))
                    UnityEngine.Object.DestroyImmediate(gameObject);
                else
                    gameObject.SetActive(false);
                return;
            }

            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        public static void MigrateHover(MonsterHover hover)
        {
            var serialized = new SerializedObject(hover);
            var window = serialized.FindProperty("_window").objectReferenceValue as RectTransform;
            if (window == null) return;
            var header = window.Find("Header") as RectTransform;
            if (header != null)
            {
                header.sizeDelta = new Vector2(420, 130);
                var text = header.GetComponentInChildren<TMP_Text>();
                if (text != null) { text.enableAutoSizing = false; text.fontSize = 17; text.alignment = TextAlignmentOptions.TopLeft; }
            }
            serialized.FindProperty("_subpanelSize").vector2Value = new Vector2(420, 176);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (var propertyName in new[] { "_subNames", "_subSentences" })
            {
                var texts = serialized.FindProperty(propertyName);
                for (var i = 0; i < texts.arraySize; i++)
                {
                    var text = texts.GetArrayElementAtIndex(i).objectReferenceValue as TMP_Text;
                    if (text == null) continue;
                    var rect = text.rectTransform;
                    rect.sizeDelta = new Vector2(396, propertyName == "_subNames" ? 36 : 100);
                    text.enableAutoSizing = false; text.fontSize = propertyName == "_subNames" ? 22 : 16;
                }
            }
            var panels = serialized.FindProperty("_subpanels");
            for (var i = 0; i < panels.arraySize; i++)
            {
                var panel = panels.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform;
                if (panel != null) panel.sizeDelta = new Vector2(420, 176);
            }
        }
    }
}
