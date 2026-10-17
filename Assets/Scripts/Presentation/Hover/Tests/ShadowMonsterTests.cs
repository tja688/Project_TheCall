using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall.Hover.Tests
{
    public sealed class ShadowMonsterTests
    {
        [Test]
        public void RetainedAssemblyCanBeSelectedAndReturnedToShadowWithoutDoublePainting()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MonsterPortrait.prefab"));
            try
            {
                var portrait = root.GetComponent<MonsterPortrait>();
                var appearance = MonsterAppearance.FromSeed(48291);
                portrait.Show(appearance);
                var settings = new SerializedObject(portrait);
                settings.FindProperty("_useShadow").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo();
                portrait.Show(appearance);
                Assert.That(portrait.UsesShadow, Is.False);
                Assert.That(portrait.HasCreature, Is.True);
                Assert.That(root.transform.Find("ShadowCreature").GetComponent<Image>().enabled, Is.False);
                Assert.That(root.transform.Find("Rig").gameObject.activeSelf, Is.True);
                portrait.UseShadowArtwork();
                portrait.Show(appearance);
                Assert.That(portrait.UsesShadow && portrait.HasCreature, Is.True);
                foreach (var image in root.transform.Find("Rig").GetComponentsInChildren<Image>(true))
                    Assert.That(image.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

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
                Assert.That(root.GetComponent<MonsterLightBody>(), Is.Null);
                Assert.That(root.transform.Find("ShadowCreature").GetComponent<Image>().raycastTarget, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
