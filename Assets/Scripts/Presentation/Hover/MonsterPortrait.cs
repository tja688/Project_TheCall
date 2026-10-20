using System;
using System.Collections.Generic;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>Renders a supplied value snapshot. Never reads the live rules while settlement is playing.</summary>
    [ExecuteAlways]
    public sealed class MonsterPortrait : MonoBehaviour
    {
        public const float ViewportWidth = 279f, ViewportHeight = 299f;
        public static Vector2 RecommendedViewportSize => new Vector2(ViewportWidth, ViewportHeight);
#if UNITY_EDITOR
        [SerializeField] bool _editorPreview = true;
        [SerializeField] int _editorPreviewSeed = 48291;
        [SerializeField] string _previewPersonality = "稳定", _previewMood = "普通";
        [SerializeField] string[] _previewSkills = Array.Empty<string>();
#endif
        sealed class PaintedPart
        {
            public RectTransform Node;
            public Image Image;
            public MonsterComposition.Piece Piece;
        }
        readonly List<MonsterComposition.Piece> _composition = new List<MonsterComposition.Piece>(36);
        readonly List<PaintedPart> _parts = new List<PaintedPart>(36);
        RectTransform _creature;
        MonsterVisualTraits _traits;
        MonsterArtCatalog _catalog;
        bool _showed, _returning, _held;
        Vector3 _home, _grip;
        Vector2 _lastPointer;
        float _headReaction, _feetReaction, _heldLean, _idleSeed;
        Rect _inkBounds;
        string _assemblySignature;
        int _catalogRevision;
        float _floor;

        public bool HasCreature
        {
            get
            {
                if (!_showed || _creature == null || !_creature.gameObject.activeSelf || _composition.Count == 0) return false;
                for (var i = 0; i < _composition.Count; i++)
                    if (_parts[i].Image == null || !_parts[i].Image.enabled || _parts[i].Image.sprite == null) return false;
                return true;
            }
        }
        public bool UsesAssembly => true;
        public bool Settled => !_returning;
        public int PartCount => _showed ? _composition.Count : 0;
        public MonsterVisualTraits Traits => _traits;
        public string VisualSignature => _showed ? _traits.Signature : "";
        public string AssemblySignature => _showed ? _assemblySignature : "";
        public Rect InkBounds => _inkBounds;
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
            Show(MonsterVisualTraits.Create(MonsterAppearance.FromSeed(_editorPreviewSeed), _previewPersonality, _previewMood, _previewSkills));
        }
#endif
        public void Show(string monsterId) => Show(MonsterAppearance.FromSeed(Seed(monsterId)));
        public void Show(MonsterAppearance appearance) => Show(MonsterVisualTraits.From(appearance));
        public void Show(MonsterView monster) => Show(MonsterVisualTraits.From(monster));
        public void Show(MonsterVisualTraits traits)
        {
            var catalog = MonsterArtCatalog.Runtime;
            if (catalog == null || !catalog.Ready)
            { Clear(); Debug.LogError("MonsterArtCatalog is missing or incomplete. Run TheCall/Monster/更新部件目录与图集.", this); return; }
            if (HasCreature && _traits.Signature == traits.Signature && _catalog == catalog && _catalogRevision == catalog.Revision) { FitViewport(); return; }
            EnsureCreature();
            _catalog = catalog; _traits = traits; _catalogRevision = catalog.Revision;
            _idleSeed = traits.Appearance.Variation * 1.37f + traits.Appearance.Recipe * .71f;
            MonsterComposition.Compose(catalog, traits, _composition);
            var signature = new StringBuilder();
            _floor = float.PositiveInfinity;
            for (var i = 0; i < _composition.Count; i++)
            {
                var piece = _composition[i]; var painted = Part(i);
                painted.Piece = piece;
                painted.Node.name = piece.Name + "_" + piece.Art.id;
                painted.Node.sizeDelta = piece.Size; painted.Node.pivot = piece.Pivot;
                painted.Node.anchoredPosition = piece.Position;
                painted.Node.localEulerAngles = new Vector3(0, 0, piece.Angle);
                painted.Node.localScale = Vector3.one;
                var image = painted.Image;
                image.sprite = piece.Art.sprite; image.color = piece.Color;
                image.enabled = true; image.raycastTarget = false; image.maskable = true; image.preserveAspect = false;
                var rect = image.rectTransform; var ink = piece.Art.ink; var spriteSize = piece.Art.sprite.rect.size;
                var factor = new Vector2(piece.Size.x / ink.width, piece.Size.y / ink.height);
                rect.anchorMin = rect.anchorMax = piece.Pivot;
                rect.sizeDelta = Vector2.Scale(spriteSize, factor);
                // Joints follow the painted silhouette, compensating the original export's transparent padding.
                var padding = Vector2.Scale(spriteSize * .5f - ink.center, factor);
                if (piece.Mirror) padding.x = -padding.x;
                rect.anchoredPosition = padding + Vector2.Scale(Vector2.one * .5f - piece.Pivot, piece.Size);
                rect.localScale = new Vector3(piece.Mirror ? -1 : 1, 1, 1);
                painted.Node.gameObject.SetActive(true);
                var bounds = Bounds(piece);
                if (piece.Role == MonsterComposition.Motion.LeftFoot || piece.Role == MonsterComposition.Motion.RightFoot)
                    _floor = Mathf.Min(_floor,bounds.yMin);
                _inkBounds = i == 0 ? bounds : Rect.MinMaxRect(Mathf.Min(_inkBounds.xMin,bounds.xMin),Mathf.Min(_inkBounds.yMin,bounds.yMin),
                    Mathf.Max(_inkBounds.xMax,bounds.xMax),Mathf.Max(_inkBounds.yMax,bounds.yMax));
                signature.Append(piece.Name).Append(':').Append(piece.Art.id).Append(':').Append((Color32)piece.Color)
                    .Append(':').Append(piece.Position.ToString("F3",System.Globalization.CultureInfo.InvariantCulture))
                    .Append(':').Append(piece.Size.ToString("F3",System.Globalization.CultureInfo.InvariantCulture))
                    .Append(':').Append(piece.Pivot.ToString("F3",System.Globalization.CultureInfo.InvariantCulture))
                    .Append(':').Append(piece.Angle.ToString("F3",System.Globalization.CultureInfo.InvariantCulture))
                    .Append(':').Append(piece.Mirror ? '1' : '0').Append(';');
            }
            for (var i = _composition.Count; i < _parts.Count; i++) _parts[i].Node.gameObject.SetActive(false);
            if (float.IsPositiveInfinity(_floor)) _floor = 0;
            _assemblySignature = signature.ToString(); _showed = true;
            _creature.gameObject.SetActive(true); FitViewport();
            PreviewPose(Application.isPlaying ? Time.unscaledTime : 0);
        }

        void EnsureCreature()
        {
            if (_creature != null) return;
            foreach (var name in new[] { "ShadowCreature", "AssembledCreature" })
            { var old = transform.Find(name); if (old != null) Retire(old.gameObject); }
            _parts.Clear();
            var node = new GameObject("AssembledCreature", typeof(RectTransform));
            node.transform.SetParent(transform, false);
            if (!Application.isPlaying) node.hideFlags = HideFlags.HideAndDontSave;
            _creature = (RectTransform)node.transform;
            _creature.anchorMin = _creature.anchorMax = new Vector2(.5f, 0);
            _creature.pivot = new Vector2(.5f, 0); _creature.sizeDelta = new Vector2(264, 292);
        }
        static void Retire(GameObject target)
        { target.SetActive(false); if (Application.isPlaying) Destroy(target); else DestroyImmediate(target); }
        PaintedPart Part(int index)
        {
            if (index < _parts.Count && _parts[index].Node != null) return _parts[index];
            var host = new GameObject("Part", typeof(RectTransform)); host.transform.SetParent(_creature, false);
            var node = (RectTransform)host.transform; node.anchorMin = node.anchorMax = new Vector2(.5f, 0);
            var art = new GameObject("Artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            art.transform.SetParent(node, false);
            var image = art.GetComponent<Image>(); image.rectTransform.pivot = Vector2.one * .5f;
            var part = new PaintedPart { Node = node, Image = image };
            if (index < _parts.Count) _parts[index] = part; else _parts.Add(part);
            return part;
        }
        void FitViewport()
        {
            if (_creature == null || _catalog == null) return;
            var size = ((RectTransform)transform).rect.size;
            var scale = Mathf.Min(size.x / 264f, size.y / 292f) * _catalog.viewportFill;
            _creature.localScale = Vector3.one * scale;
            _creature.anchoredPosition = new Vector2(0, Mathf.Max(3, size.y * .025f) - _floor * scale);
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
            var vigor = (.38f + _traits.MoodTier * .19f) * _catalog.idleMotion;
            var t = seconds * (.95f + _traits.MoodTier * .22f) + _idleSeed;
            var wave = Mathf.Sin(t * 1.7f); var breath = wave * .012f * vigor;
            var phase = Mathf.Repeat(seconds + _idleSeed, 4.8f);
            var blink = phase < .17f ? 1 - Mathf.Sin(phase / .17f * Mathf.PI) * .9f : 1;
            var droop = (2 - _traits.MoodTier) * .65f;
            _creature.localEulerAngles = new Vector3(0, 0, _headReaction * .16f + _heldLean);
            MotionSample = 0;
            for (var i = 0; i < _composition.Count; i++)
            {
                var part = _parts[i]; var piece = part.Piece;
                var at = new Vector2(piece.Position.x * (1 - breath * .35f),piece.Position.y * (1 + breath));
                var angle = piece.Angle; var sx = 1f; var sy = 1f; var sway = wave * vigor;
                switch (piece.Role)
                {
                    case MonsterComposition.Motion.Head: angle += _headReaction * .13f + sway * .65f; at.y -= droop; break;
                    case MonsterComposition.Motion.Eye:
                        sx = 1 - breath * .35f; sy = blink * (1 - _traits.MoodTier * .006f);
                        at.x += Mathf.Sin(t * .57f) * .8f; at.y -= droop; angle += _headReaction * .1f; break;
                    case MonsterComposition.Motion.Mouth: at.y -= droop; sy = 1 + breath * 2; break;
                    case MonsterComposition.Motion.LeftArm: angle += sway * 3 + _feetReaction * .19f + (_held ? -9 : 0); break;
                    case MonsterComposition.Motion.RightArm: angle -= sway * 2.7f + _feetReaction * .16f + (_held ? -9 : 0); break;
                    case MonsterComposition.Motion.LeftFoot: angle += _feetReaction * .23f + sway * .8f; break;
                    case MonsterComposition.Motion.RightFoot: angle -= _feetReaction * .19f + sway * .8f; break;
                    case MonsterComposition.Motion.Ornament: angle += Mathf.Sin(t * 1.7f - .45f) * vigor * 1.8f; break;
                    default: sx = 1 - breath * .35f; sy = 1 + breath; break;
                }
                part.Node.anchoredPosition = at; part.Node.localEulerAngles = new Vector3(0,0,angle);
                part.Node.localScale = new Vector3(sx,sy,1);
                MotionSample += at.x * .013f + at.y * .017f + angle * .023f + sy * .031f;
            }
        }
        public void SetSpringMotion(float headAngle, float feetAngle)
        { _headReaction = Mathf.Clamp(headAngle,-32,32); _feetReaction = Mathf.Clamp(feetAngle,-38,38); }
        public void RestoreMotion()
        {
            _headReaction = _feetReaction = _heldLean = 0; _held = false;
            if (_creature != null) _creature.localRotation = Quaternion.identity;
            if (_showed) PreviewPose(Application.isPlaying ? Time.unscaledTime : 0);
        }
        public void Grab(Vector2 screenPoint, Camera eventCamera)
        {
            _home = transform.position; _lastPointer = screenPoint; _held = true;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform.parent,screenPoint,eventCamera,out var world)) _grip = transform.position - world;
            _returning = false;
        }
        public void Hold(Vector2 screenPoint, Camera eventCamera)
        {
            _heldLean = Mathf.Clamp((screenPoint.x - _lastPointer.x) * -.12f,-8,8); _lastPointer = screenPoint;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform.parent,screenPoint,eventCamera,out var world)) transform.position = world + _grip;
        }
        public void Release(Vector3? returnPoint = null)
        { if (returnPoint.HasValue) _home = returnPoint.Value; _returning = true; _held = false; }
        public void MatchScreenUniform(float screenUniform)
        {
            var current = CanvasMap.ScreenUniform((RectTransform)transform);
            if (current >= 1e-4f && screenUniform >= 1e-4f && Mathf.Abs(screenUniform/current - 1) >= 1e-4f) transform.localScale *= screenUniform/current;
        }
        public void SetVisibility(float alpha)
        {
            var group = GetComponent<CanvasGroup>();
            if (group == null) { if (alpha >= 1) return; group = gameObject.AddComponent<CanvasGroup>(); }
            group.alpha = Mathf.Clamp01(alpha); group.blocksRaycasts = group.interactable = alpha >= 1;
        }
        public static MonsterPortrait SpawnGhost(RectTransform layer, MonsterPortrait source)
        {
            var go = new GameObject("MonsterGhost",typeof(RectTransform),typeof(CanvasGroup));
            var rect = (RectTransform)go.transform; rect.SetParent(layer,false);
            var group = go.GetComponent<CanvasGroup>(); group.blocksRaycasts = group.interactable = false;
            var sourceRect = (RectTransform)source.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
            var size = sourceRect.rect.size; rect.sizeDelta = size.x >= 1 && size.y >= 1 ? size : sourceRect.sizeDelta;
            var ls = layer.lossyScale; var ss = source.transform.lossyScale;
            rect.localScale = new Vector3(ss.x/Mathf.Max(.0001f,Mathf.Abs(ls.x)),ss.y/Mathf.Max(.0001f,Mathf.Abs(ls.y)),1);
            rect.position = sourceRect.TransformPoint(sourceRect.rect.center);
            var ghost = go.AddComponent<MonsterPortrait>(); if (source._showed) ghost.Show(source._traits); return ghost;
        }
        public void Clear()
        {
            _showed = _returning = _held = false; _headReaction = _feetReaction = _heldLean = 0;
            if (_creature != null) _creature.gameObject.SetActive(false);
        }
        static Rect Bounds(MonsterComposition.Piece piece)
        {
            var min = new Vector2(float.PositiveInfinity,float.PositiveInfinity); var max = new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            var rotation = Quaternion.Euler(0,0,piece.Angle);
            for (var x = 0; x <= 1; x++) for (var y = 0; y <= 1; y++)
            {
                var at = piece.Position + (Vector2)(rotation * (Vector3)Vector2.Scale(new Vector2(x,y) - piece.Pivot,piece.Size));
                min = Vector2.Min(min,at); max = Vector2.Max(max,at);
            }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        static int Seed(string id)
        { unchecked { var seed = 17; if (id != null) for (var i = 0; i < id.Length; i++) seed = seed * 31 + id[i]; return seed; } }
    }
}
