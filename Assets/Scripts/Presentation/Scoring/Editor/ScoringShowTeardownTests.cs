using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TheCall.Scoring
{
    public sealed class ScoringShowTeardownTests
    {
        bool _playing;

        [UnitySetUp]
        public IEnumerator Enter()
        {
            yield return new EnterPlayMode(false);
            _playing = true;
        }

        [UnityTearDown]
        public IEnumerator Exit()
        {
            ClearActive();
            if (!_playing)
                yield break;

            _playing = false;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator 界面一起销毁时不把正在销毁的画像重新激活()
        {
            var hits = 0;
            Application.LogCallback onLog = (condition, stack, type) =>
            {
                if (type == LogType.Error && condition.Contains("can not be made active when they are being destroyed"))
                    hits++;
            };

            var root = new GameObject("ScoringTeardown");
            LogAssert.ignoreFailingMessages = true;
            try
            {
                Application.logMessageReceived += onLog;
                var portrait = PortraitUnder(root.transform);
                portrait.gameObject.SetActive(false);
                ShowUnder(root.transform, portrait);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
            }
            finally
            {
                Application.logMessageReceived -= onLog;
                LogAssert.ignoreFailingMessages = false;
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }

            Assert.That(hits, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator 只销毁演出时把藏起的画像重新激活()
        {
            var root = new GameObject("ScoringRestore");
            MonsterPortrait portrait = null;
            try
            {
                portrait = PortraitUnder(root.transform);
                portrait.gameObject.SetActive(false);
                var showObject = ShowUnder(root.transform, portrait).gameObject;
                UnityEngine.Object.DestroyImmediate(showObject);
                Assert.That(portrait.gameObject.activeSelf, Is.True);
            }
            finally
            {
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
            }

            yield return null;
        }

        static MonsterPortrait PortraitUnder(Transform root)
        {
            var slotObject = new GameObject("Slot", typeof(RectTransform));
            slotObject.transform.SetParent(root, false);
            var view = slotObject.AddComponent<MonsterSlotView>();
            var portraitObject = new GameObject("Portrait", typeof(RectTransform));
            portraitObject.transform.SetParent(slotObject.transform, false);
            var portrait = portraitObject.AddComponent<MonsterPortrait>();
            view.portrait = portrait;
            return portrait;
        }

        static ScoringShow ShowUnder(Transform root, MonsterPortrait portrait)
        {
            var host = new GameObject("ScoringShow");
            host.transform.SetParent(root, false);
            var show = host.AddComponent<ScoringShow>();
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var slotType = typeof(ScoringShow).GetNestedType("BoardSlot", BindingFlags.NonPublic);
            var slot = Activator.CreateInstance(slotType);
            slotType.GetField("View").SetValue(slot, portrait.GetComponentInParent<MonsterSlotView>());
            slotType.GetField("Portrait").SetValue(slot, portrait);
            slotType.GetField("RestScale").SetValue(slot, Vector3.one);
            slotType.GetField("RestAnchoredPosition").SetValue(slot, Vector2.zero);
            var slots = Array.CreateInstance(slotType, 1);
            slots.SetValue(slot, 0);
            typeof(ScoringShow).GetField("_slots", flags).SetValue(show, slots);
            return show;
        }

        static void ClearActive()
        {
            var field = typeof(ScoringShow).GetField(
                "_active",
                BindingFlags.NonPublic | BindingFlags.Static);
            field.SetValue(null, null);
        }
    }
}
