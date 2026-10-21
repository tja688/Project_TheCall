using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TheCall
{
    public sealed class GameplaySfxCatalogTests
    {
        [Test]
        public void 交付音效均有直接引用且可解码为非静音短音效()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalog>("Assets/Resources/GameAudioCatalog.asset");
            var clips = new[] { catalog.monsterPickUp, catalog.monsterPlace, catalog.monsterDiscard,
                catalog.skillMultiply, catalog.settle, catalog.reachRequirement }
                .Concat(catalog.monsterEnergyGain).Concat(catalog.obtain).Concat(catalog.skillEnergy)
                .Concat(catalog.skillKill).Concat(catalog.experiencePickup).ToArray();
            Assert.That(clips, Has.Length.EqualTo(24));
            Assert.That(clips.Distinct().Count(), Is.EqualTo(24));
            foreach (var clip in clips)
            {
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.length, Is.GreaterThan(0).And.LessThan(2), clip.name);
                var path = AssetDatabase.GetAssetPath(clip);
                Assert.That(path, Does.StartWith("Assets/Sound/SFX/"));
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.defaultSampleSettings.preloadAudioData && !importer.loadInBackground && !importer.ambisonic, Is.True, path);
                Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad), path);
                Assert.That(importer.defaultSampleSettings.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM), path);
                clip.LoadAudioData();
                var samples = new float[clip.samples * clip.channels];
                Assert.That(clip.GetData(samples, 0), Is.True, path);
                Assert.That(samples.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), Is.True, path);
                Assert.That(samples.Any(v => Mathf.Abs(v) > 0.001f), Is.True, path);
            }
            Assert.That(catalog.sfxGroup, Is.Not.Null);
            Assert.That(catalog.sfxGroup.audioMixer, Is.EqualTo(catalog.mixer));
        }

        [Test]
        public void 经验序列保留编号顺序且主动废弃与技能消灭使用不同素材()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalog>("Assets/Resources/GameAudioCatalog.asset");
            Assert.That(catalog.experiencePickup.Select(c => c.name), Is.EqualTo(Enumerable.Range(1, 7).Select(i => "S_Level_ExperiencePickup_" + i.ToString("00"))));
            Assert.That(catalog.skillKill, Has.Length.EqualTo(4));
            Assert.That(catalog.skillKill.Contains(catalog.monsterDiscard), Is.False);
            Assert.That(catalog.monsterEnergyGain, Has.Length.EqualTo(2));
            Assert.That(catalog.obtain, Has.Length.EqualTo(3));
            Assert.That(catalog.skillEnergy, Has.Length.EqualTo(2));
        }
    }
}
