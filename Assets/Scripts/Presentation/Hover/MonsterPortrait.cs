using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>
    /// Shared runtime renderer for the componentized monster assembly.
    /// Rules supply stable part IDs; the catalog supplies sprites, anchors and layer data.
    /// 有骨架时待机、拖拽、计分反应都由 MonsterMotionController 在同一副骨架上叠加；
    /// 没有骨架时退回旧的分组装配，只为兼容保留。
    /// </summary>
    public sealed class MonsterPortrait : MonoBehaviour
    {
        [SerializeField] Image _tail;
        [SerializeField] Image _foot;
        [SerializeField] Image _body;
        [SerializeField] Image _hand;
        [SerializeField] Image _head;
        [SerializeField] Image _eye;
        [SerializeField] Image _mouth;
        [SerializeField] Image _hat;
        [SerializeField] Image _accessory;
        [SerializeField] Transform _headGroup;
        [SerializeField] Transform _feetGroup;
        [SerializeField] Transform _bodyGroup;
        [SerializeField] Transform _tailGroup;
        [SerializeField] MonsterMotionProfile _motionProfile;
        [SerializeField] MonsterAssemblyCatalog _assemblyCatalog;
        [SerializeField] bool _fitToViewport = true;
#if UNITY_EDITOR
        [SerializeField] bool _editorPreview = true;
        [SerializeField] int _editorPreviewSeed = 48291;
#endif

        public const float LegacyViewportWidth = 142f;
        public const float LegacyViewportHeight = 102f;

        static Vector2? _recommendedViewport;

        /// <summary>UI 卡面上 MonsterPortrait 矩形建议尺寸，按当前 Rig 静止姿势采样。</summary>
        public static Vector2 RecommendedViewportSize =>
            _recommendedViewport ??= MonsterRigRuntime.RecommendedPortraitViewport();

        bool _layersBound;
        bool _showed;
        bool _reacting;
        float _idlePhase;
        float _idleSeed;
        int _showKey;
        MonsterAppearance _appearance;
        MonsterRigSkeleton _skeleton;
        MonsterRigFrame _frame;
        MonsterMotionController _motion;
        Sprite[] _boneSprites;
        RectTransform _rigRoot;
        readonly List<Image> _rigImages = new List<Image>();

        public MonsterMotionProfile MotionProfile => _motionProfile != null
            ? _motionProfile
            : MonsterMotionProfile.Fallback;

        public MonsterAssemblyCatalog AssemblyCatalog => _assemblyCatalog != null
            ? _assemblyCatalog
            : MonsterAssemblyCatalog.Runtime;

        public static readonly Color[] Palette =
        {
            new Color32(238, 115, 101, 255),
            new Color32(100, 190, 132, 255),
            new Color32(100, 166, 218, 255),
            new Color32(236, 190, 85, 255),
            new Color32(185, 133, 210, 255),
            new Color32(88, 196, 191, 255),
        };

        /// <summary>是否已经装上了一只可以拖拽、会摆动的怪物。</summary>
        public bool HasCreature => _motion != null;

        /// <summary>抓取分量和反应都已回到静止。没有怪物时视为静止。</summary>
        public bool Settled => _motion == null || _motion.Settled;

#if UNITY_EDITOR
        void OnEnable()
        {
            if (Application.isPlaying || !_editorPreview)
                return;

            EditorApplication.delayCall += RefreshEditorPreview;
        }

        void OnDisable()
        {
            EditorApplication.delayCall -= RefreshEditorPreview;
        }

        void RefreshEditorPreview()
        {
            if (this == null || Application.isPlaying || !_editorPreview)
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
            MonsterPartLibrary.Ensure();
            var key = AppearanceSalt(appearance);
            // 同一只怪物每帧都会被刷新一次：外观和种子没变时保留它的运动状态，否则拖拽会被打断。
            if (_showed && key == _showKey && Mathf.Approximately(seed, _idleSeed))
                return;

            _showed = true;
            _showKey = key;
            _appearance = appearance;
            _idleSeed = seed;
            if (MonsterRigRuntime.TryBuildSkeleton(appearance.Recipe, key, out var skeleton))
            {
                AdoptSkeleton(skeleton);
                return;
            }

            DropSkeleton();
            HideRig();
            BindLayerParents();

            var catalog = AssemblyCatalog;
            var template = catalog.GetTemplateOrFallback(appearance.TemplateId, appearance.Recipe);
            var selectedHeadId = string.IsNullOrEmpty(appearance.HeadId)
                ? template == null ? null : template.head.partId
                : appearance.HeadId;
            var headDefinition = catalog.GetPartOrNull(selectedHeadId);

            ApplyPart(MonsterPartKind.Tail, _tail, _tailGroup, catalog, template, appearance, headDefinition);
            ApplyPart(MonsterPartKind.Foot, _foot, _feetGroup, catalog, template, appearance, headDefinition);
            ApplyPart(MonsterPartKind.Body, _body, _bodyGroup, catalog, template, appearance, headDefinition);
            ApplyPart(MonsterPartKind.Hand, _hand, _bodyGroup, catalog, template, appearance, headDefinition);
            ApplyPart(MonsterPartKind.Head, _head, _headGroup, catalog, template, appearance, headDefinition);
            ApplyPart(MonsterPartKind.Eye, _eye, _headGroup, catalog, template, appearance, headDefinition);
            ApplyPart(MonsterPartKind.Mouth, _mouth, _headGroup, catalog, template, appearance, headDefinition);
            ApplyPart(MonsterPartKind.Hat, _hat, _headGroup, catalog, template, appearance, headDefinition);
            ApplyPart(MonsterPartKind.Accessory, _accessory, _bodyGroup, catalog, template, appearance, headDefinition);
            ApplyGroupPivot(_headGroup, catalog.ResolveGroupPivot(template, MonsterPartKind.Head));
            ApplyGroupPivot(_feetGroup, catalog.ResolveGroupPivot(template, MonsterPartKind.Foot));
            ApplyGroupPivot(_bodyGroup, catalog.ResolveGroupPivot(template, MonsterPartKind.Body));
            ApplyGroupPivot(_tailGroup, catalog.ResolveGroupPivot(template, MonsterPartKind.Tail));
            RestoreMotion();
        }

        void AdoptSkeleton(MonsterRigSkeleton skeleton)
        {
            _skeleton = skeleton;
            _frame = new MonsterRigFrame(skeleton);
            _motion = new MonsterMotionController(skeleton, MotionProfile, _idleSeed);
            _boneSprites = new Sprite[skeleton.Bones.Count];
            for (var i = 0; i < _boneSprites.Length; i++)
                _boneSprites[i] = MonsterRigRuntime.SpriteFor(skeleton.Bones[i].PartId);

            _reacting = false;
            HideLegacy();
            _motion.Compose(_frame);
            ApplyRig();
            FitCreatureToViewport();
        }

        void DropSkeleton()
        {
            _skeleton = null;
            _frame = null;
            _motion = null;
            _boneSprites = null;
            _reacting = false;
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;

            if (_motion != null)
            {
                _motion.Advance(Time.unscaledDeltaTime);
                ApplyRig();
                return;
            }

            if (!MotionProfile.idleEnabled || _reacting)
                return;

            _idlePhase += Time.unscaledDeltaTime;
            SetIdleMotion(_idlePhase, _idleSeed);
        }

        /// <summary>旧装配路径的待机。有骨架时待机由控制器负责，这里不做任何事。</summary>
        public void SetIdleMotion(float time, float phase = 0f)
        {
            if (_motion != null)
                return;

            var profile = MotionProfile;
            var wave = Mathf.Sin((time + phase) * Mathf.PI * 2f * profile.idleCyclesPerSecond);
            var slowWave = Mathf.Sin((time + phase * 0.7f) * Mathf.PI * profile.idleCyclesPerSecond);
            SetMotion(
                wave * profile.idleHeadDegrees,
                wave * profile.idleFeetDegrees,
                slowWave * profile.idleTailDegrees,
                slowWave * profile.idleBodyDegrees,
                wave * profile.idleBodyLift);
        }

        /// <summary>计分时头和脚弹一下（度）。有骨架时由控制器平滑过去，松开后自动回落。</summary>
        public void SetSpringMotion(float headAngle, float feetAngle)
        {
            _reacting = true;
            if (_motion != null)
            {
                _motion.SetReaction(headAngle, feetAngle);
                return;
            }

            SetMotion(headAngle, feetAngle, 0f, 0f, 0f);
        }

        public void RestoreMotion()
        {
            _reacting = false;
            if (_motion != null)
            {
                _motion.SetReaction(0f, 0f);
                return;
            }

            SetMotion(0f, 0f, 0f, 0f, 0f);
        }

        /// <summary>抓住怪物。screenPoint 是按下时的屏幕坐标；这里只改变抓取状态，不做任何视觉切换。</summary>
        public void Grab(Vector2 screenPoint, Camera eventCamera)
        {
            if (_motion == null || !TryRigPoint(screenPoint, eventCamera, out var point))
                return;

            _motion.Grab(point.x, point.y);
        }

        public void Hold(Vector2 screenPoint, Camera eventCamera)
        {
            if (_motion == null || !TryRigPoint(screenPoint, eventCamera, out var point))
                return;

            _motion.Hold(point.x, point.y);
        }

        public void Release()
        {
            if (_motion != null)
                _motion.Release();
        }

        /// <summary>整只怪物的透明度（用于拖拽时把原位淡下去）。不挡射线。</summary>
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

        /// <summary>
        /// 拖拽时的怪物实体：与原位同样的位置、大小和外观，由自己的控制器驱动。
        /// 不复制卡片、文字或窗口，只有怪物本身。
        /// </summary>
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
            rect.sizeDelta = sourceRect.rect.size;
            var layerScale = Mathf.Max(0.0001f, Mathf.Abs(layer.lossyScale.x));
            var scale = source.transform.lossyScale.x / layerScale;
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.position = sourceRect.TransformPoint(sourceRect.rect.center);

            var ghost = go.AddComponent<MonsterPortrait>();
            ghost._motionProfile = source._motionProfile;
            ghost._assemblyCatalog = source._assemblyCatalog;
            if (source._showed)
                ghost.ShowAppearance(source._appearance, source._idleSeed);
            return ghost;
        }

        public void Clear()
        {
            _showed = false;
            DropSkeleton();
            HideRig();
            ClearImage(_tail);
            ClearImage(_foot);
            ClearImage(_body);
            ClearImage(_hand);
            ClearImage(_head);
            ClearImage(_eye);
            ClearImage(_mouth);
            ClearImage(_hat);
            ClearImage(_accessory);
            RestoreMotion();
            if (_rigRoot != null)
            {
                _rigRoot.localScale = Vector3.one;
                _rigRoot.anchoredPosition = Vector2.zero;
            }
        }

        bool TryRigPoint(Vector2 screenPoint, Camera eventCamera, out Vector2 point)
        {
            point = Vector2.zero;
            return _rigRoot != null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(_rigRoot, screenPoint, eventCamera, out point);
        }

        void BindLayerParents()
        {
            if (_layersBound)
                return;

            Reparent(_tail, _tailGroup);
            Reparent(_foot, _feetGroup);
            Reparent(_body, _bodyGroup);
            Reparent(_hand, _bodyGroup);
            Reparent(_head, _headGroup);
            Reparent(_eye, _headGroup);
            Reparent(_mouth, _headGroup);
            Reparent(_hat, _headGroup);
            Reparent(_accessory, _bodyGroup);
            _layersBound = true;
        }

        void ApplyPart(
            MonsterPartKind kind,
            Image image,
            Transform parent,
            MonsterAssemblyCatalog catalog,
            MonsterAssemblyTemplate template,
            MonsterAppearance appearance,
            MonsterPartDefinition headDefinition)
        {
            if (image == null)
                return;

            var appearancePartId = appearance.PartId(kind);
            var slot = catalog.ResolveSlot(template, kind, appearancePartId, appearance.HasExplicitParts);
            var definition = catalog.GetPartOrNull(slot.partId);
            var valid = slot.enabled
                        && definition != null
                        && definition.sprite != null
                        && catalog.IsCompatible(slot, definition);
            var rect = image.rectTransform;

            if (parent != null && image.transform.parent != parent)
                image.transform.SetParent(parent, false);

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = definition != null && definition.sprite != null
                ? definition.sprite.rect.size
                : MonsterAssemblyCatalog.CanvasSize;
            var animationPivot = ClampPivot(slot.pivot, definition);
            rect.pivot = animationPivot;
            rect.anchoredPosition = catalog.ResolveSpritePivotPosition(template, slot, definition, headDefinition);
            rect.localRotation = Quaternion.Euler(0f, 0f, slot.rotation);
            rect.localScale = new Vector3(
                Mathf.Approximately(slot.scale.x, 0f) ? 1f : slot.scale.x,
                Mathf.Approximately(slot.scale.y, 0f) ? 1f : slot.scale.y,
                1f);

            image.sprite = valid ? definition.sprite : null;
            image.color = valid ? Tint(definition.colorRole, appearance.Palette) : Color.white;
            image.enabled = valid;
            image.preserveAspect = false;
        }

        static Vector2 ClampPivot(Vector2 pivot, MonsterPartDefinition definition)
        {
            if (pivot == Vector2.zero && definition != null)
                pivot = definition.defaultPivot;

            return new Vector2(
                Mathf.Clamp01(pivot.x),
                Mathf.Clamp01(pivot.y));
        }

        static void ApplyGroupPivot(Transform group, Vector2 localPosition)
        {
            var rect = group as RectTransform;
            if (rect == null)
                return;

            rect.pivot = new Vector2(
                Mathf.Clamp01(0.5f + localPosition.x / MonsterAssemblyCatalog.CanvasSize.x),
                Mathf.Clamp01(0.5f + localPosition.y / MonsterAssemblyCatalog.CanvasSize.y));
        }

        static Color Tint(MonsterColorRole role, int palette)
        {
            if (role == MonsterColorRole.None || palette < 0)
                return Color.white;

            return Palette[Mathf.Clamp(palette, 0, Palette.Length - 1)];
        }

        void SetMotion(float headAngle, float feetAngle, float tailAngle, float bodyAngle, float bodyLift)
        {
            if (_headGroup != null)
                _headGroup.localRotation = Quaternion.Euler(0f, 0f, headAngle);
            if (_feetGroup != null)
                _feetGroup.localRotation = Quaternion.Euler(0f, 0f, feetAngle);
            if (_tailGroup != null)
                _tailGroup.localRotation = Quaternion.Euler(0f, 0f, tailAngle);
            if (_bodyGroup != null)
            {
                _bodyGroup.localRotation = Quaternion.Euler(0f, 0f, bodyAngle);
                _bodyGroup.localPosition = new Vector3(0f, bodyLift, 0f);
            }
        }

        static void Reparent(Image image, Transform parent)
        {
            if (image != null && parent != null && image.transform.parent != parent)
                image.transform.SetParent(parent, false);
        }

        static void ClearImage(Image image)
        {
            if (image == null)
                return;

            image.sprite = null;
            image.enabled = false;
            image.color = Color.white;
        }

        /// <summary>
        /// 把当前帧的骨架写到画面上。每根骨头一个 Image，挂在 rig 根下，位置、旋转、镜像都来自同一帧数据；
        /// 根骨头的安装点是原点，所以整只怪物跟着抓取位置平移。
        /// </summary>
        void ApplyRig()
        {
            if (_motion == null || _skeleton == null)
                return;

            _motion.Compose(_frame);
            EnsureRigRoot();
            var order = _skeleton.DrawOrder;
            while (_rigImages.Count < order.Length)
                _rigImages.Add(CreateRigImage());

            var bones = _skeleton.Bones;
            var originX = bones[0].MountX;
            var originY = bones[0].MountY;
            for (var slot = 0; slot < _rigImages.Count; slot++)
            {
                var image = _rigImages[slot];
                if (slot >= order.Length)
                {
                    image.enabled = false;
                    continue;
                }

                var index = order[slot];
                var bone = bones[index];
                var sprite = _boneSprites[index];
                var rect = image.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(
                    bone.Width <= 0 ? 0.5f : bone.MountX / bone.Width,
                    bone.Height <= 0 ? 0.5f : bone.MountY / bone.Height);
                rect.sizeDelta = sprite != null ? sprite.rect.size : new Vector2(bone.Width, bone.Height);
                rect.anchoredPosition = new Vector2(_frame.X[index] - originX, _frame.Y[index] - originY);
                rect.localRotation = Quaternion.Euler(0f, 0f, _frame.Rotation[index]);
                rect.localScale = new Vector3(_frame.Mirror[index] ? -1f : 1f, 1f, 1f);
                image.sprite = sprite;
                image.color = sprite == null ? Color.white : Tint(bone.Color, bone.PaletteIndex);
                image.enabled = sprite != null;
                image.preserveAspect = false;
                image.raycastTarget = false;
                image.transform.SetSiblingIndex(slot);
            }
        }

        void FitCreatureToViewport()
        {
            if (!_fitToViewport)
                return;

            if (_motion != null && _rigRoot != null)
                FitRigToViewport();
        }

        void FitRigToViewport()
        {
            var host = transform as RectTransform;
            if (host == null || _rigRoot == null)
                return;

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);
            var hasAny = false;
            for (var i = 0; i < _rigImages.Count; i++)
            {
                var image = _rigImages[i];
                if (image == null || !image.enabled)
                    continue;

                hasAny = true;
                var corners = new Vector3[4];
                image.rectTransform.GetWorldCorners(corners);
                for (var c = 0; c < corners.Length; c++)
                {
                    var local = host.InverseTransformPoint(corners[c]);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }

            if (!hasAny)
                return;

            ApplyViewportFit(host, min, max);
        }

        void ApplyViewportFit(RectTransform host, Vector3 min, Vector3 max)
        {
            var size = max - min;
            if (size.x < 1f || size.y < 1f)
                return;

            var viewport = host.rect.size;
            if (viewport.x < 1f || viewport.y < 1f)
                viewport = RecommendedViewportSize;

            const float padding = 6f;
            var scale = Mathf.Min((viewport.x - padding) / size.x, (viewport.y - padding) / size.y);
            var center = (min + max) * 0.5f;
            _rigRoot.localScale = Vector3.one * scale;
            _rigRoot.anchoredPosition = new Vector2(-center.x * scale, -center.y * scale);
        }

        void EnsureRigRoot()
        {
            if (_rigRoot != null)
                return;

            var go = new GameObject("Rig", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _rigRoot = go.GetComponent<RectTransform>();
            _rigRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _rigRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _rigRoot.pivot = new Vector2(0.5f, 0.5f);
            _rigRoot.anchoredPosition = Vector2.zero;
            _rigRoot.sizeDelta = Vector2.zero;
        }

        Image CreateRigImage()
        {
            var go = new GameObject("Part", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_rigRoot, false);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        void HideLegacy()
        {
            ClearImage(_tail);
            ClearImage(_foot);
            ClearImage(_body);
            ClearImage(_hand);
            ClearImage(_head);
            ClearImage(_eye);
            ClearImage(_mouth);
            ClearImage(_hat);
            ClearImage(_accessory);
        }

        void HideRig()
        {
            for (var i = 0; i < _rigImages.Count; i++)
            {
                if (_rigImages[i] != null)
                    _rigImages[i].enabled = false;
            }
        }

        static int AppearanceSalt(MonsterAppearance appearance)
        {
            unchecked
            {
                var salt = appearance.Palette * 131 + appearance.Recipe * 17 + 1;
                salt = Mix(salt, appearance.BodyId);
                salt = Mix(salt, appearance.HeadId);
                salt = Mix(salt, appearance.EyeId);
                salt = Mix(salt, appearance.MouthId);
                salt = Mix(salt, appearance.HandId);
                salt = Mix(salt, appearance.FootId);
                salt = Mix(salt, appearance.TailId);
                salt = Mix(salt, appearance.HatId);
                salt = Mix(salt, appearance.AccessoryId);
                return salt == 0 ? 1 : salt;
            }
        }

        static int Mix(int salt, string value)
        {
            unchecked
            {
                if (string.IsNullOrEmpty(value))
                    return salt * 31 + 1;

                for (var i = 0; i < value.Length; i++)
                    salt = salt * 31 + value[i];
                return salt;
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

    public static class MonsterPartLibrary
    {
        public static Sprite[] Body { get; private set; }
        public static Sprite[] Head { get; private set; }
        public static Sprite[] Eye { get; private set; }
        public static Sprite[] Mouth { get; private set; }
        public static Sprite[] Hand { get; private set; }
        public static Sprite[] Foot { get; private set; }
        public static Sprite[] Tail { get; private set; }
        public static Sprite[] Hat { get; private set; }
        public static Sprite[] Accessory { get; private set; }

        public static void Ensure()
        {
            if (Body != null)
                return;

            Body = Load("MonsterParts/Body");
            Head = Load("MonsterParts/Head");
            Eye = Load("MonsterParts/Eye");
            Mouth = Load("MonsterParts/Mouth");
            Hand = Load("MonsterParts/Hand");
            Foot = Load("MonsterParts/Foot");
            Tail = Load("MonsterParts/Tail");
            Hat = Load("MonsterParts/Hat");
            Accessory = Load("MonsterParts/Accessory");
        }

        static Sprite[] Load(string path)
        {
            var sprites = Resources.LoadAll<Sprite>(path);
            Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites;
        }
    }
}
