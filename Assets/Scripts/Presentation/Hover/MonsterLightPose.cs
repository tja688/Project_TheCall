using UnityEngine;

namespace TheCall
{
    /// <summary>
    /// 把一块 UI 矩形放到 2D 灯光所在的世界平面上，使精灵包围盒盖住同一块屏幕区域。
    /// 枢轴不在中心时仍对齐视觉中心；矩形被镜像时 X 缩放为负。
    /// </summary>
    public static class MonsterLightPose
    {
        public static bool TryOnViewPlane(Camera camera, Vector2 screen, out Vector3 world)
        {
            world = Vector3.zero;
            if (camera == null)
                return false;

            var ray = camera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out var distance))
                return false;

            world = ray.GetPoint(distance);
            world.z = 0f;
            return true;
        }

        public static bool TryFit(
            Vector3 bottomLeft,
            Vector3 topLeft,
            Vector3 topRight,
            Bounds spriteBounds,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            scale = Vector3.one;

            var xAxis = topRight - topLeft;
            var yAxis = topLeft - bottomLeft;
            xAxis.z = 0f;
            yAxis.z = 0f;
            var width = xAxis.magnitude;
            var height = yAxis.magnitude;
            if (width < 1e-4f || height < 1e-4f)
                return false;
            if (spriteBounds.size.x < 1e-4f || spriteBounds.size.y < 1e-4f)
                return false;

            var mirrored = Vector3.Cross(xAxis, yAxis).z < 0f;
            yAxis /= height;
            rotation = Quaternion.LookRotation(Vector3.forward, yAxis);
            scale = new Vector3(
                (mirrored ? -width : width) / spriteBounds.size.x,
                height / spriteBounds.size.y,
                1f);

            var center = (bottomLeft + topRight) * 0.5f;
            center.z = 0f;
            var localCenter = new Vector3(
                spriteBounds.center.x * scale.x,
                spriteBounds.center.y * scale.y,
                0f);
            position = center - rotation * localCenter;
            position.z = 0f;
            return true;
        }
    }
}
