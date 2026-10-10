using UnityEngine;

namespace TheCall
{
    /// <summary>
    /// 把世界空间画布和屏幕覆盖层画布换到同一套屏幕坐标。
    /// 覆盖层的世界坐标是像素，世界画布的世界坐标是米。直接互相当局部坐标会把点送到画面左下角。
    /// </summary>
    public static class CanvasMap
    {
        public static Camera EventCamera(Transform transform)
        {
            if (transform == null)
                return null;

            var canvas = transform.GetComponentInParent<Canvas>(true);
            if (canvas == null)
                return null;

            var root = canvas.rootCanvas;
            if (root == null || root.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return root.worldCamera != null ? root.worldCamera : Camera.main;
        }

        /// <summary>矩形在屏幕上的轴对齐包围盒。点在盒子里时，到盒子的距离是 0。</summary>
        public static Rect ScreenRect(RectTransform rect)
        {
            if (rect == null)
                return new Rect(0f, 0f, 0f, 0f);

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var camera = EventCamera(rect);
            var bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            var min = Vector2.Min(bottomLeft, topRight);
            var max = Vector2.Max(bottomLeft, topRight);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>矩形在屏幕上的均匀尺寸。关掉的肖像仍按变换矩阵计算，不读 lossyScale。</summary>
        public static float ScreenUniform(RectTransform rect)
        {
            var screen = ScreenRect(rect);
            if (screen.width <= 0f && screen.height <= 0f)
                return 0f;

            return (screen.width + screen.height) * 0.5f;
        }

        /// <summary>
        /// 把一个世界点放进 space 的局部坐标。先用源画布的相机投到屏幕，再投进目标矩形。
        /// 源和目标都在覆盖层时，相机是空的，结果与原来的逆变换一致。
        /// </summary>
        public static Vector2 LocalPoint(RectTransform space, Vector3 worldPoint, Camera sourceCamera)
        {
            if (space == null)
                return Vector2.zero;

            var screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, worldPoint);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, EventCamera(space), out var local))
                return local;

            return space.InverseTransformPoint(worldPoint);
        }
    }
}
