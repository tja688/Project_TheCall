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
    }
}
