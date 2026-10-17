using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TheCall
{
    [ExecuteAlways, DefaultExecutionOrder(190)]
    public sealed class FacilityLamp : MonoBehaviour
    {
        public Light2D lightSource;
        public float intensity = 0.7f;
        public Color color = Color.white;
        public float phase;
        public bool flicker = true;
        public float Pulse { get; set; }
        readonly Vector3[] _corners = new Vector3[4];

        void LateUpdate()
        {
            if (lightSource == null || Camera.main == null) return;
            var rect = (RectTransform)transform;
            var camera = Camera.main;
            rect.GetWorldCorners(_corners);
            var center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var top = RectTransformUtility.WorldToScreenPoint(null, _corners[1]);
            var bottom = RectTransformUtility.WorldToScreenPoint(null, _corners[0]);
            var right = RectTransformUtility.WorldToScreenPoint(null, _corners[2]);
            lightSource.transform.position = camera.ScreenToWorldPoint(new Vector3(center.x, center.y, 10));
            var units = camera.orthographicSize * 2 / Mathf.Max(1, camera.pixelHeight);
            if (lightSource.lightType == Light2D.LightType.Sprite && lightSource.lightCookieSprite != null)
                lightSource.transform.localScale = new Vector3(Vector2.Distance(top, right) * units / lightSource.lightCookieSprite.bounds.size.x,
                    Vector2.Distance(top, bottom) * units / lightSource.lightCookieSprite.bounds.size.y, 1);
            var time = Application.isPlaying ? Time.unscaledTime + phase : phase + 3;
            var cycle = Mathf.Repeat(time, 17.3f);
            var amount = flicker && cycle < 0.17f ? (Mathf.Sin(cycle * 110) > 0 ? 0.6f : 0.82f) : 0.97f + Mathf.Sin(time * 1.5f) * 0.03f;
            Pulse = Mathf.MoveTowards(Pulse, 0, Time.unscaledDeltaTime * 1.8f);
            lightSource.intensity = intensity * amount + Pulse * 0.4f;
            lightSource.color = color;
            lightSource.enabled = isActiveAndEnabled;
        }

        void OnEnable() { if (lightSource != null) lightSource.enabled = true; }
        void OnDisable() { if (lightSource != null) lightSource.enabled = false; }
    }
}
