using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheCall
{
    /// <summary>
    /// 怪物身上的关节类型。根是身体自己的倾斜；头、手、脚、尾巴在拖拽时各自偏转；配饰只跟着父部件走。
    /// </summary>
    public enum MonsterJoint
    {
        Root,
        Head,
        Hand,
        Foot,
        Tail,
        Trim,
    }

    public sealed class MonsterRigSocketRef
    {
        public string Id;
        public MonsterSocketGroup Group;
        public MonsterFacing Accepts;
        public bool Mount;
        public float X;
        public float Y;
    }

    /// <summary>
    /// 骨架里的一根骨头，对应一个部件。Parent 为 -1 的是根（身体）。
    /// 长度单位是原画像素，角度单位是度（正数为逆时针）。
    /// SocketOffset 是父挂点相对父安装点的偏移，已经换算到父的画布坐标系里。
    /// </summary>
    public sealed class MonsterRigBone
    {
        public string PartId;
        public string SocketId;
        public int Parent = -1;
        public int Width;
        public int Height;
        public float MountX;
        public float MountY;
        public float SocketOffsetX;
        public float SocketOffsetY;
        public bool LocalMirror;
        public int Order;
        public MonsterPartKind Kind;
        public MonsterColorRole Color;
        public MonsterJoint Joint;
        public float Swing;
        public float Drag;
        public float Inertia = 1f;
        public int PaletteIndex = -1;
        public int PinStart;
        public readonly List<MonsterRigSocketRef> Sockets = new List<MonsterRigSocketRef>();
    }

    public sealed class MonsterRigSkeleton
    {
        public readonly List<MonsterRigBone> Bones = new List<MonsterRigBone>();
        public readonly List<string> Warnings = new List<string>();
        public int PinCount;
        public int[] DrawOrder = Array.Empty<int>();
    }

    /// <summary>一帧的姿势：每根骨头的世界位置与旋转，以及每个挂点的世界位置。</summary>
    public sealed class MonsterRigFrame
    {
        public readonly float[] X;
        public readonly float[] Y;
        public readonly float[] Rotation;
        public readonly bool[] Mirror;
        public readonly float[] PinX;
        public readonly float[] PinY;

        public MonsterRigFrame(MonsterRigSkeleton skeleton)
        {
            var bones = skeleton.Bones.Count;
            X = new float[bones];
            Y = new float[bones];
            Rotation = new float[bones];
            Mirror = new bool[bones];
            PinX = new float[skeleton.PinCount];
            PinY = new float[skeleton.PinCount];
        }
    }

    /// <summary>
    /// 前向运动学。游戏和网页编辑器用的是同一套公式（motion.js 逐行对应）。
    /// 子骨头：世界旋转 = 父旋转 + 符号 × 本地角度（父镜像时符号为负），镜像 = 父镜像 异或 本地镜像。
    /// 根骨头绕抓点旋转：位置 = 枢轴 + R(θ)·(安装点 − 抓点) + (0, 抬起)。
    /// </summary>
    public static class MonsterRigKinematics
    {
        const float DegToRad = 0.017453292f;

        public static void Solve(
            MonsterRigSkeleton skeleton,
            MonsterRigFrame frame,
            float[] angles,
            float tilt,
            float pivotX,
            float pivotY,
            float gripX,
            float gripY,
            float lift)
        {
            var bones = skeleton.Bones;
            for (var i = 0; i < bones.Count; i++)
            {
                var bone = bones[i];
                if (bone.Parent < 0)
                {
                    var radians = tilt * DegToRad;
                    var cos = Mathf.Cos(radians);
                    var sin = Mathf.Sin(radians);
                    var dx = bone.MountX - gripX;
                    var dy = bone.MountY - gripY;
                    frame.X[i] = pivotX + cos * dx - sin * dy;
                    frame.Y[i] = pivotY + lift + sin * dx + cos * dy;
                    frame.Rotation[i] = tilt;
                    frame.Mirror[i] = bone.LocalMirror;
                }
                else
                {
                    var parent = bone.Parent;
                    var parentRadians = frame.Rotation[parent] * DegToRad;
                    var parentCos = Mathf.Cos(parentRadians);
                    var parentSin = Mathf.Sin(parentRadians);
                    var ox = frame.Mirror[parent] ? -bone.SocketOffsetX : bone.SocketOffsetX;
                    var oy = bone.SocketOffsetY;
                    frame.X[i] = frame.X[parent] + parentCos * ox - parentSin * oy;
                    frame.Y[i] = frame.Y[parent] + parentSin * ox + parentCos * oy;
                    var sign = frame.Mirror[parent] ? -1f : 1f;
                    frame.Rotation[i] = frame.Rotation[parent] + sign * angles[i];
                    frame.Mirror[i] = frame.Mirror[parent] ^ bone.LocalMirror;
                }

                var selfRadians = frame.Rotation[i] * DegToRad;
                var selfCos = Mathf.Cos(selfRadians);
                var selfSin = Mathf.Sin(selfRadians);
                for (var k = 0; k < bone.Sockets.Count; k++)
                {
                    var socket = bone.Sockets[k];
                    var sx = socket.X - bone.MountX;
                    var sy = socket.Y - bone.MountY;
                    if (frame.Mirror[i])
                        sx = -sx;
                    frame.PinX[bone.PinStart + k] = frame.X[i] + selfCos * sx - selfSin * sy;
                    frame.PinY[bone.PinStart + k] = frame.Y[i] + selfSin * sx + selfCos * sy;
                }
            }
        }

        public static MonsterRigPose ToPose(MonsterRigSkeleton skeleton, MonsterRigFrame frame)
        {
            var pose = new MonsterRigPose();
            pose.Warnings.AddRange(skeleton.Warnings);
            for (var i = 0; i < skeleton.Bones.Count; i++)
            {
                var bone = skeleton.Bones[i];
                pose.Nodes.Add(new MonsterRigNode
                {
                    PartId = bone.PartId,
                    SocketId = bone.SocketId,
                    Width = bone.Width,
                    Height = bone.Height,
                    AttachX = bone.MountX,
                    AttachY = bone.MountY,
                    WorldAttachX = frame.X[i],
                    WorldAttachY = frame.Y[i],
                    WorldRotation = frame.Rotation[i],
                    WorldMirror = frame.Mirror[i],
                    Order = bone.Order,
                    Kind = bone.Kind,
                    Color = bone.Color,
                    PaletteIndex = bone.PaletteIndex,
                    Bone = i,
                });

                for (var k = 0; k < bone.Sockets.Count; k++)
                {
                    var socket = bone.Sockets[k];
                    pose.Pins.Add(new MonsterRigPin
                    {
                        PartId = bone.PartId,
                        SocketId = socket.Id,
                        Group = socket.Group,
                        Accepts = socket.Accepts,
                        WorldX = frame.PinX[bone.PinStart + k],
                        WorldY = frame.PinY[bone.PinStart + k],
                        Mount = socket.Mount,
                    });
                }
            }

            return pose;
        }
    }
}
