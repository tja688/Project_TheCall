using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>
    /// Renders an authored compatible recipe. Every part shares the original 142x102 canvas.
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

        bool _dragMotionActive;
        float _idlePhase;
        float _idleSeed;

        public MonsterMotionProfile MotionProfile => _motionProfile != null
            ? _motionProfile
            : MonsterMotionProfile.Fallback;

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
            _idleSeed = (appearance.Recipe * 97 + appearance.Palette * 31) * 0.0137f;
            var recipe = appearance.Recipe;
            var palette = Palette[appearance.Palette];

            Apply(_tail, MonsterPartLibrary.Tail, recipe == 2 ? 2 : -1, Color.white);
            Apply(_foot, MonsterPartLibrary.Foot, recipe % 3, palette);
            Apply(_body, MonsterPartLibrary.Body, recipe, palette);
            Apply(_hand, MonsterPartLibrary.Hand, recipe % 5, Color.white);
            Apply(_head, MonsterPartLibrary.Head, recipe % 4, palette);
            Apply(_eye, MonsterPartLibrary.Eye, recipe % 5, Color.white);
            Apply(_mouth, MonsterPartLibrary.Mouth, recipe, Color.white);
            Apply(_hat, MonsterPartLibrary.Hat, recipe == 0 || recipe == 3 ? recipe : -1, Color.white);
            Apply(_accessory, MonsterPartLibrary.Accessory, recipe == 1 || recipe == 4 ? recipe % 3 : -1, Color.white);
            RestoreMotion();
        }

        void Update()
        {
            if (!Application.isPlaying || !_motionProfileOrFallback().idleEnabled || _dragMotionActive)
                return;

            _idlePhase += Time.unscaledDeltaTime;
            SetIdleMotion(_idlePhase, _idleSeed);
        }

        public void SetIdleMotion(float time, float phase = 0f)
        {
            var profile = _motionProfileOrFallback();
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
            var profile = _motionProfileOrFallback();
            var tail = Mathf.Clamp(feetAngle * profile.tailDragDegrees / Mathf.Max(1f, profile.feetDragDegrees), -profile.tailDragDegrees, profile.tailDragDegrees);
            var body = Mathf.Clamp(feetAngle * profile.bodyDragDegrees / Mathf.Max(1f, profile.feetDragDegrees), -profile.bodyDragDegrees, profile.bodyDragDegrees);
            SetMotion(headAngle, feetAngle, tail, body, 0f);
        }

        public void SetDragMotion(Vector2 velocity)
        {
            var profile = _motionProfileOrFallback();
            var drive = Mathf.Clamp(velocity.x / 700f, -1f, 1f);
            SetSpringMotion(-drive * profile.headDragDegrees, drive * profile.feetDragDegrees);
        }

        public void RestoreMotion()
        {
            _dragMotionActive = false;
            SetMotion(0f, 0f, 0f, 0f, 0f);
        }

        public void Clear()
        {
            Apply(_tail, null, -1, Color.white);
            Apply(_foot, null, -1, Color.white);
            Apply(_body, null, -1, Color.white);
            Apply(_hand, null, -1, Color.white);
            Apply(_head, null, -1, Color.white);
            Apply(_eye, null, -1, Color.white);
            Apply(_mouth, null, -1, Color.white);
            Apply(_hat, null, -1, Color.white);
            Apply(_accessory, null, -1, Color.white);
            RestoreMotion();
        }

        MonsterMotionProfile _motionProfileOrFallback() =>
            _motionProfile != null ? _motionProfile : MonsterMotionProfile.Fallback;

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

        static void Apply(Image image, Sprite[] options, int index, Color tint)
        {
            if (image == null)
                return;

            var valid = options != null && index >= 0 && index < options.Length;
            image.sprite = valid ? options[index] : null;
            image.color = tint;
            image.enabled = valid;
            image.preserveAspect = true;
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
            System.Array.Sort(sprites, (a, b) => string.CompareOrdinal(a.name, b.name));
            return sprites;
        }
    }
}
