using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
namespace TheCall.Hover.Tests
{
    public sealed class MonsterWholeSpritePortraitTests
    {
        [Test] public void FormalPrefabClearsAndRestoresExactlyOneDrawing()
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MonsterPortrait.prefab"));
            try
            {
                var portrait=root.GetComponent<MonsterPortrait>();var appearance=MonsterAppearance.FromSeed(1234);
                portrait.Show(appearance);var sprite=root.GetComponentInChildren<Image>().sprite;var signature=portrait.VisualSignature;
                Assert.That(portrait.HasCreature&&portrait.UsesWholeSprite,Is.True);Assert.That(portrait.PartCount,Is.EqualTo(1));
                portrait.Clear();Assert.That(portrait.HasCreature,Is.False);portrait.Show(appearance);
                Assert.That(portrait.VisualSignature,Is.EqualTo(signature));Assert.That(root.GetComponentInChildren<Image>().sprite,Is.SameAs(sprite));
            }finally{Object.DestroyImmediate(root);}
        }
        [Test] public void DragGhostCopiesTheExactSpriteAndDoesNotInterceptPointerInput()
        {
            var root=new GameObject("Source",typeof(RectTransform));var layer=new GameObject("DragLayer",typeof(RectTransform));
            try
            {
                var source=root.AddComponent<MonsterPortrait>();source.Show(MonsterAppearance.FromSeed(707));
                var ghost=MonsterPortrait.SpawnGhost((RectTransform)layer.transform,source);
                Assert.That(ghost.HasCreature&&ghost.UsesWholeSprite,Is.True);
                Assert.That(ghost.VisualSignature,Is.EqualTo(source.VisualSignature));Assert.That(ghost.PartCount,Is.EqualTo(1));
                Assert.That(ghost.GetComponentInChildren<Image>().sprite,Is.SameAs(source.GetComponentInChildren<Image>().sprite));
                Assert.That(ghost.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);
                Assert.That(ghost.GetComponentInChildren<Image>().raycastTarget,Is.False);
                ghost.Clear();Assert.That(source.HasCreature,Is.True);
            }finally{Object.DestroyImmediate(layer);Object.DestroyImmediate(root);}
        }
    }
}
