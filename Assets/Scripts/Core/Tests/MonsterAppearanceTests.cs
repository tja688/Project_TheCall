using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class MonsterAppearanceTests
    {
        [Test]
        public void 同一外观种子返回相同的稳定部件选择()
        {
            var first = MonsterAppearance.FromSeed(48291);
            var second = MonsterAppearance.FromSeed(48291);

            Assert.That(first.TemplateId, Is.EqualTo(second.TemplateId));
            Assert.That(first.PaletteId, Is.EqualTo(second.PaletteId));
            Assert.That(first.BodyId, Is.EqualTo(second.BodyId));
            Assert.That(first.HeadId, Is.EqualTo(second.HeadId));
            Assert.That(first.EyeId, Is.EqualTo(second.EyeId));
            Assert.That(first.MouthId, Is.EqualTo(second.MouthId));
            Assert.That(first.HandId, Is.EqualTo(second.HandId));
            Assert.That(first.FootId, Is.EqualTo(second.FootId));
            Assert.That(first.TailId, Is.EqualTo(second.TailId));
            Assert.That(first.HatId, Is.EqualTo(second.HatId));
            Assert.That(first.AccessoryId, Is.EqualTo(second.AccessoryId));
        }

        [Test]
        public void 子代保存独立部件id并继承父代特征与次代色板()
        {
            var first = new MonsterAppearance(
                "template_02", 1,
                "body_02", "head_01", "eye_03", "mouth_02",
                "hand_04", "foot_01", "tail_02", string.Empty, "accessory_01");
            var second = new MonsterAppearance(
                "template_05", 4,
                "body_05", "head_04", "eye_01", "mouth_06",
                "hand_02", "foot_03", "tail_01", "hat_03", string.Empty);

            var child = MonsterAppearance.Breed(first, second, 1234);

            Assert.That(child.TemplateId, Does.StartWith("template_"));
            Assert.That(child.BodyId, Does.StartWith("body_"));
            Assert.That(child.PaletteId, Is.EqualTo(second.PaletteId));
            Assert.That(child.HeadId, Is.EqualTo("head_01").Or.EqualTo("head_04"));
            if (child.HeadId == "head_01")
            {
                Assert.That(child.EyeId, Is.EqualTo("eye_03"));
                Assert.That(child.MouthId, Is.EqualTo("mouth_02"));
            }
            else
            {
                Assert.That(child.EyeId, Is.EqualTo("eye_01"));
                Assert.That(child.MouthId, Is.EqualTo("mouth_06"));
            }

            Assert.That(child.FootId, Is.EqualTo("foot_01").Or.EqualTo("foot_03"));
            Assert.That(child.TailId, Is.EqualTo("tail_01").Or.EqualTo("tail_02"));
        }
    }
}
