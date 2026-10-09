using UnityEngine;

namespace TheCall
{
    /// <summary>
    /// 从怪物栏拖向生产区或培育区时，按靠近落点的程度把怪物放大到生产区的大小。
    /// 松手没落下时再收回到怪物栏的大小。布娃娃的关节角不在这里计算。
    /// </summary>
    public static class CarryScale
    {
        public const float ReturnSeconds = 0.22f;

        public static float Uniform(Vector3 lossy)
        {
            return (Mathf.Abs(lossy.x) + Mathf.Abs(lossy.y)) * 0.5f;
        }

        /// <summary>
        /// 沿父级本地缩放乘出画面大小。空槽上的肖像常被关掉，lossyScale 这时不可靠，本地缩放仍然有效。
        /// </summary>
        public static float HierarchyUniform(Transform transform)
        {
            if (transform == null)
                return 0f;

            var scale = transform.localScale;
            var parent = transform.parent;
            while (parent != null)
            {
                scale = Vector3.Scale(scale, parent.localScale);
                parent = parent.parent;
            }

            return Uniform(scale);
        }

        /// <summary>点到轴对齐矩形的距离。点在矩形内时是 0。</summary>
        public static float DistanceToRect(Vector2 point, Vector2 min, Vector2 max)
        {
            var dx = point.x < min.x ? min.x - point.x : point.x > max.x ? point.x - max.x : 0f;
            var dy = point.y < min.y ? min.y - point.y : point.y > max.y ? point.y - max.y : 0f;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 0 表示还在拖动起点，1 表示指针已经进入生产区或培育区的卡片。
        /// startDistance 是按下时指针到这些卡片的距离。
        /// </summary>
        public static float Approach(float distance, float startDistance)
        {
            if (startDistance <= 1f)
                return distance <= 1f ? 1f : 0f;

            var traveled = 1f - Mathf.Clamp01(distance / startDistance);
            return Mathf.SmoothStep(0f, 1f, traveled);
        }

        /// <summary>
        /// 只往生产区的大小靠。已经不小于生产区的怪物保持原尺寸，拖动途中不缩小。
        /// </summary>
        public static float Blend(float from, float board, float approach)
        {
            var target = Mathf.Max(from, board);
            return Mathf.Lerp(from, target, Mathf.Clamp01(approach));
        }

        /// <summary>松手后从当前尺寸回到怪物栏尺寸。</summary>
        public static float Return(float fromRelease, float home, float elapsed)
        {
            if (ReturnSeconds <= 0f)
                return home;

            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / ReturnSeconds));
            return Mathf.Lerp(fromRelease, home, t);
        }
    }
}
