using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall.Hover.Tests
{
    public sealed class MonsterPortraitLayoutTests
    {
        [Test]
        public void 包围盒中心在原点时拟合位移为零()
        {
            var fitted = MonsterPortraitLayout.TryFit(
                new Vector2(-50f, -40f),
                new Vector2(50f, 40f),
                new Vector2(279f, 299f),
                out var scale,
                out var position);

            Assert.That(fitted, Is.True);
            Assert.That(scale, Is.EqualTo(Mathf.Min(273f / 100f, 293f / 80f)).Within(0.001f));
            Assert.That(position.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(position.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void 拟合后怪物中心落在视口中心()
        {
            var min = new Vector2(20f, -10f);
            var max = new Vector2(120f, 70f);
            Assert.That(MonsterPortraitLayout.TryFit(min, max, new Vector2(200f, 180f), out var scale, out var position), Is.True);

            var center = (min + max) * 0.5f;
            var placed = position + center * scale;
            Assert.That(placed.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(placed.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void 卡片轴心在左上角时仍然量到骨架中心()
        {
            var host = new GameObject("Portrait", typeof(RectTransform));
            var hostRect = host.GetComponent<RectTransform>();
            hostRect.pivot = new Vector2(0f, 1f);
            hostRect.sizeDelta = new Vector2(279f, 299f);

            var rig = new GameObject("Rig", typeof(RectTransform));
            rig.transform.SetParent(host.transform, false);
            var rigRect = rig.GetComponent<RectTransform>();
            rigRect.anchorMin = new Vector2(0.5f, 0.5f);
            rigRect.anchorMax = new Vector2(0.5f, 0.5f);
            rigRect.pivot = new Vector2(0.5f, 0.5f);
            rigRect.anchoredPosition = Vector2.zero;
            rigRect.localScale = Vector3.one;

            var partGo = new GameObject("Part", typeof(RectTransform), typeof(Image));
            partGo.transform.SetParent(rig.transform, false);
            var part = partGo.GetComponent<RectTransform>();
            part.anchorMin = new Vector2(0.5f, 0.5f);
            part.anchorMax = new Vector2(0.5f, 0.5f);
            part.pivot = new Vector2(0.5f, 0.5f);
            part.sizeDelta = new Vector2(100f, 80f);
            part.anchoredPosition = Vector2.zero;

            try
            {
                Assert.That(MonsterPortraitLayout.TryMeasure(rigRect, out var min, out var max), Is.True);
                var center = (min + max) * 0.5f;
                Assert.That(center.x, Is.EqualTo(0f).Within(0.5f));
                Assert.That(center.y, Is.EqualTo(0f).Within(0.5f));
                Assert.That(max.x - min.x, Is.EqualTo(100f).Within(0.5f));
                Assert.That(max.y - min.y, Is.EqualTo(80f).Within(0.5f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
