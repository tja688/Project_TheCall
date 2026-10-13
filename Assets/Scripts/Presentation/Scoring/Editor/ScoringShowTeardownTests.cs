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

        [UnityTest]
        public IEnumerator 界面先销毁后演出结束不访问已销毁的界面()
        {
            var root = new GameObject("ScoringOwnerDestroyed");
            var owner = new GameObject("Presentation");
            owner.SetActive(false);
            try
            {
                var presentation = owner.AddComponent<TheCallPresentation>();
                var completed = CompletionOf(presentation);
                var show = ShowUnder(root.transform, PortraitUnder(root.transform));
                SetCompletion(show, completed);
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(show.gameObject);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (owner != null)
                    UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator 界面已停用时演出结束不启动刷新协程()
        {
            var root = new GameObject("ScoringOwnerInactive");
            var owner = new GameObject("Presentation");
            owner.SetActive(false);
            try
            {
                var presentation = owner.AddComponent<TheCallPresentation>();
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                typeof(TheCallPresentation).GetField("_busy", flags).SetValue(presentation, true);
                var show = ShowUnder(root.transform, PortraitUnder(root.transform));
                SetCompletion(show, CompletionOf(presentation));
                UnityEngine.Object.DestroyImmediate(show.gameObject);
                Assert.That(typeof(TheCallPresentation).GetField("_busy", flags).GetValue(presentation), Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator 演出完成再销毁只通知一次()
        {
            var root = new GameObject("ScoringCompleted");
            var count = 0;
            try
            {
                var show = ShowUnder(root.transform, PortraitUnder(root.transform));
                SetCompletion(show, () => count++);
                typeof(ScoringShow).GetMethod("Finish", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(show, null);
                UnityEngine.Object.DestroyImmediate(show.gameObject);
                Assert.That(count, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator 真实播放停用后释放演出并且只通知一次()
        {
            var root = new GameObject("PlaybackLifetime", typeof(RectTransform), typeof(Canvas));
            var count = 0;
            try
            {
                var board = (RectTransform)root.transform;
                var tape = ScoringTape.Arrange(new SettlementEntry[]
                    { new SettlementPayment(0, 10, true, false, false, 0) }, Array.Empty<string>());
                var show = ScoringShow.Play(new ScoringStage(tape, Array.Empty<MonsterSlotView>(), board, board,
                    null, null, null, () => count++));
                Assert.That(ScoringShow.IsPlaying, Is.True);
                root.SetActive(false);
                Assert.That(ScoringShow.IsPlaying, Is.False);
                Assert.That(count, Is.EqualTo(1));
                UnityEngine.Object.DestroyImmediate(show.gameObject);
                Assert.That(count, Is.EqualTo(1));
                LogAssert.NoUnexpectedReceived();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator 重复播放拒绝时仍通知调用者且原播放继续()
        {
            var root = new GameObject("DuplicatePlayback", typeof(RectTransform), typeof(Canvas));
            var first = 0;
            var second = 0;
            try
            {
                var board = (RectTransform)root.transform;
                var tape = ScoringTape.Arrange(new SettlementEntry[]
                    { new SettlementPayment(0, 10, true, false, false, 0) }, Array.Empty<string>());
                var show = ScoringShow.Play(new ScoringStage(tape, Array.Empty<MonsterSlotView>(), board, board,
                    null, null, null, () => first++));
                var rejected = ScoringShow.Play(new ScoringStage(tape, Array.Empty<MonsterSlotView>(), board, board,
                    null, null, null, () => second++));
                Assert.That(rejected, Is.Null);
                Assert.That(second, Is.EqualTo(1));
                Assert.That(first, Is.EqualTo(0));
                Assert.That(ScoringShow.IsPlaying, Is.True);
                show.Cancel();
                Assert.That(first, Is.EqualTo(1));
                Assert.That(ScoringShow.IsPlaying, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            yield return null;
        }

        static Action CompletionOf(TheCallPresentation presentation) =>
            (Action)Delegate.CreateDelegate(typeof(Action), presentation,
                typeof(TheCallPresentation).GetMethod("FinishSettle", BindingFlags.NonPublic | BindingFlags.Instance));

        static void SetCompletion(ScoringShow show, Action completed) =>
            typeof(ScoringShow).GetField("_stage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(show,
                new ScoringStage(null, null, null, null, null, null, null, completed));

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
