using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall.Hover.Tests
{
    public sealed class MonsterAssemblyPortraitTests
    {
        [Test]
        public void FormalPrefabBuildsPartsAndRecoversIdenticalVisualStateAfterClear()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MonsterPortrait.prefab"));
            try
            {
                var portrait = root.GetComponent<MonsterPortrait>();
                var traits = MonsterVisualTraits.Create(MonsterAppearance.FromSeed(1234), "恋爱脑", "热情", new[] { "双头能量体", "换位手" });
                portrait.Show(traits);
                Assert.That(portrait.UsesAssembly, Is.True);
                Assert.That(portrait.HasCreature, Is.True);
                Assert.That(portrait.PartCount, Is.GreaterThanOrEqualTo(8));
                var images = root.GetComponentsInChildren<Image>(true);
                Assert.That(System.Array.FindAll(images, image => image.enabled && image.sprite != null).Length, Is.GreaterThanOrEqualTo(8));
                Assert.That(System.Array.TrueForAll(images, image => !image.raycastTarget), Is.True, "Parts must not intercept the slot's pointer input");
                var signature = portrait.VisualSignature;
                Assert.That(signature, Is.EqualTo(traits.Signature));
                portrait.Show(traits);
                Assert.That(portrait.VisualSignature, Is.EqualTo(signature));
                portrait.Clear();
                Assert.That(portrait.HasCreature, Is.False);
                portrait.Show(traits);
                Assert.That(portrait.VisualSignature, Is.EqualTo(signature));
                Assert.That(portrait.HasCreature, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void DragGhostCopiesCurrentSkillsMoodAndPartsWithoutInterceptingRays()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MonsterPortrait.prefab"));
            var layer = new GameObject("DragLayer", typeof(RectTransform));
            try
            {
                var source = root.GetComponent<MonsterPortrait>();
                source.Show(MonsterVisualTraits.Create(MonsterAppearance.FromSeed(707), "急躁", "倦怠", new[] { "情绪收集器", "回响嗓" }));
                var ghost = MonsterPortrait.SpawnGhost((RectTransform)layer.transform, source);
                Assert.That(ghost.HasCreature, Is.True);
                Assert.That(ghost.UsesAssembly, Is.True);
                Assert.That(ghost.VisualSignature, Is.EqualTo(source.VisualSignature));
                Assert.That(ghost.PartCount, Is.EqualTo(source.PartCount));
                Assert.That(ghost.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                Assert.That(System.Array.TrueForAll(ghost.GetComponentsInChildren<Image>(true), image => !image.raycastTarget), Is.True);
                ghost.Clear();
                Assert.That(ghost.HasCreature, Is.False);
                Assert.That(source.HasCreature, Is.True, "Retiring the ghost must not clear its source");
            }
            finally
            {
                Object.DestroyImmediate(layer);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AcquiredStarRemainsVisibleBesideInheritedStitchesAndSealedMouthSuppressesTongue()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MonsterPortrait.prefab"));
            try
            {
                var portrait = root.GetComponent<MonsterPortrait>();
                var genes = new MonsterAppearance(0, 0, 0, 0, 0, 0, 0, 1, 0);
                portrait.Show(MonsterVisualTraits.Create(genes, "稳定", "普通", new[] { "优秀能量体" }));
                var nodes = root.GetComponentsInChildren<RectTransform>();
                Assert.That(System.Array.Exists(nodes, node => node.name == "BodyStar_710"), Is.True, "An inherited pattern must not hide an acquired skill mark");
                Assert.That(System.Array.Exists(nodes, node => node.name == "BodyStitchSecondary_674"), Is.True);

                portrait.Show(MonsterVisualTraits.Create(genes, "稳定", "普通", new[] { "祭品腺", "回响嗓" }));
                nodes = root.GetComponentsInChildren<RectTransform>();
                Assert.That(System.Array.Exists(nodes, node => node.name == "Mouth_538"), Is.True);
                Assert.That(System.Array.Exists(nodes, node => node.name.StartsWith("OuterTongue_", System.StringComparison.Ordinal)), Is.False,
                    "A tongue must not pass through the sealed mouth");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void TwinTemplateConnectsBothHeadsAndFatBodyExtraHeadPaintsBehindTheMainEyes()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MonsterPortrait.prefab"));
            try
            {
                var portrait = root.GetComponent<MonsterPortrait>();
                portrait.Show(new MonsterAppearance(5, 0));
                var nodes = root.GetComponentsInChildren<RectTransform>();
                Assert.That(System.Array.FindAll(nodes, node => node.name.StartsWith("Body_", System.StringComparison.Ordinal) && node.name.EndsWith("_942", System.StringComparison.Ordinal)).Length,
                    Is.EqualTo(2), "Both raised heads need a neck that overlaps the shoulder body");

                portrait.Show(MonsterVisualTraits.Create(new MonsterAppearance(0, 0), "稳定", "普通", new[] { "双头能量体" }));
                nodes = root.GetComponentsInChildren<RectTransform>();
                var extra = System.Array.Find(nodes, node => node.name == "ExtraHead_936");
                var mainEye = System.Array.Find(nodes, node => node.name.StartsWith("EyeL_", System.StringComparison.Ordinal));
                Assert.That(extra, Is.Not.Null);
                Assert.That(mainEye, Is.Not.Null);
                Assert.That(extra.GetSiblingIndex(), Is.LessThan(mainEye.GetSiblingIndex()), "The extra head must not paint over the main eyes");
                var extraCorners = new Vector3[4]; var eyeCorners = new Vector3[4];
                extra.GetWorldCorners(extraCorners); mainEye.GetWorldCorners(eyeCorners);
                var rightEdge = Mathf.Max(extraCorners[0].x, extraCorners[1].x, extraCorners[2].x, extraCorners[3].x);
                var leftEdge = Mathf.Min(eyeCorners[0].x, eyeCorners[1].x, eyeCorners[2].x, eyeCorners[3].x);
                Assert.That(rightEdge, Is.LessThan(leftEdge), "The extra head must leave horizontal room for the main eye");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
