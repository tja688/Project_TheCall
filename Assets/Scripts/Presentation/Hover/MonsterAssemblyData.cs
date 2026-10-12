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

}
