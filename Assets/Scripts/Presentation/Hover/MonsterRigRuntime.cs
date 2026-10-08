using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TheCall
{
    public static class MonsterRigRuntime
    {
        static List<MonsterRigPiece> pieces;
        static Dictionary<string, Sprite> sprites;

        public static bool TryCompose(int recipe, int salt, out MonsterRigPose pose)
        {
            pose = new MonsterRigPose();
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
            pose = MonsterRigLayout.Build(bodies[index], catalog, assignments, null, false, 0f, 0f);
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
                return JsonUtility.FromJson<MonsterRigFile>(asset.text) ?? new MonsterRigFile();

#if UNITY_EDITOR
            var path = Path.Combine(Application.dataPath, "Resources/MonsterRig.json");
            if (File.Exists(path))
                return JsonUtility.FromJson<MonsterRigFile>(File.ReadAllText(path)) ?? new MonsterRigFile();
#endif
            return new MonsterRigFile();
        }
    }
}
