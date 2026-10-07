using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TheCall.Editor
{
    /// <summary>
    /// Long-lived art workspace for componentized monster assembly.
    /// The preview is interactive: select a slot, drag it in the canvas, toggle guides,
    /// and save the authored template back to the shared catalog.
    /// </summary>
    public sealed class MonsterAssemblyWorkbench : EditorWindow
    {
        static readonly MonsterPartKind[] DrawOrder =
        {
            MonsterPartKind.Tail,
            MonsterPartKind.Foot,
            MonsterPartKind.Body,
            MonsterPartKind.Hand,
            MonsterPartKind.Head,
            MonsterPartKind.Eye,
            MonsterPartKind.Mouth,
            MonsterPartKind.Accessory,
            MonsterPartKind.Hat,
        };

        static readonly MonsterPartKind[] SlotOrder =
        {
            MonsterPartKind.Body,
            MonsterPartKind.Head,
            MonsterPartKind.Eye,
            MonsterPartKind.Mouth,
            MonsterPartKind.Hand,
            MonsterPartKind.Foot,
            MonsterPartKind.Tail,
            MonsterPartKind.Hat,
            MonsterPartKind.Accessory,
        };

        static readonly string[] PaletteNames =
        {
            "珊瑚红",
            "薄荷绿",
            "天蓝",
            "琥珀黄",
            "薰衣草",
            "青瓷",
        };

        const float CanvasWidth = 142f;
        const float CanvasHeight = 102f;

        MonsterAssemblyCatalog _catalog;
        MonsterMotionProfile _profile;
        MonsterAssemblyTemplate _sourceTemplate;
        MonsterAssemblyTemplate _workingTemplate;
        IMGUIContainer _preview;
        VisualElement _controls;
        PopupField<string> _templatePicker;
        PopupField<string> _palettePicker;
        int _templateIndex;
        int _palette;
        MonsterPartKind _selectedKind = MonsterPartKind.Body;
        bool _showGuides = true;
        bool _idlePreview = true;
        bool _dragPreview;
        bool _editSelected;
        bool _previewDragging;
        Vector2 _lastPointer;
        float _dragAmount;
        double _startedAt;
        double _lastPreviewTick;
        double _lastPointerTick;
        Vector2 _previewOffset;
        Vector2 _previewOffsetVelocity;
        float _targetHeadAngle;
        float _targetFootAngle;
        float _targetTailAngle;
        float _targetBodyAngle;
        float _headAngle;
        float _headVelocity;
        float _footAngle;
        float _footVelocity;
        float _tailAngle;
        float _tailVelocity;
        float _bodyAngle;
        float _bodyVelocity;

        [MenuItem("The Call/怪物拼装工作区")]
        public static void Open()
        {
            var window = GetWindow<MonsterAssemblyWorkbench>();
            window.titleContent = new GUIContent("怪物拼装工作区");
            window.minSize = new Vector2(1080f, 680f);
            window.Show();
        }

        void OnEnable()
        {
            _startedAt = EditorApplication.timeSinceStartup;
            _lastPreviewTick = _startedAt;
            _catalog = MonsterAssemblyCatalogEditor.EnsureAsset();
            _profile = AssetDatabase.LoadAssetAtPath<MonsterMotionProfile>("Assets/Resources/MonsterMotionProfile.asset");
            if (_profile == null)
                _profile = MonsterMotionProfile.Fallback;
            else
                _profile.SetDefaultsIfUninitialized();

            SelectTemplate(Mathf.Clamp(_templateIndex, 0, TemplateCount - 1));
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
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8,
                }
            };
            header.Add(new Label("组件化怪物编辑工作区")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 16,
                    flexGrow = 1,
                }
            });
            header.Add(new Label("稳定部件 ID · 身体挂点 · 可保存模板")
            {
                style =
                {
                    color = new Color(0.55f, 0.68f, 0.72f),
                    marginRight = 10,
                }
            });
            rootVisualElement.Add(header);

            var split = new TwoPaneSplitView(0, 660f, TwoPaneSplitViewOrientation.Horizontal);
            rootVisualElement.Add(split);

            _preview = new IMGUIContainer(DrawPreview)
            {
                style =
                {
                    flexGrow = 1,
                    minWidth = 560,
                }
            };
            split.Add(_preview);

            _controls = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    minWidth = 360,
                }
            };
            split.Add(new ScrollView(ScrollViewMode.Vertical)
            {
                contentContainer =
                {
                    style =
                    {
                        paddingLeft = 12,
                        paddingRight = 6,
                    }
                }
            });
            var scroll = split[1] as ScrollView;
            scroll?.Add(_controls);
            BuildControls();
        }

        int TemplateCount => _catalog == null || _catalog.templates == null ? 0 : _catalog.templates.Count;

        void BuildControls()
        {
            if (_controls == null)
                return;

            _controls.Clear();
            if (_workingTemplate == null)
            {
                _controls.Add(new HelpBox("没有可用模板。请先生成怪物组件目录。", HelpBoxMessageType.Error));
                return;
            }

            _controls.Add(new HelpBox(
                "预览区就是工作台：打开“预览拖动”后按住怪物拖动，松开可看阻尼弹簧回弹；打开“编辑选中部件”后可直接拖拽部件。部件连接点与动画支点分开编辑。",
                HelpBoxMessageType.Info));

            var templateChoices = new List<string>();
            for (var i = 0; i < TemplateCount; i++)
                templateChoices.Add(TemplateLabel(_catalog.templates[i], i));
            _templatePicker = new PopupField<string>("模板", templateChoices, Mathf.Clamp(_templateIndex, 0, templateChoices.Count - 1));
            _templatePicker.RegisterValueChangedCallback(e =>
            {
                var index = templateChoices.IndexOf(e.newValue);
                if (index >= 0)
                    SelectTemplate(index);
            });
            _controls.Add(_templatePicker);

            _palettePicker = new PopupField<string>("色板", new List<string>(PaletteNames), Mathf.Clamp(_palette, 0, PaletteNames.Length - 1));
            _palettePicker.RegisterValueChangedCallback(e =>
            {
                _palette = Array.IndexOf(PaletteNames, e.newValue);
                MarkPreviewDirty();
            });
            _controls.Add(_palettePicker);

            var previewFoldout = new Foldout { text = "预览与反馈", value = true };
            previewFoldout.Add(Toggle("显示挂点", _showGuides, value =>
            {
                _showGuides = value;
                MarkPreviewDirty();
            }));
            previewFoldout.Add(Toggle("呼吸待机", _idlePreview, value =>
            {
                _idlePreview = value;
                MarkPreviewDirty();
            }));
            previewFoldout.Add(Toggle("预览拖动", _dragPreview, value =>
            {
                _dragPreview = value;
                if (!value)
                {
                    _dragAmount = 0f;
                    _targetHeadAngle = 0f;
                    _targetFootAngle = 0f;
                    _targetTailAngle = 0f;
                    _targetBodyAngle = 0f;
                }
                MarkPreviewDirty();
            }));
            previewFoldout.Add(Toggle("编辑选中部件", _editSelected, value =>
            {
                _editSelected = value;
                MarkPreviewDirty();
            }));
            previewFoldout.Add(new Slider("拖动量", -1f, 1f)
            {
                value = _dragAmount,
                showInputField = true,
            });
            var dragSlider = previewFoldout[previewFoldout.childCount - 1] as Slider;
            dragSlider?.RegisterValueChangedCallback(e =>
            {
                _dragAmount = e.newValue;
                MarkPreviewDirty();
            });
            _controls.Add(previewFoldout);

            var actions = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 6,
                    marginBottom = 6,
                }
            };
            var save = new Button(SaveTemplate) { text = "保存当前模板" };
            save.style.flexGrow = 1;
            actions.Add(save);
            var saveAs = new Button(SaveAsTemplate) { text = "另存为新模板" };
            saveAs.style.flexGrow = 1;
            actions.Add(saveAs);
            var reset = new Button(ResetTemplate) { text = "撤销本次修改" };
            reset.style.flexGrow = 1;
            actions.Add(reset);
            _controls.Add(actions);

            var selected = new Foldout { text = "选中部件： " + PartLabel(_selectedKind), value = true };
            selected.Add(BuildSelectedPartControls());
            _controls.Add(selected);

            var slots = new Foldout { text = "部件槽位", value = true };
            for (var i = 0; i < SlotOrder.Length; i++)
                slots.Add(BuildSlotRow(SlotOrder[i]));
            _controls.Add(slots);

            var anchors = new Foldout { text = "身体挂点（像素，原点在画布中心）", value = true };
            anchors.Add(AnchorField("脖子", () => _workingTemplate.anchors.neck, value =>
            {
                _workingTemplate.anchors.neck = value;
                MarkPreviewDirty();
            }));
            anchors.Add(AnchorField("脸部", () => _workingTemplate.anchors.face, value =>
            {
                _workingTemplate.anchors.face = value;
                MarkPreviewDirty();
            }));
            anchors.Add(AnchorField("手臂", () => _workingTemplate.anchors.hand, value =>
            {
                _workingTemplate.anchors.hand = value;
                MarkPreviewDirty();
            }));
            anchors.Add(AnchorField("脚根", () => _workingTemplate.anchors.foot, value =>
            {
                _workingTemplate.anchors.foot = value;
                MarkPreviewDirty();
            }));
            anchors.Add(AnchorField("尾巴", () => _workingTemplate.anchors.tail, value =>
            {
                _workingTemplate.anchors.tail = value;
                MarkPreviewDirty();
            }));
            anchors.Add(AnchorField("身体", () => _workingTemplate.anchors.body, value =>
            {
                _workingTemplate.anchors.body = value;
                MarkPreviewDirty();
            }));
            _controls.Add(anchors);

            var motion = new Foldout { text = "通用运动参数", value = false };
            motion.Add(ProfileSlider("头部拖动角度", () => _profile.headDragDegrees, value => _profile.headDragDegrees = value, 0f, 20f));
            motion.Add(ProfileSlider("脚部拖动角度", () => _profile.feetDragDegrees, value => _profile.feetDragDegrees = value, 0f, 20f));
            motion.Add(ProfileSlider("尾巴拖动角度", () => _profile.tailDragDegrees, value => _profile.tailDragDegrees = value, 0f, 24f));
            motion.Add(ProfileSlider("身体拖动角度", () => _profile.bodyDragDegrees, value => _profile.bodyDragDegrees = value, 0f, 12f));
            motion.Add(ProfileSlider("待机头部", () => _profile.idleHeadDegrees, value => _profile.idleHeadDegrees = value, 0f, 8f));
            motion.Add(ProfileSlider("待机尾巴", () => _profile.idleTailDegrees, value => _profile.idleTailDegrees = value, 0f, 12f));
            motion.Add(ProfileSlider("待机身体上下", () => _profile.idleBodyLift, value => _profile.idleBodyLift = value, 0f, 5f));
            motion.Add(ProfileSlider("待机频率", () => _profile.idleCyclesPerSecond, value => _profile.idleCyclesPerSecond = value, 0.05f, 4f));
            _controls.Add(motion);
        }

        VisualElement BuildSlotRow(MonsterPartKind kind)
        {
            var slot = _workingTemplate.GetSlot(kind);
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 3,
                }
            };
            var select = new Button(() =>
            {
                _selectedKind = kind;
                BuildControls();
            })
            {
                text = PartLabel(kind),
            };
            select.style.width = 70;
            row.Add(select);

            var choices = PartChoices(kind);
            var current = string.IsNullOrEmpty(slot.partId) ? choices[0] : slot.partId;
            var index = Mathf.Max(0, choices.IndexOf(current));
            var picker = new PopupField<string>(choices, index);
            picker.style.flexGrow = 1;
            picker.RegisterValueChangedCallback(e =>
            {
                var updated = _workingTemplate.GetSlot(kind);
                updated.partId = e.newValue == choices[0] ? null : e.newValue;
                updated.enabled = !string.IsNullOrEmpty(updated.partId);
                _workingTemplate.SetSlot(kind, updated);
                if (_selectedKind == kind)
                    BuildControls();
                else
                    MarkPreviewDirty();
            });
            row.Add(picker);

            var visible = new Toggle { value = slot.enabled };
            visible.style.width = 24;
            visible.tooltip = "显示/隐藏";
            visible.RegisterValueChangedCallback(e =>
            {
                var updated = _workingTemplate.GetSlot(kind);
                updated.enabled = e.newValue;
                _workingTemplate.SetSlot(kind, updated);
                MarkPreviewDirty();
            });
            row.Add(visible);
            return row;
        }

        VisualElement BuildSelectedPartControls()
        {
            var container = new VisualElement();
            var slot = _workingTemplate.GetSlot(_selectedKind);
            var part = _catalog.GetPartOrNull(slot.partId);
            container.Add(new Label(
                (part == null ? "未选择部件" : part.id + " · " + part.kind)
                + "  |  " + (_editSelected ? "画布拖拽已开启" : "可在画布中选中后编辑")));

            var anchor = new EnumField("挂点", slot.anchor);
            anchor.RegisterValueChangedCallback(e =>
            {
                var updated = _workingTemplate.GetSlot(_selectedKind);
                updated.anchor = (MonsterAnchorKind)e.newValue;
                _workingTemplate.SetSlot(_selectedKind, updated);
                MarkPreviewDirty();
            });
            container.Add(anchor);
            container.Add(Vector2Field("局部偏移", slot.offset, value =>
            {
                var updated = _workingTemplate.GetSlot(_selectedKind);
                updated.offset = value;
                _workingTemplate.SetSlot(_selectedKind, updated);
                MarkPreviewDirty();
            }));
            container.Add(Vector2Field("动画支点（归一化，左下为原点）", slot.pivot, value =>
            {
                var updated = _workingTemplate.GetSlot(_selectedKind);
                updated.pivot = new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
                _workingTemplate.SetSlot(_selectedKind, updated);
                MarkPreviewDirty();
            }));
            if (part != null)
            {
                container.Add(Vector2Field("部件连接点（归一化，左下为原点）", part.attachmentPoint, value =>
                {
                    part.attachmentPoint = new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
                    EditorUtility.SetDirty(_catalog);
                    MarkPreviewDirty();
                }));
                container.Add(new Label("连接点与模板挂点对齐；动画支点决定部件围绕自身何处转动。"));
                if (part.kind == MonsterPartKind.Head)
                {
                    container.Add(Vector2Field("头部脸部中心", part.faceAnchor, value =>
                    {
                        part.faceAnchor = value;
                        EditorUtility.SetDirty(_catalog);
                        MarkPreviewDirty();
                    }));
                    container.Add(Vector2Field("眼部基准偏移", part.eyeOffset, value =>
                    {
                        part.eyeOffset = value;
                        EditorUtility.SetDirty(_catalog);
                        MarkPreviewDirty();
                    }));
                    container.Add(Vector2Field("嘴部基准偏移", part.mouthOffset, value =>
                    {
                        part.mouthOffset = value;
                        EditorUtility.SetDirty(_catalog);
                        MarkPreviewDirty();
                    }));
                }
            }
            container.Add(Vector2Field("缩放", slot.scale, value =>
            {
                var updated = _workingTemplate.GetSlot(_selectedKind);
                updated.scale = new Vector2(
                    Mathf.Clamp(value.x, 0.1f, 3f),
                    Mathf.Clamp(value.y, 0.1f, 3f));
                _workingTemplate.SetSlot(_selectedKind, updated);
                MarkPreviewDirty();
            }));
            container.Add(FloatField("旋转", slot.rotation, value =>
            {
                var updated = _workingTemplate.GetSlot(_selectedKind);
                updated.rotation = value;
                _workingTemplate.SetSlot(_selectedKind, updated);
                MarkPreviewDirty();
            }));
            var reset = new Button(() =>
            {
                var updated = _workingTemplate.GetSlot(_selectedKind);
                var partDefinition = _catalog.GetPartOrNull(updated.partId);
                updated.offset = Vector2.zero;
                updated.rotation = partDefinition == null ? 0f : partDefinition.defaultRotation;
                updated.pivot = partDefinition == null ? new Vector2(0.5f, 0.5f) : partDefinition.defaultPivot;
                updated.scale = partDefinition == null ? Vector2.one : partDefinition.defaultScale;
                _workingTemplate.SetSlot(_selectedKind, updated);
                BuildControls();
            })
            {
                text = "重置选中部件姿态",
            };
            container.Add(reset);
            return container;
        }

        VisualElement AnchorField(string label, Func<Vector2> read, Action<Vector2> write)
        {
            return Vector2Field(label, read(), value =>
            {
                write(value);
                MarkPreviewDirty();
            });
        }

        VisualElement Vector2Field(string label, Vector2 value, Action<Vector2> changed)
        {
            var field = new Vector2Field(label) { value = value };
            field.RegisterValueChangedCallback(e => changed(e.newValue));
            return field;
        }

        VisualElement FloatField(string label, float value, Action<float> changed)
        {
            var field = new FloatField(label) { value = value };
            field.RegisterValueChangedCallback(e => changed(e.newValue));
            return field;
        }

        VisualElement ProfileSlider(string label, Func<float> read, Action<float> write, float min, float max)
        {
            var slider = new Slider(label, min, max)
            {
                value = read(),
                showInputField = true,
            };
            slider.RegisterValueChangedCallback(e =>
            {
                write(e.newValue);
                EditorUtility.SetDirty(_profile);
                MarkPreviewDirty();
            });
            return slider;
        }

        Toggle Toggle(string label, bool value, Action<bool> changed)
        {
            var toggle = new Toggle(label) { value = value };
            toggle.RegisterValueChangedCallback(e => changed(e.newValue));
            return toggle;
        }

        List<string> PartChoices(MonsterPartKind kind)
        {
            var choices = new List<string> { "(空)" };
            if (_catalog?.parts == null)
                return choices;

            for (var i = 0; i < _catalog.parts.Count; i++)
            {
                var part = _catalog.parts[i];
                if (part != null && part.kind == kind)
                    choices.Add(part.id);
            }

            return choices;
        }

        void DrawPreview()
        {
            var rect = GUILayoutUtility.GetRect(560, 560, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color32(15, 21, 29, 255));

            var scale = Mathf.Min((rect.width - 72f) / CanvasWidth, (rect.height - 90f) / CanvasHeight);
            scale = Mathf.Max(1f, scale);
            var canvas = new Rect(
                rect.x + (rect.width - CanvasWidth * scale) * 0.5f,
                rect.y + (rect.height - CanvasHeight * scale) * 0.53f,
                CanvasWidth * scale,
                CanvasHeight * scale);
            EditorGUI.DrawRect(canvas, new Color32(34, 46, 58, 255));
            DrawPreviewInput(canvas, scale);

            if (_workingTemplate == null || _catalog == null)
                return;

            var time = (float)(EditorApplication.timeSinceStartup - _startedAt);
            var phase = (_templateIndex * 97 + _palette * 31) * 0.0137f;
            var headAngle = _headAngle;
            var footAngle = _footAngle;
            var tailAngle = _tailAngle;
            var bodyAngle = _bodyAngle;
            var bodyLift = 0f;
            if (_dragPreview && !_previewDragging && Mathf.Abs(_dragAmount) > 0.001f)
            {
                headAngle += -_dragAmount * _profile.headDragDegrees;
                footAngle += _dragAmount * _profile.feetDragDegrees;
                tailAngle += _dragAmount * _profile.tailDragDegrees;
                bodyAngle += _dragAmount * _profile.bodyDragDegrees;
            }
            else if (!_previewDragging && !_hasPreviewSpring && _idlePreview && _profile.idleEnabled)
            {
                var wave = Mathf.Sin((time + phase) * Mathf.PI * 2f * _profile.idleCyclesPerSecond);
                var slowWave = Mathf.Sin((time + phase * 0.7f) * Mathf.PI * _profile.idleCyclesPerSecond);
                headAngle += wave * _profile.idleHeadDegrees;
                footAngle += wave * _profile.idleFeetDegrees;
                tailAngle += slowWave * _profile.idleTailDegrees;
                bodyAngle += slowWave * _profile.idleBodyDegrees;
                bodyLift = wave * _profile.idleBodyLift;
            }

            for (var i = 0; i < DrawOrder.Length; i++)
                DrawPart(canvas, scale, DrawOrder[i], headAngle, footAngle, tailAngle, bodyAngle, bodyLift, _previewOffset);

            if (_showGuides)
                DrawGuides(canvas, scale);

            GUI.color = Color.white;
            GUI.Label(
                new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, 24f),
                _workingTemplate.displayName + "  ·  " + PaletteNames[_palette] + "  ·  " + PartLabel(_selectedKind));
            GUI.Label(
                new Rect(rect.x + 14f, rect.yMax - 48f, rect.width - 28f, 22f),
                _editSelected
                    ? "编辑选中部件：在画布中拖拽修改挂点偏移"
                    : _dragPreview
                        ? "预览拖动：按住并拖动怪物，松开后观察阻尼回弹"
                        : "选择右侧部件槽位，打开对应的挂点和支点控制");
        }

        void DrawPreviewInput(Rect canvas, float scale)
        {
            var evt = Event.current;
            if (evt == null)
                return;

            if (evt.type == EventType.MouseDown && evt.button == 0 && canvas.Contains(evt.mousePosition))
            {
                _previewDragging = true;
                _lastPointer = evt.mousePosition;
                _lastPointerTick = EditorApplication.timeSinceStartup;
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDrag && _previewDragging)
            {
                var delta = evt.mousePosition - _lastPointer;
                _lastPointer = evt.mousePosition;
                if (_editSelected)
                {
                    var slot = _workingTemplate.GetSlot(_selectedKind);
                    slot.offset += new Vector2(delta.x / scale, -delta.y / scale);
                    _workingTemplate.SetSlot(_selectedKind, slot);
                    BuildControls();
                }
                else if (_dragPreview)
                {
                    var now = EditorApplication.timeSinceStartup;
                    var deltaTime = Mathf.Max(0.001f, (float)(now - _lastPointerTick));
                    _lastPointerTick = now;
                    _previewOffset += new Vector2(delta.x / scale, -delta.y / scale);
                    var velocity = Vector2.ClampMagnitude(delta / deltaTime, 1200f);
                    var drive = Mathf.Clamp(velocity.x / 700f, -1f, 1f);
                    _targetHeadAngle = -drive * _profile.headDragDegrees;
                    _targetFootAngle = drive * _profile.feetDragDegrees;
                    _targetTailAngle = drive * _profile.tailDragDegrees;
                    _targetBodyAngle = drive * _profile.bodyDragDegrees;
                }
                MarkPreviewDirty();
                evt.Use();
                return;
            }

            if ((evt.type == EventType.MouseUp || evt.type == EventType.Ignore) && _previewDragging)
            {
                _previewDragging = false;
                _targetHeadAngle = 0f;
                _targetFootAngle = 0f;
                _targetTailAngle = 0f;
                _targetBodyAngle = 0f;
                evt.Use();
            }
        }

        void DrawPart(
            Rect canvas,
            float scale,
            MonsterPartKind kind,
            float headAngle,
            float footAngle,
            float tailAngle,
            float bodyAngle,
            float bodyLift,
            Vector2 previewOffset)
        {
            var slot = _workingTemplate.GetSlot(kind);
            if (!slot.enabled)
                return;

            var definition = _catalog.GetPartOrNull(slot.partId);
            if (definition == null || definition.sprite == null)
                return;

            var headDefinition = _catalog.GetPartOrNull(_workingTemplate.head.partId);
            var localPosition = _catalog.ResolveSpritePivotPosition(_workingTemplate, slot, definition, headDefinition);
            var groupAnchor = GroupAnchor(kind);
            var groupAngle = GroupAngle(kind, headAngle, footAngle, tailAngle, bodyAngle);
            var position = RotateAround(localPosition, groupAnchor, groupAngle) + previewOffset;
            if (kind == MonsterPartKind.Body || kind == MonsterPartKind.Hand || kind == MonsterPartKind.Accessory)
                position += Vector2.up * bodyLift;

            var tint = Tint(definition.colorRole, _palette);
            var width = CanvasWidth * Mathf.Max(0.1f, slot.scale.x) * scale;
            var height = CanvasHeight * Mathf.Max(0.1f, slot.scale.y) * scale;
            var pivot = new Vector2(Mathf.Clamp01(slot.pivot.x), Mathf.Clamp01(slot.pivot.y));
            var topLeft = new Vector2(
                canvas.center.x + position.x * scale - width * pivot.x,
                canvas.center.y - position.y * scale - height * (1f - pivot.y));
            var drawRect = new Rect(topLeft.x, topLeft.y, width, height);
            var rotationPivot = new Vector2(
                drawRect.x + drawRect.width * pivot.x,
                drawRect.y + drawRect.height * (1f - pivot.y));

            var previousMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(slot.rotation + groupAngle, rotationPivot);
            GUI.color = tint;
            var texture = definition.sprite.texture;
            var uv = new Rect(
                definition.sprite.textureRect.x / texture.width,
                definition.sprite.textureRect.y / texture.height,
                definition.sprite.textureRect.width / texture.width,
                definition.sprite.textureRect.height / texture.height);
            GUI.DrawTextureWithTexCoords(drawRect, texture, uv, true);
            GUI.matrix = previousMatrix;
        }

        void DrawGuides(Rect canvas, float scale)
        {
            Handles.BeginGUI();
            Handles.color = new Color(0.25f, 0.92f, 0.73f, 0.85f);
            DrawCross(CanvasPoint(canvas, _workingTemplate.anchors.neck, scale), 5f);
            DrawCross(CanvasPoint(canvas, _workingTemplate.anchors.face, scale), 5f);
            DrawCross(CanvasPoint(canvas, _workingTemplate.anchors.hand, scale), 5f);
            DrawCross(CanvasPoint(canvas, _workingTemplate.anchors.foot, scale), 5f);
            DrawCross(CanvasPoint(canvas, _workingTemplate.anchors.tail, scale), 5f);
            Handles.color = new Color(1f, 0.75f, 0.25f, 0.6f);
            Handles.DrawLine(
                new Vector3(canvas.x, canvas.center.y + CanvasHeight * 0.5f * scale),
                new Vector3(canvas.xMax, canvas.center.y + CanvasHeight * 0.5f * scale));
            Handles.EndGUI();
        }

        static Vector2 CanvasPoint(Rect canvas, Vector2 local, float scale) =>
            new Vector2(canvas.center.x + local.x * scale, canvas.center.y - local.y * scale);

        static void DrawCross(Vector2 point, float radius)
        {
            Handles.DrawLine(point - Vector2.right * radius, point + Vector2.right * radius);
            Handles.DrawLine(point - Vector2.up * radius, point + Vector2.up * radius);
        }

        Vector2 GroupAnchor(MonsterPartKind kind)
        {
            switch (kind)
            {
                case MonsterPartKind.Head:
                case MonsterPartKind.Eye:
                case MonsterPartKind.Mouth:
                case MonsterPartKind.Hat:
                    return _catalog.ResolveGroupPivot(_workingTemplate, MonsterPartKind.Head);
                case MonsterPartKind.Foot:
                    return _catalog.ResolveGroupPivot(_workingTemplate, MonsterPartKind.Foot);
                case MonsterPartKind.Tail:
                    return _catalog.ResolveGroupPivot(_workingTemplate, MonsterPartKind.Tail);
                default:
                    return _catalog.ResolveGroupPivot(_workingTemplate, MonsterPartKind.Body);
            }
        }

        static float GroupAngle(MonsterPartKind kind, float head, float foot, float tail, float body)
        {
            switch (kind)
            {
                case MonsterPartKind.Head:
                case MonsterPartKind.Eye:
                case MonsterPartKind.Mouth:
                case MonsterPartKind.Hat:
                    return head;
                case MonsterPartKind.Foot:
                    return foot;
                case MonsterPartKind.Tail:
                    return tail;
                default:
                    return body;
            }
        }

        static Vector2 RotateAround(Vector2 point, Vector2 pivot, float angle)
        {
            if (Mathf.Approximately(angle, 0f))
                return point;

            var radians = angle * Mathf.Deg2Rad;
            var sin = Mathf.Sin(radians);
            var cos = Mathf.Cos(radians);
            var delta = point - pivot;
            return pivot + new Vector2(delta.x * cos - delta.y * sin, delta.x * sin + delta.y * cos);
        }

        static Color Tint(MonsterColorRole role, int palette)
        {
            if (role == MonsterColorRole.None)
                return Color.white;

            var primary = MonsterPortrait.Palette[Mathf.Clamp(palette, 0, MonsterPortrait.Palette.Length - 1)];
            return role == MonsterColorRole.Primary
                ? primary
                : Color.Lerp(primary, Color.white, 0.28f);
        }

        void SelectTemplate(int index)
        {
            if (_catalog == null || TemplateCount == 0)
                return;

            _templateIndex = Mathf.Clamp(index, 0, TemplateCount - 1);
            _sourceTemplate = _catalog.templates[_templateIndex];
            _workingTemplate = _sourceTemplate == null ? null : _sourceTemplate.Clone();
            _selectedKind = MonsterPartKind.Body;
            if (rootVisualElement != null)
                CreateGUI();
            MarkPreviewDirty();
        }

        void ResetTemplate()
        {
            _workingTemplate = _sourceTemplate == null ? null : _sourceTemplate.Clone();
            BuildControls();
            MarkPreviewDirty();
        }

        void SaveTemplate()
        {
            if (_sourceTemplate == null || _workingTemplate == null)
                return;

            CopyTemplate(_workingTemplate, _sourceTemplate);
            EditorUtility.SetDirty(_catalog);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("模板已保存到 MonsterAssemblyCatalog.asset"));
            BuildControls();
        }

        void SaveAsTemplate()
        {
            if (_workingTemplate == null)
                return;

            var id = "template_custom_01";
            var suffix = 1;
            while (_catalog.GetTemplateOrNull(id) != null)
                id = "template_custom_" + (++suffix).ToString("00");
            var clone = MonsterAssemblyCatalogEditor.CreateTemplate(
                _catalog,
                id,
                _workingTemplate.displayName + " 副本",
                _workingTemplate);
            _templateIndex = _catalog.templates.IndexOf(clone);
            _sourceTemplate = clone;
            _workingTemplate = clone.Clone();
            ShowNotification(new GUIContent("已创建新模板 " + id));
            CreateGUI();
        }

        void MarkPreviewDirty()
        {
            _preview?.MarkDirtyRepaint();
            Repaint();
        }

        void RepaintPreview()
        {
            if (_preview == null)
                return;

            var now = EditorApplication.timeSinceStartup;
            var deltaTime = Mathf.Clamp((float)(now - _lastPreviewTick), 0f, 0.05f);
            _lastPreviewTick = now;
            AdvancePreviewSpring(deltaTime);
            _preview.MarkDirtyRepaint();
            Repaint();
        }

        bool _hasPreviewSpring =>
            _previewDragging
            || _previewOffset.sqrMagnitude > 0.0001f
            || _previewOffsetVelocity.sqrMagnitude > 0.0001f
            || Mathf.Abs(_headAngle) > 0.01f
            || Mathf.Abs(_headVelocity) > 0.01f
            || Mathf.Abs(_footAngle) > 0.01f
            || Mathf.Abs(_footVelocity) > 0.01f
            || Mathf.Abs(_tailAngle) > 0.01f
            || Mathf.Abs(_tailVelocity) > 0.01f
            || Mathf.Abs(_bodyAngle) > 0.01f
            || Mathf.Abs(_bodyVelocity) > 0.01f;

        void AdvancePreviewSpring(float deltaTime)
        {
            if (deltaTime <= 0f || _profile == null)
                return;

            StepSpring(ref _headAngle, ref _headVelocity, _targetHeadAngle, deltaTime);
            StepSpring(ref _footAngle, ref _footVelocity, _targetFootAngle, deltaTime);
            StepSpring(ref _tailAngle, ref _tailVelocity, _targetTailAngle, deltaTime);
            StepSpring(ref _bodyAngle, ref _bodyVelocity, _targetBodyAngle, deltaTime);

            if (!_previewDragging)
            {
                StepSpring(ref _previewOffset.x, ref _previewOffsetVelocity.x, 0f, deltaTime);
                StepSpring(ref _previewOffset.y, ref _previewOffsetVelocity.y, 0f, deltaTime);
                if (_previewOffset.sqrMagnitude < 0.0025f && _previewOffsetVelocity.sqrMagnitude < 0.0025f)
                {
                    _previewOffset = Vector2.zero;
                    _previewOffsetVelocity = Vector2.zero;
                }
            }
        }

        void StepSpring(ref float value, ref float velocity, float target, float deltaTime)
        {
            var acceleration = (target - value) * _profile.springStiffness - velocity * _profile.springDamping;
            velocity += acceleration * deltaTime;
            value += velocity * deltaTime;
        }

        static void CopyTemplate(MonsterAssemblyTemplate source, MonsterAssemblyTemplate target)
        {
            target.displayName = source.displayName;
            target.anchors = source.anchors == null
                ? new MonsterAnchorSet()
                : new MonsterAnchorSet
                {
                    neck = source.anchors.neck,
                    face = source.anchors.face,
                    hand = source.anchors.hand,
                    foot = source.anchors.foot,
                    tail = source.anchors.tail,
                    body = source.anchors.body,
                };
            target.body = source.body;
            target.head = source.head;
            target.eye = source.eye;
            target.mouth = source.mouth;
            target.hand = source.hand;
            target.foot = source.foot;
            target.tail = source.tail;
            target.hat = source.hat;
            target.accessory = source.accessory;
        }

        static string TemplateLabel(MonsterAssemblyTemplate template, int index) =>
            (template == null ? "模板 " + (index + 1) : template.displayName)
            + "  [" + (template == null ? "缺失" : template.id) + "]";

        static string PartLabel(MonsterPartKind kind)
        {
            switch (kind)
            {
                case MonsterPartKind.Body: return "身体";
                case MonsterPartKind.Head: return "头";
                case MonsterPartKind.Eye: return "眼";
                case MonsterPartKind.Mouth: return "嘴";
                case MonsterPartKind.Hand: return "手";
                case MonsterPartKind.Foot: return "脚";
                case MonsterPartKind.Tail: return "尾巴";
                case MonsterPartKind.Hat: return "头饰";
                case MonsterPartKind.Accessory: return "配饰";
                default: return kind.ToString();
            }
        }
    }
}
