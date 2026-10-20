using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace TheCall.Hover.Tests
{
    public sealed class MonsterCombinationPortraitTests
    {
        [Test] public void EveryTemplateKeepsItsStructureColorAndEyeAnchorsAcrossAllSkillsAndMoods()
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MonsterPortrait.prefab"));
            try
            {
                var portrait=root.GetComponent<MonsterPortrait>();
                foreach(var template in Enum.GetValues(typeof(MonsterTemplate)).Cast<MonsterTemplate>())
                {
                    var a=new MonsterAppearance((int)template,0);
                    portrait.Show(MonsterVisualTraits.Create(a,"稳定","普通",Array.Empty<string>()));
                    var signature=portrait.AssemblySignature;
                    foreach(var mood in new[]{"倦怠","疲倦","普通","热情","热爱"})
                    {
                        portrait.Show(MonsterVisualTraits.Create(a,"稳定",mood,new[]{"双头能量体","换位手","回响嗓","镜眼"}));
                        Assert.That(portrait.HasCreature,Is.True); Assert.That(portrait.AssemblySignature,Is.EqualTo(signature),template+"/"+mood);
                    }
                }
            }
            finally {Object.DestroyImmediate(root);}
        }
        [Test] public void SeventyTwoColorwaysUseAuthoredEyeCountsAndKeepSourceHighlightsSeparate()
        {
            var catalog=MonsterArtCatalog.Runtime; Assert.That(catalog.ValidationError(),Is.Empty);
            var pieces=new System.Collections.Generic.List<MonsterComposition.Piece>();
            for(var t=0;t<12;t++) for(var p=0;p<6;p++)
            {
                MonsterComposition.Compose(catalog,MonsterVisualTraits.From(new MonsterAppearance(t,p)),pieces);
                Assert.That(pieces.Count(x=>x.Role==MonsterComposition.Motion.Eye&&!x.Mask),Is.EqualTo(catalog.Body(t).layers.Count(x=>x.expression)));
                Assert.That(pieces.Where(x=>x.Mask).All(x=>x.Sprite!=x.Art.sprite),Is.True);
                Assert.That(pieces.Where(x=>x.Mask&&x.Role==MonsterComposition.Motion.Eye).All(x=>x.Color==catalog.Colorway(p).eye),Is.True);
            }
        }
        [Test] public void ReorderingTemplatesDoesNotChangeIdentityOrDrawing()
        {
            var original=MonsterArtCatalog.Runtime;var copy=Object.Instantiate(original);
            try
            {
                copy.bodies=copy.bodies.Reverse().ToArray();copy.palettes=copy.palettes.Reverse().ToArray();Assert.That(copy.ValidationError(),Is.Empty);
                var left=new System.Collections.Generic.List<MonsterComposition.Piece>();var right=new System.Collections.Generic.List<MonsterComposition.Piece>();
                for(var t=0;t<12;t++)
                {
                    var traits=MonsterVisualTraits.From(new MonsterAppearance(t,0));MonsterComposition.Compose(original,traits,left);MonsterComposition.Compose(copy,traits,right);
                    Assert.That(left.Select(x=>x.Name+"/"+x.Art.id),Is.EqualTo(right.Select(x=>x.Name+"/"+x.Art.id)));
                    Assert.That(left.Select(x=>(Color32)x.Color),Is.EqualTo(right.Select(x=>(Color32)x.Color)));
                }
            }
            finally{Object.DestroyImmediate(copy);}
        }
    }
}
