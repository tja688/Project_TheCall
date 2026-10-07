using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheCall
{
    public enum MonsterColorRole
    {
        None,
        Primary,
        Secondary,
    }

    public enum MonsterAnchorKind
    {
        Canvas,
        Neck,
        Face,
        Hand,
        Foot,
        Tail,
        Body,
    }

    [Serializable]
    public sealed class MonsterPartDefinition
    {
        public string id;
        public MonsterPartKind kind;
        public Sprite sprite;
        public MonsterColorRole colorRole = MonsterColorRole.None;
        public string connectionType;
        [Tooltip("归一化连接点，原点在图片左下。此处与模板挂点对齐。")]
        public Vector2 attachmentPoint = new Vector2(0.5f, 0.5f);
        [Tooltip("部件运动时使用的动画支点，原点在图片左下。")]
        public Vector2 defaultPivot = new Vector2(0.5f, 0.5f);
        [Tooltip("头部专用：脸部排布原点相对头部连接点的像素偏移。")]
        public Vector2 faceAnchor;
        [Tooltip("头部专用：五官基准偏移，眼和嘴槽位可分别微调。")]
        public Vector2 eyeOffset;
        public Vector2 mouthOffset;
        public bool optional;
        public int sortOrder;
        public Vector2 defaultScale = Vector2.one;
        public float defaultRotation;
    }

    [Serializable]
    public struct MonsterPartSlot
    {
        public string partId;
        public MonsterAnchorKind anchor;
        public Vector2 offset;
        public Vector2 pivot;
        public Vector2 scale;
        public float rotation;
        public bool enabled;
        public string requiredConnection;

        public static MonsterPartSlot Create(string partId, MonsterAnchorKind anchor, Vector2 offset)
        {
            return new MonsterPartSlot
            {
                partId = partId,
                anchor = anchor,
                offset = offset,
                pivot = new Vector2(0.5f, 0.5f),
                scale = Vector2.one,
                rotation = 0f,
                enabled = !string.IsNullOrEmpty(partId),
            };
        }
    }

    [Serializable]
    public sealed class MonsterAnchorSet
    {
        public Vector2 neck;
        public Vector2 face;
        public Vector2 hand;
        public Vector2 foot;
        public Vector2 tail;
        public Vector2 body;

        public Vector2 Get(MonsterAnchorKind kind)
        {
            switch (kind)
            {
                case MonsterAnchorKind.Neck: return neck;
                case MonsterAnchorKind.Face: return face;
                case MonsterAnchorKind.Hand: return hand;
                case MonsterAnchorKind.Foot: return foot;
                case MonsterAnchorKind.Tail: return tail;
                case MonsterAnchorKind.Body: return body;
                default: return Vector2.zero;
            }
        }
    }

    [Serializable]
    public sealed class MonsterAssemblyTemplate
    {
        public string id;
        public string displayName;
        public MonsterAnchorSet anchors = new MonsterAnchorSet();
        public MonsterPartSlot body = MonsterPartSlot.Create(null, MonsterAnchorKind.Canvas, Vector2.zero);
        public MonsterPartSlot head = MonsterPartSlot.Create(null, MonsterAnchorKind.Neck, Vector2.zero);
        public MonsterPartSlot eye = MonsterPartSlot.Create(null, MonsterAnchorKind.Face, Vector2.zero);
        public MonsterPartSlot mouth = MonsterPartSlot.Create(null, MonsterAnchorKind.Face, Vector2.zero);
        public MonsterPartSlot hand = MonsterPartSlot.Create(null, MonsterAnchorKind.Hand, Vector2.zero);
        public MonsterPartSlot foot = MonsterPartSlot.Create(null, MonsterAnchorKind.Foot, Vector2.zero);
        public MonsterPartSlot tail = MonsterPartSlot.Create(null, MonsterAnchorKind.Tail, Vector2.zero);
        public MonsterPartSlot hat = MonsterPartSlot.Create(null, MonsterAnchorKind.Face, Vector2.zero);
        public MonsterPartSlot accessory = MonsterPartSlot.Create(null, MonsterAnchorKind.Body, Vector2.zero);

        public MonsterPartSlot GetSlot(MonsterPartKind kind)
        {
            switch (kind)
            {
                case MonsterPartKind.Body: return body;
                case MonsterPartKind.Head: return head;
                case MonsterPartKind.Eye: return eye;
                case MonsterPartKind.Mouth: return mouth;
                case MonsterPartKind.Hand: return hand;
                case MonsterPartKind.Foot: return foot;
                case MonsterPartKind.Tail: return tail;
                case MonsterPartKind.Hat: return hat;
                case MonsterPartKind.Accessory: return accessory;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public void SetSlot(MonsterPartKind kind, MonsterPartSlot slot)
        {
            switch (kind)
            {
                case MonsterPartKind.Body: body = slot; break;
                case MonsterPartKind.Head: head = slot; break;
                case MonsterPartKind.Eye: eye = slot; break;
                case MonsterPartKind.Mouth: mouth = slot; break;
                case MonsterPartKind.Hand: hand = slot; break;
                case MonsterPartKind.Foot: foot = slot; break;
                case MonsterPartKind.Tail: tail = slot; break;
                case MonsterPartKind.Hat: hat = slot; break;
                case MonsterPartKind.Accessory: accessory = slot; break;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        public MonsterAssemblyTemplate Clone()
        {
            return new MonsterAssemblyTemplate
            {
                id = id,
                displayName = displayName,
                anchors = new MonsterAnchorSet
                {
                    neck = anchors == null ? Vector2.zero : anchors.neck,
                    face = anchors == null ? Vector2.zero : anchors.face,
                    hand = anchors == null ? Vector2.zero : anchors.hand,
                    foot = anchors == null ? Vector2.zero : anchors.foot,
                    tail = anchors == null ? Vector2.zero : anchors.tail,
                    body = anchors == null ? Vector2.zero : anchors.body,
                },
                body = body,
                head = head,
                eye = eye,
                mouth = mouth,
                hand = hand,
                foot = foot,
                tail = tail,
                hat = hat,
                accessory = accessory,
            };
        }
    }

    [CreateAssetMenu(fileName = "MonsterAssemblyCatalog", menuName = "The Call/Monster Assembly Catalog")]
    public sealed class MonsterAssemblyCatalog : ScriptableObject
    {
        public const string ResourceName = "MonsterAssemblyCatalog";
        public static readonly Vector2 CanvasSize = new Vector2(142f, 102f);

        [HideInInspector] public int schemaVersion;
        public List<MonsterPartDefinition> parts = new List<MonsterPartDefinition>();
        public List<MonsterAssemblyTemplate> templates = new List<MonsterAssemblyTemplate>();

        static MonsterAssemblyCatalog _runtime;

        public static MonsterAssemblyCatalog Runtime
        {
            get
            {
                if (_runtime != null)
                    return _runtime;

                _runtime = Resources.Load<MonsterAssemblyCatalog>(ResourceName);
                if (_runtime != null)
                    return _runtime;

                _runtime = CreateFallback();
                return _runtime;
            }
        }

        public bool TryGetPart(string id, out MonsterPartDefinition definition)
        {
            if (!string.IsNullOrEmpty(id) && parts != null)
            {
                for (var i = 0; i < parts.Count; i++)
                {
                    var candidate = parts[i];
                    if (candidate != null && string.Equals(candidate.id, id, StringComparison.Ordinal))
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        public MonsterPartDefinition GetPartOrNull(string id)
        {
            TryGetPart(id, out var definition);
            return definition;
        }

        public bool IsCompatible(MonsterPartSlot slot, MonsterPartDefinition definition) =>
            definition != null
            && (string.IsNullOrEmpty(slot.requiredConnection)
                || string.IsNullOrEmpty(definition.connectionType)
                || string.Equals(slot.requiredConnection, definition.connectionType, StringComparison.Ordinal));

        public MonsterAssemblyTemplate GetTemplateOrNull(string id)
        {
            if (templates == null)
                return null;

            for (var i = 0; i < templates.Count; i++)
            {
                var candidate = templates[i];
                if (candidate != null && string.Equals(candidate.id, id, StringComparison.Ordinal))
                    return candidate;
            }

            return null;
        }

        public MonsterAssemblyTemplate GetTemplateOrFallback(int recipe)
        {
            var template = GetTemplateOrNull(MonsterAppearance.TemplateIdForRecipe(recipe));
            if (template != null)
                return template;

            if (templates != null && templates.Count > 0)
                return templates[Mathf.Clamp(recipe, 0, templates.Count - 1)];

            return null;
        }

        public MonsterAssemblyTemplate GetTemplateOrFallback(string templateId, int recipe)
        {
            var template = GetTemplateOrNull(templateId);
            return template != null ? template : GetTemplateOrFallback(recipe);
        }

        public MonsterPartSlot ResolveSlot(
            MonsterAssemblyTemplate template,
            MonsterPartKind kind,
            string appearancePartId,
            bool hasExplicitParts = false)
        {
            var slot = template == null ? MonsterPartSlot.Create(null, MonsterAnchorKind.Canvas, Vector2.zero) : template.GetSlot(kind);
            var hasAppearanceSelection = !string.IsNullOrEmpty(appearancePartId);
            if (hasExplicitParts)
            {
                slot.partId = hasAppearanceSelection ? appearancePartId : null;
                slot.enabled = hasAppearanceSelection;
            }
            else if (hasAppearanceSelection)
            {
                slot.partId = appearancePartId;
                slot.enabled = true;
            }

            if (!hasAppearanceSelection)
                slot.enabled = slot.enabled && !string.IsNullOrEmpty(slot.partId);
            return slot;
        }

        public Vector2 ResolvePosition(
            MonsterAssemblyTemplate template,
            MonsterPartSlot slot,
            MonsterPartDefinition headDefinition = null,
            MonsterPartKind? partKind = null)
        {
            if (template == null || template.anchors == null)
                return slot.offset;

            var anchor = template.anchors.Get(slot.anchor);
            if (slot.anchor == MonsterAnchorKind.Face)
            {
                anchor += template.anchors.neck + template.head.offset;
                if (headDefinition != null)
                {
                    anchor += headDefinition.faceAnchor;
                    if (partKind == MonsterPartKind.Eye)
                    {
                        anchor += headDefinition.eyeOffset;
                    }
                    else if (partKind == MonsterPartKind.Mouth)
                        anchor += headDefinition.mouthOffset;
                }
            }

            return anchor + slot.offset;
        }

        public Vector2 ResolveGroupPivot(MonsterAssemblyTemplate template, MonsterPartKind kind)
        {
            if (template == null || template.anchors == null)
                return Vector2.zero;

            switch (kind)
            {
                case MonsterPartKind.Head: return template.anchors.neck + template.head.offset;
                case MonsterPartKind.Foot: return ResolvePosition(template, template.foot);
                case MonsterPartKind.Tail: return ResolvePosition(template, template.tail);
                default: return template.anchors.body;
            }
        }

        public Vector2 ResolveSpritePivotPosition(
            MonsterAssemblyTemplate template,
            MonsterPartSlot slot,
            MonsterPartDefinition definition,
            MonsterPartDefinition headDefinition = null)
        {
            var attachment = definition == null
                ? new Vector2(0.5f, 0.5f)
                : definition.attachmentPoint;
            var delta = new Vector2(
                (slot.pivot.x - attachment.x) * CanvasSize.x,
                (slot.pivot.y - attachment.y) * CanvasSize.y);
            return ResolvePosition(template, slot, headDefinition, definition == null ? (MonsterPartKind?)null : definition.kind) + delta;
        }

        static MonsterAssemblyCatalog CreateFallback()
        {
            var catalog = CreateInstance<MonsterAssemblyCatalog>();
            catalog.hideFlags = HideFlags.HideAndDontSave;
            MonsterPartLibrary.Ensure();
            AddFallbackParts(catalog, MonsterPartKind.Body, "body_", MonsterPartLibrary.Body, MonsterColorRole.Primary, 10);
            AddFallbackParts(catalog, MonsterPartKind.Head, "head_", MonsterPartLibrary.Head, MonsterColorRole.Primary, 40);
            AddFallbackParts(catalog, MonsterPartKind.Eye, "eye_", MonsterPartLibrary.Eye, MonsterColorRole.None, 50);
            AddFallbackParts(catalog, MonsterPartKind.Mouth, "mouth_", MonsterPartLibrary.Mouth, MonsterColorRole.None, 60);
            AddFallbackParts(catalog, MonsterPartKind.Hand, "hand_", MonsterPartLibrary.Hand, MonsterColorRole.Primary, 20);
            AddFallbackParts(catalog, MonsterPartKind.Foot, "foot_", MonsterPartLibrary.Foot, MonsterColorRole.Primary, 30);
            AddFallbackParts(catalog, MonsterPartKind.Tail, "tail_", MonsterPartLibrary.Tail, MonsterColorRole.Primary, 0, true);
            AddFallbackParts(catalog, MonsterPartKind.Hat, "hat_", MonsterPartLibrary.Hat, MonsterColorRole.None, 70, true);
            AddFallbackParts(catalog, MonsterPartKind.Accessory, "accessory_", MonsterPartLibrary.Accessory, MonsterColorRole.Secondary, 35, true);
            AddFallbackTemplates(catalog);
            ConfigureSeedGeometry(catalog);
            return catalog;
        }

        static void AddFallbackParts(
            MonsterAssemblyCatalog catalog,
            MonsterPartKind kind,
            string prefix,
            Sprite[] sprites,
            MonsterColorRole colorRole,
            int sortOrder,
            bool optional = false)
        {
            if (sprites == null)
                return;

            for (var i = 0; i < sprites.Length; i++)
            {
                var part = new MonsterPartDefinition
                {
                    id = prefix + (i + 1).ToString("00"),
                    kind = kind,
                    sprite = sprites[i],
                    colorRole = colorRole,
                    connectionType = DefaultConnectionType(kind),
                    attachmentPoint = DefaultAttachmentPoint(kind),
                    optional = optional,
                    sortOrder = sortOrder,
                    defaultPivot = new Vector2(0.5f, 0.5f),
                    defaultScale = Vector2.one,
                };
                ConfigureDefaultHeadLayout(part);
                catalog.parts.Add(part);
            }
        }

        static void AddFallbackTemplates(MonsterAssemblyCatalog catalog)
        {
            for (var i = 0; i < MonsterAppearance.RecipeCount; i++)
            {
                var template = new MonsterAssemblyTemplate
                {
                    id = MonsterAppearance.TemplateIdForRecipe(i),
                    displayName = "体型 " + (i + 1),
                    anchors = new MonsterAnchorSet(),
                    body = MonsterPartSlot.Create("body_" + (i + 1).ToString("00"), MonsterAnchorKind.Canvas, Vector2.zero),
                    head = MonsterPartSlot.Create("head_" + (i % 4 + 1).ToString("00"), MonsterAnchorKind.Neck, Vector2.zero),
                    eye = MonsterPartSlot.Create("eye_" + (i % 5 + 1).ToString("00"), MonsterAnchorKind.Face, Vector2.zero),
                    mouth = MonsterPartSlot.Create("mouth_" + (i % 6 + 1).ToString("00"), MonsterAnchorKind.Face, Vector2.zero),
                    hand = MonsterPartSlot.Create("hand_" + (i % 5 + 1).ToString("00"), MonsterAnchorKind.Hand, Vector2.zero),
                    foot = MonsterPartSlot.Create("foot_" + (i % 3 + 1).ToString("00"), MonsterAnchorKind.Foot, Vector2.zero),
                    tail = MonsterPartSlot.Create(i == 2 ? "tail_03" : null, MonsterAnchorKind.Tail, Vector2.zero),
                    hat = MonsterPartSlot.Create(i == 0 || i == 3 ? "hat_" + (i + 1).ToString("00") : null, MonsterAnchorKind.Face, Vector2.zero),
                    accessory = MonsterPartSlot.Create(i == 1 || i == 4 ? "accessory_02" : null, MonsterAnchorKind.Body, Vector2.zero),
                };
                SetConnectionRequirements(template);
                catalog.templates.Add(template);
            }
        }

        public static Vector2 DefaultAttachmentPoint(MonsterPartKind kind)
        {
            switch (kind)
            {
                case MonsterPartKind.Head: return new Vector2(0.5f, 0.16f);
                case MonsterPartKind.Hand:
                case MonsterPartKind.Foot: return new Vector2(0.5f, 0.8f);
                case MonsterPartKind.Tail: return new Vector2(0.38f, 0.5f);
                default: return new Vector2(0.5f, 0.5f);
            }
        }

        public static string DefaultConnectionType(MonsterPartKind kind)
        {
            switch (kind)
            {
                case MonsterPartKind.Body: return "body.core";
                case MonsterPartKind.Head: return "neck.standard";
                case MonsterPartKind.Eye:
                case MonsterPartKind.Mouth: return "face.standard";
                case MonsterPartKind.Hand: return "arm.standard";
                case MonsterPartKind.Foot: return "leg.standard";
                case MonsterPartKind.Tail: return "tail.standard";
                case MonsterPartKind.Hat: return "crown.standard";
                case MonsterPartKind.Accessory: return "body.attach";
                default: return string.Empty;
            }
        }

        public static void SetConnectionRequirements(MonsterAssemblyTemplate template)
        {
            if (template == null)
                return;

            template.body.requiredConnection = DefaultConnectionType(MonsterPartKind.Body);
            template.head.requiredConnection = DefaultConnectionType(MonsterPartKind.Head);
            template.eye.requiredConnection = DefaultConnectionType(MonsterPartKind.Eye);
            template.mouth.requiredConnection = DefaultConnectionType(MonsterPartKind.Mouth);
            template.hand.requiredConnection = DefaultConnectionType(MonsterPartKind.Hand);
            template.foot.requiredConnection = DefaultConnectionType(MonsterPartKind.Foot);
            template.tail.requiredConnection = DefaultConnectionType(MonsterPartKind.Tail);
            template.hat.requiredConnection = DefaultConnectionType(MonsterPartKind.Hat);
            template.accessory.requiredConnection = DefaultConnectionType(MonsterPartKind.Accessory);
        }

        public static void ConfigureDefaultHeadLayout(MonsterPartDefinition part)
        {
            if (part == null || part.kind != MonsterPartKind.Head)
                return;

            if (part.id == "head_01")
            {
                part.faceAnchor = new Vector2(18f, 33f);
                part.eyeOffset = new Vector2(0f, 6f);
                part.mouthOffset = new Vector2(0f, -7f);
            }
            else if (part.id == "head_02")
            {
                part.faceAnchor = new Vector2(0f, 36f);
                part.eyeOffset = new Vector2(0f, 5f);
                part.mouthOffset = new Vector2(0f, -9f);
            }
            else if (part.id == "head_03")
            {
                part.faceAnchor = new Vector2(0f, 35f);
                part.eyeOffset = new Vector2(0f, 5f);
                part.mouthOffset = new Vector2(0f, -9f);
            }
            else if (part.id == "head_04")
            {
                part.faceAnchor = new Vector2(0f, 35f);
                part.eyeOffset = new Vector2(0f, 4f);
                part.mouthOffset = new Vector2(0f, -9f);
            }
        }

        public static void ConfigureSeedGeometry(MonsterAssemblyCatalog catalog)
        {
            if (catalog == null || catalog.templates == null)
                return;

            var necks = new[]
            {
                new Vector2(0f, 12f),
                new Vector2(0f, 24f),
                new Vector2(0f, 21f),
                new Vector2(0f, 16f),
                new Vector2(0f, 18f),
                new Vector2(0f, 29f),
            };
            for (var i = 0; i < RecipeCount; i++)
            {
                var template = catalog.GetTemplateOrNull(MonsterAppearance.TemplateIdForRecipe(i));
                if (template == null)
                    continue;

                template.anchors ??= new MonsterAnchorSet();
                template.displayName = SeedTemplateName(i);
                SetConnectionRequirements(template);
                template.anchors.neck = necks[i];
                template.anchors.face = Vector2.zero;
                template.anchors.hand = new Vector2(i == 1 || i == 5 ? -38f : -31f, 0f);
                template.anchors.foot = new Vector2(0f, -31f);
                template.anchors.tail = new Vector2(35f, -4f);
                template.anchors.body = new Vector2(0f, 0f);

                var eye = template.eye;
                eye.anchor = MonsterAnchorKind.Face;
                eye.offset = Vector2.zero;
                template.eye = eye;
                var mouth = template.mouth;
                mouth.anchor = MonsterAnchorKind.Face;
                mouth.offset = Vector2.zero;
                template.mouth = mouth;
                var hat = template.hat;
                hat.anchor = MonsterAnchorKind.Face;
                hat.offset = new Vector2(0f, 29f);
                template.hat = hat;
            }
        }

        static string SeedTemplateName(int index)
        {
            switch (index)
            {
                case 0: return "直立·脑袋";
                case 1: return "曲身·绷带";
                case 2: return "刺团·宽脸";
                case 3: return "双团·补丁";
                case 4: return "短身·脑袋";
                default: return "高身·绷带";
            }
        }

        const int RecipeCount = 6;
    }
}
