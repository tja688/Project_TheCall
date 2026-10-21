using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheCall.Editor
{
    public static class GameplayAudioIntegration
    {
        const string Root = "Assets/Sound/SFX/";

        [MenuItem("TheCall/Audio/更新玩法音效目录")]
        public static void Apply()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalog>("Assets/Resources/GameAudioCatalog.asset");
            if (catalog == null) throw new InvalidOperationException("Missing GameAudioCatalog");
            catalog.monsterPickUp = Clip("Monster/S_Monster_PickUp");
            catalog.monsterPlace = Clip("Monster/S_Monster_Place");
            catalog.monsterDiscard = Clip("Monster/S_Monster_Discard");
            catalog.monsterEnergyGain = Clips("Monster/S_Monster_EnergyGain", 2);
            catalog.obtain = Clips("Player/S_Player_Obtain", 3);
            catalog.skillEnergy = Clips("Skill/S_Skill_Energy", 2);
            catalog.skillMultiply = Clip("Skill/S_Skill_Multiply");
            catalog.skillKill = Clips("Skill/S_Skill_Kill", 4);
            catalog.settle = Clip("Level/S_Level_Settle");
            catalog.reachRequirement = Clip("Level/S_Level_Reach");
            catalog.experiencePickup = Clips("Level/S_Level_ExperiencePickup", 7);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        static AudioClip[] Clips(string prefix, int count) => Enumerable.Range(1, count).Select(i => Clip(prefix + "_" + i.ToString("00"))).ToArray();

        static AudioClip Clip(string name)
        {
            var path = Root + name + ".wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException("Missing supplied audio: " + path);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = false;
            importer.ambisonic = false;
            importer.SaveAndReimport();
            return clip;
        }
    }
}
