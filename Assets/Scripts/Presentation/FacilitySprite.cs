using UnityEngine;

namespace TheCall
{
    // A RectTransform is the common authoring coordinate for art, its hit target and 2D lighting.
    // The artwork itself is a lit world SpriteRenderer, rendered before the overlay controls.
    [ExecuteAlways, DefaultExecutionOrder(180)]
    public sealed class FacilitySprite : MonoBehaviour
    {
        public Sprite sprite;
        public Material material;
        public Color tint = Color.white;
        public int order;
        public SpriteRenderer Renderer => _renderer;
        SpriteRenderer _renderer;
        readonly Vector3[] _corners = new Vector3[4];

        void LateUpdate()
        {
            var camera = Camera.main;
            if (sprite == null || material == null || camera == null) return;
            if (_renderer == null)
            {
                var node = new GameObject("FacilityArt_" + name) { hideFlags = HideFlags.HideAndDontSave };
                _renderer = node.AddComponent<SpriteRenderer>();
            }
            var rect = (RectTransform)transform;
            rect.GetWorldCorners(_corners);
            var canvas = GetComponentInParent<Canvas>();
            var source = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var bl = camera.ScreenToWorldPoint(new Vector3(RectTransformUtility.WorldToScreenPoint(source, _corners[0]).x,
                RectTransformUtility.WorldToScreenPoint(source, _corners[0]).y, 10));
            var tl = camera.ScreenToWorldPoint(new Vector3(RectTransformUtility.WorldToScreenPoint(source, _corners[1]).x,
                RectTransformUtility.WorldToScreenPoint(source, _corners[1]).y, 10));
            var tr = camera.ScreenToWorldPoint(new Vector3(RectTransformUtility.WorldToScreenPoint(source, _corners[2]).x,
                RectTransformUtility.WorldToScreenPoint(source, _corners[2]).y, 10));
            var right = tr - tl;
            var up = tl - bl;
            _renderer.transform.SetPositionAndRotation((bl + tr) * 0.5f, Quaternion.Euler(0, 0, Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg));
            _renderer.transform.localScale = new Vector3(right.magnitude / sprite.bounds.size.x, up.magnitude / sprite.bounds.size.y, 1);
            _renderer.sprite = sprite;
            _renderer.sharedMaterial = material;
            _renderer.sortingOrder = order;
            var color = tint;
            for (var current = transform; current != null; current = current.parent)
            {
                var group = current.GetComponent<CanvasGroup>();
                if (group == null) continue;
                color.a *= group.alpha;
                if (group.ignoreParentGroups) break;
            }
            _renderer.color = color;
            _renderer.enabled = isActiveAndEnabled && color.a > 0.001f;
        }

        void OnDisable() { if (_renderer != null) _renderer.enabled = false; }
        void OnDestroy()
        {
            if (_renderer == null) return;
            if (Application.isPlaying) Destroy(_renderer.gameObject); else DestroyImmediate(_renderer.gameObject);
        }
    }
}
