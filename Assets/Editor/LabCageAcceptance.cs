using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QFramework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    // Local UI fixtures only; resources are injected by the existing fixture builder.
    public static class LabCageAcceptance
    {
        static OperationScreenView Op => Object.FindAnyObjectByType<OperationScreenView>();
        static LabCageLift Lift => (LabCageLift)Op.lab.cageScroll;
        static string _directory;
        static string _action;
        static int _capture;
        static int _frame;
        static float _start;
        static float _target;
        static readonly List<Sample> Samples = new List<Sample>();
        [Serializable] sealed class Sample { public int frame; public float time; public float offset; }
        public static string MotionStatus { get; private set; } = "idle";

        static void Need(bool value, string message)
        { if (!value) throw new InvalidOperationException("CAGE_FAIL: " + message); }

        public static string Check(int count)
        {
            Canvas.ForceUpdateCanvases();
            var lift = Lift;
            var active = Op.cageSlots.Where(slot => slot.gameObject.activeInHierarchy).ToArray();
            Need(active.Length == count, "Inventory count differs from the model");
            Need(active.Select(slot => slot.monsterId).SequenceEqual(TheCallApp.Interface.SendQuery(new MonsterCageQuery()).Select(monster => monster.Id)), "Slot IDs reordered");
            Need(lift.verticalScrollbar == null && lift.GetComponentsInChildren<Scrollbar>(true).Length == 0, "Scrollbar remains");
            Need(lift.viewport.GetComponent<Mask>().enabled && !lift.viewport.GetComponent<Mask>().showMaskGraphic, "Aperture stencil missing");
            var rows = Mathf.Max(2, Mathf.CeilToInt(count / 2f));
            Need(Mathf.Abs(lift.content.rect.height - rows * 140) < 0.01f, "Inventory height incorrect");
            Need(lift.content.anchoredPosition.y >= -0.1f && lift.content.anchoredPosition.y <= rows * 140 - 280 + 0.1f, "Inventory out of bounds");
            var fronts = lift.content.GetComponentsInChildren<Image>().Where(image => image.name.StartsWith("CageIron")).ToArray();
            Need(fronts.Length == rows, "Missing/duplicate moving cage rows");
            foreach (var slot in active)
            {
                var rect = (RectTransform)slot.transform;
                Need(rect.rect.size == new Vector2(132, 140), "Monster doesn't own one whole cage");
                Need(slot.GetComponent<RectMask2D>() != null, "Individual cage clipping missing");
                Need(slot.GetComponent<OperationPayloadDrag>() != null && slot.GetComponent<OperationZone>() != null, "Input identity lost");
                Need(slot.portrait.HasCreature && slot.portrait.GetComponentsInChildren<Image>().Any(image => image.enabled && image.sprite != null), "Occupied cage has no painted monster: " + slot.monsterId);
                var row = Mathf.RoundToInt(-rect.anchoredPosition.y / 140);
                var front = fronts.Single(image => image.name == "CageIron" + row);
                Need(front.transform.GetSiblingIndex() > slot.transform.GetSiblingIndex() && !front.raycastTarget, "Iron doesn't cover monster or blocks input");
            }
            Need(lift.viewport.Find("LabCageTopShadow").GetSiblingIndex() > lift.content.GetSiblingIndex(), "Aperture shading behind cages");
            return "PASS " + count + " unique monsters, one cage each; " + rows + " moving rows, foreground iron, individual clipping, no scrollbar";
        }

        static Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        public static string CheckHits()
        {
            Canvas.ForceUpdateCanvases();
            var hits = new List<RaycastResult>();
            var visible = 0;
            foreach (var slot in Op.cageSlots.Where(slot => slot.gameObject.activeInHierarchy))
            {
                var center = Center((RectTransform)slot.transform);
                var data = new PointerEventData(EventSystem.current) { position = center };
                hits.Clear(); EventSystem.current.RaycastAll(data, hits);
                if (RectTransformUtility.RectangleContainsScreenPoint(Lift.viewport, center))
                {
                    Need(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<MonsterSlotView>() == slot, "Visible cage blocked by iron/shadow");
                    visible++;
                }
                else Need(!hits.Any(hit => hit.gameObject.GetComponentInParent<MonsterSlotView>() == slot), "Clipped cage remains clickable");
            }
            var first = Mathf.RoundToInt(Lift.content.anchoredPosition.y / 140) * 2;
            Need(visible == Mathf.Min(4, Op.cageSlots.Count(slot => slot.gameObject.activeInHierarchy) - first), "Visible cage count incorrect");
            return "PASS " + visible + " visible cages receive input through iron; hidden cages reject input";
        }

        public static string BeginMotion(string action, string directory)
        {
            Need(!Lift.IsMoving, "Finish current lift motion first");
            _directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(_directory);
            _action = action;
            _capture = 0;
            _frame = Time.frameCount;
            Samples.Clear();
            _start = Lift.content.anchoredPosition.y;
            if (action == "down") HandbookAcceptance.ClickVisible("LabNextCagePage");
            else if (action == "up") HandbookAcceptance.ClickVisible("LabPreviousCagePage");
            else
            {
                var wheel = new PointerEventData(EventSystem.current)
                { position = Center(Lift.viewport), scrollDelta = new Vector2(0, action == "wheel-down" ? -1 : 1) };
                Need(ExecuteEvents.ExecuteHierarchy(Lift.viewport.gameObject, wheel, ExecuteEvents.scrollHandler) != null, "Wheel didn't route to lift");
            }
            _target = Lift.TargetOffset;
            Need(Mathf.Abs(Lift.content.anchoredPosition.y - _start) < 0.01f, "Navigation teleported instead of animating");
            Need(Mathf.Abs(_target - _start) > 1, "Navigation didn't change target");
            MotionStatus = "running";
            EditorApplication.update -= SampleMotion;
            EditorApplication.update += SampleMotion;
            return "PASS actual " + action + " input starts continuous lift " + _start + " -> " + _target;
        }

        static void SampleMotion()
        {
            if (!Application.isPlaying || Op == null) { Finish("FAIL interrupted"); return; }
            if (Time.frameCount == _frame) return;
            _frame = Time.frameCount;
            var offset = Lift.content.anchoredPosition.y;
            Samples.Add(new Sample { frame = _frame, time = Time.unscaledTime, offset = offset });
            var progress = Mathf.Abs((offset - _start) / (_target - _start));
            var thresholds = new[] { 0f, 0.2f, 0.5f, 0.8f, 0.9999f };
            if (_capture < thresholds.Length && progress >= thresholds[_capture])
                ScreenCapture.CaptureScreenshot(Path.Combine(_directory, _action + "-" + _capture++ + ".png"));
            if (Lift.IsMoving) return;
            var direction = Mathf.Sign(_target - _start);
            var last = _start;
            var maximumStep = 0f;
            foreach (var sample in Samples)
            {
                if ((sample.offset - last) * direction < -0.05f) { Finish("FAIL reversal/overshoot"); return; }
                maximumStep = Mathf.Max(maximumStep, Mathf.Abs(sample.offset - last));
                last = sample.offset;
            }
            if (Samples.Count < 3 || maximumStep >= 140 || Mathf.Abs(offset - _target) > 0.1f)
            { Finish("FAIL stepping/snap " + Samples.Count + " max=" + maximumStep); return; }
            Finish("PASS " + _action + " settles at " + offset + "; " + Samples.Count + " advancing-frame samples, largest step " + maximumStep.ToString("F2"));
        }

        static void Finish(string result)
        {
            MotionStatus = result;
            EditorApplication.update -= SampleMotion;
            File.WriteAllText(Path.Combine(_directory, _action + "-motion.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new { result, start = _start, target = _target, samples = Samples }, Newtonsoft.Json.Formatting.Indented));
        }

        public static string CheckReset()
        {
            Need(!Lift.IsMoving && Mathf.Abs(Lift.content.anchoredPosition.y) < 0.01f, "Reopened lift retains retired animation");
            return "PASS reopened lift starts at the first row with no retired motion";
        }
    }
}
