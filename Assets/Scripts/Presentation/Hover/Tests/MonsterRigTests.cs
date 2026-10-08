using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace TheCall.Hover.Tests
{
    public sealed class MonsterRigTests
    {
        [Test]
        public void 文件夹把身体和头当作主体把手脚眼当作次主体()
        {
            Assert.That(Role("身体"), Is.EqualTo(MonsterPartRole.Primary));
            Assert.That(Role("头"), Is.EqualTo(MonsterPartRole.Primary));
            Assert.That(Role("手"), Is.EqualTo(MonsterPartRole.SubPrimary));
            Assert.That(Role("脚"), Is.EqualTo(MonsterPartRole.SubPrimary));
            Assert.That(Role("眼"), Is.EqualTo(MonsterPartRole.SubPrimary));
            Assert.That(Role("尾巴"), Is.EqualTo(MonsterPartRole.Secondary));
            Assert.That(Role("配饰"), Is.EqualTo(MonsterPartRole.Secondary));
            Assert.That(Role("头饰"), Is.EqualTo(MonsterPartRole.Secondary));
            Assert.That(Role("嘴"), Is.EqualTo(MonsterPartRole.Secondary));
        }

        [Test]
        public void 新主体没有挂点副体不能再加挂点()
        {
            var body = Piece("身体", "身体1", 142, 102);
            Assert.That(body.Data.sockets, Is.Empty);

            var error = MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Hand, 10, 12, MonsterFacing.None);
            Assert.That(error, Is.Null);
            Assert.That(body.Data.sockets[0].accepts, Is.EqualTo(MonsterFacing.Right));

            var foot = Piece("脚", "脚1", 142, 102);
            Assert.That(MonsterRigEdits.AddSocket(foot, MonsterSocketGroup.Hand, 0, 0, MonsterFacing.Left), Is.EqualTo("只有主体能加挂点"));
        }

        [Test]
        public void 左右挂点交替默认并且必须标明一侧()
        {
            var body = Piece("身体", "身体1", 142, 102);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Hand, 1, 1, MonsterFacing.None);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Hand, 2, 2, MonsterFacing.None);
            Assert.That(body.Data.sockets[0].accepts, Is.EqualTo(MonsterFacing.Right));
            Assert.That(body.Data.sockets[1].accepts, Is.EqualTo(MonsterFacing.Left));
            Assert.That(MonsterRigEdits.SetFacing(body, body.Data.sockets[0].id, MonsterFacing.None), Is.EqualTo("左右挂点必须标明左或右"));
        }

        [Test]
        public void 子部件的安装点落在父挂点上左挂点镜像素材()
        {
            var body = Piece("身体", "身体1", 10, 10);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Hand, 8, 2, MonsterFacing.Left);
            var hand = Piece("手", "手1", 6, 4);
            hand.Data.attachX = 1;
            hand.Data.attachY = 1;
            hand.Data.nativeFacing = MonsterFacing.Right;
            var assignments = new List<MonsterRigAssignment>
            {
                new MonsterRigAssignment
                {
                    socketId = body.Data.sockets[0].id,
                    partId = hand.Data.id,
                },
            };

            var pose = MonsterRigLayout.Build(body, new[] { body, hand }, assignments, null, false, 0f, 0.42f);
            var child = Node(pose, hand.Data.id);

            Assert.That(child.WorldAttachX, Is.EqualTo(8f).Within(0.001f));
            Assert.That(child.WorldAttachY, Is.EqualTo(2f).Within(0.001f));
            Assert.That(child.WorldMirror, Is.True);
            Assert.That(child.Width, Is.EqualTo(6));
            Assert.That(child.Height, Is.EqualTo(4));

            var placement = MonsterRigLayout.PlacementFor(child);
            Assert.That(placement.Size, Is.EqualTo(new Vector2(6f, 4f)));
            Assert.That(placement.Scale.x, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(placement.Pivot.x, Is.EqualTo(1f / 6f).Within(0.001f));
        }

        [Test]
        public void 旋转九十度后挂点偏移和手算一致()
        {
            MonsterRigLayout.TransformPoint(4, 4, 90, false, 4, 4, 8, 2, out var x, out var y);
            Assert.That(x, Is.EqualTo(6).Within(0.001));
            Assert.That(y, Is.EqualTo(8).Within(0.001));
        }

        [Test]
        public void 摆动为零时子部件跟着父体转()
        {
            var body = Piece("身体", "身体1", 10, 10);
            body.Data.swingDegrees = 10f;
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Tail, 8, 2, MonsterFacing.None);
            var tail = Piece("尾巴", "尾巴1", 4, 4);
            tail.Data.attachX = 1;
            tail.Data.attachY = 1;
            tail.Data.swingDegrees = 0f;
            var assignments = new List<MonsterRigAssignment>
            {
                new MonsterRigAssignment { socketId = body.Data.sockets[0].id, partId = tail.Data.id },
            };

            var pose = MonsterRigLayout.Build(body, new[] { body, tail }, assignments, null, false, 1f, 0.25f);

            Assert.That(pose.Nodes[0].WorldRotation, Is.EqualTo(10f).Within(0.001f));
            Assert.That(pose.Nodes[1].WorldRotation, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void 扭头以头部对接点为轴镜像()
        {
            var head = Piece("头", "头1", 10, 10);
            MonsterRigEdits.AddSocket(head, MonsterSocketGroup.Neck, 4, 2, MonsterFacing.None);
            MonsterRigEdits.AddSocket(head, MonsterSocketGroup.Eye, 8, 7, MonsterFacing.Right);
            var eye = Piece("眼", "眼1", 2, 2);
            eye.Data.attachX = 1;
            eye.Data.attachY = 1;
            eye.Data.nativeFacing = MonsterFacing.Right;
            var assignments = new List<MonsterRigAssignment>
            {
                new MonsterRigAssignment { socketId = head.Data.sockets[1].id, partId = eye.Data.id },
            };

            var off = MonsterRigLayout.Build(head, new[] { head, eye }, assignments, null, false, 0f, 0f);
            var on = MonsterRigLayout.Build(head, new[] { head, eye }, assignments, null, true, 0f, 0f);

            Assert.That(off.Nodes[0].WorldMirror, Is.False);
            Assert.That(on.Nodes[0].WorldAttachX, Is.EqualTo(4f).Within(0.001f));
            Assert.That(on.Nodes[0].WorldAttachY, Is.EqualTo(2f).Within(0.001f));
            Assert.That(on.Nodes[0].WorldMirror, Is.True);
            Assert.That(on.Nodes[1].WorldMirror, Is.True);
            Assert.That(on.Nodes[1].WorldAttachX, Is.EqualTo(0f).Within(0.001f));
            Assert.That(head.Data.headTurn, Is.True);
            Assert.That(MonsterRigEdits.SetHeadTurn(head, false), Is.Null);
            var blocked = MonsterRigLayout.Build(head, new[] { head, eye }, assignments, null, true, 0f, 0f);
            Assert.That(blocked.Nodes[0].WorldMirror, Is.False);
        }

        [Test]
        public void 配饰画在头后面()
        {
            var body = Piece("身体", "身体A", 10, 10);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Head, 5, 8, MonsterFacing.None);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.BodyTrim, 5, 4, MonsterFacing.None);
            var head = Piece("头", "头1", 4, 4);
            var trim = Piece("配饰", "配饰1", 4, 4);
            var assignments = new List<MonsterRigAssignment>
            {
                new MonsterRigAssignment { socketId = body.Data.sockets[0].id, partId = head.Data.id },
                new MonsterRigAssignment { socketId = body.Data.sockets[1].id, partId = trim.Data.id },
            };

            var pose = MonsterRigLayout.Build(body, new[] { body, head, trim }, assignments, null, false, 0f, 0f);
            var headOrder = 0;
            var trimOrder = 0;
            for (var i = 0; i < pose.Nodes.Count; i++)
            {
                if (pose.Nodes[i].PartId == head.Data.id)
                    headOrder = pose.Nodes[i].Order;
                if (pose.Nodes[i].PartId == trim.Data.id)
                    trimOrder = pose.Nodes[i].Order;
            }

            Assert.That(trimOrder, Is.LessThan(headOrder));
        }

        [Test]
        public void 默认图层从前往后是眼嘴头头饰身体手脚配饰尾巴()
        {
            var pose = SampleCreature();
            Assert.That(OrderOf(pose, "眼/眼1"), Is.GreaterThan(OrderOf(pose, "嘴/嘴1")));
            Assert.That(OrderOf(pose, "嘴/嘴1"), Is.GreaterThan(OrderOf(pose, "头/头1")));
            Assert.That(OrderOf(pose, "头/头1"), Is.GreaterThan(OrderOf(pose, "头饰/头饰1")));
            Assert.That(OrderOf(pose, "头饰/头饰1"), Is.GreaterThan(OrderOf(pose, "身体/身体1")));
            Assert.That(OrderOf(pose, "身体/身体1"), Is.GreaterThan(OrderOf(pose, "手/手1")));
            Assert.That(OrderOf(pose, "手/手1"), Is.GreaterThan(OrderOf(pose, "脚/脚1")));
            Assert.That(OrderOf(pose, "脚/脚1"), Is.GreaterThan(OrderOf(pose, "配饰/配饰1")));
            Assert.That(OrderOf(pose, "配饰/配饰1"), Is.GreaterThan(OrderOf(pose, "尾巴/尾巴1")));
        }

        [Test]
        public void 单个部件可以改到一定在前或一定在后()
        {
            var body = Piece("身体", "身体1", 10, 10);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Tail, 2, 2, MonsterFacing.None);
            var tail = Piece("尾巴", "尾巴1", 4, 4);
            Assert.That(MonsterRigEdits.SetLayer(tail, MonsterLayerBand.AlwaysFront), Is.Null);
            var pose = MonsterRigLayout.Build(
                body,
                new[] { body, tail },
                new List<MonsterRigAssignment>
                {
                    new MonsterRigAssignment { socketId = body.Data.sockets[0].id, partId = tail.Data.id },
                },
                null,
                false,
                0f,
                0f);

            Assert.That(OrderOf(pose, tail.Data.id), Is.GreaterThan(OrderOf(pose, body.Data.id)));

            Assert.That(MonsterRigEdits.SetLayer(tail, MonsterLayerBand.AlwaysBack), Is.Null);
            pose = MonsterRigLayout.Build(
                body,
                new[] { body, tail },
                new List<MonsterRigAssignment>
                {
                    new MonsterRigAssignment { socketId = body.Data.sockets[0].id, partId = tail.Data.id },
                },
                null,
                false,
                0f,
                0f);
            Assert.That(OrderOf(pose, tail.Data.id), Is.LessThan(OrderOf(pose, body.Data.id)));
        }

        [Test]
        public void 进入整只预览会清掉组件上的临时搭配()
        {
            var body = Piece("身体", "身体1", 10, 10);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Hand, 8, 2, MonsterFacing.Left);
            var hand = Piece("手", "手1", 4, 4);
            var edit = new List<MonsterRigAssignment>
            {
                new MonsterRigAssignment { socketId = body.Data.sockets[0].id, partId = hand.Data.id },
            };
            var visible = new HashSet<MonsterSocketGroup> { MonsterSocketGroup.Hand };
            var monster = new List<MonsterRigAssignment>
            {
                new MonsterRigAssignment { socketId = body.Data.sockets[0].id, partId = hand.Data.id },
            };

            MonsterRigPreview.EnterWhole(edit, visible);
            var pose = MonsterRigLayout.Build(body, new[] { body, hand }, edit, visible, false, 0f, 0f);

            Assert.That(edit, Is.Empty);
            Assert.That(visible, Is.Empty);
            Assert.That(pose.Nodes, Has.Count.EqualTo(1));
            Assert.That(pose.Nodes[0].PartId, Is.EqualTo(body.Data.id));
            Assert.That(pose.Pins, Has.Count.EqualTo(1));
            Assert.That(monster, Has.Count.EqualTo(1));
        }

        [Test]
        public void 整只预览回到上次随机的身体而不是当前点开的手()
        {
            System.Func<string, bool> isBody = id => id != null && id.StartsWith("身体");
            Assert.That(MonsterRigPreview.WholeRoot("身体/身体4", "手/手1", isBody), Is.EqualTo("身体/身体4"));
            Assert.That(MonsterRigPreview.WholeRoot(null, "身体/身体2", isBody), Is.EqualTo("身体/身体2"));
            Assert.That(MonsterRigPreview.WholeRoot("手/手1", "眼/眼1", isBody), Is.Null);
        }

        [Test]
        public void 身体转动九十度时手绕身体挂点转出()
        {
            var nodes = new List<MonsterRigNode>
            {
                new MonsterRigNode
                {
                    PartId = "身体/身体1",
                    Kind = MonsterPartKind.Body,
                    WorldAttachX = 71f,
                    WorldAttachY = 51f,
                },
                new MonsterRigNode
                {
                    PartId = "手/手1",
                    SocketId = "身体/身体1#1",
                    Kind = MonsterPartKind.Hand,
                    WorldAttachX = 80f,
                    WorldAttachY = 51f,
                },
            };

            MonsterRigMotion.Apply(nodes, new MonsterMotionSample(0f, 0f, 0f, 90f, 0f));

            Assert.That(nodes[1].WorldAttachX, Is.EqualTo(71f).Within(0.001f));
            Assert.That(nodes[1].WorldAttachY, Is.EqualTo(60f).Within(0.001f));
            Assert.That(nodes[1].WorldRotation, Is.EqualTo(90f).Within(0.001f));
        }

        [Test]
        public void 待机在四分之一周期时头和身体抬起用的是动作配置()
        {
            var profile = ScriptableObject.CreateInstance<MonsterMotionProfile>();
            var time = 1f / (4f * profile.idleCyclesPerSecond);
            var sample = MonsterMotionSample.Idle(profile, time, 0f);

            Assert.That(sample.Head, Is.EqualTo(profile.idleHeadDegrees).Within(0.001f));
            Assert.That(sample.Feet, Is.EqualTo(profile.idleFeetDegrees).Within(0.001f));
            Assert.That(sample.Lift, Is.EqualTo(profile.idleBodyLift).Within(0.001f));
            Assert.That(sample.Tail, Is.EqualTo(profile.idleTailDegrees * Mathf.Sin(Mathf.PI / 4f)).Within(0.001f));
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void 已有身体按自己的槽位生成脚和配饰()
        {
            var path = Path.Combine(Application.dataPath, "Resources/MonsterRig.json");
            var file = JsonUtility.FromJson<MonsterRigFile>(File.ReadAllText(path));
            var catalog = MonsterRigCatalog.PiecesFrom(file);
            var bare = FindPiece(catalog, "身体/身体4");
            var twoTrim = FindPiece(catalog, "身体/身体2");
            var oneTrim = FindPiece(catalog, "身体/身体5");

            var barePose = Filled(bare, catalog, 4);
            var twoPose = Filled(twoTrim, catalog, 2);
            var onePose = Filled(oneTrim, catalog, 5);

            Assert.That(CountKind(barePose, MonsterPartKind.Foot), Is.EqualTo(0));
            Assert.That(CountKind(barePose, MonsterPartKind.Hand), Is.EqualTo(0));
            Assert.That(CountKind(barePose, MonsterPartKind.Accessory), Is.EqualTo(3));
            Assert.That(CountKind(twoPose, MonsterPartKind.Foot), Is.EqualTo(2));
            Assert.That(CountKind(twoPose, MonsterPartKind.Accessory), Is.EqualTo(2));
            Assert.That(CountKind(onePose, MonsterPartKind.Foot), Is.EqualTo(0));
            Assert.That(CountKind(onePose, MonsterPartKind.Accessory), Is.EqualTo(1));
        }

        [Test]
        public void 第三套配方生成没脚且有三件配饰的画像()
        {
            Assert.That(MonsterRigRuntime.TryCompose(3, 3, out var pose), Is.True);
            Assert.That(CountKind(pose, MonsterPartKind.Foot), Is.EqualTo(0));
            Assert.That(CountKind(pose, MonsterPartKind.Hand), Is.EqualTo(0));
            Assert.That(CountKind(pose, MonsterPartKind.Accessory), Is.EqualTo(3));
        }

        [Test]
        public void 未看过的部件更常被抽到但扰动可以抽到看过的()
        {
            var pool = new List<string> { "a", "b" };
            var recent = new List<string> { "b" };

            var unseen = MonsterRigRandom.Pick(pool, recent, Sequence(1, 0));
            var seen = MonsterRigRandom.Pick(pool, recent, Sequence(0, 1));

            Assert.That(unseen, Is.EqualTo("a"));
            Assert.That(seen, Is.EqualTo("b"));
        }

        [Test]
        public void 主体排除次主体时左右都不能再抽到并且排除会传给头上的副体()
        {
            var body = Piece("身体", "身体A", 10, 10);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Hand, 1, 1, MonsterFacing.Right);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Hand, 2, 2, MonsterFacing.Left);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Head, 3, 3, MonsterFacing.None);
            var handKept = Piece("手", "手留", 4, 4);
            var handBanned = Piece("手", "手禁", 4, 4);
            var head = Piece("头", "头1", 10, 10);
            MonsterRigEdits.AddSocket(head, MonsterSocketGroup.HeadTrim, 4, 4, MonsterFacing.None);
            var hatKept = Piece("头饰", "头饰留", 4, 4);
            var hatBanned = Piece("头饰", "头饰禁", 4, 4);
            MonsterRigEdits.SetExcluded(body, handBanned.Data.id, true);
            MonsterRigEdits.SetExcluded(body, hatBanned.Data.id, true);
            var catalog = new List<MonsterRigPiece> { body, handKept, handBanned, head, hatKept, hatBanned };
            var assignments = new List<MonsterRigAssignment>();
            var groups = new HashSet<MonsterSocketGroup>
            {
                MonsterSocketGroup.Hand,
                MonsterSocketGroup.Head,
            };

            MonsterRigRandom.Fill(body, catalog, assignments, groups, new Dictionary<MonsterSocketGroup, List<string>>(), () => 0.5, true, null);

            Assert.That(Assigned(assignments, body.Data.sockets[0].id), Is.EqualTo(handKept.Data.id));
            Assert.That(Assigned(assignments, body.Data.sockets[1].id), Is.EqualTo(handKept.Data.id));
            Assert.That(Assigned(assignments, body.Data.sockets[2].id), Is.EqualTo(head.Data.id));
            Assert.That(Assigned(assignments, head.Data.sockets[0].id), Is.EqualTo(hatKept.Data.id));
        }

        static MonsterRigPose SampleCreature()
        {
            var body = Piece("身体", "身体1", 10, 10);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Head, 5, 8, MonsterFacing.None);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Hand, 2, 4, MonsterFacing.Right);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Foot, 3, 1, MonsterFacing.Right);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.BodyTrim, 6, 4, MonsterFacing.None);
            MonsterRigEdits.AddSocket(body, MonsterSocketGroup.Tail, 8, 2, MonsterFacing.None);
            var head = Piece("头", "头1", 8, 8);
            MonsterRigEdits.AddSocket(head, MonsterSocketGroup.Eye, 6, 6, MonsterFacing.Right);
            MonsterRigEdits.AddSocket(head, MonsterSocketGroup.Mouth, 5, 3, MonsterFacing.None);
            MonsterRigEdits.AddSocket(head, MonsterSocketGroup.HeadTrim, 4, 7, MonsterFacing.None);
            var eye = Piece("眼", "眼1", 2, 2);
            var mouth = Piece("嘴", "嘴1", 2, 2);
            var hat = Piece("头饰", "头饰1", 2, 2);
            var hand = Piece("手", "手1", 2, 2);
            var foot = Piece("脚", "脚1", 2, 2);
            var trim = Piece("配饰", "配饰1", 2, 2);
            var tail = Piece("尾巴", "尾巴1", 2, 2);
            var catalog = new List<MonsterRigPiece> { body, head, eye, mouth, hat, hand, foot, trim, tail };
            var assignments = new List<MonsterRigAssignment>
            {
                Assign(body, 0, head),
                Assign(body, 1, hand),
                Assign(body, 2, foot),
                Assign(body, 3, trim),
                Assign(body, 4, tail),
                Assign(head, 0, eye),
                Assign(head, 1, mouth),
                Assign(head, 2, hat),
            };
            return MonsterRigLayout.Build(body, catalog, assignments, null, false, 0f, 0f);
        }

        static MonsterRigAssignment Assign(MonsterRigPiece host, int socket, MonsterRigPiece child)
        {
            return new MonsterRigAssignment
            {
                socketId = host.Data.sockets[socket].id,
                partId = child.Data.id,
            };
        }

        static MonsterRigPose Filled(MonsterRigPiece body, List<MonsterRigPiece> catalog, int salt)
        {
            var assignments = new List<MonsterRigAssignment>();
            MonsterRigRandom.Fill(
                body,
                catalog,
                assignments,
                null,
                new Dictionary<MonsterSocketGroup, List<string>>(),
                MonsterRigRandom.Units(salt),
                true,
                null);
            return MonsterRigLayout.Build(body, catalog, assignments, null, false, 0f, 0f);
        }

        static MonsterRigPiece FindPiece(List<MonsterRigPiece> catalog, string id)
        {
            for (var i = 0; i < catalog.Count; i++)
            {
                if (catalog[i].Data.id == id)
                    return catalog[i];
            }

            Assert.Fail(id);
            return null;
        }

        static int CountKind(MonsterRigPose pose, MonsterPartKind kind)
        {
            var count = 0;
            for (var i = 0; i < pose.Nodes.Count; i++)
            {
                if (pose.Nodes[i].Kind == kind)
                    count += 1;
            }

            return count;
        }

        static int OrderOf(MonsterRigPose pose, string partId)
        {
            return Node(pose, partId).Order;
        }

        static MonsterRigNode Node(MonsterRigPose pose, string partId)
        {
            for (var i = 0; i < pose.Nodes.Count; i++)
            {
                if (pose.Nodes[i].PartId == partId)
                    return pose.Nodes[i];
            }

            Assert.Fail(partId);
            return null;
        }

        static MonsterPartRole Role(string folder)
        {
            Assert.That(MonsterRigCatalog.TryFolder(folder, out var spec), Is.True);
            return spec.Role;
        }

        static MonsterRigPiece Piece(string folder, string leaf, int width, int height)
        {
            Assert.That(MonsterRigCatalog.TryFolder(folder, out var spec), Is.True);
            var id = MonsterRigCatalog.PartId(folder, leaf);
            return new MonsterRigPiece
            {
                Data = MonsterRigEdits.CreatePart(id, width, height, spec.Kind == MonsterPartKind.Head),
                Folder = folder,
                Width = width,
                Height = height,
                Role = spec.Role,
                Kind = spec.Kind,
                Color = spec.Color,
            };
        }

        static System.Func<double> Sequence(params double[] values)
        {
            var index = 0;
            return () =>
            {
                var value = values[System.Math.Min(index, values.Length - 1)];
                index += 1;
                return value;
            };
        }

        static string Assigned(List<MonsterRigAssignment> assignments, string socketId)
        {
            for (var i = 0; i < assignments.Count; i++)
            {
                if (assignments[i].socketId == socketId)
                    return assignments[i].partId;
            }

            return null;
        }
    }
}
