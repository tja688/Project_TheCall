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

        public static readonly Color[] Palette =
        {
            new Color32(238, 115, 101, 255),
            new Color32(100, 190, 132, 255),
            new Color32(100, 166, 218, 255),
            new Color32(236, 190, 85, 255),
            new Color32(185, 133, 210, 255),
            new Color32(88, 196, 191, 255),
        };

        public void Show(string monsterId) => Show(MonsterAppearance.FromSeed(Seed(monsterId)));

        public void Show(MonsterAppearance appearance)
        {
            MonsterPartLibrary.Ensure();
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

        public void SetSpringMotion(float headAngle, float feetAngle)
        {
            if (_headGroup != null)
                _headGroup.localRotation = Quaternion.Euler(0f, 0f, headAngle);
            if (_feetGroup != null)
                _feetGroup.localRotation = Quaternion.Euler(0f, 0f, feetAngle);
        }

        public void SetDragMotion(Vector2 velocity)
        {
            var drive = Mathf.Clamp(velocity.x / 700f, -1f, 1f);
            SetSpringMotion(-drive * 7f, drive * 5f);
        }

        public void RestoreMotion()
        {
            if (_headGroup != null)
                _headGroup.localRotation = Quaternion.identity;
            if (_feetGroup != null)
                _feetGroup.localRotation = Quaternion.identity;
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
