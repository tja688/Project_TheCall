using NUnit.Framework;
using UnityEngine;

namespace TheCall.Hover.Tests
{
    public sealed class HoverLayoutTests
    {
        static readonly Rect Board = new Rect(0f, 0f, 1500f, 2000f);
        static readonly Rect Wide = new Rect(-2000f, -2000f, 4000f, 4000f);
        static readonly Rect Slot = new Rect(100f, 200f, 80f, 40f);
        static readonly Rect RightSlot = new Rect(1200f, 400f, 100f, 80f);

        [Test]
        public void 左半屏一张技能时栈在槽的右侧且子面板在窗口中线()
        {
            var placement = HoverLayout.Place(Slot, 10f, 1920f, 1, Metrics(Board));

            Assert.That(placement.StackOnRight, Is.True);
            Assert.That(placement.Sub0, Is.EqualTo(new Vector2(0f, 0f)));
            Assert.That(placement.WindowAnchoredPosition, Is.EqualTo(new Vector2(362f, 220f)));
        }

        [Test]
        public void 两张技能的子面板关于卡中心对称且第一张在上()
        {
            var placement = HoverLayout.Place(Slot, 10f, 1920f, 2, Metrics(Board));

            Assert.That(placement.Sub0.y, Is.EqualTo(52f));
            Assert.That(placement.Sub1.y, Is.EqualTo(-52f));
            Assert.That(placement.Sub0.y, Is.GreaterThan(placement.Sub1.y));
        }

        [Test]
        public void 三张技能按总高居中从上往下排()
        {
            var placement = HoverLayout.Place(Slot, 10f, 1920f, 3, Metrics(Board));

            Assert.That(placement.Sub0.y, Is.EqualTo(104f));
            Assert.That(placement.Sub1.y, Is.EqualTo(0f));
            Assert.That(placement.Sub2.y, Is.EqualTo(-104f));
        }

        [Test]
        public void 四张技能按总高居中从上往下排()
        {
            var placement = HoverLayout.Place(Slot, 10f, 1920f, 4, Metrics(Board));

            Assert.That(placement.Sub0.y, Is.EqualTo(156f));
            Assert.That(placement.Sub1.y, Is.EqualTo(52f));
            Assert.That(placement.Sub2.y, Is.EqualTo(-52f));
            Assert.That(placement.Sub3.y, Is.EqualTo(-156f));
        }

        [Test]
        public void 指针在屏幕中线时栈在槽的左侧()
        {
            var placement = HoverLayout.Place(Slot, 960f, 1920f, 1, Metrics(Wide));

            Assert.That(placement.StackOnRight, Is.False);
            Assert.That(placement.WindowAnchoredPosition, Is.EqualTo(new Vector2(-82f, 220f)));
            Assert.That(placement.Sub0, Is.EqualTo(new Vector2(0f, 0f)));
        }

        [Test]
        public void 栈超出画布右缘时只把窗口中心往回挪且子面板本地坐标不变()
        {
            var placement = HoverLayout.Place(RightSlot, 10f, 1920f, 1, Metrics(Board));

            Assert.That(placement.StackOnRight, Is.True);
            Assert.That(placement.WindowAnchoredPosition.x, Is.EqualTo(1330f));
            Assert.That(placement.WindowAnchoredPosition.y, Is.EqualTo(440f));
            Assert.That(placement.Sub0, Is.EqualTo(new Vector2(0f, 0f)));
        }

        [Test]
        public void 没有技能时布局拒绝()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                HoverLayout.Place(Slot, 10f, 1920f, 0, Metrics(Board)));
        }

        static HoverMetrics Metrics(Rect canvas) =>
            new HoverMetrics(new Vector2(340f, 96f), 12f, 8f, canvas);
    }
}
