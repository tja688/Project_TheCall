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
            var book = ContentBook.Parse(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "call-book.json")));
            M3PresentationSetup.CreateSpriteCatalog();
            RepairAssemblyCatalog();
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
            return "Presentation migrated locally and saved: six research entries, separate tool icons, badges and readable details.";
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
            var assembly = AssetDatabase.LoadAssetAtPath<MonsterAssemblyCatalog>("Assets/Resources/MonsterAssemblyCatalog.asset");
            if (assembly == null || assembly.parts.Count != 39 || assembly.templates.Count != 6 || assembly.parts.Any(part => part.sprite == null)) throw new InvalidOperationException("Saved assembly references mismatch.");
            var operation = UnityEngine.Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
            if (operation == null || operation.ownedToolViews.Length != 18) throw new InvalidOperationException("Owned tool views mismatch.");
            foreach (var tool in operation.ownedToolViews)
                if (tool.iconImage == null || tool.iconImage.transform == tool.transform) throw new InvalidOperationException("Tool icon overwrites layout root.");
            return "Saved references verified: 25 personalities, 18 tools, six research nodes, monster badge consumers, 39 assembly parts / six templates.";
        }

        public static void RepairAssemblyCatalog()
        {
            const string path = "Assets/Resources/MonsterAssemblyCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterAssemblyCatalog>(path);
            if (catalog != null) return;
            // Fix the missing script through SerializedObject; preserve original part IDs and all authored geometry.
            var raw = AssetDatabase.LoadMainAssetAtPath(path);
            if (raw != null)
            {
                var probe = ScriptableObject.CreateInstance<MonsterAssemblyCatalog>();
                var script = MonoScript.FromScriptableObject(probe);
                var serialized = new SerializedObject(raw);
                var property = serialized.FindProperty("m_Script");
                if (property == null) throw new InvalidOperationException("Assembly catalog has no serialized script field.");
                property.objectReferenceValue = script;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(raw);
                AssetDatabase.SaveAssets();
                UnityEngine.Object.DestroyImmediate(probe);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                catalog = AssetDatabase.LoadAssetAtPath<MonsterAssemblyCatalog>(path);
            }
            if (catalog == null)
            {
                var recoveryPath = Path.Combine(Application.dataPath, "../docs/audit/2026-10-12/remediation/assembly-catalog-recovery.json");
                var recovery = JsonUtility.FromJson<AssemblyRecovery>(File.ReadAllText(recoveryPath));
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
                    if (hash != recovery.originalHash) throw new InvalidOperationException("Saved catalog changed since recovery snapshot; do not overwrite authored data.");
                }
                var beforeGuid = AssetDatabase.AssetPathToGUID(path);
                catalog = ScriptableObject.CreateInstance<MonsterAssemblyCatalog>();
                catalog.schemaVersion = recovery.schemaVersion;
                catalog.parts = recovery.parts; catalog.templates = recovery.templates;
                foreach (var reference in recovery.sprites)
                {
                    var spritePath = AssetDatabase.GUIDToAssetPath(reference.guid);
                    var sprite = AssetDatabase.LoadAllAssetsAtPath(spritePath).OfType<Sprite>().FirstOrDefault(candidate =>
                    {
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out string guid, out long localId);
                        return localId == reference.fileId && guid == reference.guid;
                    });
                    if (sprite == null) throw new InvalidOperationException("Cannot recover sprite " + reference.id + " at " + spritePath);
                    catalog.parts.Single(part => part.id == reference.id).sprite = sprite;
                }
                if (catalog.parts.Count != 39 || catalog.templates.Count != 6) throw new InvalidOperationException("Incomplete catalog recovery.");
                // CreateAsset overwrites the unreadable serialized object while retaining its existing meta GUID.
                AssetDatabase.CreateAsset(catalog, path);
                EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
                if (AssetDatabase.AssetPathToGUID(path) != beforeGuid) throw new InvalidOperationException("Catalog GUID changed during recovery.");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                catalog = AssetDatabase.LoadAssetAtPath<MonsterAssemblyCatalog>(path);
            }
            if (catalog.parts.Count != 39 || catalog.templates.Count != 6 || catalog.parts.Any(part => part == null || part.sprite == null))
                throw new InvalidOperationException("Assembly catalog recovery must preserve 39 valid parts and six templates.");
        }

        [Serializable] sealed class AssemblyRecovery
        {
            public int schemaVersion;
            public string originalHash;
            public List<MonsterPartDefinition> parts;
            public List<MonsterAssemblyTemplate> templates;
            public List<SpriteReference> sprites;
        }
        [Serializable] sealed class SpriteReference { public string id; public string guid; public long fileId; }

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
            slot.personalityIcon = Badge(slot, "PersonalityBadge", 0);
            slot.raceIcon = Badge(slot, "RaceBadge", 1);
            slot.moodIcon = Badge(slot, "MoodBadge", 2);
            EditorUtility.SetDirty(slot);
        }
        static Image Badge(MonsterSlotView slot, string name, int index)
        {
            var found = slot.transform.Find(name);
            if (found == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(slot.transform, false); found = go.transform;
                var rect = (RectTransform)found; rect.anchorMin = rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = new Vector2(-4 - index * 20, -4); rect.sizeDelta = new Vector2(18, 18);
            }
            var image = found.GetComponent<Image>(); image.raycastTarget = false; image.preserveAspect = true;
            return image;
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
