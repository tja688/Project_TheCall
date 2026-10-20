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
            if (fresh || resetPlans || catalog.bodies.Length != 6) catalog.bodies = Plans();
            if (fresh || resetPlans || catalog.palettes.Length != 6) catalog.palettes = Palettes();
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
            SpriteAtlasExtensions.Add(atlas, catalog.parts.Select(p => (Object)p.sprite).ToArray());
            SpriteAtlasExtensions.SetPackingSettings(atlas, new SpriteAtlasPackingSettings { padding = 4, enableRotation = false, enableTightPacking = false });
            SpriteAtlasExtensions.SetTextureSettings(atlas, new SpriteAtlasTextureSettings { readable = false, generateMipMaps = false, sRGB = true, filterMode = FilterMode.Bilinear });
            SpriteAtlasExtensions.SetIncludeInBuild(atlas, true);
            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
            Debug.Log("Monster art ready: " + catalog.parts.Length + " original parts, six body plans, six palettes. Authored plans preserved on reimport.");
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
                    ink = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1) };
            }
            finally { Object.DestroyImmediate(texture); }
        }

        static MonsterArtCatalog.BodyPiece Piece(string id, float x, float y, float w, float h, float angle = 0, bool flip = false) =>
            new MonsterArtCatalog.BodyPiece { part = id, position = new Vector2(x, y), size = new Vector2(w, h), angle = angle, mirror = flip };

        static MonsterArtCatalog.BodyPlan Plan(string name, MonsterArtCatalog.BodyPiece[] pieces, Vector2 face, float faceWidth,
            Vector2 mouth, Vector2 crown, Vector2 shoulder, Vector2 hip, bool integrated = false) =>
            new MonsterArtCatalog.BodyPlan { name = name, pieces = pieces, face = face, faceWidth = faceWidth, mouth = mouth,
                crown = crown, leftShoulder = new Vector2(-shoulder.x, shoulder.y), rightShoulder = shoulder,
                leftHip = new Vector2(-hip.x, hip.y), rightHip = hip, integratedHead = integrated };

        static MonsterArtCatalog.BodyPlan[] Plans()
        {
            var small = Plan("小胖体", new[] { Piece("508", -5, 130, 153, 178) }, new Vector2(-9, 185), 64,
                new Vector2(27, 149), new Vector2(-7, 219), new Vector2(57, 128), new Vector2(35, 49), true);
            small.oral = new Rect(-33, 124, 103, 52); small.mouthWidth = 65;
            small.rightShoulder = new Vector2(17, 114);
            small.leftHip = new Vector2(-35, 59); small.rightHip = new Vector2(7, 56);
            var large = Plan("大胖体", new[] { Piece("520", -3, 131, 174, 183) }, new Vector2(-7, 187), 74,
                new Vector2(34, 151), new Vector2(-11, 223), new Vector2(66, 130), new Vector2(42, 47), true);
            large.oral = new Rect(-28, 131, 98, 49); large.mouthWidth = 73;
            large.leftHip = new Vector2(-38, 60); large.rightHip = new Vector2(37, 61);
            var arc = Plan("弯长躯", new[] { Piece("997", 0, 124, 108, 164), Piece("head", -26, 217, 102, 77) },
                new Vector2(-26, 222), 64, new Vector2(-24, 199), new Vector2(-26, 253), new Vector2(40, 128), new Vector2(28, 48));
            arc.mouthWidth = 52;
            arc.leftShoulder = new Vector2(26, 142); arc.rightShoulder = new Vector2(42, 147);
            arc.leftHip = new Vector2(-27, 61); arc.rightHip = new Vector2(2, 65);
            var bend = Plan("弯躯", new[] { Piece("918", 7, 106, 145, 133), Piece("942", -47, 161, 49, 79, -13), Piece("head", -43, 216, 107, 80) },
                new Vector2(-43, 223), 65, new Vector2(-42, 198), new Vector2(-43, 253), new Vector2(50, 123), new Vector2(38, 47));
            bend.mouthWidth = 52;
            bend.rightShoulder = new Vector2(22, 104);
            bend.leftHip = new Vector2(-11, 66); bend.rightHip = new Vector2(45, 63);
            var cup = Plan("长颈杯身", new[] { Piece("943", 0, 105, 125, 129), Piece("942", 2, 169, 53, 82, 5), Piece("head", 0, 218, 110, 82) },
                new Vector2(0, 224), 66, new Vector2(0, 199), new Vector2(0, 255), new Vector2(49, 126), new Vector2(33, 48));
            cup.mouthWidth = 54;
            cup.leftHip = new Vector2(-28, 62); cup.rightHip = new Vector2(28, 62);
            var twin = Plan("双端宽肩", new[] { Piece("918", -32, 117, 105, 117), Piece("918", 32, 117, 105, 117, 0, true),
                Piece("942", -48, 175, 34, 44, -6), Piece("942", 48, 175, 34, 44, 6, true),
                Piece("head", -48, 202, 84, 65), Piece("head", 48, 202, 84, 65) },
                new Vector2(-48, 209), 48, new Vector2(-48, 187), new Vector2(-48, 233), new Vector2(68, 120), new Vector2(37, 56));
            twin.twin = true; twin.secondFace = new Vector2(48, 209); twin.mouthWidth = 40;
            twin.leftHip = new Vector2(-35, 85); twin.rightHip = new Vector2(35, 85);
            return new[] { small, large, arc, bend, cup, twin };
        }

        static MonsterArtCatalog.Palette[] Palettes() => new[]
        {
            Colorway("莓紫与苔绿", new Color32(142,99,156,255),new Color32(132,168,114,255),new Color32(244,191,123,255)),
            Colorway("铜绿与奶黄",new Color32(101,164,153,255),new Color32(197,162,100,255),new Color32(244,199,155,255)),
            Colorway("赭黄与灰蓝",new Color32(193,152,96,255),new Color32(117,149,163,255),new Color32(209,118,125,255)),
            Colorway("雾蓝与藕紫",new Color32(115,147,183,255),new Color32(165,128,170,255),new Color32(230,183,116,255)),
            Colorway("胭脂与鼠尾草",new Color32(185,118,135,255),new Color32(135,172,147,255),new Color32(245,209,146,255)),
            Colorway("苔绿与陶土",new Color32(153,173,113,255),new Color32(180,128,105,255),new Color32(228,194,138,255))
        };
        static MonsterArtCatalog.Palette Colorway(string name, Color body, Color limbs, Color accent) =>
            new MonsterArtCatalog.Palette { name = name, body = body, limbs = limbs, accent = accent };
    }
}
