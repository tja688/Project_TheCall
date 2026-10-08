using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall.Hover.Tests
{
    public sealed class MonsterPortraitRecoveryTests
    {
        const string PortraitPrefabPath = "Assets/Prefabs/UI/MonsterPortrait.prefab";

        [Test]
        public void 预览骨架被销毁后再次显示会重新装上立绘()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PortraitPrefabPath);
            var instance = Object.Instantiate(prefab);
            instance.SetActive(false);
            var portrait = instance.GetComponent<MonsterPortrait>();
            var serialized = new SerializedObject(portrait);
            serialized.FindProperty("_editorPreview").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            instance.SetActive(true);

            try
            {
                var appearance = MonsterAppearance.FromSeed(48291);
                portrait.Show(appearance);
                Assert.That(VisibleSprites(portrait), Is.GreaterThan(0));

                var rig = instance.transform.Find("Rig");
                Assert.That(rig, Is.Not.Null);
                Object.DestroyImmediate(rig.gameObject);

                portrait.Show(appearance);
                Assert.That(VisibleSprites(portrait), Is.GreaterThan(0), "同一外观再次显示后立绘应重新装上");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        static int VisibleSprites(MonsterPortrait portrait)
        {
            var images = portrait.GetComponentsInChildren<Image>(false);
            var count = 0;
            for (var i = 0; i < images.Length; i++)
            {
                if (images[i].enabled && images[i].sprite != null)
                    count++;
            }

            return count;
        }
    }
}
