using NUnit.Framework;
using UnityEngine;

namespace TheCall.Hover.Tests
{
    public sealed class MonsterLightPoseTests
    {
        const int Pixels = 1080;
        const float OrthoSize = 5.4f;

        GameObject _cameraObject;
        RenderTexture _target;

        [TearDown]
        public void TearDown()
        {
            if (_cameraObject != null)
                Object.DestroyImmediate(_cameraObject);
            if (_target != null)
                Object.DestroyImmediate(_target);
        }

        [Test]
        public void 屏幕中心落在灯光平面原点()
        {
            var camera = CameraLookingAtOrigin();
            var center = new Vector2(Pixels * 0.5f, Pixels * 0.5f);

            Assert.That(MonsterLightPose.TryOnViewPlane(camera, center, out var world), Is.True);
            Assert.That(world.x, Is.EqualTo(0f).Within(0.02f));
            Assert.That(world.y, Is.EqualTo(0f).Within(0.02f));
            Assert.That(world.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void 一百像素对应一个世界单位()
        {
            var camera = CameraLookingAtOrigin();
            var center = Pixels * 0.5f;

            Assert.That(MonsterLightPose.TryOnViewPlane(camera, new Vector2(center + 100f, center), out var world), Is.True);
            Assert.That(world.x, Is.EqualTo(1f).Within(0.02f));
            Assert.That(world.y, Is.EqualTo(0f).Within(0.02f));
        }

        [Test]
        public void 偏心包围盒仍盖住矩形()
        {
            var bottomLeft = new Vector3(0f, 0f, 0f);
            var topLeft = new Vector3(0f, 2f, 0f);
            var topRight = new Vector3(4f, 2f, 0f);
            var bounds = new Bounds(new Vector3(0.5f, -0.25f, 0f), new Vector3(2f, 1f, 0.2f));

            Assert.That(MonsterLightPose.TryFit(bottomLeft, topLeft, topRight, bounds, out var position, out var rotation, out var scale), Is.True);

            var center = VisualCenter(position, rotation, scale, bounds);
            Assert.That(center.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(center.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(WorldEdge(rotation, scale, bounds.size.x, 0f), Is.EqualTo(4f).Within(0.001f));
            Assert.That(WorldEdge(rotation, scale, 0f, bounds.size.y), Is.EqualTo(2f).Within(0.001f));
            Assert.That(scale.x, Is.GreaterThan(0f));
        }

        [Test]
        public void 镜像矩形把精灵翻到同一块区域()
        {
            var bottomLeft = new Vector3(4f, 0f, 0f);
            var topLeft = new Vector3(4f, 2f, 0f);
            var topRight = new Vector3(0f, 2f, 0f);
            var bounds = new Bounds(new Vector3(0.5f, 0f, 0f), new Vector3(2f, 1f, 0.2f));

            Assert.That(MonsterLightPose.TryFit(bottomLeft, topLeft, topRight, bounds, out var position, out var rotation, out var scale), Is.True);

            var center = VisualCenter(position, rotation, scale, bounds);
            Assert.That(center.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(center.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(scale.x, Is.LessThan(0f));
            Assert.That(Vector3.Dot(rotation * Vector3.up, Vector3.up), Is.GreaterThan(0.999f));
        }

        [Test]
        public void 旋转矩形的上方向跟着左边()
        {
            var rotationIn = Quaternion.Euler(0f, 0f, 30f);
            var bottomLeft = rotationIn * new Vector3(-2f, -1f, 0f);
            var topLeft = rotationIn * new Vector3(-2f, 1f, 0f);
            var topRight = rotationIn * new Vector3(2f, 1f, 0f);
            var bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.1f));

            Assert.That(MonsterLightPose.TryFit(bottomLeft, topLeft, topRight, bounds, out var position, out var rotation, out var scale), Is.True);

            Assert.That(Quaternion.Angle(rotation, rotationIn), Is.LessThan(0.5f));
            Assert.That(position.magnitude, Is.LessThan(0.001f));
            Assert.That(scale.x, Is.EqualTo(4f).Within(0.001f));
            Assert.That(scale.y, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void 屏幕矩形投到平面后尺寸不变()
        {
            var camera = CameraLookingAtOrigin();
            var bottomLeft = new Vector2(440f, 500f);
            var topLeft = new Vector2(440f, 620f);
            var topRight = new Vector2(640f, 620f);
            Assert.That(MonsterLightPose.TryOnViewPlane(camera, bottomLeft, out var worldBottomLeft), Is.True);
            Assert.That(MonsterLightPose.TryOnViewPlane(camera, topLeft, out var worldTopLeft), Is.True);
            Assert.That(MonsterLightPose.TryOnViewPlane(camera, topRight, out var worldTopRight), Is.True);
            var bounds = new Bounds(new Vector3(0.2f, 0.1f, 0f), new Vector3(2f, 2f, 0.1f));

            Assert.That(MonsterLightPose.TryFit(
                worldBottomLeft,
                worldTopLeft,
                worldTopRight,
                bounds,
                out var position,
                out var rotation,
                out var scale), Is.True);

            var back = camera.WorldToScreenPoint(VisualCenter(position, rotation, scale, bounds));
            Assert.That(back.x, Is.EqualTo(540f).Within(1f));
            Assert.That(back.y, Is.EqualTo(560f).Within(1f));
            Assert.That(WorldEdge(rotation, scale, bounds.size.x, 0f), Is.EqualTo(2f).Within(0.02f));
            Assert.That(WorldEdge(rotation, scale, 0f, bounds.size.y), Is.EqualTo(1.2f).Within(0.02f));
        }

        Camera CameraLookingAtOrigin()
        {
            _target = new RenderTexture(Pixels, Pixels, 0);
            _cameraObject = new GameObject("MonsterLightCamera");
            var camera = _cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthoSize;
            camera.aspect = 1f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.targetTexture = _target;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        static Vector3 VisualCenter(Vector3 position, Quaternion rotation, Vector3 scale, Bounds bounds)
        {
            return position + rotation * Vector3.Scale(bounds.center, scale);
        }

        static float WorldEdge(Quaternion rotation, Vector3 scale, float sizeX, float sizeY)
        {
            return (rotation * new Vector3(sizeX * scale.x, sizeY * scale.y, 0f)).magnitude;
        }
    }
}
