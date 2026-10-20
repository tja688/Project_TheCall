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

    }
}
