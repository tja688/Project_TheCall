using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>
    /// 一只怪物的分层画像。各层是场景里已摆好的 Image，运行时只更换 Sprite，不改位置、缩放和层级。
    /// 部件画在同一张 142×102 画布上，所以各层应铺满同一个矩形。要微调拼接，直接拖这些 Image。
    /// </summary>
    public sealed class MonsterPortrait : MonoBehaviour
    {
        [Header("各层应铺满同一个矩形。改拼接时拖这些 Image，运行时不会改它们的位置。")]
        [SerializeField] Image _tail;
        [SerializeField] Image _foot;
        [SerializeField] Image _body;
        [SerializeField] Image _hand;
        [SerializeField] Image _head;
        [SerializeField] Image _eye;
        [SerializeField] Image _mouth;
        [SerializeField] Image _hat;
        [SerializeField] Image _accessory;

        public void Show(string monsterId)
        {
            MonsterPartLibrary.Ensure();
            var seed = Seed(monsterId);
            Apply(_tail, MonsterPartLibrary.Tail, seed);
            Apply(_foot, MonsterPartLibrary.Foot, seed + 1);
            Apply(_body, MonsterPartLibrary.Body, seed + 2);
            Apply(_hand, MonsterPartLibrary.Hand, seed + 3);
            Apply(_head, MonsterPartLibrary.Head, seed + 4);
            Apply(_eye, MonsterPartLibrary.Eye, seed + 5);
            Apply(_mouth, MonsterPartLibrary.Mouth, seed + 6);
            Apply(_hat, MonsterPartLibrary.Hat, seed + 7);
            Apply(_accessory, MonsterPartLibrary.Accessory, seed + 8);
        }

        public void Clear()
        {
            Apply(_tail, null, 0);
            Apply(_foot, null, 0);
            Apply(_body, null, 0);
            Apply(_hand, null, 0);
            Apply(_head, null, 0);
            Apply(_eye, null, 0);
            Apply(_mouth, null, 0);
            Apply(_hat, null, 0);
            Apply(_accessory, null, 0);
        }

        static void Apply(Image image, Sprite[] options, int seed)
        {
            if (image == null)
                return;

            var sprite = options == null ? null : Pick(options, seed);
            image.sprite = sprite;
            image.enabled = sprite != null;
        }

        static Sprite Pick(Sprite[] sprites, int seed)
        {
            if (sprites.Length == 0)
                return null;

            return sprites[(seed & int.MaxValue) % sprites.Length];
        }

        static int Seed(string id)
        {
            if (string.IsNullOrEmpty(id))
                return 1;

            var seed = 17;
            for (var i = 0; i < id.Length; i++)
                seed = seed * 31 + id[i];

            return seed & int.MaxValue;
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
