using NUnit.Framework;

namespace TheCall.Tests
{
    public sealed class MonsterAppearanceTests
    {
        [Test]
        public void 同一外观种子返回相同配方和色板()
        {
            var first = MonsterAppearance.FromSeed(48291);
            var second = MonsterAppearance.FromSeed(48291);

            Assert.That(first.Recipe, Is.EqualTo(second.Recipe));
            Assert.That(first.Palette, Is.EqualTo(second.Palette));
            Assert.That(first.Recipe, Is.GreaterThanOrEqualTo(0).And.LessThan(MonsterAppearance.RecipeCount));
            Assert.That(first.Palette, Is.GreaterThanOrEqualTo(0).And.LessThan(MonsterAppearance.PaletteCount));
        }

        [Test]
        public void 子代色板跟随次代亲本且同一亲本种子配方稳定()
        {
            var first = new MonsterAppearance(1, 1);
            var second = new MonsterAppearance(4, 4);
            var child = MonsterAppearance.Breed(first, second, 1234);
            var again = MonsterAppearance.Breed(first, second, 1234);

            Assert.That(child.Palette, Is.EqualTo(second.Palette));
            Assert.That(child.Recipe, Is.EqualTo(again.Recipe));
            Assert.That(child.Recipe, Is.GreaterThanOrEqualTo(0).And.LessThan(MonsterAppearance.RecipeCount));
        }
    }
}
