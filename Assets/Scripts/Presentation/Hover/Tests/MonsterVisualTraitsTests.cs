using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace TheCall.Hover.Tests
{
    public sealed class MonsterVisualTraitsTests
    {
        [Serializable] sealed class Entry { public string name; }
        [Serializable] sealed class Book { public Entry[] skills; public Entry[] personalities; }
        [Test] public void EveryRealSkillPersonalityAndMoodLeavesBirthArtworkUnchanged()
        {
            var book=JsonUtility.FromJson<Book>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"call-book.json")));
            var appearance=MonsterAppearance.FromSeed(124);
            var expected=MonsterVisualTraits.From(appearance).Signature;
            foreach(var personality in book.personalities)
            foreach(var mood in new[]{"倦怠","疲倦","普通","热情","热爱"})
            {
                var monster=new MonsterView("probe","probe",book.skills.Select(s=>new SkillView(s.name,0,Rarity.White)).ToArray(),0,false,0,appearance,personality.name,"森林",mood);
                Assert.That(MonsterVisualTraits.From(monster).Signature,Is.EqualTo(expected),personality.name+"/"+mood);
            }
        }
        [Test] public void ArtIdentityChangesOnlyWithCompleteDrawingOrColor()
        {
            var original=MonsterVisualTraits.From(new MonsterAppearance(5,0));
            Assert.That(MonsterVisualTraits.From(new MonsterAppearance(5,0,31)).Signature,Is.EqualTo(original.Signature));
            Assert.That(MonsterVisualTraits.From(new MonsterAppearance(6,0)).Signature,Is.Not.EqualTo(original.Signature));
            Assert.That(MonsterVisualTraits.From(new MonsterAppearance(5,1)).Signature,Is.Not.EqualTo(original.Signature));
        }
    }
}
