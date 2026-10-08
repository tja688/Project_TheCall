using System.Collections.Generic;
using System.Reflection;
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

        [Test]
        public void 场景实例上的预览刷新会装上立绘()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PortraitPrefabPath);
            var instance = Object.Instantiate(prefab);
            instance.SetActive(false);
            var portrait = instance.GetComponent<MonsterPortrait>();
            var serialized = new SerializedObject(portrait);
            serialized.FindProperty("_editorPreview").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            instance.SetActive(true);

            try
            {
                InvokeRefresh(portrait);
                Assert.That(VisibleSprites(portrait), Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void 预制体资源上的预览刷新不会改父级()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PortraitPrefabPath);
            var portrait = prefab.GetComponent<MonsterPortrait>();
            var childCount = prefab.transform.childCount;
            var existing = ExistingRigs();

            try
            {
                InvokeRefresh(portrait);
                Assert.That(prefab.transform.childCount, Is.EqualTo(childCount));
                Assert.That(EditorUtility.IsPersistent(prefab), Is.True);
            }
            finally
            {
                DestroyLeakedRigs(existing);
            }
        }

        static void InvokeRefresh(MonsterPortrait portrait)
        {
            var method = typeof(MonsterPortrait).GetMethod(
                "RefreshEditorPreview",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(portrait, null);
        }

        static List<Transform> ExistingRigs()
        {
            var rigs = new List<Transform>();
            var objects = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null && objects[i].name == "Rig")
                    rigs.Add(objects[i]);
            }

            return rigs;
        }

        static void DestroyLeakedRigs(List<Transform> existing)
        {
            var objects = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < objects.Length; i++)
            {
                var rig = objects[i];
                if (rig == null || rig.name != "Rig" || existing.Contains(rig))
                    continue;

                if (EditorUtility.IsPersistent(rig))
                    continue;

                Object.DestroyImmediate(rig.gameObject);
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
