using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheCall.Editor
{
    public static class MonsterSlotPrefabLibrary
    {
        public const string PrefabDirectory = "Assets/Prefabs/MonsterSlotCard";

        public const string ActionPath = PrefabDirectory + "/MonsterSlotCard_Action.prefab";
        public const string WideActionPath = PrefabDirectory + "/MonsterSlotCard_WideAction.prefab";
        public const string GlassyBreedingPath = PrefabDirectory + "/MonsterSlotCard_GlassyBreeding.prefab";
        public const string GlassyExtractionPath = PrefabDirectory + "/MonsterSlotCard_GlassyExtraction.prefab";
        public const string CompactPath = PrefabDirectory + "/MonsterSlotCard_Compact.prefab";
        public const string StripPath = PrefabDirectory + "/MonsterSlotCard_Strip.prefab";

        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string PortraitPrefabPath = "Assets/Prefabs/UI/MonsterPortrait.prefab";

        readonly struct TemplateGroup
        {
            public readonly string SourceName;
            public readonly string PrefabPath;
            public readonly string[] MemberNames;

            public TemplateGroup(string sourceName, string prefabPath, params string[] memberNames)
            {
                SourceName = sourceName;
                PrefabPath = prefabPath;
                MemberNames = memberNames ?? Array.Empty<string>();
            }
        }

        [MenuItem("The Call/同步怪物立绘视口")]
        public static void SyncPortraitViewportsFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("同步立绘视口", "请先退出播放模式。", "确定");
                return;
            }

            var report = SyncPortraitViewports();
            EditorUtility.DisplayDialog("同步立绘视口", report, "确定");
        }

        [MenuItem("The Call/导出怪物卡预制体")]
        public static void ExportFromMenu()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("导出怪物卡", "请先退出播放模式。", "确定");
                return;
            }

            var report = ExportAndConnect();
            EditorUtility.DisplayDialog("导出怪物卡", report, "确定");
        }

        public static string ExportAndConnect()
        {
            EnsurePrefabDirectory();

            if (!File.Exists(MainScenePath))
                throw new InvalidOperationException("Missing scene: " + MainScenePath);

            if (EditorSceneManager.GetActiveScene().path != MainScenePath)
                EditorSceneManager.OpenScene(MainScenePath);

            var slots = UnityEngine.Object
                .FindObjectsByType<MonsterSlotView>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .ToDictionary(view => view.gameObject.name, view => view.gameObject);

            var groups = BuildGroups();
            var lines = new List<string>();

            foreach (var group in groups)
            {
                if (!slots.TryGetValue(group.SourceName, out var source) || source == null)
                {
                    lines.Add("跳过 " + group.PrefabPath + "：找不到 " + group.SourceName);
                    continue;
                }

                ExportGroup(source, group.PrefabPath, group.MemberNames, slots);
                lines.Add("已导出 " + group.PrefabPath + "（来源 " + group.SourceName + "，关联 "
                            + (1 + group.MemberNames.Length) + " 个实例）");
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            lines.Add(SyncPortraitViewports());
            return string.Join("\n", lines);
        }

        public static string SyncPortraitViewports()
        {
            var hidden = HideBakedPortraitParts();
            var size = MonsterPortrait.RecommendedViewportSize;
            return "立绘视口保持各张卡上已经摆好的 Portrait 矩形。建议新卡起点 "
                   + size.x + "×" + size.y
                   + "。已关掉 " + hidden + " 张静态零件图，预制体里显示的是拼接预览。";
        }

        public static int HideBakedPortraitParts()
        {
            var root = PrefabUtility.LoadPrefabContents(PortraitPrefabPath);
            if (root == null)
                return 0;

            var count = 0;
            var images = root.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i] == null || !images[i].gameObject.activeSelf)
                    continue;

                images[i].gameObject.SetActive(false);
                count++;
            }

            if (count > 0)
                PrefabUtility.SaveAsPrefabAsset(root, PortraitPrefabPath);

            PrefabUtility.UnloadPrefabContents(root);
            return count;
        }

        static TemplateGroup[] BuildGroups()
        {
            var sellMembers = new string[17];
            for (var i = 1; i < 18; i++)
                sellMembers[i - 1] = "SellCard_" + i;

            var cageMembers = new string[17];
            for (var i = 1; i < 18; i++)
                cageMembers[i - 1] = "CageCard_" + i;

            var extractMembers = new string[5];
            for (var i = 1; i < 6; i++)
                extractMembers[i - 1] = "ExtractionSlot_" + i;

            return new[]
            {
                new TemplateGroup("Candidate_0", ActionPath, "Candidate_1", "Candidate_2"),
                new TemplateGroup("SellCard_0", WideActionPath, sellMembers),
                new TemplateGroup("BreedingSeat_0", GlassyBreedingPath, "BreedingSeat_1", "BreedingSeat_2"),
                new TemplateGroup("ExtractionSlot_0", GlassyExtractionPath, extractMembers),
                new TemplateGroup("CageCard_0", CompactPath, cageMembers),
                new TemplateGroup("BreedingSeat_3", StripPath, "BreedingSeat_4", "BreedingSeat_5"),
            };
        }

        static void ExportGroup(
            GameObject source,
            string prefabPath,
            IReadOnlyList<string> memberNames,
            IReadOnlyDictionary<string, GameObject> slots)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(source, prefabPath, InteractionMode.AutomatedAction);
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
                throw new InvalidOperationException("Failed to write prefab: " + prefabPath);

            var settings = new ConvertToPrefabInstanceSettings();
            for (var i = 0; i < memberNames.Count; i++)
            {
                var name = memberNames[i];
                if (!slots.TryGetValue(name, out var instance) || instance == null || instance == source)
                    continue;

                if (PrefabUtility.IsPartOfPrefabInstance(instance)
                    && PrefabUtility.GetCorrespondingObjectFromSource(instance) == prefabRoot)
                    continue;

                PrefabUtility.ConvertToPrefabInstance(instance, prefabRoot, settings, InteractionMode.AutomatedAction);
            }
        }

        public static string ResolvePrefabPath(float w, float h, string action, bool glassy)
        {
            if (action == "留下它")
                return ActionPath;
            if (action == "出售")
                return WideActionPath;
            if (glassy && h <= 80f)
                return StripPath;
            if (glassy && w >= 280f)
                return GlassyBreedingPath;
            if (glassy)
                return GlassyExtractionPath;

            return CompactPath;
        }

        public static bool TryLoadPrefab(float w, float h, string action, bool glassy, out GameObject prefab)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResolvePrefabPath(w, h, action, glassy));
            return prefab != null;
        }

        static void EnsurePrefabDirectory()
        {
            if (AssetDatabase.IsValidFolder(PrefabDirectory))
                return;

            const string parent = "Assets/Prefabs";
            if (!AssetDatabase.IsValidFolder(parent))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            AssetDatabase.CreateFolder(parent, "MonsterSlotCard");
        }
    }
}
