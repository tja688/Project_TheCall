using NUnit.Framework;
using UnityEngine;

namespace TheCall.Hover.Tests
{
    public sealed class CanvasMapTests
    {
        const int Pixels = 1080;
        const float OrthoSize = 5.4f;

        GameObject _cameraObject;
        GameObject _world;
        RenderTexture _target;

        [TearDown]
        public void TearDown()
        {
            if (_cameraObject != null)
                Object.DestroyImmediate(_cameraObject);
            if (_world != null)
                Object.DestroyImmediate(_world);
            if (_target != null)
                Object.DestroyImmediate(_target);
        }

        [Test]
        public void 世界画布上的肖像按屏幕像素放大层级缩放不会放大()
        {
            var camera = CameraLookingAtOrigin();
            var portrait = WorldPortrait(camera, new Vector2(279f, 299f), 0.5f);
            var pixelsPerUnit = Pixels / (OrthoSize * 2f);
            var expectedWidth = 279f * 0.5f * 0.01f * pixelsPerUnit;
            var expectedHeight = 299f * 0.5f * 0.01f * pixelsPerUnit;
            var expected = (expectedWidth + expectedHeight) * 0.5f;

            var screen = CanvasMap.ScreenUniform(portrait);
            var hierarchy = CarryScale.HierarchyUniform(portrait);

            Assert.That(screen, Is.EqualTo(expected).Within(1f));
            Assert.That(hierarchy, Is.LessThan(0.01f));
            Assert.That(CarryScale.Blend(80f, hierarchy, 1f), Is.EqualTo(80f).Within(0.001f));
            Assert.That(CarryScale.Blend(80f, screen, 1f), Is.EqualTo(screen).Within(1f));
            Assert.That(screen, Is.GreaterThan(80f));
        }

        [Test]
        public void 关掉的世界肖像仍能量出屏幕尺寸()
        {
            var camera = CameraLookingAtOrigin();
            var portrait = WorldPortrait(camera, new Vector2(200f, 200f), 0.5f);
            portrait.gameObject.SetActive(false);

            var pixelsPerUnit = Pixels / (OrthoSize * 2f);
            var expected = 200f * 0.5f * 0.01f * pixelsPerUnit;

            Assert.That(CanvasMap.ScreenUniform(portrait), Is.EqualTo(expected).Within(1f));
        }

        [Test]
        public void 指针在世界槽位的屏幕投影内时距离为零()
        {
            var camera = CameraLookingAtOrigin();
            var portrait = WorldPortrait(camera, new Vector2(200f, 200f), 1f);
            var screen = CanvasMap.ScreenRect(portrait);

            Assert.That(CarryScale.DistanceToRect(screen.center, screen.min, screen.max), Is.EqualTo(0f).Within(0.001f));

            var treatedAsPixels = RectTransformUtility.WorldToScreenPoint(null, portrait.position);
            Assert.That(Vector2.Distance(treatedAsPixels, screen.center), Is.GreaterThan(100f));
        }

        Camera CameraLookingAtOrigin()
        {
            _target = new RenderTexture(Pixels, Pixels, 0);
            _cameraObject = new GameObject("CanvasMapCamera");
            var camera = _cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthoSize;
            camera.aspect = 1f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.targetTexture = _target;
            camera.enabled = true;
            return camera;
        }

        RectTransform WorldPortrait(Camera camera, Vector2 size, float portraitScale)
        {
            _world = new GameObject("WorldCanvas", typeof(RectTransform), typeof(Canvas));
            var canvas = _world.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            _world.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            _world.transform.position = Vector3.zero;

            var portraitObject = new GameObject("Portrait", typeof(RectTransform));
            portraitObject.transform.SetParent(_world.transform, false);
            var portrait = portraitObject.GetComponent<RectTransform>();
            portrait.anchorMin = new Vector2(0.5f, 0.5f);
            portrait.anchorMax = new Vector2(0.5f, 0.5f);
            portrait.pivot = new Vector2(0.5f, 0.5f);
            portrait.sizeDelta = size;
            portrait.localScale = new Vector3(portraitScale, portraitScale, 1f);
            portrait.anchoredPosition = Vector2.zero;
            return portrait;
        }
    }
}
