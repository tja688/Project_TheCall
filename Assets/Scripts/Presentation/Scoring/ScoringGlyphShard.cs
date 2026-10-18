using UnityEngine;

namespace TheCall.Scoring
{
    // Reuses the actual TMP glyph triangles and atlas, including CJK fallback materials.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ScoringGlyphShard : UnityEngine.UI.MaskableGraphic
    {
        UIVertex[] _vertices;
        Texture _atlas;
        public override Texture mainTexture => _atlas != null ? _atlas : Texture2D.whiteTexture;

        public void SetGlyph(UIVertex[] vertices, Material glyphMaterial)
        {
            _vertices = vertices;
            material = glyphMaterial;
            _atlas = glyphMaterial.mainTexture;
            raycastTarget = false;
            maskable = false;
            SetAllDirty();
        }

        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear();
            if (_vertices == null) return;
            foreach (var original in _vertices)
            {
                var vertex = original;
                vertex.color = (Color)original.color * color;
                vh.AddVert(vertex);
            }
            vh.AddTriangle(0, 1, 2);
        }
    }
}
