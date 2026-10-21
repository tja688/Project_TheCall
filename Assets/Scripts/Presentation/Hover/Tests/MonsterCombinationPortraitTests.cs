using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace TheCall.Hover.Tests
{
    public sealed class MonsterCombinationPortraitTests
    {
        [Test] public void AllSeventyTwoVariantsRenderOneCompleteSpriteWithTheSourceAspect()
        {
            var root=new GameObject("Portrait",typeof(RectTransform));var portrait=root.AddComponent<MonsterPortrait>();
            try
            {
                var catalog=MonsterArtCatalog.Runtime;Assert.That(catalog.ValidationError(),Is.Empty);
                for(var t=0;t<12;t++)for(var p=0;p<6;p++)
                {
                    var appearance=new MonsterAppearance(t,p);portrait.Show(appearance);
                    var images=root.GetComponentsInChildren<Image>(false);
                    Assert.That(images.Length,Is.EqualTo(1));
                    Assert.That(images[0].sprite,Is.SameAs(catalog.Sprite(appearance)));
                    Assert.That(images[0].color,Is.EqualTo(Color.white));
                    Assert.That(images[0].preserveAspect,Is.True);
                    Assert.That(images[0].raycastTarget,Is.False);
                }
            }finally{Object.DestroyImmediate(root);}
        }
        [Test] public void ReorderingTheCatalogDoesNotChangeSpriteIdentity()
        {
            var original=MonsterArtCatalog.Runtime;var copy=Object.Instantiate(original);
            try
            {
                copy.bodies=copy.bodies.Reverse().ToArray();copy.palettes=copy.palettes.Reverse().ToArray();
                Assert.That(copy.ValidationError(),Is.Empty);
                for(var t=0;t<12;t++)for(var p=0;p<6;p++)Assert.That(copy.Sprite(new MonsterAppearance(t,p))==original.Sprite(new MonsterAppearance(t,p)),Is.True);
            }finally{Object.DestroyImmediate(copy);}
        }
    }
}
