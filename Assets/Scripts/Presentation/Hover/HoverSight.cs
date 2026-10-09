using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>结算挡板要接住点击，但不该挡住底下怪物的悬停。</summary>
    public sealed class HoverPassthrough : MonoBehaviour
    {
    }

    /// <summary>换位飞行时肖像离开槽位，悬停认肖像上的这只怪物。</summary>
    public sealed class HoverBody : MonoBehaviour
    {
        public string monsterId;
    }

    public static class HoverRide
    {
        const string HitName = "HoverHit";

        public static void Attach(Component host, string monsterId)
        {
            if (host == null || string.IsNullOrEmpty(monsterId))
                return;

            var body = host.GetComponent<HoverBody>();
            if (body == null)
                body = host.gameObject.AddComponent<HoverBody>();
            body.monsterId = monsterId;

            var existing = host.transform.Find(HitName);
            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                return;
            }

            var go = new GameObject(HitName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(host.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.sprite = Pixel();
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
        }

        public static void Detach(Component host)
        {
            if (host == null)
                return;

            var body = host.GetComponent<HoverBody>();
            if (body != null)
                Object.Destroy(body);

            var hit = host.transform.Find(HitName);
            if (hit != null)
                Object.Destroy(hit.gameObject);
        }

        static Sprite _pixel;
        static Texture2D _pixelTexture;

        static Sprite Pixel()
        {
            if (_pixel != null)
                return _pixel;

            _pixelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _pixelTexture.SetPixel(0, 0, Color.white);
            _pixelTexture.Apply();
            _pixel = Sprite.Create(_pixelTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return _pixel;
        }
    }
}
