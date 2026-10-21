#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall.Editor
{
    /// <summary>
    /// Paints the real portrait component into a texture. The card is the in-game 279×299 frame at 2×;
    /// the margin stays visible so parts that leave the frame can be seen.
    /// </summary>
    static class MonsterPortraitCapture
    {
        const int CardWidth = 558;
        const int CardHeight = 598;
        const int StageWidth = 760;
        const int StageHeight = 860;

        static readonly Vector3 Park = new Vector3(0f, 100000f, 0f);

        static GameObject _root;
        static Camera _camera;
        static RenderTexture _target;
        static MonsterPortrait _portrait;
        static Texture2D _readback;

        static MonsterPortraitCapture()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) Shutdown();
            };
        }

        public static byte[] Render(MonsterVisualTraits traits, float seconds)
        {
            EnsureRig();
            _root.SetActive(true);
            try
            {
                _portrait.Show(traits);
                Canvas.ForceUpdateCanvases();
                _portrait.Show(traits);
                _portrait.PreviewPose(seconds);
                Canvas.ForceUpdateCanvases();
                HideTree(_root);
                if (!_portrait.HasCreature)
                    throw new InvalidOperationException("画像没有画出完整立绘。");

                _camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = _target;
                _readback.ReadPixels(new Rect(0, 0, StageWidth, StageHeight), 0, 0);
                _readback.Apply(false, false);
                RenderTexture.active = previous;
                return _readback.EncodeToPNG();
            }
            finally
            {
                if (_root != null) _root.SetActive(false);
            }
        }

        public static void Shutdown()
        {
            if (_target != null)
            {
                _target.Release();
                UnityEngine.Object.DestroyImmediate(_target);
                _target = null;
            }

            if (_readback != null)
            {
                UnityEngine.Object.DestroyImmediate(_readback);
                _readback = null;
            }

            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
                _root = null;
            }

            _camera = null;
            _portrait = null;
        }

        static void EnsureRig()
        {
            if (_root != null && _portrait != null && _camera != null && _target != null) return;
            Shutdown();

            var catalog = MonsterArtCatalog.Runtime;
            if (catalog == null || !catalog.Ready)
                throw new InvalidOperationException(catalog == null
                    ? "找不到完整怪物目录。"
                    : catalog.ValidationError());

            _root = new GameObject("MonsterGalleryCapture");
            _root.transform.position = Park;
            _root.hideFlags = HideFlags.HideAndDontSave;

            var cameraObject = new GameObject("Camera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = StageHeight * 0.5f;
            _camera.aspect = StageWidth / (float)StageHeight;
            _camera.transform.localPosition = new Vector3(0f, 0f, -10f);
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 30f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.831f, 0.796f, 0.741f, 1f);
            _camera.cullingMask = ~0;
            _camera.allowMSAA = false;
            _camera.allowHDR = false;
            _target = new RenderTexture(StageWidth, StageHeight, 24, RenderTextureFormat.ARGB32)
            {
                hideFlags = HideFlags.HideAndDontSave,
                antiAliasing = 1,
            };
            _target.Create();
            _camera.targetTexture = _target;
            _readback = new Texture2D(StageWidth, StageHeight, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };

            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.hideFlags = HideFlags.HideAndDontSave;
            canvasObject.transform.SetParent(_root.transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(StageWidth, StageHeight);
            canvasRect.pivot = new Vector2(0.5f, 0.5f);
            canvasRect.localScale = Vector3.one;
            canvasRect.localPosition = Vector3.zero;

            var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.hideFlags = HideFlags.HideAndDontSave;
            card.transform.SetParent(canvasRect, false);
            var cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            cardRect.anchoredPosition = Vector2.zero;
            var cardImage = card.GetComponent<Image>();
            cardImage.sprite = White();
            cardImage.color = new Color(0.933f, 0.910f, 0.863f, 1f);
            cardImage.raycastTarget = false;

            var host = new GameObject("Portrait", typeof(RectTransform));
            host.hideFlags = HideFlags.HideAndDontSave;
            host.SetActive(false);
            host.transform.SetParent(canvasRect, false);
            _portrait = host.AddComponent<MonsterPortrait>();
            var serialized = new SerializedObject(_portrait);
            serialized.FindProperty("_editorPreview").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var portraitRect = (RectTransform)host.transform;
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            portraitRect.anchoredPosition = Vector2.zero;
            host.SetActive(true);
            Canvas.ForceUpdateCanvases();
            HideTree(_root);
            _root.SetActive(false);
        }

        static void HideTree(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        static Sprite White()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
#endif
