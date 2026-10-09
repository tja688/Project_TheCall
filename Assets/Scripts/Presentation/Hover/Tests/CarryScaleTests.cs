using NUnit.Framework;
using UnityEngine;

namespace TheCall.Hover.Tests
{
    public sealed class CarryScaleTests
    {
        [Test]
        public void 指针在卡片内时距离为零卡片外按边计算()
        {
            var min = new Vector2(100f, 200f);
            var max = new Vector2(300f, 400f);

            Assert.That(CarryScale.DistanceToRect(new Vector2(180f, 260f), min, max), Is.EqualTo(0f).Within(0.001f));
            Assert.That(CarryScale.DistanceToRect(new Vector2(70f, 260f), min, max), Is.EqualTo(30f).Within(0.001f));
            Assert.That(CarryScale.DistanceToRect(new Vector2(340f, 160f), min, max), Is.EqualTo(Mathf.Sqrt(40f * 40f + 40f * 40f)).Within(0.001f));
        }

        [Test]
        public void 靠近生产区时放大到生产区离开时保持怪物栏大小()
        {
            Assert.That(CarryScale.Approach(400f, 400f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(CarryScale.Approach(0f, 400f), Is.EqualTo(1f).Within(0.001f));
            Assert.That(CarryScale.Approach(200f, 400f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(CarryScale.Approach(0f, 0f), Is.EqualTo(1f).Within(0.001f));
            Assert.That(CarryScale.Approach(40f, 0f), Is.EqualTo(0f).Within(0.001f));

            Assert.That(CarryScale.Blend(0.25f, 0.5f, 0f), Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(CarryScale.Blend(0.25f, 0.5f, 1f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(CarryScale.Blend(0.5f, 0.5f, 1f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(CarryScale.Blend(0.82f, 0.5f, 1f), Is.EqualTo(0.82f).Within(0.001f));
        }

        [Test]
        public void 松手后在固定时间内回到怪物栏尺寸()
        {
            Assert.That(CarryScale.Return(0.5f, 0.25f, 0f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(CarryScale.Return(0.5f, 0.25f, CarryScale.ReturnSeconds), Is.EqualTo(0.25f).Within(0.001f));
            var mid = CarryScale.Return(0.5f, 0.25f, CarryScale.ReturnSeconds * 0.5f);
            Assert.That(mid, Is.GreaterThan(0.25f));
            Assert.That(mid, Is.LessThan(0.5f));
        }

        [Test]
        public void 关掉的肖像仍按父级缩放算出画面大小()
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform));
            var card = new GameObject("Card", typeof(RectTransform));
            var portrait = new GameObject("Portrait", typeof(RectTransform));
            card.transform.SetParent(canvas.transform, false);
            portrait.transform.SetParent(card.transform, false);
            canvas.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            card.transform.localScale = Vector3.one;
            portrait.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            portrait.SetActive(false);

            try
            {
                Assert.That(CarryScale.HierarchyUniform(portrait.transform), Is.EqualTo(0.6f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(canvas);
            }
        }
    }
}
