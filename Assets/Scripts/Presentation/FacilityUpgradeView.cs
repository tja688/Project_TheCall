using UnityEngine;

namespace TheCall
{
    public sealed class FacilityUpgradeView : MonoBehaviour
    {
        public enum AmbientMotion { None, Geometry, Television, Candy, Plant }
        [Tooltip("Exact content name. Leave blank until the design defines the unlock.")]
        public string toolName;
        public string techName;
        public GameObject artwork;
        public bool previewUnlocked;
        public AmbientMotion motion;
        Vector2 _rest;
        bool _captured;
        public void Render(RunLedger run)
        {
            var unlocked = previewUnlocked || (!string.IsNullOrEmpty(toolName) && PresentationReadout.Contains(run.Tools, toolName))
                || (!string.IsNullOrEmpty(techName) && PresentationReadout.Contains(run.UnlockedTech, techName));
            if (artwork != null) artwork.SetActive(unlocked);
        }

        void Update()
        {
            if (artwork == null || !artwork.activeSelf || motion == AmbientMotion.None) return;
            var rect = (RectTransform)artwork.transform;
            if (!_captured) { _rest = rect.anchoredPosition; _captured = true; }
            var t = Time.unscaledTime;
            var wave = Mathf.Sin(t * (motion == AmbientMotion.Geometry ? 0.6f : 1.4f));
            rect.anchoredPosition = _rest + Vector2.up * (wave * (motion == AmbientMotion.Geometry ? 5 : 1.5f));
            rect.localEulerAngles = new Vector3(0, 0, wave * (motion == AmbientMotion.Geometry ? 2 : motion == AmbientMotion.Plant ? 0.8f : 0));
            if (motion == AmbientMotion.Television)
            {
                var sprite = artwork.GetComponent<FacilitySprite>();
                var color = sprite.tint; color.a = 0.9f + Mathf.Sin(t * 13) * 0.07f; sprite.tint = color;
            }
        }
    }
}
