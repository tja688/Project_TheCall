using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>
    /// 把拼接出来的怪物放进 Portrait 矩形。
    /// 测量发生在骨架根的本地坐标里，宿主矩形的轴心不影响结果，所以卡片上的轴心和拖拽虚影的轴心画出来是同一个位置。
    /// </summary>
    public static class MonsterPortraitLayout
    {
        public const float ViewportPadding = 6f;

        public static bool TryMeasure(RectTransform rigRoot, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            if (rigRoot == null)
                return false;

            var has = false;
            for (var i = 0; i < rigRoot.childCount; i++)
            {
                var part = rigRoot.GetChild(i) as RectTransform;
                if (part == null)
                    continue;

                var image = part.GetComponent<Image>();
                if (image != null && !image.enabled)
                    continue;

                has = true;
                var rect = part.rect;
                Accumulate(rigRoot, part, new Vector2(rect.xMin, rect.yMin), ref min, ref max);
                Accumulate(rigRoot, part, new Vector2(rect.xMax, rect.yMin), ref min, ref max);
                Accumulate(rigRoot, part, new Vector2(rect.xMin, rect.yMax), ref min, ref max);
                Accumulate(rigRoot, part, new Vector2(rect.xMax, rect.yMax), ref min, ref max);
            }

            return has && max.x - min.x >= 1f && max.y - min.y >= 1f;
        }

        /// <summary>
        /// 把包围盒放进视口并居中。返回的位移是骨架根的 anchoredPosition，锚点在矩形中心。
        /// 包围盒中心在骨架原点时，位移是零，和宿主轴心无关。
        /// </summary>
        public static bool TryFit(Vector2 min, Vector2 max, Vector2 viewport, out float scale, out Vector2 anchoredPosition)
        {
            scale = 1f;
            anchoredPosition = Vector2.zero;
            var size = max - min;
            if (size.x < 1f || size.y < 1f || viewport.x < 1f || viewport.y < 1f)
                return false;

            var roomX = viewport.x - ViewportPadding;
            var roomY = viewport.y - ViewportPadding;
            if (roomX < 1f || roomY < 1f)
                return false;

            scale = Mathf.Min(roomX / size.x, roomY / size.y);
            var center = (min + max) * 0.5f;
            anchoredPosition = new Vector2(-center.x * scale, -center.y * scale);
            return true;
        }

        static void Accumulate(RectTransform rigRoot, RectTransform part, Vector2 corner, ref Vector2 min, ref Vector2 max)
        {
            var point = (Vector2)rigRoot.InverseTransformPoint(part.TransformPoint(new Vector3(corner.x, corner.y, 0f)));
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
    }
}
