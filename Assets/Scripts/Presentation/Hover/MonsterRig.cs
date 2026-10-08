using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheCall
{
    public enum MonsterPartRole
    {
        Primary,
        SubPrimary,
        Secondary,
    }

    public enum MonsterFacing
    {
        None,
        Left,
        Right,
    }

    public enum MonsterSocketGroup
    {
        Head,
        Hand,
        Foot,
        BodyTrim,
        Tail,
        Neck,
        Eye,
        HeadTrim,
        Mouth,
    }

    public enum MonsterLayerBand
    {
        Default = 0,
        AlwaysBack = 1,
        Tail = 2,
        Accessory = 3,
        Foot = 4,
        Hand = 5,
        Body = 6,
        Hat = 7,
        Head = 8,
        Mouth = 9,
        Eye = 10,
        AlwaysFront = 11,
    }

    [Serializable]
    public sealed class MonsterRigSocket
    {
        public string id;
        public MonsterSocketGroup group;
        public int x;
        public int y;
        public MonsterFacing accepts;
    }

    [Serializable]
    public sealed class MonsterRigPart
    {
        public string id;
        public int attachX;
        public int attachY;
        public MonsterFacing nativeFacing;
        public float swingDegrees;
        public float dragDegrees;
        public float inertia = 1f;
        public bool headTurn;
        public string mountSocketId;
        public int nextSocket = 1;
        public MonsterLayerBand layerBand;
        public List<string> excluded = new List<string>();
        public List<MonsterRigSocket> sockets = new List<MonsterRigSocket>();
    }

    [Serializable]
    public sealed class MonsterRigFile
    {
        public int version = 2;
        public List<MonsterRigPart> parts = new List<MonsterRigPart>();

        /// <summary>
        /// v1 没有拖拽角度和惯性：按部件类型补上默认值。
        /// 已经写过的待机摆动（非零）保留，零则换成该类型的默认摆动。
        /// </summary>
        public void Upgrade()
        {
            if (version >= 2)
                return;

            if (parts != null)
            {
                for (var i = 0; i < parts.Count; i++)
                {
                    var part = parts[i];
                    if (part == null || !MonsterRigCatalog.TrySplitId(part.id, out var folder, out _))
                        continue;
                    if (!MonsterRigCatalog.TryFolder(folder, out var spec))
                        continue;

                    var swing = part.swingDegrees;
                    MonsterRigEdits.ApplyKindDefaults(part, spec.Kind);
                    if (swing != 0f)
                        part.swingDegrees = swing;
                }
            }

            version = 2;
        }
    }

    public sealed class MonsterRigAssignment
    {
        public string socketId;
        public string partId;
    }

    public sealed class MonsterFolderSpec
    {
        public string Folder;
        public MonsterPartRole Role;
        public MonsterPartKind Kind;
        public MonsterColorRole Color;
        public MonsterSocketGroup[] Groups;
    }

    public sealed class MonsterSocketGroupSpec
    {
        public MonsterSocketGroup Group;
        public string Label;
        public string Color;
        public bool Sided;
        public bool Mount;
        public MonsterPartKind Accepts;
    }

    public sealed class MonsterRigPiece
    {
        public MonsterRigPart Data;
        public string Folder;
        public int Width;
        public int Height;
        public MonsterPartRole Role;
        public MonsterPartKind Kind;
        public MonsterColorRole Color;
        public bool Missing;
    }

    public sealed class MonsterRigNode
    {
        public string PartId;
        public string SocketId;
        public int Width;
        public int Height;
        public float AttachX;
        public float AttachY;
        public float WorldAttachX;
        public float WorldAttachY;
        public float WorldRotation;
        public bool WorldMirror;
        public int Order;
        public MonsterPartKind Kind;
        public MonsterColorRole Color;
        public int PaletteIndex = -1;
        public int Bone = -1;
    }

    public sealed class MonsterRigPin
    {
        public string PartId;
        public string SocketId;
        public MonsterSocketGroup Group;
        public MonsterFacing Accepts;
        public float WorldX;
        public float WorldY;
        public bool Mount;
    }

    public sealed class MonsterRigPose
    {
        public readonly List<MonsterRigNode> Nodes = new List<MonsterRigNode>();
        public readonly List<MonsterRigPin> Pins = new List<MonsterRigPin>();
        public readonly List<string> Warnings = new List<string>();
    }

    public struct SpritePlacement
    {
        public Vector2 Size;
        public Vector2 Pivot;
        public Vector2 AnchoredPosition;
        public float Rotation;
        public Vector3 Scale;
    }

    public static class MonsterRigCatalog
    {
        public const string ArtRoot = "Assets/Arts/Others/怪物部位";

        public static readonly MonsterFolderSpec[] Folders =
        {
            Spec("身体", MonsterPartRole.Primary, MonsterPartKind.Body, MonsterColorRole.Primary,
                MonsterSocketGroup.Head, MonsterSocketGroup.Hand, MonsterSocketGroup.Foot,
                MonsterSocketGroup.BodyTrim, MonsterSocketGroup.Tail),
            Spec("头", MonsterPartRole.Primary, MonsterPartKind.Head, MonsterColorRole.Primary,
                MonsterSocketGroup.Neck, MonsterSocketGroup.Eye, MonsterSocketGroup.HeadTrim,
                MonsterSocketGroup.Mouth),
            Spec("手", MonsterPartRole.SubPrimary, MonsterPartKind.Hand, MonsterColorRole.Primary),
            Spec("脚", MonsterPartRole.SubPrimary, MonsterPartKind.Foot, MonsterColorRole.Primary),
            Spec("眼", MonsterPartRole.SubPrimary, MonsterPartKind.Eye, MonsterColorRole.None),
            Spec("尾巴", MonsterPartRole.Secondary, MonsterPartKind.Tail, MonsterColorRole.Primary),
            Spec("配饰", MonsterPartRole.Secondary, MonsterPartKind.Accessory, MonsterColorRole.Secondary),
            Spec("头饰", MonsterPartRole.Secondary, MonsterPartKind.Hat, MonsterColorRole.None),
            Spec("嘴", MonsterPartRole.Secondary, MonsterPartKind.Mouth, MonsterColorRole.None),
        };

        public static readonly MonsterSocketGroupSpec[] Groups =
        {
            Group(MonsterSocketGroup.Head, "头部", "#e07a5f", false, false, MonsterPartKind.Head),
            Group(MonsterSocketGroup.Hand, "手部", "#7dba8a", true, false, MonsterPartKind.Hand),
            Group(MonsterSocketGroup.Foot, "脚部", "#6aa6d6", true, false, MonsterPartKind.Foot),
            Group(MonsterSocketGroup.BodyTrim, "身体配饰", "#e2b656", false, false, MonsterPartKind.Accessory),
            Group(MonsterSocketGroup.Tail, "尾巴", "#d48a3a", false, false, MonsterPartKind.Tail),
            Group(MonsterSocketGroup.Neck, "身体对接", "#d6c15a", false, true, MonsterPartKind.Body),
            Group(MonsterSocketGroup.Eye, "眼部", "#8e7cc3", true, false, MonsterPartKind.Eye),
            Group(MonsterSocketGroup.HeadTrim, "头部配饰", "#d67ab0", false, false, MonsterPartKind.Hat),
            Group(MonsterSocketGroup.Mouth, "嘴部", "#d86a6a", false, false, MonsterPartKind.Mouth),
        };

        public static bool TryFolder(string folder, out MonsterFolderSpec spec)
        {
            for (var i = 0; i < Folders.Length; i++)
            {
                if (string.Equals(Folders[i].Folder, folder, StringComparison.Ordinal))
                {
                    spec = Folders[i];
                    return true;
                }
            }

            spec = null;
            return false;
        }

        public static MonsterSocketGroupSpec GroupSpec(MonsterSocketGroup group)
        {
            for (var i = 0; i < Groups.Length; i++)
            {
                if (Groups[i].Group == group)
                    return Groups[i];
            }

            return null;
        }

        public static bool FolderAllows(MonsterFolderSpec folder, MonsterSocketGroup group)
        {
            if (folder == null || folder.Groups == null)
                return false;

            for (var i = 0; i < folder.Groups.Length; i++)
            {
                if (folder.Groups[i] == group)
                    return true;
            }

            return false;
        }

        public static string PartId(string folder, string fileNameWithoutExtension)
        {
            return folder + "/" + fileNameWithoutExtension;
        }

        public static bool TrySplitId(string id, out string folder, out string leaf)
        {
            folder = null;
            leaf = null;
            if (string.IsNullOrEmpty(id))
                return false;

            var slash = id.IndexOf('/');
            if (slash <= 0 || slash >= id.Length - 1)
                return false;

            folder = id.Substring(0, slash);
            leaf = id.Substring(slash + 1);
            return leaf.IndexOf('/') < 0;
        }

        public static List<MonsterRigPiece> PiecesFrom(MonsterRigFile file)
        {
            var list = new List<MonsterRigPiece>();
            if (file == null || file.parts == null)
                return list;

            for (var i = 0; i < file.parts.Count; i++)
            {
                var part = file.parts[i];
                if (part == null || !TrySplitId(part.id, out var folder, out _))
                    continue;
                if (!TryFolder(folder, out var spec))
                    continue;

                list.Add(new MonsterRigPiece
                {
                    Data = part,
                    Folder = folder,
                    Width = 142,
                    Height = 102,
                    Role = spec.Role,
                    Kind = spec.Kind,
                    Color = spec.Color,
                });
            }

            return list;
        }

        static MonsterFolderSpec Spec(
            string folder,
            MonsterPartRole role,
            MonsterPartKind kind,
            MonsterColorRole color,
            params MonsterSocketGroup[] groups)
        {
            return new MonsterFolderSpec
            {
                Folder = folder,
                Role = role,
                Kind = kind,
                Color = color,
                Groups = groups == null || groups.Length == 0 ? Array.Empty<MonsterSocketGroup>() : groups,
            };
        }

        static MonsterSocketGroupSpec Group(
            MonsterSocketGroup group,
            string label,
            string color,
            bool sided,
            bool mount,
            MonsterPartKind accepts)
        {
            return new MonsterSocketGroupSpec
            {
                Group = group,
                Label = label,
                Color = color,
                Sided = sided,
                Mount = mount,
                Accepts = accepts,
            };
        }
    }

    public static class MonsterRigEdits
    {
        public static MonsterRigPart CreatePart(string id, int width, int height, bool head)
        {
            return new MonsterRigPart
            {
                id = id,
                attachX = width / 2,
                attachY = height / 2,
                nativeFacing = MonsterFacing.Right,
                swingDegrees = 0f,
                dragDegrees = 0f,
                inertia = 1f,
                headTurn = head,
                excluded = new List<string>(),
                sockets = new List<MonsterRigSocket>(),
                nextSocket = 1,
            };
        }

        public static string AddSocket(MonsterRigPiece piece, MonsterSocketGroup group, int x, int y, MonsterFacing facing)
        {
            if (piece == null || piece.Data == null)
                return "没有选中的部件";

            if (piece.Role != MonsterPartRole.Primary)
                return "只有主体能加挂点";

            if (!MonsterRigCatalog.TryFolder(piece.Folder, out var folder) || !MonsterRigCatalog.FolderAllows(folder, group))
                return "这个部件没有这一组挂点";

            var spec = MonsterRigCatalog.GroupSpec(group);
            if (spec == null)
                return "未知挂点组";

            if (spec.Sided)
            {
                if (facing == MonsterFacing.None)
                    facing = NextSide(piece.Data, group);
            }
            else
            {
                facing = MonsterFacing.None;
            }

            piece.Data.sockets ??= new List<MonsterRigSocket>();
            var socket = new MonsterRigSocket
            {
                id = piece.Data.id + "#" + piece.Data.nextSocket.ToString(),
                group = group,
                x = x,
                y = y,
                accepts = facing,
            };
            piece.Data.nextSocket += 1;
            piece.Data.sockets.Add(socket);
            if (spec.Mount && string.IsNullOrEmpty(piece.Data.mountSocketId))
                piece.Data.mountSocketId = socket.id;
            return null;
        }

        public static string MoveSocket(MonsterRigPiece piece, string socketId, int x, int y)
        {
            var socket = Find(piece, socketId);
            if (socket == null)
                return "找不到挂点";

            socket.x = x;
            socket.y = y;
            return null;
        }

        public static string SetFacing(MonsterRigPiece piece, string socketId, MonsterFacing facing)
        {
            var socket = Find(piece, socketId);
            if (socket == null)
                return "找不到挂点";

            var spec = MonsterRigCatalog.GroupSpec(socket.group);
            if (spec == null || !spec.Sided)
                return "这一组不区分左右";

            if (facing == MonsterFacing.None)
                return "左右挂点必须标明左或右";

            socket.accepts = facing;
            return null;
        }

        public static string RemoveSocket(MonsterRigPiece piece, string socketId, List<MonsterRigAssignment> assignments)
        {
            if (piece == null || piece.Data == null || piece.Data.sockets == null)
                return "找不到挂点";

            var removed = false;
            for (var i = piece.Data.sockets.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(piece.Data.sockets[i].id, socketId, StringComparison.Ordinal))
                    continue;

                piece.Data.sockets.RemoveAt(i);
                removed = true;
            }

            if (!removed)
                return "找不到挂点";

            if (string.Equals(piece.Data.mountSocketId, socketId, StringComparison.Ordinal))
                piece.Data.mountSocketId = FirstMount(piece.Data);

            if (assignments != null)
            {
                for (var i = assignments.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(assignments[i].socketId, socketId, StringComparison.Ordinal))
                        assignments.RemoveAt(i);
                }
            }

            return null;
        }

        public static string MoveAttachment(MonsterRigPiece piece, int x, int y)
        {
            if (piece == null || piece.Data == null)
                return "没有选中的部件";

            if (piece.Role == MonsterPartRole.Primary)
                return "主体的对接点在挂点组里";

            piece.Data.attachX = x;
            piece.Data.attachY = y;
            return null;
        }

        public static string SetLayer(MonsterRigPiece piece, MonsterLayerBand band)
        {
            if (piece == null || piece.Data == null)
                return "没有选中的部件";

            if (band != MonsterLayerBand.Default && !MonsterLayerBands.Contains(band))
                return "没有这个图层";

            piece.Data.layerBand = band;
            return null;
        }

        public const float MaxSwingDegrees = 24f;

        public static string SetSwing(MonsterRigPiece piece, float degrees)
        {
            if (piece == null || piece.Data == null)
                return "没有选中的部件";

            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
                return "摆动幅度无效";

            piece.Data.swingDegrees = Mathf.Clamp(degrees, 0f, MaxSwingDegrees);
            return null;
        }

        public const float MaxDragDegrees = 45f;
        public const float MinInertia = 0.5f;
        public const float MaxInertia = 2.5f;

        /// <summary>待机摆动的默认值：身体只是轻微呼吸，尾巴摆得最明显。</summary>
        public static float DefaultSwing(MonsterPartKind kind)
        {
            switch (kind)
            {
                case MonsterPartKind.Body: return 0.45f;
                case MonsterPartKind.Head: return 1.35f;
                case MonsterPartKind.Hand: return 0.6f;
                case MonsterPartKind.Foot: return 0.35f;
                case MonsterPartKind.Tail: return 1.8f;
                default: return 0f;
            }
        }

        /// <summary>拖拽时的最大偏转（度）。配饰和五官不参与拖拽，固定为 0。</summary>
        public static float DefaultDrag(MonsterPartKind kind)
        {
            switch (kind)
            {
                case MonsterPartKind.Body: return 10f;
                case MonsterPartKind.Head: return 9f;
                case MonsterPartKind.Hand: return 14f;
                case MonsterPartKind.Foot: return 7f;
                case MonsterPartKind.Tail: return 22f;
                default: return 0f;
            }
        }

        public static void ApplyKindDefaults(MonsterRigPart part, MonsterPartKind kind)
        {
            part.swingDegrees = DefaultSwing(kind);
            part.dragDegrees = DefaultDrag(kind);
            part.inertia = 1f;
        }

        public static string SetDragDegrees(MonsterRigPiece piece, float degrees)
        {
            if (piece == null || piece.Data == null)
                return "没有选中的部件";

            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
                return "拖拽角度无效";

            piece.Data.dragDegrees = Mathf.Clamp(degrees, 0f, MaxDragDegrees);
            return null;
        }

        public static string SetInertia(MonsterRigPiece piece, float value)
        {
            if (piece == null || piece.Data == null)
                return "没有选中的部件";

            if (float.IsNaN(value) || float.IsInfinity(value))
                return "惯性无效";

            piece.Data.inertia = Mathf.Clamp(value, MinInertia, MaxInertia);
            return null;
        }

        public static string SetHeadTurn(MonsterRigPiece piece, bool enabled)
        {
            if (piece == null || piece.Data == null)
                return "没有选中的部件";

            if (piece.Kind != MonsterPartKind.Head)
                return "只有头部能扭头";

            piece.Data.headTurn = enabled;
            return null;
        }

        public static string SetMount(MonsterRigPiece piece, string socketId)
        {
            var socket = Find(piece, socketId);
            if (socket == null)
                return "找不到挂点";

            var spec = MonsterRigCatalog.GroupSpec(socket.group);
            if (spec == null || !spec.Mount)
                return "只有身体对接点能当作扭头和安装的基底";

            piece.Data.mountSocketId = socket.id;
            return null;
        }

        public static string SetExcluded(MonsterRigPiece piece, string bannedId, bool excluded)
        {
            if (piece == null || piece.Data == null)
                return "没有选中的部件";

            if (piece.Role != MonsterPartRole.Primary)
                return "只有主体能排除部件";

            if (string.IsNullOrEmpty(bannedId) || string.Equals(bannedId, piece.Data.id, StringComparison.Ordinal))
                return "不能排除自己";

            piece.Data.excluded ??= new List<string>();
            var index = IndexOf(piece.Data.excluded, bannedId);
            if (excluded)
            {
                if (index < 0)
                    piece.Data.excluded.Add(bannedId);
            }
            else if (index >= 0)
            {
                piece.Data.excluded.RemoveAt(index);
            }

            return null;
        }

        public static void MountPoint(MonsterRigPiece piece, out float x, out float y)
        {
            x = 0f;
            y = 0f;
            if (piece == null || piece.Data == null)
                return;

            if (piece.Kind == MonsterPartKind.Head)
            {
                var mount = FindMount(piece.Data);
                if (mount != null)
                {
                    x = mount.x;
                    y = mount.y;
                    return;
                }
            }

            if (piece.Role == MonsterPartRole.Primary && piece.Kind == MonsterPartKind.Body)
            {
                x = piece.Width * 0.5f;
                y = piece.Height * 0.5f;
                return;
            }

            x = piece.Data.attachX;
            y = piece.Data.attachY;
        }

        static MonsterFacing NextSide(MonsterRigPart part, MonsterSocketGroup group)
        {
            var count = 0;
            if (part.sockets != null)
            {
                for (var i = 0; i < part.sockets.Count; i++)
                {
                    if (part.sockets[i].group == group)
                        count += 1;
                }
            }

            return count % 2 == 0 ? MonsterFacing.Right : MonsterFacing.Left;
        }

        static MonsterRigSocket Find(MonsterRigPiece piece, string socketId)
        {
            if (piece == null || piece.Data == null || piece.Data.sockets == null || string.IsNullOrEmpty(socketId))
                return null;

            for (var i = 0; i < piece.Data.sockets.Count; i++)
            {
                if (string.Equals(piece.Data.sockets[i].id, socketId, StringComparison.Ordinal))
                    return piece.Data.sockets[i];
            }

            return null;
        }

        static MonsterRigSocket FindMount(MonsterRigPart part)
        {
            if (part.sockets == null)
                return null;

            if (!string.IsNullOrEmpty(part.mountSocketId))
            {
                for (var i = 0; i < part.sockets.Count; i++)
                {
                    var socket = part.sockets[i];
                    if (socket.group == MonsterSocketGroup.Neck
                        && string.Equals(socket.id, part.mountSocketId, StringComparison.Ordinal))
                        return socket;
                }
            }

            for (var i = 0; i < part.sockets.Count; i++)
            {
                if (part.sockets[i].group == MonsterSocketGroup.Neck)
                    return part.sockets[i];
            }

            return null;
        }

        static string FirstMount(MonsterRigPart part)
        {
            var mount = FindMount(part);
            return mount == null ? null : mount.id;
        }

        static int IndexOf(List<string> values, string id)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], id, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }
    }

    public static class MonsterRigLayout
    {
        public static float LocalSwing(float swingDegrees, float time, float cyclesPerSecond)
        {
            if (swingDegrees == 0f || cyclesPerSecond == 0f)
                return 0f;

            return swingDegrees * (float)Math.Sin((time) * Math.PI * 2.0 * cyclesPerSecond);
        }

        public static void Rotate(ref double x, ref double y, double degrees)
        {
            var radians = degrees * Math.PI / 180.0;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            var nextX = x * cos - y * sin;
            var nextY = x * sin + y * cos;
            x = nextX;
            y = nextY;
        }

        /// <summary>把一只怪物（或编辑中的一个部件）编译成骨架：谁挂在谁身上、每根骨头的参数。</summary>
        public static MonsterRigSkeleton Compile(
            MonsterRigPiece root,
            IReadOnlyList<MonsterRigPiece> catalog,
            IReadOnlyList<MonsterRigAssignment> assignments,
            ISet<MonsterSocketGroup> visibleGroups,
            bool headTurnPreview)
        {
            var skeleton = new MonsterRigSkeleton();
            if (root == null || root.Data == null)
            {
                skeleton.Warnings.Add("没有可预览的部件");
                return skeleton;
            }

            AddBone(skeleton, root, catalog, assignments, visibleGroups, headTurnPreview, -1, null, 0f, 0f, 0, true);

            var pins = 0;
            for (var i = 0; i < skeleton.Bones.Count; i++)
            {
                skeleton.Bones[i].PinStart = pins;
                pins += skeleton.Bones[i].Sockets.Count;
            }

            skeleton.PinCount = pins;
            skeleton.DrawOrder = DrawOrder(skeleton);
            return skeleton;
        }

        /// <summary>静止姿势（不加任何摆动），用于编辑器的预览和随机配色。</summary>
        public static MonsterRigPose Rest(MonsterRigSkeleton skeleton)
        {
            return Posed(skeleton, 0f, 0f);
        }

        public static MonsterRigPose Build(
            MonsterRigPiece root,
            IReadOnlyList<MonsterRigPiece> catalog,
            IReadOnlyList<MonsterRigAssignment> assignments,
            ISet<MonsterSocketGroup> visibleGroups,
            bool headTurnPreview,
            float time,
            float cyclesPerSecond)
        {
            var skeleton = Compile(root, catalog, assignments, visibleGroups, headTurnPreview);
            var pose = Posed(skeleton, time, cyclesPerSecond);
            pose.Nodes.Sort((a, b) => a.Order.CompareTo(b.Order));
            return pose;
        }

        static MonsterRigPose Posed(MonsterRigSkeleton skeleton, float time, float cyclesPerSecond)
        {
            var frame = new MonsterRigFrame(skeleton);
            if (skeleton.Bones.Count > 0)
            {
                var angles = new float[skeleton.Bones.Count];
                for (var i = 1; i < angles.Length; i++)
                    angles[i] = LocalSwing(skeleton.Bones[i].Swing, time, cyclesPerSecond);

                var root = skeleton.Bones[0];
                var tilt = LocalSwing(root.Swing, time, cyclesPerSecond);
                MonsterRigKinematics.Solve(skeleton, frame, angles, tilt, root.MountX, root.MountY, root.MountX, root.MountY, 0f);
            }

            return MonsterRigKinematics.ToPose(skeleton, frame);
        }

        static int[] DrawOrder(MonsterRigSkeleton skeleton)
        {
            var order = new int[skeleton.Bones.Count];
            for (var i = 0; i < order.Length; i++)
                order[i] = i;

            // 稳定的插入排序：部件数量很少，同层的保持原来的先后。
            for (var i = 1; i < order.Length; i++)
            {
                var current = order[i];
                var j = i - 1;
                while (j >= 0 && skeleton.Bones[order[j]].Order > skeleton.Bones[current].Order)
                {
                    order[j + 1] = order[j];
                    j -= 1;
                }

                order[j + 1] = current;
            }

            return order;
        }

        static int AddBone(
            MonsterRigSkeleton skeleton,
            MonsterRigPiece piece,
            IReadOnlyList<MonsterRigPiece> catalog,
            IReadOnlyList<MonsterRigAssignment> assignments,
            ISet<MonsterSocketGroup> visibleGroups,
            bool headTurnPreview,
            int parent,
            MonsterRigSocket parentSocket,
            float socketOffsetX,
            float socketOffsetY,
            int order,
            bool isRoot)
        {
            MonsterRigEdits.MountPoint(piece, out var mountX, out var mountY);
            var index = skeleton.Bones.Count;
            var bone = new MonsterRigBone
            {
                PartId = piece.Data.id,
                SocketId = parentSocket == null ? null : parentSocket.id,
                Parent = parent,
                Width = piece.Width,
                Height = piece.Height,
                MountX = mountX,
                MountY = mountY,
                SocketOffsetX = socketOffsetX,
                SocketOffsetY = socketOffsetY,
                LocalMirror = LocalMirror(piece, parentSocket, headTurnPreview),
                Order = MonsterLayerBands.Order(piece, order),
                Kind = piece.Kind,
                Color = piece.Color,
                Joint = isRoot ? MonsterJoint.Root : JointFor(piece.Kind),
                Swing = piece.Data.swingDegrees,
                Drag = piece.Data.dragDegrees,
                Inertia = Mathf.Clamp(piece.Data.inertia > 0f ? piece.Data.inertia : 1f, 0.25f, 4f),
            };
            skeleton.Bones.Add(bone);

            if (piece.Kind == MonsterPartKind.Head && piece.Data.sockets != null)
            {
                var hasNeck = false;
                for (var i = 0; i < piece.Data.sockets.Count; i++)
                {
                    if (piece.Data.sockets[i].group == MonsterSocketGroup.Neck)
                        hasNeck = true;
                }

                if (!hasNeck)
                    skeleton.Warnings.Add(piece.Data.id + " 还没有身体对接点，暂时用画布中心");
            }

            if (piece.Data.sockets == null)
                return index;

            for (var i = 0; i < piece.Data.sockets.Count; i++)
            {
                var socket = piece.Data.sockets[i];
                var spec = MonsterRigCatalog.GroupSpec(socket.group);
                bone.Sockets.Add(new MonsterRigSocketRef
                {
                    Id = socket.id,
                    Group = socket.group,
                    Accepts = socket.accepts,
                    Mount = spec != null && spec.Mount,
                    X = socket.x,
                    Y = socket.y,
                });

                if (spec == null || spec.Mount)
                    continue;

                if (visibleGroups != null && !visibleGroups.Contains(socket.group))
                    continue;

                var child = FindPiece(catalog, Assigned(assignments, socket.id));
                if (child == null)
                    continue;

                AddBone(
                    skeleton,
                    child,
                    catalog,
                    assignments,
                    visibleGroups,
                    headTurnPreview,
                    index,
                    socket,
                    socket.x - mountX,
                    socket.y - mountY,
                    i,
                    false);
            }

            return index;
        }

        static MonsterJoint JointFor(MonsterPartKind kind)
        {
            switch (kind)
            {
                case MonsterPartKind.Head: return MonsterJoint.Head;
                case MonsterPartKind.Hand: return MonsterJoint.Hand;
                case MonsterPartKind.Foot: return MonsterJoint.Foot;
                case MonsterPartKind.Tail: return MonsterJoint.Tail;
                default: return MonsterJoint.Trim;
            }
        }

        public static SpritePlacement PlacementFor(MonsterRigNode node)
        {
            var width = Mathf.Max(1, node.Width);
            var height = Mathf.Max(1, node.Height);
            return new SpritePlacement
            {
                Size = new Vector2(width, height),
                Pivot = new Vector2(node.AttachX / width, node.AttachY / height),
                AnchoredPosition = new Vector2(node.WorldAttachX, node.WorldAttachY),
                Rotation = node.WorldRotation,
                Scale = new Vector3(node.WorldMirror ? -1f : 1f, 1f, 1f),
            };
        }

        public static void TransformPoint(
            double originX,
            double originY,
            double rotation,
            bool mirror,
            double mountX,
            double mountY,
            double pointX,
            double pointY,
            out double worldX,
            out double worldY)
        {
            var x = pointX - mountX;
            var y = pointY - mountY;
            if (mirror)
                x = -x;
            Rotate(ref x, ref y, rotation);
            worldX = originX + x;
            worldY = originY + y;
        }

        static bool LocalMirror(MonsterRigPiece piece, MonsterRigSocket parentSocket, bool headTurnPreview)
        {
            if (piece.Kind == MonsterPartKind.Head && piece.Data.headTurn && headTurnPreview)
                return true;

            if (piece.Role != MonsterPartRole.SubPrimary || parentSocket == null)
                return false;

            return parentSocket.accepts != MonsterFacing.None
                && parentSocket.accepts != piece.Data.nativeFacing;
        }

        static string Assigned(IReadOnlyList<MonsterRigAssignment> assignments, string socketId)
        {
            if (assignments == null)
                return null;

            for (var i = 0; i < assignments.Count; i++)
            {
                if (string.Equals(assignments[i].socketId, socketId, StringComparison.Ordinal))
                    return assignments[i].partId;
            }

            return null;
        }

        static MonsterRigPiece FindPiece(IReadOnlyList<MonsterRigPiece> catalog, string id)
        {
            if (catalog == null || string.IsNullOrEmpty(id))
                return null;

            for (var i = 0; i < catalog.Count; i++)
            {
                var piece = catalog[i];
                if (piece != null && piece.Data != null && string.Equals(piece.Data.id, id, StringComparison.Ordinal))
                    return piece;
            }

            return null;
        }

    }

    public static class MonsterLayerBands
    {
        public static readonly MonsterLayerBand[] Editable =
        {
            MonsterLayerBand.AlwaysFront,
            MonsterLayerBand.Eye,
            MonsterLayerBand.Mouth,
            MonsterLayerBand.Head,
            MonsterLayerBand.Hat,
            MonsterLayerBand.Body,
            MonsterLayerBand.Hand,
            MonsterLayerBand.Foot,
            MonsterLayerBand.Accessory,
            MonsterLayerBand.Tail,
            MonsterLayerBand.AlwaysBack,
        };

        public static bool Contains(MonsterLayerBand band)
        {
            for (var i = 0; i < Editable.Length; i++)
            {
                if (Editable[i] == band)
                    return true;
            }

            return false;
        }

        public static MonsterLayerBand ForKind(MonsterPartKind kind)
        {
            switch (kind)
            {
                case MonsterPartKind.Eye: return MonsterLayerBand.Eye;
                case MonsterPartKind.Mouth: return MonsterLayerBand.Mouth;
                case MonsterPartKind.Head: return MonsterLayerBand.Head;
                case MonsterPartKind.Hat: return MonsterLayerBand.Hat;
                case MonsterPartKind.Body: return MonsterLayerBand.Body;
                case MonsterPartKind.Hand: return MonsterLayerBand.Hand;
                case MonsterPartKind.Foot: return MonsterLayerBand.Foot;
                case MonsterPartKind.Accessory: return MonsterLayerBand.Accessory;
                case MonsterPartKind.Tail: return MonsterLayerBand.Tail;
                default: return MonsterLayerBand.Body;
            }
        }

        public static MonsterLayerBand Resolve(MonsterRigPart part, MonsterPartKind kind)
        {
            if (part == null || part.layerBand == MonsterLayerBand.Default || !Contains(part.layerBand))
                return ForKind(kind);

            return part.layerBand;
        }

        public static int Order(MonsterRigPiece piece, int socketIndex)
        {
            var kind = piece == null ? MonsterPartKind.Body : piece.Kind;
            var band = Resolve(piece == null ? null : piece.Data, kind);
            if (socketIndex < 0)
                socketIndex = 0;
            if (socketIndex > 99)
                socketIndex = 99;
            return (int)band * 100 + socketIndex;
        }

        public static string Label(MonsterLayerBand band)
        {
            switch (band)
            {
                case MonsterLayerBand.AlwaysFront: return "一定在前";
                case MonsterLayerBand.Eye: return "眼";
                case MonsterLayerBand.Mouth: return "嘴";
                case MonsterLayerBand.Head: return "头";
                case MonsterLayerBand.Hat: return "头饰";
                case MonsterLayerBand.Body: return "身体";
                case MonsterLayerBand.Hand: return "手";
                case MonsterLayerBand.Foot: return "脚";
                case MonsterLayerBand.Accessory: return "身体配饰";
                case MonsterLayerBand.Tail: return "尾巴";
                case MonsterLayerBand.AlwaysBack: return "一定在后";
                default: return "按部件";
            }
        }
    }

    public static class MonsterRigPreview
    {
        public static void EnterWhole(List<MonsterRigAssignment> editAssignments, ISet<MonsterSocketGroup> visibleGroups)
        {
            if (editAssignments != null)
                editAssignments.Clear();
            if (visibleGroups != null)
                visibleGroups.Clear();
        }

        public static string WholeRoot(string monsterRootId, string selectedId, Func<string, bool> isBody)
        {
            if (isBody != null && isBody(monsterRootId))
                return monsterRootId;
            if (isBody != null && isBody(selectedId))
                return selectedId;
            return null;
        }
    }

    public static class MonsterRigColor
    {
        public static int IndexFor(int salt, string key, int count)
        {
            if (count <= 0)
                return 0;

            unchecked
            {
                var hash = salt == 0 ? 1 : salt;
                if (!string.IsNullOrEmpty(key))
                {
                    for (var i = 0; i < key.Length; i++)
                        hash = hash * 31 + key[i];
                }

                var index = hash % count;
                return index < 0 ? index + count : index;
            }
        }

        public static void Paint(MonsterRigPose pose, Func<string, int> roll)
        {
            if (pose == null || roll == null)
                return;

            var ownerByPart = new Dictionary<string, MonsterRigNode>(StringComparer.Ordinal);
            for (var i = 0; i < pose.Pins.Count; i++)
            {
                var pin = pose.Pins[i];
                if (pin == null || string.IsNullOrEmpty(pin.PartId) || ownerByPart.ContainsKey(pin.PartId))
                    continue;

                for (var n = 0; n < pose.Nodes.Count; n++)
                {
                    var node = pose.Nodes[n];
                    if (node != null && string.Equals(node.PartId, pin.PartId, StringComparison.Ordinal))
                    {
                        ownerByPart[pin.PartId] = node;
                        break;
                    }
                }
            }

            var parentBySocket = new Dictionary<string, MonsterRigNode>(StringComparer.Ordinal);
            for (var i = 0; i < pose.Pins.Count; i++)
            {
                var pin = pose.Pins[i];
                if (pin == null || string.IsNullOrEmpty(pin.SocketId))
                    continue;
                if (ownerByPart.TryGetValue(pin.PartId ?? "", out var owner))
                    parentBySocket[pin.SocketId] = owner;
            }

            var remaining = new List<MonsterRigNode>();
            for (var i = 0; i < pose.Nodes.Count; i++)
            {
                if (pose.Nodes[i] != null)
                    remaining.Add(pose.Nodes[i]);
            }

            var painted = new HashSet<MonsterRigNode>();
            var guard = remaining.Count + 1;
            while (remaining.Count > 0 && guard-- > 0)
            {
                var progressed = false;
                for (var i = remaining.Count - 1; i >= 0; i--)
                {
                    var node = remaining[i];
                    MonsterRigNode parent = null;
                    var waiting = !string.IsNullOrEmpty(node.SocketId)
                        && parentBySocket.TryGetValue(node.SocketId, out parent)
                        && !painted.Contains(parent);
                    if (waiting)
                        continue;

                    node.PaletteIndex = Choose(node, parent, roll);
                    painted.Add(node);
                    remaining.RemoveAt(i);
                    progressed = true;
                }

                if (!progressed)
                    break;
            }
        }

        static int Choose(MonsterRigNode node, MonsterRigNode parent, Func<string, int> roll)
        {
            if (node.Color == MonsterColorRole.None)
                return -1;
            if (InheritsParent(node) && parent != null && parent.PaletteIndex >= 0)
                return parent.PaletteIndex;

            var key = string.IsNullOrEmpty(node.SocketId) ? node.PartId : node.SocketId;
            var index = roll(key ?? "");
            return index < 0 ? 0 : index;
        }

        static bool InheritsParent(MonsterRigNode node)
        {
            return node.Kind == MonsterPartKind.Hand
                || node.Kind == MonsterPartKind.Foot
                || node.Kind == MonsterPartKind.Eye;
        }
    }

    public static class MonsterMotion
    {
        public static void StepSpring(
            ref float angle,
            ref float velocity,
            float target,
            float deltaTime,
            float stiffness,
            float damping)
        {
            var acceleration = (target - angle) * stiffness - velocity * damping;
            velocity += acceleration * deltaTime;
            angle += velocity * deltaTime;
        }
    }

    public static class MonsterRigRandom
    {
        public const int RecentLimit = 12;

        public static Func<double> Units(int salt)
        {
            var state = unchecked((uint)salt) | 1u;
            return () =>
            {
                state = state * 1664525u + 1013904223u;
                return (state & 0xFFFFFF) / 16777216.0;
            };
        }

        public static string Pick(IReadOnlyList<string> pool, List<string> recent, Func<double> nextUnit)
        {
            if (pool == null || pool.Count == 0)
                return null;

            var best = double.NegativeInfinity;
            var chosen = pool[0];
            for (var i = 0; i < pool.Count; i++)
            {
                var id = pool[i];
                var unseen = Contains(recent, id) ? 1.0 : 2.2;
                var roll = nextUnit == null ? 0.5 : nextUnit();
                if (roll < 0)
                    roll = 0;
                if (roll > 1)
                    roll = 1;
                var weight = unseen * (0.35 + roll * 1.3);
                if (weight > best)
                {
                    best = weight;
                    chosen = id;
                }
            }

            return chosen;
        }

        static bool Contains(List<string> values, string id)
        {
            if (values == null)
                return false;

            for (var i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], id, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static void Remember(List<string> recent, string id)
        {
            if (recent == null || string.IsNullOrEmpty(id))
                return;

            recent.Add(id);
            while (recent.Count > RecentLimit)
                recent.RemoveAt(0);
        }

        public static void Fill(
            MonsterRigPiece host,
            IReadOnlyList<MonsterRigPiece> catalog,
            List<MonsterRigAssignment> assignments,
            ISet<MonsterSocketGroup> groups,
            Dictionary<MonsterSocketGroup, List<string>> recent,
            Func<double> nextUnit,
            bool fillMountedHeads,
            HashSet<string> inheritedBan)
        {
            if (host == null || host.Data == null || host.Data.sockets == null)
                return;

            var ban = new HashSet<string>(StringComparer.Ordinal);
            if (inheritedBan != null)
            {
                foreach (var id in inheritedBan)
                    ban.Add(id);
            }

            if (host.Data.excluded != null)
            {
                for (var i = 0; i < host.Data.excluded.Count; i++)
                    ban.Add(host.Data.excluded[i]);
            }

            var sharedLimb = new Dictionary<MonsterSocketGroup, string>();
            for (var i = 0; i < host.Data.sockets.Count; i++)
            {
                var socket = host.Data.sockets[i];
                var spec = MonsterRigCatalog.GroupSpec(socket.group);
                if (spec == null || spec.Mount)
                    continue;

                if (groups != null && !groups.Contains(socket.group))
                    continue;

                var pool = Pool(catalog, spec.Accepts, ban, host.Data.id);
                if (SameLimb(spec.Accepts) && sharedLimb.TryGetValue(socket.group, out var shared))
                {
                    SetAssignment(assignments, socket.id, shared);
                    continue;
                }

                List<string> memory = null;
                if (recent != null && !recent.TryGetValue(socket.group, out memory))
                {
                    memory = new List<string>();
                    recent[socket.group] = memory;
                }

                var picked = Pick(pool, memory, nextUnit);
                if (SameLimb(spec.Accepts))
                    sharedLimb[socket.group] = picked;
                SetAssignment(assignments, socket.id, picked);
                Remember(memory, picked);
                if (!fillMountedHeads || string.IsNullOrEmpty(picked))
                    continue;

                var child = Find(catalog, picked);
                if (child == null || child.Role != MonsterPartRole.Primary)
                    continue;

                Fill(child, catalog, assignments, null, recent, nextUnit, true, new HashSet<string>(ban, StringComparer.Ordinal));
            }
        }

        static bool SameLimb(MonsterPartKind kind)
        {
            return kind == MonsterPartKind.Hand || kind == MonsterPartKind.Foot;
        }

        public static List<string> Pool(
            IReadOnlyList<MonsterRigPiece> catalog,
            MonsterPartKind kind,
            HashSet<string> ban,
            string hostId)
        {
            var pool = new List<string>();
            if (catalog == null)
                return pool;

            for (var i = 0; i < catalog.Count; i++)
            {
                var piece = catalog[i];
                if (piece == null || piece.Data == null || piece.Missing || piece.Kind != kind)
                    continue;

                if (string.Equals(piece.Data.id, hostId, StringComparison.Ordinal))
                    continue;

                if (ban != null && ban.Contains(piece.Data.id))
                    continue;

                pool.Add(piece.Data.id);
            }

            pool.Sort(StringComparer.Ordinal);
            return pool;
        }

        static void SetAssignment(List<MonsterRigAssignment> assignments, string socketId, string partId)
        {
            if (assignments == null)
                return;

            for (var i = 0; i < assignments.Count; i++)
            {
                if (!string.Equals(assignments[i].socketId, socketId, StringComparison.Ordinal))
                    continue;

                if (string.IsNullOrEmpty(partId))
                    assignments.RemoveAt(i);
                else
                    assignments[i].partId = partId;
                return;
            }

            if (!string.IsNullOrEmpty(partId))
            {
                assignments.Add(new MonsterRigAssignment
                {
                    socketId = socketId,
                    partId = partId,
                });
            }
        }

        static MonsterRigPiece Find(IReadOnlyList<MonsterRigPiece> catalog, string id)
        {
            for (var i = 0; i < catalog.Count; i++)
            {
                var piece = catalog[i];
                if (piece != null && piece.Data != null && string.Equals(piece.Data.id, id, StringComparison.Ordinal))
                    return piece;
            }

            return null;
        }
    }
}
