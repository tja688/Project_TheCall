using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>
    /// Shared runtime renderer for the componentized monster assembly.
    /// Rules supply stable part IDs; the catalog supplies sprites, anchors and layer data.
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

        bool _dragMotionActive;
        bool _layersBound;
        bool _useRig;
        float _idlePhase;
        float _idleSeed;
        float _restOriginX;
        float _restOriginY;
        int _paletteIndex;
        MonsterRigPose _restPose;
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

        public void Show(string monsterId)
        {
            var seed = Seed(monsterId);
            Show(MonsterAppearance.FromSeed(seed));
            _idleSeed = (seed & 1023) * 0.0137f;
        }

        public void Show(MonsterAppearance appearance)
        {
            MonsterPartLibrary.Ensure();
            _paletteIndex = appearance.Palette;
            _idleSeed = (appearance.Recipe * 97 + appearance.Palette * 31) * 0.0137f;
            if (MonsterRigRuntime.TryCompose(appearance.Recipe, AppearanceSalt(appearance), out var pose))
            {
                _useRig = true;
                _restPose = pose;
                _restOriginX = 0f;
                _restOriginY = 0f;
                for (var i = 0; i < pose.Nodes.Count; i++)
                {
                    if (!string.IsNullOrEmpty(pose.Nodes[i].SocketId))
                        continue;

                    _restOriginX = pose.Nodes[i].WorldAttachX;
                    _restOriginY = pose.Nodes[i].WorldAttachY;
                    break;
                }

                HideLegacy();
                RestoreMotion();
                return;
            }

            _useRig = false;
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

        void Update()
        {
            if (!Application.isPlaying || !MotionProfile.idleEnabled || _dragMotionActive)
                return;

            _idlePhase += Time.unscaledDeltaTime;
            SetIdleMotion(_idlePhase, _idleSeed);
        }

        public void SetIdleMotion(float time, float phase = 0f)
        {
            if (_useRig)
            {
                ApplyRig(MonsterMotionSample.Idle(MotionProfile, time, phase));
                return;
            }

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

        public void SetSpringMotion(float headAngle, float feetAngle)
        {
            _dragMotionActive = true;
            if (_useRig)
            {
                ApplyRig(MonsterMotionSample.Drag(MotionProfile, headAngle, feetAngle));
                return;
            }

            var profile = MotionProfile;
            var tail = Mathf.Clamp(
                feetAngle * profile.tailDragDegrees / Mathf.Max(1f, profile.feetDragDegrees),
                -profile.tailDragDegrees,
                profile.tailDragDegrees);
            var body = Mathf.Clamp(
                feetAngle * profile.bodyDragDegrees / Mathf.Max(1f, profile.feetDragDegrees),
                -profile.bodyDragDegrees,
                profile.bodyDragDegrees);
            SetMotion(headAngle, feetAngle, tail, body, 0f);
        }

        public void SetDragMotion(Vector2 velocity)
        {
            var profile = MotionProfile;
            var drive = Mathf.Clamp(velocity.x / 700f, -1f, 1f);
            SetSpringMotion(-drive * profile.headDragDegrees, drive * profile.feetDragDegrees);
        }

        public void RestoreMotion()
        {
            _dragMotionActive = false;
            if (_useRig)
            {
                ApplyRig(new MonsterMotionSample(0f, 0f, 0f, 0f, 0f));
                return;
            }

            SetMotion(0f, 0f, 0f, 0f, 0f);
        }

        public void Clear()
        {
            _useRig = false;
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
            if (role == MonsterColorRole.None)
                return Color.white;

            var primary = Palette[Mathf.Clamp(palette, 0, Palette.Length - 1)];
            if (role == MonsterColorRole.Primary)
                return primary;

            return Color.Lerp(primary, Color.white, 0.28f);
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

        void ApplyRig(MonsterMotionSample sample)
        {
            if (_restPose == null)
                return;

            var posed = MonsterRigMotion.Present(_restPose, sample);
            EnsureRigRoot();
            while (_rigImages.Count < posed.Nodes.Count)
                _rigImages.Add(CreateRigImage());

            for (var i = 0; i < _rigImages.Count; i++)
            {
                var image = _rigImages[i];
                if (i >= posed.Nodes.Count)
                {
                    image.enabled = false;
                    continue;
                }

                var node = posed.Nodes[i];
                var sprite = MonsterRigRuntime.SpriteFor(node.PartId);
                var rect = image.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(
                    node.Width <= 0 ? 0.5f : node.AttachX / node.Width,
                    node.Height <= 0 ? 0.5f : node.AttachY / node.Height);
                rect.sizeDelta = sprite != null ? sprite.rect.size : new Vector2(node.Width, node.Height);
                rect.anchoredPosition = new Vector2(node.WorldAttachX - _restOriginX, node.WorldAttachY - _restOriginY);
                rect.localRotation = Quaternion.Euler(0f, 0f, node.WorldRotation);
                rect.localScale = new Vector3(node.WorldMirror ? -1f : 1f, 1f, 1f);
                image.sprite = sprite;
                image.color = sprite == null ? Color.white : Tint(node.Color, _paletteIndex);
                image.enabled = sprite != null;
                image.preserveAspect = false;
                image.raycastTarget = false;
                image.transform.SetSiblingIndex(i);
            }
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
