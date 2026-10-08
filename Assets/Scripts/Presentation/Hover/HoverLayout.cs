using System;
using UnityEngine;

namespace TheCall
{
    internal static class HoverLayout
    {
        internal static HoverPlacement Place(
            Rect anchorInCanvas,
            float pointerScreenX,
            float screenWidth,
            int skillCount,
            in HoverMetrics metrics)
        {
            if (skillCount < 1 || skillCount > 4)
                throw new ArgumentOutOfRangeException(nameof(skillCount));

            var stackOnRight = pointerScreenX < screenWidth * 0.5f;
            var sub = metrics.SubpanelSize;
            var slotGap = metrics.SlotGap;
            var subpanelGap = metrics.SubpanelGap;
            var centerX = stackOnRight
                ? anchorInCanvas.xMax + slotGap + sub.x * 0.5f
                : anchorInCanvas.xMin - slotGap - sub.x * 0.5f;
            var centerY = anchorInCanvas.center.y;
            var total = skillCount * sub.y + (skillCount - 1) * subpanelGap;
            var sub0 = Local(0);
            var sub1 = skillCount > 1 ? Local(1) : default;
            var sub2 = skillCount > 2 ? Local(2) : default;
            var sub3 = skillCount > 3 ? Local(3) : default;

            var minX = -sub.x * 0.5f;
            var maxX = sub.x * 0.5f;
            var minY = -total * 0.5f;
            var maxY = total * 0.5f;

            var canvas = metrics.CanvasLocal;
            centerX += Shift(centerX + minX, centerX + maxX, canvas.xMin, canvas.xMax);
            centerY += Shift(centerY + minY, centerY + maxY, canvas.yMin, canvas.yMax);
            return new HoverPlacement(
                new Vector2(centerX, centerY),
                stackOnRight,
                skillCount,
                sub0,
                sub1,
                sub2,
                sub3);

            Vector2 Local(int index)
            {
                var y = total * 0.5f - sub.y * 0.5f - index * (sub.y + subpanelGap);
                return new Vector2(0f, y);
            }
        }

        static float Shift(float min, float max, float canvasMin, float canvasMax)
        {
            if (max - min > canvasMax - canvasMin)
                return canvasMin - min;
            if (min < canvasMin)
                return canvasMin - min;
            if (max > canvasMax)
                return canvasMax - max;

            return 0f;
        }
    }
}
