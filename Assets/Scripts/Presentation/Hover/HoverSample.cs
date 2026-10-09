using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheCall
{
    internal readonly struct PointerState
    {
        public readonly bool DevicePresent;
        public readonly bool Pressed;
        public readonly bool PressedThisFrame;
        public readonly Vector2 Screen;
        public readonly float DragThreshold;

        public PointerState(bool devicePresent, bool pressed, bool pressedThisFrame, Vector2 screen, float dragThreshold)
        {
            DevicePresent = devicePresent;
            Pressed = pressed;
            PressedThisFrame = pressedThisFrame;
            Screen = screen;
            DragThreshold = dragThreshold;
        }
    }

    internal readonly struct PressLatch
    {
        public readonly bool Down;
        public readonly Vector2 Origin;

        public PressLatch(bool down, Vector2 origin)
        {
            Down = down;
            Origin = origin;
        }
    }

    internal readonly struct HoverTarget
    {
        public readonly string MonsterId;
        public readonly Rect AnchorInCanvas;
        public readonly float PointerScreenX;

        public HoverTarget(string monsterId, Rect anchorInCanvas, float pointerScreenX)
        {
            MonsterId = monsterId;
            AnchorInCanvas = anchorInCanvas;
            PointerScreenX = pointerScreenX;
        }
    }

    internal readonly struct HoverMetrics
    {
        public readonly Vector2 SubpanelSize;
        public readonly float SlotGap;
        public readonly float SubpanelGap;
        public readonly Rect CanvasLocal;

        public HoverMetrics(Vector2 subpanelSize, float slotGap, float subpanelGap, Rect canvasLocal)
        {
            SubpanelSize = subpanelSize;
            SlotGap = slotGap;
            SubpanelGap = subpanelGap;
            CanvasLocal = canvasLocal;
        }
    }

    internal readonly struct HoverPlacement
    {
        public readonly Vector2 WindowAnchoredPosition;
        public readonly bool StackOnRight;
        public readonly int SkillCount;
        public readonly Vector2 Sub0;
        public readonly Vector2 Sub1;
        public readonly Vector2 Sub2;
        public readonly Vector2 Sub3;

        public HoverPlacement(
            Vector2 windowAnchoredPosition,
            bool stackOnRight,
            int skillCount,
            Vector2 sub0,
            Vector2 sub1,
            Vector2 sub2,
            Vector2 sub3)
        {
            WindowAnchoredPosition = windowAnchoredPosition;
            StackOnRight = stackOnRight;
            SkillCount = skillCount;
            Sub0 = sub0;
            Sub1 = sub1;
            Sub2 = sub2;
            Sub3 = sub3;
        }
    }

    internal static class HoverSample
    {
        internal static PointerState ReadPointer()
        {
            var device = Pointer.current;
            var events = EventSystem.current;
            if (device == null || events == null)
                return new PointerState(false, false, false, default, 0f);

            return new PointerState(
                true,
                device.press.isPressed,
                device.press.wasPressedThisFrame,
                device.position.ReadValue(),
                events.pixelDragThreshold);
        }

        internal static PressLatch NextLatch(PressLatch latch, in PointerState pointer)
        {
            if (!pointer.Pressed)
                return new PressLatch(false, default);

            if (!latch.Down)
                return new PressLatch(true, pointer.Screen);

            return latch;
        }

        internal static bool IsDragging(in PressLatch latch, in PointerState pointer)
        {
            if (!pointer.DevicePresent)
                return true;

            var offset = pointer.Screen - latch.Origin;
            return latch.Down
                   && pointer.Pressed
                   && offset.sqrMagnitude >= pointer.DragThreshold * pointer.DragThreshold;
        }

        internal static bool TryOwner(GameObject topHit, out string monsterId, out RectTransform anchor)
        {
            monsterId = null;
            anchor = null;
            if (topHit == null)
                return false;

            var node = topHit.transform;
            while (node != null)
            {
                var body = node.GetComponent<HoverBody>();
                if (body != null)
                {
                    if (string.IsNullOrEmpty(body.monsterId))
                        return false;

                    anchor = body.transform as RectTransform;
                    if (anchor == null)
                        return false;

                    monsterId = body.monsterId;
                    return true;
                }

                var slot = node.GetComponent<MonsterSlotView>();
                if (slot != null)
                {
                    if (string.IsNullOrEmpty(slot.monsterId))
                        return false;

                    anchor = slot.transform as RectTransform;
                    if (anchor == null)
                        return false;

                    monsterId = slot.monsterId;
                    return true;
                }

                var card = node.GetComponent<ShopCardView>();
                if (card != null)
                {
                    if (string.IsNullOrEmpty(card.monsterId))
                        return false;

                    anchor = card.transform as RectTransform;
                    if (anchor == null)
                        return false;

                    monsterId = card.monsterId;
                    return true;
                }

                node = node.parent;
            }

            return false;
        }

        internal static Rect ToCanvas(RectTransform anchor, RectTransform canvas)
        {
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            var cam = OverlayCamera(canvas);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas,
                RectTransformUtility.WorldToScreenPoint(cam, corners[0]),
                cam,
                out var bottomLeft);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas,
                RectTransformUtility.WorldToScreenPoint(cam, corners[2]),
                cam,
                out var topRight);
            var min = Vector2.Min(bottomLeft, topRight);
            var max = Vector2.Max(bottomLeft, topRight);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        internal static bool TryRead(
            in PointerState pointer,
            in PressLatch latch,
            List<RaycastResult> hits,
            RectTransform canvas,
            out HoverTarget target)
        {
            target = default;
            if (IsDragging(latch, pointer) || canvas == null || EventSystem.current == null)
                return false;

            var data = new PointerEventData(EventSystem.current) { position = pointer.Screen };
            hits.Clear();
            EventSystem.current.RaycastAll(data, hits);
            if (!TryPick(hits, out var monsterId, out var anchor))
                return false;

            target = new HoverTarget(monsterId, ToCanvas(anchor, canvas), pointer.Screen.x);
            return true;
        }

        internal static bool TryPick(List<RaycastResult> hits, out string monsterId, out RectTransform anchor)
        {
            monsterId = null;
            anchor = null;
            if (hits == null)
                return false;

            for (var i = 0; i < hits.Count; i++)
            {
                var hit = hits[i].gameObject;
                if (hit == null || hit.GetComponentInParent<HoverPassthrough>() != null)
                    continue;
                if (TryOwner(hit, out monsterId, out anchor))
                    return true;
            }

            return false;
        }

        static Camera OverlayCamera(RectTransform canvas)
        {
            var view = canvas.GetComponent<Canvas>();
            if (view == null || view.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return view.worldCamera;
        }
    }
}
