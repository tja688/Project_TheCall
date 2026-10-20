using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheCall
{
    /// <summary>仅在转场期间按结构裁切已有底图。收尾换回原图，不生成或修改源美术文件。</summary>
    [DisallowMultipleComponent]
    public sealed class AssemblySurface : MonoBehaviour
    {
        [Serializable]
        public struct Region
        {
            [Tooltip("1920×1080 设计坐标，原点在左上。换底图结构时调整这些矩形，必须铺满整图。")]
            public Rect area;
            public Vector2 offset;
            public float delay;
        }

        public Region[] regions = Array.Empty<Region>();
        public float seconds = 0.56f;
        readonly List<ScreenAssembly.Part> _parts = new List<ScreenAssembly.Part>();
        readonly List<Sprite> _sprites = new List<Sprite>();
        Behaviour _source;
        bool _sourceEnabled;
        bool _open;
        Sprite _builtFrom;

        public IReadOnlyList<ScreenAssembly.Part> Open()
        {
            Close();
            var world = GetComponent<FacilitySprite>();
            var ui = GetComponent<UnityEngine.UI.Image>();
            var sprite = world != null ? world.sprite : ui != null ? ui.sprite : null;
            if (sprite == null || regions.Length == 0) return _parts;
            if (_builtFrom != sprite) Build(sprite, world, ui);
            _source = world != null ? (Behaviour)world : ui;
            _sourceEnabled = _source.enabled;
            _source.enabled = false;
            _open = true;
            foreach (var part in _parts) part.target.gameObject.SetActive(true);
            return _parts;
        }

        void Build(Sprite sprite, FacilitySprite world, UnityEngine.UI.Image ui)
        {
            Clear();
            _builtFrom = sprite;
            var rect = (RectTransform)transform;
            var sourceRect = sprite.rect;
            foreach (var region in regions)
            {
                var a = region.area;
                var crop = new Rect(sourceRect.x + a.x / 1920 * sourceRect.width,
                    sourceRect.y + (1080 - a.yMax) / 1080 * sourceRect.height,
                    a.width / 1920 * sourceRect.width, a.height / 1080 * sourceRect.height);
                var tile = Sprite.Create(sprite.texture, crop, Vector2.one * 0.5f,
                    sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                tile.name = sprite.name + "_Assembly" + _parts.Count;
                _sprites.Add(tile);
                var node = new GameObject("AssemblyPiece_" + _parts.Count, typeof(RectTransform), typeof(CanvasGroup));
                node.hideFlags = HideFlags.DontSave;
                var target = (RectTransform)node.transform;
                target.SetParent(rect, false);
                target.anchorMin = target.anchorMax = new Vector2(0, 1);
                target.pivot = new Vector2(0, 1);
                target.sizeDelta = new Vector2(a.width / 1920 * rect.rect.width, a.height / 1080 * rect.rect.height);
                target.anchoredPosition = new Vector2(a.x / 1920 * rect.rect.width, -a.y / 1080 * rect.rect.height);
                if (world != null)
                {
                    var art = node.AddComponent<FacilitySprite>();
                    art.sprite = tile; art.material = world.material; art.tint = world.tint; art.order = world.order;
                }
                else
                {
                    var art = node.AddComponent<UnityEngine.UI.Image>();
                    art.sprite = tile; art.color = ui.color; art.material = ui.material; art.raycastTarget = false;
                }
                _parts.Add(new ScreenAssembly.Part { target = target, offset = region.offset, delay = region.delay, seconds = seconds });
            }
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            foreach (var part in _parts) if (part.target != null) part.target.gameObject.SetActive(false);
            if (_source != null) _source.enabled = _sourceEnabled;
        }

        void Clear()
        {
            foreach (var part in _parts) if (part.target != null) Release(part.target.gameObject);
            foreach (var sprite in _sprites) if (sprite != null) Release(sprite);
            _parts.Clear(); _sprites.Clear();
        }

        static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        void OnDisable() => Close();
        void OnDestroy() => Clear();
    }
}
