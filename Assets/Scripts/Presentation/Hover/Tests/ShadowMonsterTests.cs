using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall.Hover.Tests
{
    public sealed class ShadowMonsterTests
    {
        [Test]
        public void FormalPrefabUsesStableWholeSilhouetteAndRecoversAfterClear()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MonsterPortrait.prefab"));
            try
            {
                var portrait = root.GetComponent<MonsterPortrait>();
                var appearance = MonsterAppearance.FromSeed(1234);
                portrait.Show(appearance);
                Assert.That(portrait.UsesShadow, Is.True);
                Assert.That(portrait.HasCreature, Is.True);
                var sprite = portrait.ShadowSprite;
                portrait.Show(appearance);
                Assert.That(portrait.ShadowSprite, Is.SameAs(sprite));
                portrait.Clear();
                Assert.That(portrait.HasCreature, Is.False);
                portrait.Show(appearance);
                Assert.That(portrait.ShadowSprite, Is.SameAs(sprite));
                Assert.That(portrait.HasCreature, Is.True);
                Assert.That(root.transform.Find("ShadowCreature").GetComponent<Image>().raycastTarget, Is.False);
                Assert.That(root.transform.Find("Head"), Is.Null);
                Assert.That(root.transform.Find("Rig"), Is.Null);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
