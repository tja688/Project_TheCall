using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>
    /// 拖拽虚影仍留在覆盖层上，抓取、回弹和落点都不改。
    /// 身体中心离开不透明的覆盖层界面后，改由世界精灵绘制，才能采样 2D 灯光。
    /// 还盖在卡片上时继续用原来的 UI，避免精灵被界面挡住后怪物消失。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class MonsterLightBody : MonoBehaviour
    {
        const int SortingOrder = 30;
        const float OpaqueOverlayAlpha = 0.35f;

        static Material _lit;

        readonly List<Image> _images = new List<Image>();
        readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        readonly List<Color> _tints = new List<Color>();
        readonly List<RaycastResult> _hits = new List<RaycastResult>();
        readonly Vector3[] _corners = new Vector3[4];
        PointerEventData _pointer;
        Transform _root;
        bool _yieldToOverlay;

        /// <summary>
        /// 拖拽虚影在盖住不透明覆盖层时退回 UI，避免卡片把世界精灵挡住后怪物消失。
        /// 已经放在世界画布上的怪物不退让。
        /// </summary>
        public void YieldToOverlay()
        {
            _yieldToOverlay = true;
        }

        void LateUpdate()
        {
            var material = SharedLit();
            var camera = Camera.main;
            if (material == null || camera == null)
                return;

            _images.Clear();
            GetComponentsInChildren(false, _images);
            if (!HasVisiblePart())
            {
                HideSprites();
                return;
            }

            if (_yieldToOverlay && OverlayCovers(PartScreenCenter()))
            {
                HideSprites();
                return;
            }

            EnsureRoot();
            var alpha = InheritedAlpha(transform);
            for (var i = 0; i < _images.Count; i++)
            {
                var renderer = RendererAt(i, material);
                var shown = TryMirror(_images[i], camera, alpha, renderer, i);
                renderer.enabled = shown;
                renderer.sortingOrder = SortingOrder + i;
                if (shown)
                    HideUi(_images[i]);
            }

            for (var i = _images.Count; i < _renderers.Count; i++)
                _renderers[i].enabled = false;
        }

        void OnDisable()
        {
            HideSprites();
        }

        void OnDestroy()
        {
            if (_root == null)
                return;

            if (Application.isPlaying)
                Destroy(_root.gameObject);
            else
                DestroyImmediate(_root.gameObject);
            _root = null;
        }

        bool HasVisiblePart()
        {
            for (var i = 0; i < _images.Count; i++)
            {
                var image = _images[i];
                if (image != null && image.enabled && image.sprite != null)
                    return true;
            }

            return false;
        }

        Vector2 PartScreenCenter()
        {
            var sum = Vector2.zero;
            var count = 0;
            for (var i = 0; i < _images.Count; i++)
            {
                var image = _images[i];
                if (image == null || !image.enabled || image.sprite == null)
                    continue;

                var rect = image.rectTransform;
                sum += RectTransformUtility.WorldToScreenPoint(CanvasMap.EventCamera(rect), rect.position);
                count++;
            }

            return count == 0 ? Vector2.zero : sum / count;
        }

        /// <summary>覆盖层上有一块不透明图挡住身体中心时，怪物应留在界面里。</summary>
        bool OverlayCovers(Vector2 screen)
        {
            var system = EventSystem.current;
            if (system == null)
                return false;

            if (_pointer == null)
                _pointer = new PointerEventData(system);

            _pointer.Reset();
            _pointer.position = screen;
            _hits.Clear();
            system.RaycastAll(_pointer, _hits);
            for (var i = 0; i < _hits.Count; i++)
            {
                var hit = _hits[i].gameObject;
                if (hit == null || hit.transform.IsChildOf(transform))
                    continue;

                var canvas = hit.GetComponentInParent<Canvas>();
                if (canvas == null || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;

                var graphic = hit.GetComponent<Graphic>();
                if (graphic == null || !graphic.enabled || graphic.color.a < OpaqueOverlayAlpha)
                    continue;

                return true;
            }

            return false;
        }

        bool TryMirror(Image image, Camera camera, float groupAlpha, SpriteRenderer renderer, int index)
        {
            if (image == null || !image.enabled || image.sprite == null)
                return false;

            var rect = image.rectTransform;
            rect.GetWorldCorners(_corners);
            var source = CanvasMap.EventCamera(rect);
            if (!ScreenPoint(source, _corners[0], out var bottomLeft))
                return false;
            if (!ScreenPoint(source, _corners[1], out var topLeft))
                return false;
            if (!ScreenPoint(source, _corners[2], out var topRight))
                return false;
            if (!MonsterLightPose.TryOnViewPlane(camera, bottomLeft, out var worldBottomLeft))
                return false;
            if (!MonsterLightPose.TryOnViewPlane(camera, topLeft, out var worldTopLeft))
                return false;
            if (!MonsterLightPose.TryOnViewPlane(camera, topRight, out var worldTopRight))
                return false;
            if (!MonsterLightPose.TryFit(
                    worldBottomLeft,
                    worldTopLeft,
                    worldTopRight,
                    image.sprite.bounds,
                    out var position,
                    out var rotation,
                    out var scale))
                return false;

            renderer.sprite = image.sprite;
            var color = Tint(image, index);
            color.a *= groupAlpha;
            renderer.color = color;
            renderer.transform.SetPositionAndRotation(position, rotation);
            renderer.transform.localScale = scale;
            return true;
        }

        static bool ScreenPoint(Camera source, Vector3 world, out Vector2 screen)
        {
            screen = RectTransformUtility.WorldToScreenPoint(source, world);
            return true;
        }

        Color Tint(Image image, int index)
        {
            while (_tints.Count <= index)
                _tints.Add(Color.white);

            var color = image.color;
            if (color.a > 0.001f)
                _tints[index] = color;
            else
                color = _tints[index];

            return color;
        }

        static void HideUi(Image image)
        {
            var hidden = image.color;
            hidden.a = 0f;
            image.color = hidden;
        }

        void HideSprites()
        {
            for (var i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] != null)
                    _renderers[i].enabled = false;
            }
        }

        SpriteRenderer RendererAt(int index, Material material)
        {
            while (_renderers.Count <= index)
                _renderers.Add(CreatePart(material));

            var renderer = _renderers[index];
            renderer.transform.SetParent(_root, false);
            renderer.sharedMaterial = material;
            return renderer;
        }

        void EnsureRoot()
        {
            if (_root != null)
                return;

            var go = new GameObject("MonsterLight");
            _root = go.transform;
        }

        static SpriteRenderer CreatePart(Material material)
        {
            var go = new GameObject("Part");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerID = 0;
            renderer.sortingOrder = SortingOrder;
            return renderer;
        }

        static float InheritedAlpha(Transform node)
        {
            var alpha = 1f;
            for (var current = node; current != null; current = current.parent)
            {
                var group = current.GetComponent<CanvasGroup>();
                if (group == null)
                    continue;

                alpha *= group.alpha;
                if (group.ignoreParentGroups)
                    break;
            }

            return alpha;
        }

        static Material SharedLit()
        {
            if (_lit != null)
                return _lit;

            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (shader == null)
                return null;

            _lit = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _lit;
        }
    }
}
