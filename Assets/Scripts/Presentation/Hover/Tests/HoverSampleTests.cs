using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheCall.Hover.Tests
{
    public sealed class HoverSampleTests
    {
        readonly List<GameObject> _roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _roots.Count; i++)
            {
                if (_roots[i] != null)
                    Object.DestroyImmediate(_roots[i]);
            }

            _roots.Clear();
        }

        [Test]
        public void 按下记下原点之后的移动和同一次按下的第二次调用都不改原点()
        {
            var origin = new Vector2(18f, 42f);
            var pressed = new PointerState(true, true, true, origin, 8f);
            var first = HoverSample.NextLatch(default, pressed);

            Assert.That(first.Down, Is.True);
            Assert.That(first.Origin, Is.EqualTo(origin));

            var moved = new PointerState(true, true, false, new Vector2(80f, 90f), 8f);
            var held = HoverSample.NextLatch(first, moved);
            Assert.That(held.Origin, Is.EqualTo(origin));

            var second = HoverSample.NextLatch(first, new PointerState(true, true, true, new Vector2(3f, 4f), 8f));
            Assert.That(second.Origin, Is.EqualTo(origin));
        }

        [Test]
        public void 位移平方达到阈值平方才算拖拽没有设备也算拖拽()
        {
            var latch = new PressLatch(true, Vector2.zero);
            var below = new PointerState(true, true, false, new Vector2(9f, 0f), 10f);
            var reached = new PointerState(true, true, false, new Vector2(10f, 0f), 10f);
            var absent = new PointerState(false, false, false, Vector2.zero, 10f);

            Assert.That(HoverSample.IsDragging(latch, below), Is.False);
            Assert.That(HoverSample.IsDragging(latch, reached), Is.True);
            Assert.That(HoverSample.IsDragging(default, absent), Is.True);
        }

        [Test]
        public void 槽位子图形返回该槽的怪物和矩形()
        {
            var slot = Root("slot");
            slot.AddComponent<MonsterSlotView>().monsterId = "m1";
            var graphic = Child(slot, "graphic");
            graphic.AddComponent<Image>();

            var found = HoverSample.TryOwner(graphic, out var monsterId, out var anchor);

            Assert.That(found, Is.True);
            Assert.That(monsterId, Is.EqualTo("m1"));
            Assert.That(anchor, Is.SameAs(slot.GetComponent<RectTransform>()));
        }

        [Test]
        public void 空怪物id的槽停止向上查找()
        {
            var outer = Root("outer");
            outer.AddComponent<MonsterSlotView>().monsterId = "outer-id";
            var inner = Child(outer, "inner");
            inner.AddComponent<MonsterSlotView>().monsterId = "";
            var graphic = Child(inner, "graphic");
            graphic.AddComponent<Image>();

            Assert.That(HoverSample.TryOwner(graphic, out _, out _), Is.False);
        }

        [Test]
        public void 购买按钮命中返回卡上的怪物和按钮矩形()
        {
            var card = Root("card");
            var view = card.AddComponent<ShopCardView>();
            view.monsterId = "m2";
            var buttonObject = Child(card, "buy");
            buttonObject.AddComponent<Image>();
            view.buyButton = buttonObject.AddComponent<Button>();

            var found = HoverSample.TryOwner(buttonObject, out var monsterId, out var anchor);

            Assert.That(found, Is.True);
            Assert.That(monsterId, Is.EqualTo("m2"));
            Assert.That(anchor, Is.SameAs(buttonObject.GetComponent<RectTransform>()));
        }

        [Test]
        public void 工具卡没有怪物id时购买按钮也不出详情()
        {
            var card = Root("tool");
            var view = card.AddComponent<ShopCardView>();
            view.monsterId = "";
            var buttonObject = Child(card, "buy");
            buttonObject.AddComponent<Image>();
            view.buyButton = buttonObject.AddComponent<Button>();

            Assert.That(HoverSample.TryOwner(buttonObject, out _, out _), Is.False);
        }

        [Test]
        public void 挡板命中时继续找下面的怪物()
        {
            var blocker = Root("blocker");
            blocker.AddComponent<HoverPassthrough>();
            var slot = Root("slot");
            slot.AddComponent<MonsterSlotView>().monsterId = "m1";
            var graphic = Child(slot, "graphic");
            graphic.AddComponent<Image>();
            var hits = new List<RaycastResult>
            {
                new RaycastResult { gameObject = blocker },
                new RaycastResult { gameObject = graphic },
            };

            var found = HoverSample.TryPick(hits, out var monsterId, out var anchor);

            Assert.That(found, Is.True);
            Assert.That(monsterId, Is.EqualTo("m1"));
            Assert.That(anchor, Is.SameAs(slot.GetComponent<RectTransform>()));
        }

        [Test]
        public void 飞行中的肖像比槽位上的旧编号优先()
        {
            var body = Root("body");
            body.AddComponent<HoverBody>().monsterId = "flying";
            var graphic = Child(body, "graphic");
            var slot = Root("slot");
            slot.AddComponent<MonsterSlotView>().monsterId = "stale";
            var slotGraphic = Child(slot, "graphic");
            var hits = new List<RaycastResult>
            {
                new RaycastResult { gameObject = graphic },
                new RaycastResult { gameObject = slotGraphic },
            };

            var found = HoverSample.TryPick(hits, out var monsterId, out _);

            Assert.That(found, Is.True);
            Assert.That(monsterId, Is.EqualTo("flying"));
        }

        [Test]
        public void 消失后的空槽不再给出技能对象()
        {
            var slot = Root("slot");
            slot.AddComponent<MonsterSlotView>().monsterId = null;
            var graphic = Child(slot, "graphic");
            var hits = new List<RaycastResult> { new RaycastResult { gameObject = graphic } };

            Assert.That(HoverSample.TryPick(hits, out _, out _), Is.False);
        }

        GameObject Root(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.hideFlags = HideFlags.HideAndDontSave;
            _roots.Add(go);
            return go;
        }

        static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.SetParent(parent.transform, false);
            return go;
        }
    }
}
