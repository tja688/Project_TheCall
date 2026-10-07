using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TheCall.Editor
{
    public static class MonsterAssemblyCatalogEditor
    {
        const string CatalogPath = "Assets/Resources/MonsterAssemblyCatalog.asset";
        const string PartsRoot = "Assets/Resources/MonsterParts";

        [MenuItem("The Call/生成怪物组件目录")]
        public static void Generate()
        {
            EnsureAsset();
            EditorUtility.DisplayDialog(
                "怪物组件目录",
                "已生成或补齐 MonsterAssemblyCatalog.asset。工作区、运行时和预览现在共用稳定部件 ID。",
                "好的");
        }

        public static MonsterAssemblyCatalog EnsureAsset()
        {
            EnsureResourcesFolder();
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterAssemblyCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MonsterAssemblyCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var needsMigration = catalog.schemaVersion < 4;
            var changed = EnsureParts(catalog);
            changed |= EnsureTemplates(catalog);
            if (needsMigration)
            {
                for (var i = 0; i < catalog.parts.Count; i++)
                {
                    var part = catalog.parts[i];
                    if (part != null)
                    {
                        part.attachmentPoint = MonsterAssemblyCatalog.DefaultAttachmentPoint(part.kind);
                        part.connectionType = MonsterAssemblyCatalog.DefaultConnectionType(part.kind);
                        MonsterAssemblyCatalog.ConfigureDefaultHeadLayout(part);
                    }
                }

                MonsterAssemblyCatalog.ConfigureSeedGeometry(catalog);
                catalog.schemaVersion = 4;
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return catalog;
        }

        public static MonsterAssemblyTemplate CreateTemplate(
            MonsterAssemblyCatalog catalog,
            string id,
            string displayName,
            MonsterAssemblyTemplate source)
        {
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            var template = source == null ? new MonsterAssemblyTemplate() : source.Clone();
            template.id = id;
            template.displayName = displayName;
            catalog.templates ??= new List<MonsterAssemblyTemplate>();
            catalog.templates.Add(template);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return template;
        }

        static bool EnsureParts(MonsterAssemblyCatalog catalog)
        {
            var changed = false;
            changed |= EnsurePartGroup(catalog, MonsterPartKind.Body, "body_", "Body", MonsterColorRole.Primary, 10, false);
            changed |= EnsurePartGroup(catalog, MonsterPartKind.Head, "head_", "Head", MonsterColorRole.Primary, 40, false);
            changed |= EnsurePartGroup(catalog, MonsterPartKind.Eye, "eye_", "Eye", MonsterColorRole.None, 50, false);
            changed |= EnsurePartGroup(catalog, MonsterPartKind.Mouth, "mouth_", "Mouth", MonsterColorRole.None, 60, false);
            changed |= EnsurePartGroup(catalog, MonsterPartKind.Hand, "hand_", "Hand", MonsterColorRole.Primary, 20, false);
            changed |= EnsurePartGroup(catalog, MonsterPartKind.Foot, "foot_", "Foot", MonsterColorRole.Primary, 30, false);
            changed |= EnsurePartGroup(catalog, MonsterPartKind.Tail, "tail_", "Tail", MonsterColorRole.Primary, 0, true);
            changed |= EnsurePartGroup(catalog, MonsterPartKind.Hat, "hat_", "Hat", MonsterColorRole.None, 70, true);
            changed |= EnsurePartGroup(catalog, MonsterPartKind.Accessory, "accessory_", "Accessory", MonsterColorRole.Secondary, 35, true);
            return changed;
        }

        static bool EnsurePartGroup(
            MonsterAssemblyCatalog catalog,
            MonsterPartKind kind,
            string prefix,
            string folder,
            MonsterColorRole colorRole,
            int sortOrder,
            bool optional)
        {
            var changed = false;
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { PartsRoot + "/" + folder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);

            for (var index = 0; index < paths.Count; index++)
            {
                var sprite = LoadSprite(paths[index]);
                if (sprite == null)
                    continue;

                var id = StablePartId(prefix, sprite.name, paths[index], index + 1);
                var part = FindPart(catalog, id);
                if (part == null)
                {
                    part = new MonsterPartDefinition
                    {
                        id = id,
                        kind = kind,
                        sprite = sprite,
                        colorRole = colorRole,
                        connectionType = MonsterAssemblyCatalog.DefaultConnectionType(kind),
                        attachmentPoint = MonsterAssemblyCatalog.DefaultAttachmentPoint(kind),
                        optional = optional,
                        sortOrder = sortOrder,
                        defaultPivot = new Vector2(0.5f, 0.5f),
                        defaultScale = Vector2.one,
                    };
                    MonsterAssemblyCatalog.ConfigureDefaultHeadLayout(part);
                    catalog.parts.Add(part);
                    changed = true;
                }
                else if (part.sprite == null)
                {
                    part.sprite = sprite;
                    changed = true;
                }
            }

            return changed;
        }

        static string StablePartId(string prefix, string spriteName, string path, int fallbackIndex)
        {
            var end = spriteName == null ? 0 : spriteName.Length;
            var start = end;
            while (start > 0 && char.IsDigit(spriteName[start - 1]))
                start--;

            if (start < end && int.TryParse(spriteName.Substring(start), out var number) && number > 0)
                return prefix + number.ToString("00");

            var guid = AssetDatabase.AssetPathToGUID(path);
            var suffix = string.IsNullOrEmpty(guid)
                ? fallbackIndex.ToString("00")
                : "x" + guid.Substring(0, Math.Min(8, guid.Length));
            return prefix + suffix;
        }

        static bool EnsureTemplates(MonsterAssemblyCatalog catalog)
        {
            var changed = false;
            for (var i = 0; i < MonsterAppearance.RecipeCount; i++)
            {
                var id = MonsterAppearance.TemplateIdForRecipe(i);
                if (catalog.GetTemplateOrNull(id) != null)
                    continue;

                var template = new MonsterAssemblyTemplate
                {
                    id = id,
                    displayName = "体型 " + (i + 1),
                    anchors = new MonsterAnchorSet(),
                    body = MonsterPartSlot.Create("body_" + (i + 1).ToString("00"), MonsterAnchorKind.Canvas, Vector2.zero),
                    head = MonsterPartSlot.Create("head_" + (i % 4 + 1).ToString("00"), MonsterAnchorKind.Neck, Vector2.zero),
                    eye = MonsterPartSlot.Create("eye_" + (i % 5 + 1).ToString("00"), MonsterAnchorKind.Face, Vector2.zero),
                    mouth = MonsterPartSlot.Create("mouth_" + (i % 6 + 1).ToString("00"), MonsterAnchorKind.Face, Vector2.zero),
                    hand = MonsterPartSlot.Create("hand_" + (i % 5 + 1).ToString("00"), MonsterAnchorKind.Hand, Vector2.zero),
                    foot = MonsterPartSlot.Create("foot_" + (i % 3 + 1).ToString("00"), MonsterAnchorKind.Foot, Vector2.zero),
                    tail = MonsterPartSlot.Create(i == 2 ? "tail_03" : null, MonsterAnchorKind.Tail, Vector2.zero),
                    hat = MonsterPartSlot.Create(i == 0 || i == 3 ? "hat_" + (i + 1).ToString("00") : null, MonsterAnchorKind.Face, Vector2.zero),
                    accessory = MonsterPartSlot.Create(i == 1 || i == 4 ? "accessory_02" : null, MonsterAnchorKind.Body, Vector2.zero),
                };
                MonsterAssemblyCatalog.SetConnectionRequirements(template);
                catalog.templates.Add(template);
                changed = true;
            }

            return changed;
        }

        static MonsterPartDefinition FindPart(MonsterAssemblyCatalog catalog, string id)
        {
            if (catalog.parts == null)
                catalog.parts = new List<MonsterPartDefinition>();

            for (var i = 0; i < catalog.parts.Count; i++)
            {
                if (catalog.parts[i] != null && string.Equals(catalog.parts[i].id, id, StringComparison.Ordinal))
                    return catalog.parts[i];
            }

            return null;
        }

        static Sprite LoadSprite(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                    return sprite;
            }

            return null;
        }

        static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }
}
