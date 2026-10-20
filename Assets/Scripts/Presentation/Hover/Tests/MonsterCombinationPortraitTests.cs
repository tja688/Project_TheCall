using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TheCall.Hover.Tests
{
    public sealed class MonsterCombinationPortraitTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void AllAcquiredMarksRemainVisibleForEveryInheritedPattern(int pattern)
        {
            WithPortrait(portrait =>
            {
                var genes = new MonsterAppearance(0, 0, 0, 2, 0, 0, 0, pattern, 0);
                portrait.Show(MonsterVisualTraits.Create(genes, "稳定", "普通", new[] { "不稳定肉体", "能量体", "优秀能量体" }));
                var nodes = ActiveNodes(portrait);
                Assert.That(nodes.Any(node => node.name == "BodyStitch_674"), Is.True);
                Assert.That(nodes.Any(node => node.name == "BodyStarSecondary_710"), Is.True);
                Assert.That(nodes.Any(node => node.name == "BodySpotsTertiary_709"), Is.True);
                Assert.That(nodes.Count(node => node.name.StartsWith("BodyStitch", StringComparison.Ordinal) ||
                    node.name.StartsWith("BodyStar", StringComparison.Ordinal) || node.name.StartsWith("BodySpots", StringComparison.Ordinal)),
                    Is.EqualTo(pattern == 0 ? 3 : 4));
            });
        }

        [TestCase("啃食体", "407")]
        [TestCase("回响嗓", "482")]
        public void SealedMouthMovesTongueToAnIndependentGraft(string skill, string art)
        {
            WithPortrait(portrait =>
            {
                portrait.Show(MonsterVisualTraits.Create(new MonsterAppearance(0, 0), "稳定", "普通", new[] { "祭品腺", skill }));
                var nodes = ActiveNodes(portrait);
                Assert.That(nodes.Any(node => node.name == "Mouth_538"), Is.True);
                Assert.That(nodes.Any(node => node.name == "SideMouth_848"), Is.True);
                Assert.That(nodes.Any(node => node.name == "GraftedTongue_" + art), Is.True);
                Assert.That(nodes.Any(node => node.name.StartsWith("OuterTongue_", StringComparison.Ordinal)), Is.False);
                var main = nodes.Single(node => node.name == "Mouth_538");
                var side = nodes.Single(node => node.name == "SideMouth_848");
                Assert.That(side.anchoredPosition.y, Is.LessThan(main.anchoredPosition.y - 15));
            });
        }

        [Test]
        public void DarkLimbsKeepAVisibleTentacleAndPoolRetiresItAfterSkillRemoval()
        {
            WithPortrait(portrait =>
            {
                var genes = new MonsterAppearance(0, 0);
                portrait.Show(MonsterVisualTraits.Create(genes, "稳定", "普通", new[] { "祭品腺", "汲取鼻" }));
                var nodes = ActiveNodes(portrait);
                Assert.That(nodes.Any(node => node.name == "RearArm_806"), Is.True);
                Assert.That(nodes.Any(node => node.name == "FrontFoot_812"), Is.True);
                Assert.That(nodes.Any(node => node.name == "GraftedTentacle_799"), Is.True);
                Assert.That(nodes.Any(node => node.name == "GraftedTongue_407"), Is.True);
                portrait.Show(MonsterVisualTraits.Create(genes, "稳定", "普通", new[] { "祭品腺" }));
                nodes = ActiveNodes(portrait);
                Assert.That(nodes.Any(node => node.name.StartsWith("GraftedTentacle_", StringComparison.Ordinal) ||
                    node.name.StartsWith("GraftedTongue_", StringComparison.Ordinal)), Is.False,
                    "Unused pooled nodes must be hidden or rebound after acquired skills disappear");
            });
        }

        [Test]
        public void LargeAndSmallHandsUseOppositeArmsAcrossAllBodyPlans()
        {
            WithPortrait(portrait =>
            {
                for (var recipe = 0; recipe < MonsterAppearance.RecipeCount; recipe++)
                {
                    portrait.Show(MonsterVisualTraits.Create(new MonsterAppearance(recipe, 0), "稳定", "普通", new[] { "优质肉体", "良好肉体" }));
                    var nodes = ActiveNodes(portrait);
                    Assert.That(nodes.Any(node => node.name == "FrontArm_745"), Is.True, "body=" + recipe);
                    Assert.That(nodes.Any(node => node.name == "RearArm_752"), Is.True, "body=" + recipe);
                }
            });
        }

        [Test]
        public void TwinBodyGrowsASmallBudHeadForExtraHeadSkill()
        {
            WithPortrait(portrait =>
            {
                portrait.Show(MonsterVisualTraits.Create(new MonsterAppearance(5, 0), "恋爱脑", "普通", new[] { "双头能量体" }));
                var nodes = ActiveNodes(portrait);
                var bud = nodes.Single(node => node.name == "BudHead_936");
                Assert.That(bud.sizeDelta.x, Is.LessThanOrEqualTo(43));
                Assert.That(bud.sizeDelta.y, Is.LessThanOrEqualTo(34));
                Assert.That(nodes.Any(node => node.name == "BudNeck_942"), Is.True);
                Assert.That(nodes.Any(node => node.name.StartsWith("BudEyeL_", StringComparison.Ordinal)), Is.True);
                Assert.That(nodes.Any(node => node.name.StartsWith("ExtraHead_", StringComparison.Ordinal)), Is.False);
            });
        }

        static RectTransform[] ActiveNodes(MonsterPortrait portrait) => portrait.GetComponentsInChildren<Image>()
            .Where(image => image.enabled && image.sprite != null).Select(image => (RectTransform)image.transform.parent).ToArray();

        static void WithPortrait(Action<MonsterPortrait> verify)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MonsterPortrait.prefab"));
            try { verify(root.GetComponent<MonsterPortrait>()); }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
