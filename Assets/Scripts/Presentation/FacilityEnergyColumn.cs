using UnityEngine;

namespace TheCall
{
    // A seat holds energy until the scoring head leaves it. The show owns its progress.
    public sealed class FacilityEnergyColumn : MonoBehaviour
    {
        public RectTransform fluid;
        public FacilitySprite fluidArt;
        public FacilityLamp lamp;
        public UnityEngine.UI.Image seat;
        public float Fill { get; private set; }
        public float Glow { get; private set; }
        public Vector3 Surface => fluid.TransformPoint(new Vector3(fluid.rect.center.x, fluid.rect.yMax, 0));

        public void Show(float fill, float glow)
        {
            Fill = Mathf.Clamp01(fill);
            Glow = Mathf.Clamp01(glow);
            if (fluid != null) fluid.localScale = new Vector3(1, Mathf.Max(0.015f, Fill), 1);
            if (fluidArt != null) fluidArt.tint = Color.Lerp(new Color(0.32f, 0.55f, 0.58f, 0.3f), new Color(0.72f, 1.35f, 1.24f, 1), Glow);
            if (lamp != null) lamp.Energy = Glow;
            if (seat != null) seat.color = Color.Lerp(new Color32(67, 104, 116, 255), new Color32(186, 255, 233, 255), Glow);
        }

        void OnDisable() => Show(0, 0);
    }
}
