using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    /// <summary>Rebuilds references to the supplied art, never rasterizes or overwrites it.</summary>
    public static class MonsterArtIntegration
    {
        const string Source = "Assets/Arts/Pngs/部位组件";
        public const string CatalogPath = "Assets/Resources/MonsterArtCatalog.asset";

        [MenuItem("TheCall/Monster/更新部件目录与图集")]
        public static void Apply() => Apply(false);

        public static void Apply(bool resetPlans)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before importing monster art.");
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterArtCatalog>(CatalogPath);
            var fresh = catalog == null;
            if (fresh) catalog = ScriptableObject.CreateInstance<MonsterArtCatalog>();
            catalog.parts = Directory.GetFiles(Source, "*.png", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal).Select(ImportPart).ToArray();
            if (fresh || resetPlans || catalog.schemaVersion != 2) catalog.bodies = MonsterTemplateDefaults.Plans();
            if (fresh || resetPlans || catalog.schemaVersion != 2) catalog.palettes = MonsterTemplateDefaults.Palettes();
            catalog.schemaVersion = 2;
            catalog.MarkChanged();
            var validation = catalog.ValidationError();
            if (!string.IsNullOrEmpty(validation)) throw new InvalidOperationException(validation);
            if (fresh) AssetDatabase.CreateAsset(catalog, CatalogPath);
            EditorUtility.SetDirty(catalog);

            const string atlasPath = "Assets/Resources/MonsterPartsAtlas.spriteatlas";
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            if (atlas == null) { atlas = new SpriteAtlas(); AssetDatabase.CreateAsset(atlas, atlasPath); }
            var old = SpriteAtlasExtensions.GetPackables(atlas);
            if (old.Length > 0) SpriteAtlasExtensions.Remove(atlas, old);
            SpriteAtlasExtensions.Add(atlas, catalog.parts.SelectMany(p => p.fillMask == null ? new Object[] { p.sprite } : new Object[] { p.sprite, p.fillMask }).ToArray());
            SpriteAtlasExtensions.SetPackingSettings(atlas, new SpriteAtlasPackingSettings { padding = 4, enableRotation = false, enableTightPacking = false });
            SpriteAtlasExtensions.SetTextureSettings(atlas, new SpriteAtlasTextureSettings { readable = false, generateMipMaps = false, sRGB = true, filterMode = FilterMode.Bilinear });
            SpriteAtlasExtensions.SetIncludeInBuild(atlas, true);
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
            Debug.Log("Monster art ready: " + catalog.parts.Length + " original parts, twelve authored templates, six complete colorways. Authored plans preserved on reimport.");
        }

        static MonsterArtCatalog.Part ImportPart(string path)
        {
            path = path.Replace('\\', '/');
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null) throw new InvalidOperationException("No imported sprite: " + path);
            var texture = new Texture2D(2, 2);
            try
            {
                ImageConversion.LoadImage(texture, File.ReadAllBytes(path));
                var pixels = texture.GetPixels32();
                var sr = sprite.rect;
                var x0 = (int)sr.x; var y0 = (int)sr.y;
                var minX = (int)sr.width; var minY = (int)sr.height; var maxX = -1; var maxY = -1;
                for (var y = 0; y < (int)sr.height; y++)
                    for (var x = 0; x < (int)sr.width; x++)
                        if (pixels[(y + y0) * texture.width + x + x0].a > 8)
                        { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
                if (maxX < 0) throw new InvalidOperationException("Empty part: " + path);
                var file = Path.GetFileNameWithoutExtension(path);
                // Stable numeric source-layer IDs also work when the artist renames the Chinese suffix.
                var segments = file.Split('_');
                var id = segments.First(s => int.TryParse(s, out _));
                return new MonsterArtCatalog.Part { id = id, sprite = sprite,
                    ink = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1),
                    fillMask = MonsterTintMasks.Import(id, texture) };
            }
            finally { Object.DestroyImmediate(texture); }
        }

    }
}
