using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheCall
{
    /// <summary>Authorable art, attachment sites and safe proportions. Contains no gameplay state.</summary>
    [CreateAssetMenu(menuName = "TheCall/Monster art catalog")]
    public sealed class MonsterArtCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Part
        {
            public string id;
            public Sprite sprite;
            // Alpha bounds relative to sprite.rect, in pixels. Transparent PSB export padding is not a joint.
            public Rect ink;
        }

        [Serializable]
        public sealed class BodyPiece
        {
            public string part;
            public Vector2 position;
            public Vector2 size;
            public float angle;
            public bool mirror;
        }

        [Serializable]
        public sealed class BodyPlan
        {
            public string name;
            public BodyPiece[] pieces;
            public Vector2 face;
            public float faceWidth = 74;
            public Vector2 mouth;
            public float mouthWidth = 64;
            public Vector2 leftShoulder, rightShoulder;
            public Vector2 leftHip, rightHip;
            public Vector2 crown;
            public bool integratedHead;
            public bool twin;
            public Vector2 secondFace;
            public Rect oral;
        }

        [Serializable]
        public sealed class Palette
        {
            public string name;
            public Color body = Color.white;
            public Color limbs = Color.white;
            public Color accent = Color.white;
        }

        public Part[] parts = Array.Empty<Part>();
        public BodyPlan[] bodies = Array.Empty<BodyPlan>();
        public Palette[] palettes = Array.Empty<Palette>();
        [Range(.65f, 1f)] public float viewportFill = .91f;
        [Range(0f, 1f)] public float idleMotion = 1f;
        static MonsterArtCatalog _runtime;
        public static MonsterArtCatalog Runtime => _runtime != null ? _runtime :
            _runtime = Resources.Load<MonsterArtCatalog>("MonsterArtCatalog");
        public int Revision { get; private set; }
        public void MarkChanged() => Revision++;
        void OnValidate() => MarkChanged();

        public Part Find(string id)
        {
            for (var i = 0; i < parts.Length; i++)
                if (parts[i].id == id) return parts[i];
            return null;
        }

        public bool Ready => string.IsNullOrEmpty(ValidationError());

        // External art and Inspector data are checked here; composition can then trust its input.
        public string ValidationError()
        {
            if (parts == null || parts.Length < 70) return "Expected the 70 supplied parts.";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var part in parts)
            {
                if (part == null || part.sprite == null || string.IsNullOrEmpty(part.id) || !ids.Add(part.id)) return "Missing or duplicate part.";
                if (!(part.ink.width > 0) || !(part.ink.height > 0) || !Finite(part.ink.x) || !Finite(part.ink.y) ||
                    !Finite(part.ink.width) || !Finite(part.ink.height) || part.ink.xMin < 0 || part.ink.yMin < 0 ||
                    part.ink.xMax > part.sprite.rect.width || part.ink.yMax > part.sprite.rect.height) return "Invalid alpha bounds: " + part.id;
            }
            foreach (var id in RequiredParts)
                if (!ids.Contains(id)) return "Missing required part: " + id;
            if (bodies == null || bodies.Length != MonsterAppearance.RecipeCount || palettes == null || palettes.Length != MonsterAppearance.PaletteCount)
                return "Expected six body plans and palettes.";
            foreach (var body in bodies)
            {
                if (body == null || body.pieces == null || body.pieces.Length == 0 || !(body.faceWidth > 0) || !(body.mouthWidth > 0) ||
                    !Finite(body.faceWidth) || !Finite(body.mouthWidth) || !Finite(body.oral.x) || !Finite(body.oral.y) || !Finite(body.oral.width) || !Finite(body.oral.height) ||
                    !Valid(body.face) || !Valid(body.mouth) || !Valid(body.crown) || !Valid(body.leftShoulder) || !Valid(body.rightShoulder) ||
                    !Valid(body.leftHip) || !Valid(body.rightHip) || !Valid(body.secondFace)) return "Invalid body plan.";
                if (body.integratedHead && (!(body.oral.width > 0) || !(body.oral.height > 0))) return "Missing oral opening.";
                foreach (var piece in body.pieces)
                    if (piece == null || (piece.part != "head" && !ids.Contains(piece.part)) || !(piece.size.x > 0) || !(piece.size.y > 0) ||
                        !Valid(piece.size) || !Valid(piece.position) || !Finite(piece.angle)) return "Invalid body piece.";
            }
            foreach (var palette in palettes) if (palette == null) return "Missing palette.";
            if (!Finite(viewportFill) || viewportFill <= 0 || viewportFill > 1 || !Finite(idleMotion) || idleMotion < 0) return "Invalid viewport or motion scale.";
            return "";
        }
        static readonly string[] RequiredParts = "406 407 464 468 482 484 506 507 508 516 517 520 538 552 634 639 649 674 704 705 709 710 712 713 718 719 729 730 745 752 754 768 799 801 806 807 810 812 817 833 835 837 844 848 857 861 863 867 872 909 912 918 936 942 943 951 990 997".Split(' ');
        static bool Valid(Vector2 value) => Finite(value.x) && Finite(value.y);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
