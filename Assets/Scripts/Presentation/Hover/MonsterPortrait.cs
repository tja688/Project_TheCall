using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;
namespace TheCall
{
    /// <summary>One complete sprite from a birth snapshot. Shared by slots, drag ghosts and settlement.</summary>
    [ExecuteAlways]
    public sealed class MonsterPortrait : MonoBehaviour
    {
        public const float ViewportWidth = 279f, ViewportHeight = 299f;
        public static Vector2 RecommendedViewportSize => new Vector2(ViewportWidth, ViewportHeight);
#if UNITY_EDITOR
        [SerializeField] bool _editorPreview = true;
        [SerializeField] int _editorPreviewSeed = 48291;
        [SerializeField] int _previewTemplate = -1, _previewPalette;
#endif
        RectTransform _creature;
        Image _image;
        MonsterVisualTraits _traits;
        MonsterArtCatalog _catalog;
        bool _showed, _returning, _held;
        Vector3 _home, _grip;
        Vector2 _lastPointer;
        float _headReaction, _feetReaction, _heldLean, _idleSeed, _fitScale;
        int _catalogRevision;
        public bool HasCreature => _showed && _image != null && _image.enabled && _image.sprite != null && _creature.gameObject.activeSelf;
        public bool UsesWholeSprite => true;
        public bool Settled => !_returning;
        public int PartCount => HasCreature ? 1 : 0;
        public MonsterVisualTraits Traits => _traits;
        public string VisualSignature => _showed ? _traits.Signature : "";
        public string ArtworkSignature => HasCreature ? _image.sprite.name : "";
        public Rect InkBounds => _catalog == null ? default : _catalog.Body(_traits.Appearance.Recipe).frame;
        public float MotionSample { get; private set; }
        void OnEnable()
        {
#if UNITY_EDITOR
            ScheduleEditorPreview();
#endif
        }
        void OnDisable()
        {
            RestoreMotion();
#if UNITY_EDITOR
            EditorApplication.delayCall -= RefreshEditorPreview;
#endif
        }
#if UNITY_EDITOR
        void OnValidate() => ScheduleEditorPreview();
        void ScheduleEditorPreview()
        {
            if (Application.isPlaying || !_editorPreview || EditorUtility.IsPersistent(this)) return;
            EditorApplication.delayCall -= RefreshEditorPreview;
            EditorApplication.delayCall += RefreshEditorPreview;
        }
        void RefreshEditorPreview()
        {
            if (this == null || Application.isPlaying || !_editorPreview || EditorUtility.IsPersistent(this)) return;
            var appearance = _previewTemplate < 0 ? MonsterAppearance.FromSeed(_editorPreviewSeed) : new MonsterAppearance(_previewTemplate, _previewPalette);
            if (MonsterArtCatalog.Runtime != null && MonsterArtCatalog.Runtime.Ready) Show(appearance);
        }
#endif
        public void Show(string monsterId) => Show(MonsterAppearance.FromSeed(Seed(monsterId)));
        public void Show(MonsterAppearance appearance) => Show(MonsterVisualTraits.From(appearance));
        public void Show(MonsterView monster) => Show(MonsterVisualTraits.From(monster));
        public void Show(MonsterVisualTraits traits)
        {
            var catalog = MonsterArtCatalog.Runtime;
            if (catalog == null || !catalog.Ready)
            { Clear(); Debug.LogError("Whole monster sprites missing. Run TheCall/Monster/从参考图提取完整怪物.", this); return; }
            if (HasCreature && _traits.Signature == traits.Signature && _catalog == catalog && _catalogRevision == catalog.Revision) { _traits = traits; FitViewport(); return; }
            EnsureCreature();
            _catalog = catalog; _traits = traits; _catalogRevision = catalog.Revision;
            // Motion is a whole-image response and never depends on skills, mood or personality.
            _idleSeed = traits.Appearance.Recipe * .71f + traits.Appearance.Palette * 1.37f;
            _image.sprite = catalog.Sprite(traits.Appearance); _image.color = Color.white;
            _image.enabled = true; _image.raycastTarget = false; _image.maskable = true;
            _image.preserveAspect = true;
            _showed = true; _creature.gameObject.SetActive(true); FitViewport();
            PreviewPose(Application.isPlaying ? Time.unscaledTime : 0);
        }
        void EnsureCreature()
        {
            if (_creature != null) return;
            foreach (var name in new[] { "ShadowCreature", "AssembledCreature", "WholeCreature" })
            {
                var old = transform.Find(name);
                if (old == null) continue;
                old.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
            }
            var node = new GameObject("WholeCreature", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            node.transform.SetParent(transform, false);
            if (!Application.isPlaying) node.hideFlags = HideFlags.HideAndDontSave;
            _creature = (RectTransform)node.transform;
            _creature.anchorMin = _creature.anchorMax = new Vector2(.5f, 0);
            _creature.pivot = new Vector2(.5f, .5f);
            _image = node.GetComponent<Image>();
        }
        void FitViewport()
        {
            if (_creature == null || _catalog == null) return;
            var size = ((RectTransform)transform).rect.size;
            var frame = _catalog.Body(_traits.Appearance.Recipe).frame;
            _fitScale = Mathf.Min(size.x / frame.width, size.y / frame.height) * _catalog.viewportFill;
            _creature.sizeDelta = frame.size;
            _creature.anchoredPosition = new Vector2(0, size.y * .5f);
            _creature.localScale = Vector3.one * _fitScale;
        }
        void OnRectTransformDimensionsChange() => FitViewport();
        void Update()
        {
            if (!_showed || _creature == null) return;
            if (_catalog != null && _catalogRevision != _catalog.Revision) Show(_traits);
            if (!Application.isPlaying) return;
            var dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            _headReaction = Mathf.MoveTowards(_headReaction, 0, dt * 32);
            _feetReaction = Mathf.MoveTowards(_feetReaction, 0, dt * 40);
            _heldLean = Mathf.Lerp(_heldLean, 0, 1 - Mathf.Exp(-dt * 9));
            PreviewPose(Time.unscaledTime);
            if (_returning)
            {
                transform.position = Vector3.Lerp(transform.position, _home, 1 - Mathf.Exp(-dt * 16));
                if (Vector3.Distance(transform.position, _home) < .5f) { transform.position = _home; _returning = false; }
            }
        }
        public void PreviewPose(float seconds)
        {
            if (_catalog == null || _creature == null || !_showed) return;
            var breath = Mathf.Sin(seconds * 1.7f + _idleSeed) * .008f * _catalog.idleMotion;
            var angle = _headReaction * .13f + _feetReaction * .06f + _heldLean + (_held ? 1.2f : 0);
            _creature.localEulerAngles = new Vector3(0, 0, angle);
            _creature.localScale = new Vector3(_fitScale * (1 - breath * .25f), _fitScale * (1 + breath), _fitScale);
            MotionSample = breath + angle;
        }
        public void SetSpringMotion(float headAngle, float feetAngle)
        { _headReaction = Mathf.Clamp(headAngle, -32, 32); _feetReaction = Mathf.Clamp(feetAngle, -38, 38); }
        public void RestoreMotion()
        {
            _headReaction = _feetReaction = _heldLean = 0; _held = false;
            if (_creature != null) _creature.localRotation = Quaternion.identity;
            if (_showed) PreviewPose(Application.isPlaying ? Time.unscaledTime : 0);
        }
        public void Grab(Vector2 screenPoint, Camera eventCamera)
        {
            _home = transform.position; _lastPointer = screenPoint; _held = true;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform.parent, screenPoint, eventCamera, out var world)) _grip = transform.position - world;
            _returning = false;
        }
        public void Hold(Vector2 screenPoint, Camera eventCamera)
        {
            _heldLean = Mathf.Clamp((screenPoint.x - _lastPointer.x) * -.12f, -8, 8); _lastPointer = screenPoint;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform.parent, screenPoint, eventCamera, out var world)) transform.position = world + _grip;
        }
        public void Release(Vector3? returnPoint = null)
        { if (returnPoint.HasValue) _home = returnPoint.Value; _returning = true; _held = false; }
        public void MatchScreenUniform(float screenUniform)
        {
            var current = CanvasMap.ScreenUniform((RectTransform)transform);
            if (current >= 1e-4f && screenUniform >= 1e-4f && Mathf.Abs(screenUniform / current - 1) >= 1e-4f) transform.localScale *= screenUniform / current;
        }
        public void SetVisibility(float alpha)
        {
            var group = GetComponent<CanvasGroup>();
            if (group == null) { if (alpha >= 1) return; group = gameObject.AddComponent<CanvasGroup>(); }
            group.alpha = Mathf.Clamp01(alpha); group.blocksRaycasts = group.interactable = alpha >= 1;
        }
        public static MonsterPortrait SpawnGhost(RectTransform layer, MonsterPortrait source)
        {
            var go = new GameObject("MonsterGhost", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform; rect.SetParent(layer, false);
            var group = go.GetComponent<CanvasGroup>(); group.blocksRaycasts = group.interactable = false;
            var sourceRect = (RectTransform)source.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            var size = sourceRect.rect.size; rect.sizeDelta = size.x >= 1 && size.y >= 1 ? size : sourceRect.sizeDelta;
            var ls = layer.lossyScale; var ss = source.transform.lossyScale;
            rect.localScale = new Vector3(ss.x / Mathf.Max(.0001f, Mathf.Abs(ls.x)), ss.y / Mathf.Max(.0001f, Mathf.Abs(ls.y)), 1);
            rect.position = sourceRect.TransformPoint(sourceRect.rect.center);
            var ghost = go.AddComponent<MonsterPortrait>(); if (source._showed) ghost.Show(source._traits); return ghost;
        }
        public void Clear()
        {
            _showed = _returning = _held = false; _headReaction = _feetReaction = _heldLean = 0;
            if (_creature != null) _creature.gameObject.SetActive(false);
        }
        static int Seed(string id)
        { unchecked { var seed = 17; if (id != null) for (var i = 0; i < id.Length; i++) seed = seed * 31 + id[i]; return seed; } }
    }
}
