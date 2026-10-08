using System.Collections.Generic;
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
            var child = pose.Nodes[1];

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
