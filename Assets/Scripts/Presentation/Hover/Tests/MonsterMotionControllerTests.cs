using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace TheCall.Hover.Tests
{
    public sealed class MonsterMotionControllerTests
    {
        [Test]
        public void 拖拽角度不会超过关节上限()
        {
            var profile = CreateProfile();
            var skeleton = Creature(14f);
            var controller = new MonsterMotionController(skeleton, profile, 0f);
            var frame = new MonsterRigFrame(skeleton);

            controller.Grab(0f, 0f);
            var peak = 0f;
            for (var i = 0; i < 120; i++)
            {
                controller.Hold((i + 1) * 8f, 0f);
                controller.Advance(1f / 60f);
                controller.Compose(frame);
                var bend = Mathf.Abs(frame.Rotation[1] - frame.Rotation[0]);
                peak = Mathf.Max(peak, bend);
                Assert.That(bend, Is.LessThanOrEqualTo(14f + 0.01f));
            }

            Assert.That(peak, Is.GreaterThan(10f));
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void 按下点偏离中心时身体先不动再把中心贴到指针上()
        {
            var profile = CreateProfile();
            profile.idleEnabled = false;
            var skeleton = Creature(14f);
            var controller = new MonsterMotionController(skeleton, profile, 0f);
            var frame = new MonsterRigFrame(skeleton);

            controller.Grab(30f, -12f);
            controller.Compose(frame);
            Assert.That(frame.X[0], Is.EqualTo(71f).Within(0.02f));
            Assert.That(frame.Y[0], Is.EqualTo(51f).Within(0.02f));

            controller.Hold(35f, -12f);
            controller.Compose(frame);
            Assert.That(frame.X[0], Is.EqualTo(76f).Within(0.02f));
            Assert.That(frame.Y[0], Is.EqualTo(51f).Within(0.02f));

            controller.Advance(MonsterMotionController.MaxFrameSeconds);
            controller.Compose(frame);
            Assert.That(frame.X[0], Is.EqualTo(71f + 35f).Within(0.02f));
            Assert.That(frame.Y[0], Is.EqualTo(51f - 12f).Within(0.02f));

            controller.Hold(80f, 10f);
            controller.Advance(1f / 60f);
            controller.Compose(frame);
            Assert.That(frame.X[0], Is.EqualTo(71f + 80f).Within(0.02f));
            Assert.That(frame.Y[0], Is.EqualTo(51f + 10f).Within(0.02f));
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void 松手后抓取分量回到静止()
        {
            var profile = CreateProfile();
            var skeleton = Creature(14f);
            var controller = new MonsterMotionController(skeleton, profile, 0f);
            var frame = new MonsterRigFrame(skeleton);

            controller.Grab(0f, 0f);
            for (var i = 0; i < 60; i++)
            {
                controller.Hold(300f, 40f);
                controller.Advance(1f / 60f);
            }

            controller.Release();
            for (var i = 0; i < 300; i++)
                controller.Advance(1f / 60f);

            Assert.That(controller.Holding, Is.False);
            Assert.That(controller.Settled, Is.True);
            controller.Compose(frame);
            Assert.That(frame.Rotation[1] - frame.Rotation[0], Is.EqualTo(0f).Within(0.01f));
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void 计分反应弹出后会回落到静止()
        {
            var profile = CreateProfile();
            var skeleton = Creature(14f);
            var controller = new MonsterMotionController(skeleton, profile, 0f);
            var frame = new MonsterRigFrame(skeleton);

            controller.SetReaction(-10f, 7f);
            for (var i = 0; i < 36; i++)
                controller.Advance(1f / 60f);
            controller.Compose(frame);
            Assert.That(frame.Rotation[2] - frame.Rotation[0], Is.EqualTo(-10f).Within(1.5f));

            controller.SetReaction(0f, 0f);
            for (var i = 0; i < 240; i++)
                controller.Advance(1f / 60f);
            Assert.That(controller.Settled, Is.True);
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void 镜像父骨头时子骨头的转向相反()
        {
            var skeleton = new MonsterRigSkeleton();
            skeleton.Bones.Add(new MonsterRigBone
            {
                PartId = "身体/身体1",
                Parent = -1,
                Joint = MonsterJoint.Root,
                Kind = MonsterPartKind.Body,
                LocalMirror = true,
                Inertia = 1f,
            });
            skeleton.Bones.Add(new MonsterRigBone
            {
                PartId = "手/手1",
                SocketId = "身体/身体1#1",
                Parent = 0,
                SocketOffsetX = 9f,
                Joint = MonsterJoint.Hand,
                Kind = MonsterPartKind.Hand,
                Inertia = 1f,
            });

            var frame = new MonsterRigFrame(skeleton);
            MonsterRigKinematics.Solve(skeleton, frame, new float[] { 0f, 20f }, 0f, 0f, 0f, 0f, 0f, 0f);

            Assert.That(frame.Rotation[1], Is.EqualTo(-20f).Within(0.001f));
            Assert.That(frame.X[1], Is.EqualTo(-9f).Within(0.001f));
            Assert.That(frame.Mirror[1], Is.True);
        }

        [Test]
        public void 旧版文件升级后补上默认的摆动拖拽和惯性()
        {
            var file = new MonsterRigFile
            {
                version = 1,
                parts = new List<MonsterRigPart>
                {
                    new MonsterRigPart { id = "身体/身体1", swingDegrees = 0f },
                },
            };

            file.Upgrade();

            var part = file.parts[0];
            Assert.That(file.version, Is.EqualTo(2));
            Assert.That(part.swingDegrees, Is.EqualTo(0.45f).Within(0.001f));
            Assert.That(part.dragDegrees, Is.EqualTo(10f).Within(0.001f));
            Assert.That(part.inertia, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void 版本二的文件不会覆盖用户设成零的摆动()
        {
            var file = new MonsterRigFile
            {
                version = 2,
                parts = new List<MonsterRigPart>
                {
                    new MonsterRigPart { id = "尾巴/尾巴1", swingDegrees = 0f, dragDegrees = 0f, inertia = 1f },
                },
            };

            file.Upgrade();

            Assert.That(file.parts[0].swingDegrees, Is.EqualTo(0f));
            Assert.That(file.parts[0].dragDegrees, Is.EqualTo(0f));
        }

        [Test]
        public void 拖拽角度和惯性在编辑器里被夹到合理范围()
        {
            var piece = Piece("身体", "身体1", 10, 10);

            Assert.That(MonsterRigEdits.SetDragDegrees(piece, 99f), Is.Null);
            Assert.That(piece.Data.dragDegrees, Is.EqualTo(MonsterRigEdits.MaxDragDegrees).Within(0.001f));
            Assert.That(MonsterRigEdits.SetInertia(piece, 0.1f), Is.Null);
            Assert.That(piece.Data.inertia, Is.EqualTo(MonsterRigEdits.MinInertia).Within(0.001f));
        }

        static MonsterMotionProfile CreateProfile()
        {
            return ScriptableObject.CreateInstance<MonsterMotionProfile>();
        }

        /// <summary>身体为根，手和头各挂一根骨头；手和头都是拖拽关节。</summary>
        static MonsterRigSkeleton Creature(float handDrag)
        {
            var skeleton = new MonsterRigSkeleton();
            skeleton.Bones.Add(new MonsterRigBone
            {
                PartId = "身体/身体1",
                Parent = -1,
                MountX = 71f,
                MountY = 51f,
                Joint = MonsterJoint.Root,
                Kind = MonsterPartKind.Body,
                Drag = 10f,
                Inertia = 1f,
            });
            skeleton.Bones.Add(new MonsterRigBone
            {
                PartId = "手/手1",
                SocketId = "身体/身体1#1",
                Parent = 0,
                SocketOffsetX = 30f,
                Joint = MonsterJoint.Hand,
                Kind = MonsterPartKind.Hand,
                Drag = handDrag,
                Inertia = 1f,
            });
            skeleton.Bones.Add(new MonsterRigBone
            {
                PartId = "头/头1",
                SocketId = "身体/身体1#0",
                Parent = 0,
                SocketOffsetY = 30f,
                Joint = MonsterJoint.Head,
                Kind = MonsterPartKind.Head,
                Drag = 9f,
                Inertia = 1f,
            });
            return skeleton;
        }

        static MonsterRigPiece Piece(string folder, string leaf, int width, int height)
        {
            Assert.That(MonsterRigCatalog.TryFolder(folder, out var spec), Is.True);
            return new MonsterRigPiece
            {
                Data = MonsterRigEdits.CreatePart(MonsterRigCatalog.PartId(folder, leaf), width, height, false),
                Folder = folder,
                Width = width,
                Height = height,
                Role = spec.Role,
                Kind = spec.Kind,
                Color = spec.Color,
            };
        }
    }
}
