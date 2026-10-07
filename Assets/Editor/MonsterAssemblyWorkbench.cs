using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TheCall.Editor
{
    /// <summary>
    /// A small, persistent art workbench for the authored monster recipes.
    /// It previews the same part ordering and motion profile used by MonsterPortrait.
    /// </summary>
    public sealed class MonsterAssemblyWorkbench : EditorWindow
    {
        const string ProfilePath = "Assets/Resources/MonsterMotionProfile.asset";

        Sprite[] _body;
        Sprite[] _head;
        Sprite[] _eye;
        Sprite[] _mouth;
        Sprite[] _hand;
        Sprite[] _foot;
        Sprite[] _tail;
        Sprite[] _hat;
        Sprite[] _accessory;
        MonsterMotionProfile _profile;
        IMGUIContainer _preview;

        int _recipe;
        int _palette;
        bool _idle = true;
        bool _drag;
        bool _guides;
        float _dragAmount;
        double _startedAt;

        [MenuItem("The Call/怪物拼装工作区")]
        public static void Open()
        {
            var window = GetWindow<MonsterAssemblyWorkbench>();
            window.titleContent = new GUIContent("怪物拼装工作区");
            window.minSize = new Vector2(920f, 600f);
            window.Show();
        }

        void OnEnable()
        {
            _startedAt = EditorApplication.timeSinceStartup;
            LoadParts();
            EnsureProfile();
            EditorApplication.update += RepaintPreview;
        }

        void OnDisable()
        {
            EditorApplication.update -= RepaintPreview;
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            var split = new TwoPaneSplitView(0, 560f, TwoPaneSplitViewOrientation.Horizontal);
            rootVisualElement.Add(split);

            _preview = new IMGUIContainer(DrawPreview)
            {
                style =
                {
                    flexGrow = 1,
                    minWidth = 480,
                }
            };
            split.Add(_preview);
            split.Add(BuildControls());
        }

        VisualElement BuildControls()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    paddingLeft = 12,
                    paddingRight = 6,
                    flexGrow = 1,
                }
            };
            scroll.Add(new Label("组合与运动"));
            scroll.Add(new HelpBox(
                "这里预览的部件顺序、配方映射、调色和运动参数与运行时 MonsterPortrait 共用。拖动滑条可以直接看最大摆幅是否露接缝。",
                HelpBoxMessageType.Info));

            var recipe = new SliderInt("配方", 0, MonsterAppearance.RecipeCount - 1)
            {
                value = _recipe
            };
            recipe.RegisterValueChangedCallback(e =>
            {
                _recipe = e.newValue;
                _preview?.MarkDirtyRepaint();
            });
            scroll.Add(recipe);

            var palette = new SliderInt("色板", 0, MonsterPortrait.Palette.Length - 1)
            {
                value = _palette
            };
            palette.RegisterValueChangedCallback(e =>
            {
                _palette = e.newValue;
                _preview?.MarkDirtyRepaint();
            });
            scroll.Add(palette);

            var idle = new Toggle("显示待机")
            {
                value = _idle
            };
            idle.RegisterValueChangedCallback(e => _idle = e.newValue);
            scroll.Add(idle);

            var drag = new Toggle("预览拖动")
            {
                value = _drag
            };
            drag.RegisterValueChangedCallback(e => _drag = e.newValue);
            scroll.Add(drag);

            var dragAmount = new Slider("拖动速度", -1f, 1f)
            {
                value = _dragAmount
            };
            dragAmount.RegisterValueChangedCallback(e => _dragAmount = e.newValue);
            scroll.Add(dragAmount);

            var guides = new Toggle("显示挂点")
            {
                value = _guides
            };
            guides.RegisterValueChangedCallback(e => _guides = e.newValue);
            scroll.Add(guides);

            scroll.Add(new Label("拖动摆幅"));
            AddProfileSlider(scroll, "头部角度", () => _profile.headDragDegrees, v => _profile.headDragDegrees = v, 0f, 20f);
            AddProfileSlider(scroll, "脚部角度", () => _profile.feetDragDegrees, v => _profile.feetDragDegrees = v, 0f, 20f);
            AddProfileSlider(scroll, "尾巴角度", () => _profile.tailDragDegrees, v => _profile.tailDragDegrees = v, 0f, 24f);
            AddProfileSlider(scroll, "身体角度", () => _profile.bodyDragDegrees, v => _profile.bodyDragDegrees = v, 0f, 12f);
            AddProfileSlider(scroll, "弹簧刚度", () => _profile.springStiffness, v => _profile.springStiffness = v, 1f, 240f);
            AddProfileSlider(scroll, "弹簧阻尼", () => _profile.springDamping, v => _profile.springDamping = v, 1f, 80f);

            scroll.Add(new Label("待机幅度"));
            AddProfileSlider(scroll, "头部待机", () => _profile.idleHeadDegrees, v => _profile.idleHeadDegrees = v, 0f, 8f);
            AddProfileSlider(scroll, "脚部待机", () => _profile.idleFeetDegrees, v => _profile.idleFeetDegrees = v, 0f, 8f);
            AddProfileSlider(scroll, "尾巴待机", () => _profile.idleTailDegrees, v => _profile.idleTailDegrees = v, 0f, 12f);
            AddProfileSlider(scroll, "身体待机", () => _profile.idleBodyDegrees, v => _profile.idleBodyDegrees = v, 0f, 8f);
            AddProfileSlider(scroll, "身体上下", () => _profile.idleBodyLift, v => _profile.idleBodyLift = v, 0f, 5f);
            AddProfileSlider(scroll, "待机频率", () => _profile.idleCyclesPerSecond, v => _profile.idleCyclesPerSecond = v, 0.05f, 4f);

            var save = new Button(SaveProfile)
            {
                text = "保存到 MonsterMotionProfile.asset"
            };
            save.style.marginTop = 10;
            scroll.Add(save);
            return scroll;
        }

        void AddProfileSlider(
            VisualElement parent,
            string label,
            Func<float> read,
            Action<float> write,
            float min,
            float max)
        {
            var slider = new Slider(label, min, max)
            {
                value = read()
            };
            slider.RegisterValueChangedCallback(e =>
            {
                write(e.newValue);
                EditorUtility.SetDirty(_profile);
                _preview?.MarkDirtyRepaint();
            });
            parent.Add(slider);
        }

        void DrawPreview()
        {
            var rect = GUILayoutUtility.GetRect(480, 480, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color32(18, 24, 32, 255));

            var scale = Mathf.Min((rect.width - 48f) / 142f, (rect.height - 48f) / 102f);
            scale = Mathf.Max(1f, scale);
            var canvas = new Rect(
                rect.x + (rect.width - 142f * scale) * 0.5f,
                rect.y + (rect.height - 102f * scale) * 0.5f,
                142f * scale,
                102f * scale);
            EditorGUI.DrawRect(canvas, new Color32(35, 45, 56, 255));

            var time = (float)(EditorApplication.timeSinceStartup - _startedAt);
            var phase = (_recipe * 97 + _palette * 31) * 0.0137f;
            var headAngle = 0f;
            var feetAngle = 0f;
            var tailAngle = 0f;
            var bodyAngle = 0f;
            var bodyLift = 0f;
            if (_drag)
            {
                headAngle = -_dragAmount * _profile.headDragDegrees;
                feetAngle = _dragAmount * _profile.feetDragDegrees;
                tailAngle = _dragAmount * _profile.tailDragDegrees;
                bodyAngle = _dragAmount * _profile.bodyDragDegrees;
            }
            else if (_idle && _profile.idleEnabled)
            {
                var wave = Mathf.Sin((time + phase) * Mathf.PI * 2f * _profile.idleCyclesPerSecond);
                var slow = Mathf.Sin((time + phase * 0.7f) * Mathf.PI * _profile.idleCyclesPerSecond);
                headAngle = wave * _profile.idleHeadDegrees;
                feetAngle = wave * _profile.idleFeetDegrees;
                tailAngle = slow * _profile.idleTailDegrees;
                bodyAngle = slow * _profile.idleBodyDegrees;
                bodyLift = wave * _profile.idleBodyLift;
            }

            var palette = MonsterPortrait.Palette[Mathf.Clamp(_palette, 0, MonsterPortrait.Palette.Length - 1)];
            DrawPart(Part(_tail, _recipe == 2 ? 2 : -1), canvas, Color.white, 0f, 0f, scale);
            DrawPart(Part(_foot, _recipe % 3), canvas, palette, feetAngle, 80f, scale);
            DrawPart(Part(_body, _recipe), canvas, palette, bodyAngle, 55f, scale, bodyLift);
            DrawPart(Part(_hand, _recipe % 5), canvas, Color.white, 0f, 0f, scale);
            DrawPart(Part(_head, _recipe % 4), canvas, palette, headAngle, 34f, scale);
            DrawPart(Part(_eye, _recipe % 5), canvas, Color.white, headAngle, 34f, scale);
            DrawPart(Part(_mouth, _recipe), canvas, Color.white, headAngle, 34f, scale);
            DrawPart(Part(_accessory, _recipe == 1 || _recipe == 4 ? _recipe % 3 : -1), canvas, Color.white, 0f, 0f, scale);
            DrawPart(Part(_hat, _recipe == 0 || _recipe == 3 ? _recipe : -1), canvas, Color.white, headAngle, 34f, scale);

            if (_guides)
                DrawGuides(canvas, scale);

            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 12f, 450f, 22f),
                $"配方 {_recipe}  ·  色板 {_palette}  ·  {(float.IsNaN(headAngle) ? 0f : headAngle):0.0}°");
        }

        void DrawGuides(Rect canvas, float scale)
        {
            Handles.BeginGUI();
            Handles.color = new Color(0.35f, 0.95f, 0.75f, 0.75f);
            DrawCross(new Vector2(canvas.x + 71f * scale, canvas.y + 34f * scale), 5f * scale);
            DrawCross(new Vector2(canvas.x + 71f * scale, canvas.y + 80f * scale), 5f * scale);
            Handles.color = new Color(1f, 0.75f, 0.25f, 0.6f);
            Handles.DrawLine(
                new Vector3(canvas.x, canvas.y + 80f * scale),
                new Vector3(canvas.xMax, canvas.y + 80f * scale));
            Handles.EndGUI();
        }

        static void DrawCross(Vector2 point, float radius)
        {
            Handles.DrawLine(point - Vector2.right * radius, point + Vector2.right * radius);
            Handles.DrawLine(point - Vector2.up * radius, point + Vector2.up * radius);
        }

        static Sprite Part(Sprite[] parts, int index) =>
            parts != null && index >= 0 && index < parts.Length ? parts[index] : null;

        static void DrawPart(
            Sprite sprite,
            Rect canvas,
            Color tint,
            float angle,
            float pivotY,
            float scale,
            float lift = 0f)
        {
            if (sprite == null || sprite.texture == null)
                return;

            var previousMatrix = GUI.matrix;
            var pivot = new Vector2(canvas.x + 71f * scale, canvas.y + (pivotY + lift) * scale);
            GUIUtility.RotateAroundPivot(angle, pivot);
            GUI.color = tint;
            var texture = sprite.texture;
            var uv = new Rect(
                sprite.textureRect.x / texture.width,
                sprite.textureRect.y / texture.height,
                sprite.textureRect.width / texture.width,
                sprite.textureRect.height / texture.height);
            GUI.DrawTextureWithTexCoords(canvas, texture, uv, true);
            GUI.matrix = previousMatrix;
        }

        void LoadParts()
        {
            _body = Load("Body");
            _head = Load("Head");
            _eye = Load("Eye");
            _mouth = Load("Mouth");
            _hand = Load("Hand");
            _foot = Load("Foot");
            _tail = Load("Tail");
            _hat = Load("Hat");
            _accessory = Load("Accessory");
        }

        static Sprite[] Load(string folder)
        {
            var parts = Resources.LoadAll<Sprite>("MonsterParts/" + folder);
            Array.Sort(parts, (a, b) => string.CompareOrdinal(a.name, b.name));
            return parts;
        }

        void EnsureProfile()
        {
            _profile = AssetDatabase.LoadAssetAtPath<MonsterMotionProfile>(ProfilePath);
            if (_profile != null)
            {
                _profile.SetDefaultsIfUninitialized();
                EditorUtility.SetDirty(_profile);
                return;
            }

            _profile = ScriptableObject.CreateInstance<MonsterMotionProfile>();
            _profile.SetDefaultsIfUninitialized();
            AssetDatabase.CreateAsset(_profile, ProfilePath);
            AssetDatabase.SaveAssets();
        }

        void SaveProfile()
        {
            EditorUtility.SetDirty(_profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent("已保存怪物运动配置"));
        }

        void RepaintPreview()
        {
            if (_preview == null)
                return;

            _preview.MarkDirtyRepaint();
            Repaint();
        }
    }
}
