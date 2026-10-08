using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TheCall
{
    public static class MonsterRigRuntime
    {
        static List<MonsterRigPiece> pieces;
        static Dictionary<string, Sprite> sprites;

        /// <summary>按配方挑一个身体、随机装上配件，编译成骨架并配好配色。游戏里的怪物和编辑器的预览都从这里来。</summary>
        public static bool TryBuildSkeleton(int recipe, int salt, out MonsterRigSkeleton skeleton)
        {
            skeleton = null;
            var catalog = Catalog();
            var bodies = new List<MonsterRigPiece>();
            for (var i = 0; i < catalog.Count; i++)
            {
                if (catalog[i] != null && catalog[i].Kind == MonsterPartKind.Body)
                    bodies.Add(catalog[i]);
            }

            if (bodies.Count == 0)
                return false;

            bodies.Sort((a, b) => string.CompareOrdinal(a.Data.id, b.Data.id));
            var index = recipe % bodies.Count;
            if (index < 0)
                index += bodies.Count;

            var assignments = new List<MonsterRigAssignment>();
            MonsterRigRandom.Fill(
                bodies[index],
                catalog,
                assignments,
                null,
                new Dictionary<MonsterSocketGroup, List<string>>(),
                MonsterRigRandom.Units(salt),
                true,
                null);

            var built = MonsterRigLayout.Compile(bodies[index], catalog, assignments, null, false);
            var rest = MonsterRigLayout.Rest(built);
            var paintSalt = salt;
            MonsterRigColor.Paint(rest, key => MonsterRigColor.IndexFor(paintSalt, key, MonsterPortrait.Palette.Length));
            for (var i = 0; i < rest.Nodes.Count; i++)
            {
                var node = rest.Nodes[i];
                if (node.Bone >= 0 && node.Bone < built.Bones.Count)
                    built.Bones[node.Bone].PaletteIndex = node.PaletteIndex;
            }

            skeleton = built;
            return built.Bones.Count > 0;
        }

        /// <summary>静止姿势，供需要一次性画出怪物而不需要动画的场景（测试和旧工具）。</summary>
        public static bool TryCompose(int recipe, int salt, out MonsterRigPose pose)
        {
            pose = new MonsterRigPose();
            if (!TryBuildSkeleton(recipe, salt, out var skeleton))
                return false;

            pose = MonsterRigLayout.Rest(skeleton);
            return pose.Nodes.Count > 0;
        }

        public static Sprite SpriteFor(string partId)
        {
            EnsureSprites();
            if (sprites != null && sprites.TryGetValue(partId ?? "", out var sprite))
                return sprite;
            return null;
        }

        public static IReadOnlyList<MonsterRigPiece> Catalog()
        {
            if (pieces != null)
                return pieces;

            pieces = MonsterRigCatalog.PiecesFrom(ReadFile());
            EnsureSprites();
            for (var i = 0; i < pieces.Count; i++)
            {
                var sprite = SpriteFor(pieces[i].Data.id);
                if (sprite == null)
                    continue;

                pieces[i].Width = Mathf.RoundToInt(sprite.rect.width);
                pieces[i].Height = Mathf.RoundToInt(sprite.rect.height);
            }

            return pieces;
        }

        static void EnsureSprites()
        {
            if (sprites != null)
                return;

            MonsterPartLibrary.Ensure();
            sprites = new Dictionary<string, Sprite>(System.StringComparer.Ordinal);
            AddSprites("身体", MonsterPartLibrary.Body);
            AddSprites("头", MonsterPartLibrary.Head);
            AddSprites("眼", MonsterPartLibrary.Eye);
            AddSprites("嘴", MonsterPartLibrary.Mouth);
            AddSprites("手", MonsterPartLibrary.Hand);
            AddSprites("脚", MonsterPartLibrary.Foot);
            AddSprites("尾巴", MonsterPartLibrary.Tail);
            AddSprites("头饰", MonsterPartLibrary.Hat);
            AddSprites("配饰", MonsterPartLibrary.Accessory);
        }

        static void AddSprites(string folder, Sprite[] loaded)
        {
            if (loaded == null)
                return;

            for (var i = 0; i < loaded.Length; i++)
            {
                var sprite = loaded[i];
                if (sprite == null)
                    continue;

                sprites[MonsterRigCatalog.PartId(folder, sprite.name)] = sprite;
            }
        }

        static MonsterRigFile ReadFile()
        {
            var asset = Resources.Load<TextAsset>("MonsterRig");
            if (asset != null && !string.IsNullOrEmpty(asset.text))
                return Parse(asset.text);

#if UNITY_EDITOR
            var path = Path.Combine(Application.dataPath, "Resources/MonsterRig.json");
            if (File.Exists(path))
                return Parse(File.ReadAllText(path));
#endif
            return new MonsterRigFile();
        }

        static MonsterRigFile Parse(string json)
        {
            var parsed = JsonUtility.FromJson<MonsterRigFile>(json) ?? new MonsterRigFile();
            parsed.Upgrade();
            return parsed;
        }

        /// <summary>静止姿势下整只怪物的轴对齐包围盒（与 MonsterPortrait 里 Rig 坐标一致）。</summary>
        public static bool TryMeasureRestBounds(int recipe, int salt, out Rect bounds)
        {
            bounds = default;
            if (!TryCompose(recipe, salt, out var pose) || pose.Nodes.Count == 0)
                return false;

            bounds = MeasurePoseBounds(pose);
            return bounds.width > 0.5f && bounds.height > 0.5f;
        }

        /// <summary>覆盖开局/商店常见配方后，给 UI 卡面留出的推荐视口大小。</summary>
        public static Vector2 RecommendedPortraitViewport(int recipeSamples = 12, int saltSamples = 8)
        {
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            for (var recipe = 0; recipe < recipeSamples; recipe++)
            {
                for (var salt = 1; salt <= saltSamples; salt++)
                {
                    if (!TryMeasureRestBounds(recipe, salt * 9973 + recipe * 131, out var bounds))
                        continue;

                    minX = Mathf.Min(minX, bounds.xMin);
                    minY = Mathf.Min(minY, bounds.yMin);
                    maxX = Mathf.Max(maxX, bounds.xMax);
                    maxY = Mathf.Max(maxY, bounds.yMax);
                }
            }

            if (float.IsPositiveInfinity(minX))
                return new Vector2(142f, 102f);

            return new Vector2(Mathf.Ceil(maxX - minX + 12f), Mathf.Ceil(maxY - minY + 12f));
        }

        static Rect MeasurePoseBounds(MonsterRigPose pose)
        {
            var minX = float.PositiveInfinity;
            var minY = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            var maxY = float.NegativeInfinity;
            for (var i = 0; i < pose.Nodes.Count; i++)
            {
                var node = pose.Nodes[i];
                var placement = MonsterRigLayout.PlacementFor(node);
                AccumulatePlacement(ref minX, ref minY, ref maxX, ref maxY, placement);
            }

            if (float.IsPositiveInfinity(minX))
                return default;

            return Rect.MinMaxRect(minX, minY, maxX, maxY);
        }

        static void AccumulatePlacement(
            ref float minX,
            ref float minY,
            ref float maxX,
            ref float maxY,
            SpritePlacement placement)
        {
            var size = placement.Size;
            var pivot = placement.Pivot;
            var local = new Vector2[4];
            local[0] = new Vector2(-pivot.x * size.x, -pivot.y * size.y);
            local[1] = new Vector2((1f - pivot.x) * size.x, -pivot.y * size.y);
            local[2] = new Vector2((1f - pivot.x) * size.x, (1f - pivot.y) * size.y);
            local[3] = new Vector2(-pivot.x * size.x, (1f - pivot.y) * size.y);

            var radians = placement.Rotation * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            for (var i = 0; i < local.Length; i++)
            {
                var x = local[i].x * placement.Scale.x;
                var y = local[i].y * placement.Scale.y;
                var rotatedX = x * cos - y * sin;
                var rotatedY = x * sin + y * cos;
                var worldX = placement.AnchoredPosition.x + rotatedX;
                var worldY = placement.AnchoredPosition.y + rotatedY;
                minX = Mathf.Min(minX, worldX);
                minY = Mathf.Min(minY, worldY);
                maxX = Mathf.Max(maxX, worldX);
                maxY = Mathf.Max(maxY, worldY);
            }
        }
    }
}
