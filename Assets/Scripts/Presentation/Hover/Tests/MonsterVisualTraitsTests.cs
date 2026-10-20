using System;
using System.Collections.Generic;
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

        static Book ActualBook() => JsonUtility.FromJson<Book>(
            File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "call-book.json")));

        [Test]
        public void EveryActualPersonalityKeepsItsEyeFamilyAcrossAllFiveMoods()
        {
            var book = ActualBook();
            var moods = new[] { "倦怠", "疲倦", "普通", "热情", "热爱" };
            var families = new HashSet<MonsterEyeFamily>();
            Assert.That(book.personalities.Length, Is.EqualTo(25));
            foreach (var personality in book.personalities)
            {
                var states = moods.Select(mood => MonsterVisualTraits.Create(
                    MonsterAppearance.FromSeed(124), personality.name, mood, new[] { "双头能量体" })).ToArray();
                Assert.That(states.Select(state => state.MoodTier), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }), personality.name);
                Assert.That(states.Select(state => state.EyeFamily).Distinct().Count(), Is.EqualTo(1), personality.name);
                Assert.That(states.Select(state => state.Signature).Distinct().Count(), Is.EqualTo(5), "Mood must participate in refresh identity");
                families.Add(states[0].EyeFamily);
            }
            Assert.That(families.Count, Is.EqualTo(5), "All five delivered eye families must be reachable");
        }

        [Test] public void AllActualSkillsLeaveTheBirthAppearanceUnchanged()
        {
            var appearance=MonsterAppearance.FromSeed(218); var baseline=MonsterVisualTraits.From(appearance).Signature;
            foreach(var skill in ActualBook().skills)
                Assert.That(MonsterVisualTraits.Create(appearance,"稳定","普通",new[]{skill.name}).Signature,Is.EqualTo(baseline));
        }

        [Test]
        public void OnlyPersonalityAndMoodRefreshVisualTraits()
        {
            var appearance = MonsterAppearance.FromSeed(12);
            var baseline = MonsterVisualTraits.From(appearance);
            var mood = MonsterVisualTraits.Create(appearance, "稳定", "倦怠", Array.Empty<string>());
            var personality = MonsterVisualTraits.Create(appearance, "恋爱脑", "普通", Array.Empty<string>());
            var skills = MonsterVisualTraits.Create(appearance, "稳定", "普通", new[] { "双头能量体" });
            Assert.That(new[] { baseline.Signature, mood.Signature, personality.Signature, skills.Signature }.Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void TraitsSnapshotDoesNotRetainTheCallersMutableSkillList()
        {
            var skills = new List<string> { "双头能量体" };
            var appearance = MonsterAppearance.FromSeed(66);
            var snapshot = MonsterVisualTraits.Create(appearance, "稳定", "普通", skills);
            var signature = snapshot.Signature;
            skills.Clear();
            skills.Add("回响嗓");
            Assert.That(snapshot.Signature, Is.EqualTo(signature));
            Assert.That(MonsterVisualTraits.Create(appearance, "稳定", "普通", skills).Signature, Is.EqualTo(signature));
        }

        [Test]
        public void SkillOrderAndDuplicateInstancesDoNotShuffleAnExistingAssembly()
        {
            var appearance = MonsterAppearance.FromSeed(998);
            var first = MonsterVisualTraits.Create(appearance, "自律", "热情", new[] { "换位手", "双头能量体", "换位手" });
            var second = MonsterVisualTraits.Create(appearance, "自律", "热情", new[] { "双头能量体", "换位手" });
            Assert.That(first.Signature, Is.EqualTo(second.Signature));
        }

    }
}