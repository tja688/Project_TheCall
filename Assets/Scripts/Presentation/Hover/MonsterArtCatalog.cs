using System;
using System.Collections.Generic;
using UnityEngine;
namespace TheCall
{
    [CreateAssetMenu(menuName = "TheCall/Monster art catalog")]
    public sealed class MonsterArtCatalog : ScriptableObject
    {
        [Serializable] public sealed class BodyPlan
        {
            public MonsterTemplate id;
            public string name;
            public Sprite source;
            public Sprite[] colorways = Array.Empty<Sprite>();
            public Rect frame;
        }
        [Serializable] public sealed class Palette
        {
            public MonsterPalette id;
            public string name;
            public Color body, pattern, eye, mouth;
        }
        public int schemaVersion;
        public BodyPlan[] bodies = Array.Empty<BodyPlan>();
        public Palette[] palettes = Array.Empty<Palette>();
        [Range(.65f, 1f)] public float viewportFill = .91f;
        [Range(0f, 1f)] public float idleMotion = 1f;
        static MonsterArtCatalog _runtime;
        public static MonsterArtCatalog Runtime => _runtime != null ? _runtime : _runtime = Resources.Load<MonsterArtCatalog>("MonsterArtCatalog");
        public int Revision { get; private set; }
        public void MarkChanged() => Revision++;
        void OnValidate() => MarkChanged();
        public BodyPlan Body(int id) => Array.Find(bodies, b => (int)b.id == id);
        public Palette Colorway(int id) => Array.Find(palettes, p => (int)p.id == id);
        public Sprite Sprite(MonsterAppearance appearance) => Body(appearance.Recipe).colorways[appearance.Palette];
        public bool Ready => string.IsNullOrEmpty(ValidationError());
        public string ValidationError()
        {
            if (schemaVersion != 3) return "Run TheCall/Monster/从参考图提取完整怪物 to migrate the sprite catalog.";
            if (bodies == null || bodies.Length != MonsterAppearance.RecipeCount || palettes == null || palettes.Length != MonsterAppearance.PaletteCount)
                return "Expected twelve whole drawings and six colorways.";
            var ids = new HashSet<int>();
            foreach (var body in bodies)
            {
                if (body == null || (int)body.id < 0 || (int)body.id >= MonsterAppearance.RecipeCount || !ids.Add((int)body.id) || body.source == null || body.frame.width <= 0 || body.frame.height <= 0)
                    return "Missing or duplicate whole drawing.";
                if (body.colorways == null || body.colorways.Length != MonsterAppearance.PaletteCount || Array.Exists(body.colorways, s => s == null || s.rect.size != body.source.rect.size))
                    return "Missing or misaligned gradient sprites: " + body.name;
            }
            ids.Clear();
            foreach (var p in palettes)
                if (p == null || (int)p.id < 0 || (int)p.id >= MonsterAppearance.PaletteCount || !ids.Add((int)p.id)) return "Invalid gradient identity.";
            return "";
        }
    }
}
