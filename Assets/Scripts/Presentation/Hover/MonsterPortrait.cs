using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>
    /// 槽位、结算和拖拽共用的怪物画像。
    /// 旧拼接骨架已删除。这里只画 ShadowMonsterCatalog 里的整张剪影。
    /// 新怪物系统替换 ShowAppearance。见 Assets/Notes/新怪物系统交接.md。
    /// </summary>
    [ExecuteAlways]
    public sealed class MonsterPortrait : MonoBehaviour
    {
        public const float ViewportWidth = 279f;
        public const float ViewportHeight = 299f;

        /// <summary>新建画像矩形的尺寸。与当前 MonsterPortrait 预制体一致。</summary>
        public static Vector2 RecommendedViewportSize => new Vector2(ViewportWidth, ViewportHeight);

        [SerializeField] Color _shadowTint = new Color32(20, 43, 51, 255);
#if UNITY_EDITOR
        [SerializeField] bool _editorPreview = true;
        [SerializeField] int _editorPreviewSeed = 48291;
#endif

        Image _shadow;
        Vector3 _shadowHome;
        Vector3 _shadowGrip;
        bool _shadowReturning;
        float _shadowReaction;
        bool _showed;
        float _idleSeed;
        int _showKey;
        MonsterAppearance _appearance;

        public bool HasCreature => Painted(_shadow);
        public bool UsesShadow => true;
        public Sprite ShadowSprite => _shadow != null ? _shadow.sprite : null;

        public void SetShadowTint(Color color)
        {
            _shadowTint = color;
            if (_shadow != null) _shadow.color = color;
        }

        public bool Settled => !_shadowReturning;

        /// <summary>旧拼接开关已删除。画像只有剪影。</summary>
        public void UseShadowArtwork()
        {
        }

#if UNITY_EDITOR
        void OnEnable()
        {
            ScheduleEditorPreview();
        }

        void OnDisable()
        {
            EditorApplication.delayCall -= RefreshEditorPreview;
        }

        void OnValidate()
        {
            ScheduleEditorPreview();
        }

        void ScheduleEditorPreview()
        {
            if (!CanRefreshEditorPreview())
                return;

            EditorApplication.delayCall -= RefreshEditorPreview;
            EditorApplication.delayCall += RefreshEditorPreview;
        }

        bool CanRefreshEditorPreview()
        {
            return !Application.isPlaying && _editorPreview && !EditorUtility.IsPersistent(this);
        }

        void RefreshEditorPreview()
        {
            if (this == null || !CanRefreshEditorPreview())
                return;

            Show(MonsterAppearance.FromSeed(_editorPreviewSeed));
        }
#endif

        public void Show(string monsterId)
        {
            var seed = Seed(monsterId);
            ShowAppearance(MonsterAppearance.FromSeed(seed), (seed & 1023) * 0.0137f);
        }

        public void Show(MonsterAppearance appearance)
        {
            ShowAppearance(appearance, (appearance.Recipe * 97 + appearance.Palette * 31) * 0.0137f);
        }

        void ShowAppearance(MonsterAppearance appearance, float seed)
        {
            ShowShadow(appearance, seed);
        }

        void Update()
        {
            if (_shadow == null || !_showed) return;
            var dt = Application.isPlaying ? Time.unscaledDeltaTime : 0;
            _shadowReaction = Mathf.MoveTowards(_shadowReaction, 0, dt * 24);
            var breath = Application.isPlaying ? Mathf.Sin(Time.unscaledTime * 1.7f + _idleSeed) * 0.012f : 0;
            _shadow.rectTransform.localScale = new Vector3(1 - breath * 0.35f, 1 + breath, 1);
            _shadow.rectTransform.localEulerAngles = new Vector3(0, 0, _shadowReaction);
            if (_shadowReturning)
            {
                transform.position = Vector3.Lerp(transform.position, _shadowHome, 1 - Mathf.Exp(-dt * 16));
                if (Vector3.Distance(transform.position, _shadowHome) < 0.5f)
                {
                    transform.position = _shadowHome;
                    _shadowReturning = false;
                }
            }
        }

        /// <summary>结算反应。旧骨架弹簧已删除，头角只换成剪影的小幅倾斜。</summary>
        public void SetSpringMotion(float headAngle, float feetAngle)
        {
            _ = feetAngle;
            _shadowReaction = Mathf.Clamp(headAngle * 0.25f, -7, 7);
        }

        public void RestoreMotion()
        {
            _shadowReaction = 0;
        }

        public void Grab(Vector2 screenPoint, Camera eventCamera)
        {
            _shadowHome = transform.position;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform.parent, screenPoint, eventCamera, out var world))
                _shadowGrip = transform.position - world;
            _shadowReturning = false;
        }

        public void Hold(Vector2 screenPoint, Camera eventCamera)
        {
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform.parent, screenPoint, eventCamera, out var world))
                transform.position = world + _shadowGrip;
        }

        public void Release(Vector3? returnPoint = null)
        {
            if (returnPoint.HasValue) _shadowHome = returnPoint.Value;
            _shadowReturning = true;
        }

        public void MatchScreenUniform(float screenUniform)
        {
            var rect = transform as RectTransform;
            var current = CanvasMap.ScreenUniform(rect);
            if (current < 1e-4f || screenUniform < 1e-4f)
                return;

            var ratio = screenUniform / current;
            if (Mathf.Abs(ratio - 1f) < 1e-4f)
                return;

            transform.localScale *= ratio;
        }

        public void SetVisibility(float alpha)
        {
            var group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                if (alpha >= 1f)
                    return;

                group = gameObject.AddComponent<CanvasGroup>();
            }

            var visible = alpha >= 1f;
            group.alpha = Mathf.Clamp01(alpha);
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        public static MonsterPortrait SpawnGhost(RectTransform layer, MonsterPortrait source)
        {
            var go = new GameObject("MonsterGhost", typeof(RectTransform), typeof(CanvasGroup));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(layer, false);
            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var sourceRect = source.transform as RectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var viewport = sourceRect.rect.size;
            if (viewport.x < 1f || viewport.y < 1f)
                viewport = sourceRect.sizeDelta;
            rect.sizeDelta = viewport;
            var layerScale = layer.lossyScale;
            var sourceScale = source.transform.lossyScale;
            rect.localScale = new Vector3(
                sourceScale.x / Mathf.Max(0.0001f, Mathf.Abs(layerScale.x)),
                sourceScale.y / Mathf.Max(0.0001f, Mathf.Abs(layerScale.y)),
                1f);
            rect.position = sourceRect.TransformPoint(sourceRect.rect.center);

            var ghost = go.AddComponent<MonsterPortrait>();
            ghost._shadowTint = source._shadowTint;
            if (source._showed)
                ghost.ShowAppearance(source._appearance, source._idleSeed);

            return ghost;
        }

        public void Clear()
        {
            _showed = false;
            _shadowReturning = false;
            _shadowReaction = 0;
            if (_shadow != null) _shadow.enabled = false;
        }

        void ShowShadow(MonsterAppearance appearance, float seed)
        {
            var key = AppearanceSalt(appearance);
            var sprite = ShadowMonsterCatalog.Runtime?.Pick(unchecked(appearance.Recipe * 19349663 + appearance.Palette * 83492791));
            if (_showed && key == _showKey && Painted(_shadow) && _shadow.sprite == sprite) return;
            _showed = true;
            _showKey = key;
            _appearance = appearance;
            _idleSeed = seed;
            if (_shadow == null)
            {
                var existing = transform.Find("ShadowCreature");
                if (existing != null) _shadow = existing.GetComponent<Image>();
                if (_shadow == null)
                {
                    var node = new GameObject("ShadowCreature", typeof(RectTransform), typeof(Image));
                    if (!Application.isPlaying) node.hideFlags = HideFlags.HideAndDontSave;
                    node.transform.SetParent(transform, false);
                    _shadow = node.GetComponent<Image>();
                }
            }

            if (!Application.isPlaying) _shadow.gameObject.hideFlags = HideFlags.HideAndDontSave;
            var rect = _shadow.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.05f);
            _shadow.sprite = sprite;
            _shadow.color = _shadowTint;
            _shadow.preserveAspect = true;
            _shadow.raycastTarget = false;
            _shadow.enabled = sprite != null;
            _shadow.gameObject.SetActive(true);
        }

        static bool Painted(Image image) =>
            image != null && image.enabled && image.sprite != null && image.gameObject.activeSelf;

        static int AppearanceSalt(MonsterAppearance appearance)
        {
            unchecked
            {
                var salt = appearance.Palette * 131 + appearance.Recipe * 17 + 1;
                return salt == 0 ? 1 : salt;
            }
        }

        static int Seed(string id)
        {
            if (string.IsNullOrEmpty(id))
                return 1;

            var seed = 17;
            for (var i = 0; i < id.Length; i++)
                seed = seed * 31 + id[i];
            return seed;
        }
    }
}
