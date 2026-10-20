using System;
using System.Collections.Generic;
using UnityEngine;
namespace TheCall
{
    [CreateAssetMenu(menuName = "TheCall/Monster art catalog")]
    public sealed class MonsterArtCatalog : ScriptableObject
    {
        public enum ColorRole { Body, RearBody, Pattern, Eye, Mouth, Tongue, Bone, Ink, Original }
        [Serializable] public sealed class Part
        {
            public string id; public Sprite sprite; public Sprite fillMask; public Rect ink;
        }
        [Serializable] public sealed class Layer
        {
            public string name, part;
            public Vector2 position, size;
            public float angle;
            public bool mirror, expression, stretch;
            public int group;
            public ColorRole color;
        }
        [Serializable] public sealed class MotionGroup
        {
            public Vector2 pivot;
            public float swing = .6f, phase;
        }
        [Serializable] public sealed class Accessory
        {
            public int id; public Layer[] layers = Array.Empty<Layer>();
        }
        [Serializable] public sealed class BodyPlan
        {
            public MonsterTemplate id; public string name;
            public Layer[] layers = Array.Empty<Layer>();
            public Accessory[] head = Array.Empty<Accessory>(), back = Array.Empty<Accessory>();
            public MotionGroup[] groups = Array.Empty<MotionGroup>();
            public Rect frame = new Rect(-132, 0, 264, 292);
        }
        [Serializable] public sealed class Palette
        {
            public MonsterPalette id; public string name;
            public Color body, pattern, eye, mouth;
            public Color ColorFor(ColorRole role)
            {
                switch (role)
                {
                    case ColorRole.Body: return body;
                    case ColorRole.RearBody: return new Color(body.r * .83f, body.g * .83f, body.b * .83f, 1);
                    case ColorRole.Pattern: return pattern;
                    case ColorRole.Eye: return eye;
                    case ColorRole.Mouth: return Color.Lerp(mouth, Color.black, .22f);
                    case ColorRole.Tongue: return Color.Lerp(mouth, Color.white, .18f);
                    case ColorRole.Bone: return new Color32(244, 239, 222, 255);
                    case ColorRole.Ink: return new Color32(24, 25, 27, 255);
                    default: return Color.white;
                }
            }
        }
        public int schemaVersion;
        public Part[] parts = Array.Empty<Part>();
        public BodyPlan[] bodies = Array.Empty<BodyPlan>();
        public Palette[] palettes = Array.Empty<Palette>();
        [Range(.65f, 1f)] public float viewportFill = .91f;
        [Range(0f, 1f)] public float idleMotion = 1f;
        static MonsterArtCatalog _runtime;
        public static MonsterArtCatalog Runtime => _runtime != null ? _runtime : _runtime = Resources.Load<MonsterArtCatalog>("MonsterArtCatalog");
        public int Revision { get; private set; }
        public void MarkChanged() => Revision++;
        void OnValidate() => MarkChanged();
        public Part Find(string id) => Array.Find(parts, p => p.id == id);
        public BodyPlan Body(int id) => Array.Find(bodies, b => (int)b.id == id);
        public Palette Colorway(int id) => Array.Find(palettes, p => (int)p.id == id);
        public bool Ready => string.IsNullOrEmpty(ValidationError());
        public string ValidationError()
        {
            if (schemaVersion != 2) return "Run TheCall/Monster/更新部件目录与图集 to migrate the template catalog.";
            if (parts == null || parts.Length != 70) return "Expected 70 original parts.";
            var ids = new HashSet<string>();
            foreach (var p in parts)
                if (p == null || p.sprite == null || !ids.Add(p.id) || !RectValid(p.ink) || p.ink.xMin < 0 || p.ink.yMin < 0 || p.ink.xMax > p.sprite.rect.width || p.ink.yMax > p.sprite.rect.height)
                    return "Invalid original art or alpha bounds.";
            foreach (var eye in new[] { "713","768","857","861","863","867","872","909","912" })
                if (Find(eye) == null || Find(eye).fillMask == null) return "Missing expression-eye art/mask: " + eye;
            if (bodies == null || bodies.Length != MonsterAppearance.RecipeCount || palettes == null || palettes.Length != MonsterAppearance.PaletteCount) return "Expected twelve templates and six colorways.";
            var templates = new HashSet<int>();
            foreach (var body in bodies)
            {
                if (body == null) return "Missing template definition.";
                var id = (int)body.id;
                if (id < 0 || id >= MonsterAppearance.RecipeCount || !templates.Add(id) || !RectValid(body.frame) || body.groups == null || body.groups.Length == 0) return "Invalid template identity, frame or motion groups.";
                foreach (var group in body.groups)
                    if (group == null || !Finite(group.pivot.x) || !Finite(group.pivot.y) || !Finite(group.swing) || !Finite(group.phase)) return "Invalid motion group.";
                if (!LayersValid(body, body.layers) || !Array.Exists(body.layers, l => l.expression)) return "Invalid layers or missing expression eye: " + body.name;
                if (body.head == null || body.back == null) return "Missing accessory definitions.";
                for (var h = 1; h < 4; h++) if (MonsterAppearance.Compatible(id, h, 0) && !AccessoryValid(body, body.head, h)) return "Missing head accessory: " + body.name;
                for (var b = 1; b < 3; b++) if (MonsterAppearance.Compatible(id, 0, b) && !AccessoryValid(body, body.back, b)) return "Missing back accessory: " + body.name;
            }
            var paletteIds = new HashSet<int>();
            foreach (var p in palettes)
                if (p == null || (int)p.id < 0 || (int)p.id >= MonsterAppearance.PaletteCount || !paletteIds.Add((int)p.id) || !Valid(p.body) || !Valid(p.pattern) || !Valid(p.eye) || !Valid(p.mouth)) return "Invalid or duplicate colorway identity.";
            if (!Finite(viewportFill) || viewportFill <= 0 || viewportFill > 1 || !Finite(idleMotion) || idleMotion < 0) return "Invalid portrait settings.";
            return "";
        }
        bool AccessoryValid(BodyPlan body, Accessory[] values, int id)
        { var value = Array.Find(values, a => a != null && a.id == id); return value != null && value.layers.Length > 0 && LayersValid(body, value.layers); }
        bool LayersValid(BodyPlan body, Layer[] layers)
        {
            if (layers == null || layers.Length == 0) return false;
            var names = new HashSet<string>();
            foreach (var l in layers)
                if (l == null || string.IsNullOrEmpty(l.name) || !names.Add(l.name) || Find(l.part) == null || !Finite(l.position.x) || !Finite(l.position.y) || !Finite(l.size.x) || !Finite(l.size.y) || l.size.x <= 0 || l.size.y <= 0 || !Finite(l.angle) || l.group < 0 || l.group >= body.groups.Length) return false;
            return true;
        }
        static bool Valid(Color c) => Finite(c.r) && Finite(c.g) && Finite(c.b) && c.r >= 0 && c.r <= 1 && c.g >= 0 && c.g <= 1 && c.b >= 0 && c.b <= 1 && c.a == 1;
        static bool RectValid(Rect r) => Finite(r.x) && Finite(r.y) && Finite(r.width) && Finite(r.height) && r.width > 0 && r.height > 0;
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
