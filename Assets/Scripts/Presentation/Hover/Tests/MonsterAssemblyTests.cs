using NUnit.Framework;
using UnityEngine;

namespace TheCall.Hover.Tests
{
    public sealed class MonsterAssemblyTests
    {
        MonsterAssemblyCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<MonsterAssemblyCatalog>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_catalog != null)
                Object.DestroyImmediate(_catalog);
        }

        [Test]
        public void 五官先对齐头型脸部锚点动画支点偏移独立计算()
        {
            var template = new MonsterAssemblyTemplate
            {
                anchors = new MonsterAnchorSet
                {
                    neck = new Vector2(10f, 20f),
                    face = new Vector2(2f, 3f),
                },
                head = MonsterPartSlot.Create("head_01", MonsterAnchorKind.Neck, new Vector2(1f, 2f)),
            };
            var slot = MonsterPartSlot.Create("eye_01", MonsterAnchorKind.Face, new Vector2(4f, 5f));
            slot.pivot = new Vector2(0.5f, 0.4f);
            var part = new MonsterPartDefinition
            {
                kind = MonsterPartKind.Eye,
                attachmentPoint = new Vector2(0.5f, 0.3f),
            };
            var head = new MonsterPartDefinition
            {
                kind = MonsterPartKind.Head,
                faceAnchor = new Vector2(6f, 7f),
                eyeOffset = new Vector2(8f, 9f),
            };

            var pivotPosition = _catalog.ResolveSpritePivotPosition(template, slot, part, head);

            Assert.That(pivotPosition.x, Is.EqualTo(31f).Within(0.001f));
            Assert.That(pivotPosition.y, Is.EqualTo(56.2f).Within(0.001f));
        }

        [Test]
        public void 挂点接口与部件接口不相同时组合不兼容()
        {
            var slot = MonsterPartSlot.Create("foot_01", MonsterAnchorKind.Foot, Vector2.zero);
            slot.requiredConnection = "leg.standard";
            var part = new MonsterPartDefinition
            {
                kind = MonsterPartKind.Foot,
                connectionType = "neck.standard",
            };

            Assert.That(_catalog.IsCompatible(slot, part), Is.False);
            part.connectionType = "leg.standard";
            Assert.That(_catalog.IsCompatible(slot, part), Is.True);
        }
    }
}
