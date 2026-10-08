#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace TheCall.Editor
{
    public static class MonsterRigSession
    {
        const string WorkingKey = "TheCall.MonsterRig.Working";
        const string BaselineKey = "TheCall.MonsterRig.Baseline";
        const string RecoverKey = "TheCall.MonsterRig.Recover";

        static readonly Dictionary<MonsterSocketGroup, List<string>> Recent = new Dictionary<MonsterSocketGroup, List<string>>();
        static readonly HashSet<MonsterSocketGroup> Visible = new HashSet<MonsterSocketGroup>();
        static readonly List<MonsterRigAssignment> Assignments = new List<MonsterRigAssignment>();
        static readonly Dictionary<string, ArtFile> Art = new Dictionary<string, ArtFile>(StringComparer.Ordinal);

        static MonsterRigFile file = new MonsterRigFile();
        static List<MonsterRigPiece> pieces = new List<MonsterRigPiece>();
        static string baselineJson = "";
        static string recoverJson;
        static bool loaded;
        static bool blocked;
        static long revision = 1;
        static string selectedPartId;
        static string selectedSocketId;
        static string armedGroup;
        static string mode = "edit";
        static int palette;
        static bool headTurnPreview;
        static float previewTime;
        static float cycles = 0.42f;
        static System.Random random = new System.Random(1);

        public static long Revision => revision;

        public static void EnsureLoaded()
        {
            if (loaded)
                return;

            var profile = AssetDatabase.LoadAssetAtPath<MonsterMotionProfile>("Assets/Resources/MonsterMotionProfile.asset");
            if (profile != null)
                cycles = profile.idleCyclesPerSecond;

            ScanArt();
            var disk = ReadDisk();
            baselineJson = disk ?? "";
            var savedWorking = SessionState.GetString(WorkingKey, "");
            var savedBaseline = SessionState.GetString(BaselineKey, "");
            recoverJson = SessionState.GetString(RecoverKey, "");
            if (!string.IsNullOrEmpty(savedWorking) && savedBaseline == Hash(baselineJson))
                file = ParseOrEmpty(savedWorking);
            else if (!string.IsNullOrEmpty(savedWorking) && savedBaseline != Hash(baselineJson) && !string.IsNullOrEmpty(baselineJson))
            {
                file = ParseOrEmpty(disk);
                recoverJson = savedWorking;
                blocked = true;
            }
            else
                file = ParseOrEmpty(disk);

            MergeScan();
            foreach (MonsterSocketGroup group in Enum.GetValues(typeof(MonsterSocketGroup)))
                Visible.Add(group);
            var body = FirstBody();
            if (body != null)
                selectedPartId = body.Data.id;
            else if (pieces.Count > 0)
                selectedPartId = pieces[0].Data.id;
            loaded = true;
            revision = 1;
        }

        public static object SnapshotEnvelope()
        {
            EnsureLoaded();
            return new
            {
                type = "snapshot",
                protocolVersion = 1,
                revision,
                payload = Payload(),
            };
        }

        public static object Dispatch(string body)
        {
            EnsureLoaded();
            JObject message;
            try
            {
                message = JObject.Parse(string.IsNullOrEmpty(body) ? "{}" : body);
            }
            catch (Exception)
            {
                return Result("", false, "命令不是 JSON");
            }

            var requestId = message.Value<string>("requestId") ?? "";
            var command = message.Value<string>("command") ?? "";
            var payload = message["payload"] as JObject ?? new JObject();
            var error = Run(command, payload);
            if (error == null)
            {
                revision += 1;
                if (command != "setPreviewTime" && command != "selectPart" && command != "selectSocket" && command != "armGroup" && command != "setMode")
                    PersistTransient();
            }

            return Result(requestId, error == null, error);
        }

        public static byte[] ArtBytes(string id, out string error)
        {
            EnsureLoaded();
            error = null;
            if (!Art.TryGetValue(id ?? "", out var art) || art.Missing)
            {
                error = "没有这张素材";
                return null;
            }

            var root = Path.GetFullPath(ArtRoot()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(art.AbsolutePath);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                error = "素材路径无效";
                return null;
            }

            return File.ReadAllBytes(full);
        }

        static object Result(string requestId, bool ok, string error)
        {
            return new
            {
                type = "commandResult",
                requestId,
                ok,
                revision,
                payload = Payload(),
                error = error ?? "",
            };
        }

        static string Run(string command, JObject payload)
        {
            switch (command)
            {
                case "selectPart":
                    return Select(Str(payload, "id"));
                case "selectSocket":
                    selectedSocketId = Str(payload, "socketId");
                    return null;
                case "armGroup":
                    armedGroup = Str(payload, "group");
                    return null;
                case "addSocket":
                    return AddSocket(payload);
                case "moveSocket":
                    return EditSocket(payload, true);
                case "setFacing":
                    return EditSocket(payload, false);
                case "removeSocket":
                    return RemoveSocket(Str(payload, "socketId"));
                case "moveAttachment":
                    return MoveAttachment(Int(payload, "x"), Int(payload, "y"));
                case "setSwing":
                    return WithPiece(Str(payload, "partId"), piece => MonsterRigEdits.SetSwing(piece, Float(payload, "degrees")));
                case "setHeadTurn":
                    return WithPiece(Str(payload, "partId"), piece => MonsterRigEdits.SetHeadTurn(piece, Bool(payload, "enabled")));
                case "setNativeFacing":
                    return SetNativeFacing(Str(payload, "partId"), Str(payload, "facing"));
                case "setMount":
                    return WithPiece(SelectedId(payload), piece => MonsterRigEdits.SetMount(piece, Str(payload, "socketId")));
                case "setExcluded":
                    return WithPiece(SelectedId(payload), piece => MonsterRigEdits.SetExcluded(piece, Str(payload, "bannedId"), Bool(payload, "excluded")));
                case "setGroupVisible":
                    return SetVisible(Str(payload, "group"), Bool(payload, "visible"));
                case "assign":
                    return Assign(Str(payload, "socketId"), Str(payload, "partId"));
                case "clearAssign":
                    return Assign(Str(payload, "socketId"), null);
                case "randomize":
                    return Randomize(false);
                case "randomizeMonster":
                    return Randomize(true);
                case "setPalette":
                    palette = Mathf.Clamp(Int(payload, "index"), 0, MonsterPortrait.Palette.Length - 1);
                    return null;
                case "setHeadTurnPreview":
                    headTurnPreview = Bool(payload, "shown");
                    return null;
                case "setPreviewTime":
                    previewTime = Float(payload, "time");
                    return null;
                case "setMode":
                    mode = Str(payload, "mode") == "monster" ? "monster" : "edit";
                    return null;
                case "save":
                    return Save();
                case "revert":
                    return Revert();
                case "recoverTransient":
                    return Recover();
                case "discardTransient":
                    return DiscardRecover();
                default:
                    return "未知命令";
            }
        }

        static string Select(string id)
        {
            if (Find(id) == null)
                return "没有这个部件";

            selectedPartId = id;
            selectedSocketId = null;
            if (mode == "monster")
                mode = "edit";
            return null;
        }

        static string AddSocket(JObject payload)
        {
            if (!Enum.TryParse(Str(payload, "group"), out MonsterSocketGroup group))
                return "未知挂点组";

            var piece = Find(SelectedId(payload));
            if (piece == null)
                return "没有选中的部件";

            var error = MonsterRigEdits.AddSocket(piece, group, Int(payload, "x"), Int(payload, "y"), Facing(payload));
            if (error == null && piece.Data.sockets.Count > 0)
                selectedSocketId = piece.Data.sockets[piece.Data.sockets.Count - 1].id;
            return error;
        }

        static string EditSocket(JObject payload, bool move)
        {
            var socketId = Str(payload, "socketId");
            var piece = Owner(socketId);
            if (piece == null)
                return "找不到挂点";

            selectedSocketId = socketId;
            return move
                ? MonsterRigEdits.MoveSocket(piece, socketId, Int(payload, "x"), Int(payload, "y"))
                : MonsterRigEdits.SetFacing(piece, socketId, Facing(payload));
        }

        static string RemoveSocket(string socketId)
        {
            var piece = Owner(socketId);
            if (piece == null)
                return "找不到挂点";

            var error = MonsterRigEdits.RemoveSocket(piece, socketId, Assignments);
            if (error == null && selectedSocketId == socketId)
                selectedSocketId = null;
            return error;
        }

        static string MoveAttachment(int x, int y)
        {
            var piece = Find(selectedPartId);
            if (piece == null)
                return "没有选中的部件";

            return MonsterRigEdits.MoveAttachment(piece, x, y);
        }

        static string SetNativeFacing(string partId, string facingName)
        {
            var piece = Find(string.IsNullOrEmpty(partId) ? selectedPartId : partId);
            if (piece == null)
                return "没有选中的部件";

            if (piece.Role != MonsterPartRole.SubPrimary)
                return "只有次主体区分左右素材";

            if (!Enum.TryParse(facingName, out MonsterFacing facing) || facing == MonsterFacing.None)
                return "素材朝向要标明左或右";

            piece.Data.nativeFacing = facing;
            return null;
        }

        static string SetVisible(string groupName, bool visible)
        {
            if (!Enum.TryParse(groupName, out MonsterSocketGroup group))
                return "未知挂点组";

            if (visible)
                Visible.Add(group);
            else
                Visible.Remove(group);
            return null;
        }

        static string Assign(string socketId, string partId)
        {
            var piece = Owner(socketId);
            if (piece == null)
                return "找不到挂点";

            MonsterRigSocket socket = null;
            for (var i = 0; i < piece.Data.sockets.Count; i++)
            {
                if (piece.Data.sockets[i].id == socketId)
                    socket = piece.Data.sockets[i];
            }

            var spec = socket == null ? null : MonsterRigCatalog.GroupSpec(socket.group);
            if (spec == null || spec.Mount)
                return "对接点不装部件";

            if (!string.IsNullOrEmpty(partId))
            {
                var child = Find(partId);
                if (child == null || child.Kind != spec.Accepts)
                    return "这个挂点不接受该部件";

                if (piece.Data.excluded != null && piece.Data.excluded.Contains(partId))
                    return "这个主体排除了该部件";
            }

            for (var i = Assignments.Count - 1; i >= 0; i--)
            {
                if (Assignments[i].socketId != socketId)
                    continue;

                if (string.IsNullOrEmpty(partId))
                    Assignments.RemoveAt(i);
                else
                    Assignments[i].partId = partId;
                return null;
            }

            if (!string.IsNullOrEmpty(partId))
                Assignments.Add(new MonsterRigAssignment { socketId = socketId, partId = partId });
            return null;
        }

        static string Randomize(bool wholeMonster)
        {
            EnsurePieces();
            if (wholeMonster || mode == "monster")
            {
                var bodies = MonsterRigRandom.Pool(pieces, MonsterPartKind.Body, null, null);
                var bodyId = MonsterRigRandom.Pick(bodies, null, NextUnit);
                var body = Find(bodyId);
                if (body == null)
                    return "没有身体可以预览";

                selectedPartId = body.Data.id;
                mode = "monster";
                MonsterRigRandom.Fill(body, pieces, Assignments, null, Recent, NextUnit, true, null);
                return null;
            }

            var host = Find(selectedPartId);
            if (host == null || host.Role != MonsterPartRole.Primary)
                return "先选一个身体或头，再随机勾选的挂点组";

            MonsterRigRandom.Fill(host, pieces, Assignments, Visible, Recent, NextUnit, true, null);
            return null;
        }

        static string Save()
        {
            if (blocked)
                return "磁盘上的装配记录已经变了。先恢复这次编辑，或放弃这次编辑。";

            var json = JsonUtility.ToJson(file, true);
            var path = RigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? path);
            File.WriteAllText(path, json, Encoding.UTF8);
            StandardizeArt();
            SyncRuntimeCopies();
            AssetDatabase.Refresh();
            baselineJson = json;
            blocked = false;
            recoverJson = "";
            return null;
        }

        static string Revert()
        {
            if (blocked)
                return "先处理磁盘冲突。";

            file = ParseOrEmpty(baselineJson);
            MergeScan();
            Assignments.Clear();
            return null;
        }

        static string Recover()
        {
            if (string.IsNullOrEmpty(recoverJson))
                return "没有可恢复的编辑";

            file = ParseOrEmpty(recoverJson);
            MergeScan();
            blocked = false;
            recoverJson = "";
            return Save();
        }

        static string DiscardRecover()
        {
            recoverJson = "";
            blocked = false;
            file = ParseOrEmpty(baselineJson);
            MergeScan();
            Assignments.Clear();
            return null;
        }

        static void StandardizeArt()
        {
            foreach (var art in Art.Values)
            {
                if (!art.Missing)
                    ApplyImport(art.AssetPath);
            }
        }

        static void SyncRuntimeCopies()
        {
            foreach (var art in Art.Values)
            {
                if (art.Missing || !FolderToResource(art.Folder, out var resourceFolder))
                    continue;

                var destination = Path.Combine(Application.dataPath, "Resources/MonsterParts", resourceFolder, art.Leaf + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? destination);
                if (!File.Exists(destination) || !BytesEqual(art.AbsolutePath, destination))
                    File.Copy(art.AbsolutePath, destination, true);

                var assetPath = "Assets/Resources/MonsterParts/" + resourceFolder + "/" + art.Leaf + ".png";
                ApplyImport(assetPath);
            }
        }

        static void ApplyImport(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return;

            var changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                changed = true;
            }

            var platform = importer.GetDefaultPlatformTextureSettings();
            if (platform.textureCompression != TextureImporterCompression.Uncompressed)
            {
                platform.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(platform);
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();
        }

        static bool FolderToResource(string folder, out string resourceFolder)
        {
            switch (folder)
            {
                case "身体": resourceFolder = "Body"; return true;
                case "头": resourceFolder = "Head"; return true;
                case "眼": resourceFolder = "Eye"; return true;
                case "嘴": resourceFolder = "Mouth"; return true;
                case "手": resourceFolder = "Hand"; return true;
                case "脚": resourceFolder = "Foot"; return true;
                case "尾巴": resourceFolder = "Tail"; return true;
                case "头饰": resourceFolder = "Hat"; return true;
                case "配饰": resourceFolder = "Accessory"; return true;
                default:
                    resourceFolder = null;
                    return false;
            }
        }

        static object Payload()
        {
            EnsurePieces();
            var root = mode == "monster" ? Find(selectedPartId) : Find(selectedPartId);
            if (mode == "monster" && (root == null || root.Kind != MonsterPartKind.Body))
                root = FirstBody();

            var visible = mode == "monster" ? null : Visible;
            var pose = MonsterRigLayout.Build(root, pieces, Assignments, visible, headTurnPreview, previewTime, cycles);
            return new
            {
                dirty = Hash(JsonUtility.ToJson(file, true)) != Hash(baselineJson),
                blocked,
                recover = !string.IsNullOrEmpty(recoverJson),
                mode,
                selectedPartId,
                selectedSocketId,
                armedGroup,
                palette,
                headTurnPreview,
                cyclesPerSecond = cycles,
                palettes = PalettePayload(),
                groups = GroupPayload(),
                parts = PartPayload(),
                visibleGroups = VisibleNames(),
                assignments = AssignmentPayload(),
                pose = PosePayload(pose),
            };
        }

        static object[] PalettePayload()
        {
            var list = new object[MonsterPortrait.Palette.Length];
            for (var i = 0; i < list.Length; i++)
            {
                var primary = MonsterPortrait.Palette[i];
                var secondary = Color.Lerp(primary, Color.white, 0.28f);
                list[i] = new { index = i, primary = Hex(primary), secondary = Hex(secondary) };
            }

            return list;
        }

        static object[] GroupPayload()
        {
            var list = new object[MonsterRigCatalog.Groups.Length];
            for (var i = 0; i < list.Length; i++)
            {
                var group = MonsterRigCatalog.Groups[i];
                list[i] = new
                {
                    id = group.Group.ToString(),
                    label = group.Label,
                    color = group.Color,
                    sided = group.Sided,
                    mount = group.Mount,
                    accepts = group.Accepts.ToString(),
                };
            }

            return list;
        }

        static object[] PartPayload()
        {
            var list = new object[pieces.Count];
            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                var data = piece.Data;
                list[i] = new
                {
                    id = data.id,
                    folder = piece.Folder,
                    leaf = Leaf(data.id),
                    role = piece.Role.ToString(),
                    kind = piece.Kind.ToString(),
                    color = piece.Color.ToString(),
                    width = piece.Width,
                    height = piece.Height,
                    missing = piece.Missing,
                    groups = AllowedGroups(piece.Folder),
                    attachX = data.attachX,
                    attachY = data.attachY,
                    nativeFacing = data.nativeFacing.ToString(),
                    swingDegrees = data.swingDegrees,
                    headTurn = data.headTurn,
                    mountSocketId = data.mountSocketId,
                    excluded = data.excluded ?? new List<string>(),
                    sockets = SocketPayload(data),
                };
            }

            return list;
        }

        static string[] AllowedGroups(string folder)
        {
            if (!MonsterRigCatalog.TryFolder(folder, out var spec) || spec.Groups == null)
                return Array.Empty<string>();

            var names = new string[spec.Groups.Length];
            for (var i = 0; i < names.Length; i++)
                names[i] = spec.Groups[i].ToString();
            return names;
        }

        static object[] SocketPayload(MonsterRigPart data)
        {
            var sockets = data.sockets ?? new List<MonsterRigSocket>();
            var list = new object[sockets.Count];
            for (var i = 0; i < sockets.Count; i++)
            {
                var socket = sockets[i];
                list[i] = new
                {
                    id = socket.id,
                    group = socket.group.ToString(),
                    x = socket.x,
                    y = socket.y,
                    accepts = socket.accepts.ToString(),
                };
            }

            return list;
        }

        static string[] VisibleNames()
        {
            var names = new List<string>();
            foreach (var group in Visible)
                names.Add(group.ToString());
            return names.ToArray();
        }

        static object[] AssignmentPayload()
        {
            var list = new object[Assignments.Count];
            for (var i = 0; i < Assignments.Count; i++)
                list[i] = new { socketId = Assignments[i].socketId, partId = Assignments[i].partId };
            return list;
        }

        static object PosePayload(MonsterRigPose pose)
        {
            var nodes = new object[pose.Nodes.Count];
            for (var i = 0; i < pose.Nodes.Count; i++)
            {
                var node = pose.Nodes[i];
                var placement = MonsterRigLayout.PlacementFor(node);
                nodes[i] = new
                {
                    partId = node.PartId,
                    socketId = node.SocketId,
                    width = node.Width,
                    height = node.Height,
                    attachX = node.AttachX,
                    attachY = node.AttachY,
                    worldAttachX = node.WorldAttachX,
                    worldAttachY = node.WorldAttachY,
                    worldRotation = node.WorldRotation,
                    worldMirror = node.WorldMirror,
                    order = node.Order,
                    color = node.Color.ToString(),
                    sizeX = placement.Size.x,
                    sizeY = placement.Size.y,
                    pivotX = placement.Pivot.x,
                    pivotY = placement.Pivot.y,
                    anchoredX = placement.AnchoredPosition.x,
                    anchoredY = placement.AnchoredPosition.y,
                    scaleX = placement.Scale.x,
                };
            }

            var pins = new object[pose.Pins.Count];
            for (var i = 0; i < pose.Pins.Count; i++)
            {
                var pin = pose.Pins[i];
                var spec = MonsterRigCatalog.GroupSpec(pin.Group);
                pins[i] = new
                {
                    partId = pin.PartId,
                    socketId = pin.SocketId,
                    group = pin.Group.ToString(),
                    label = spec == null ? pin.Group.ToString() : spec.Label,
                    color = spec == null ? "#ffffff" : spec.Color,
                    accepts = pin.Accepts.ToString(),
                    worldX = pin.WorldX,
                    worldY = pin.WorldY,
                    mount = pin.Mount,
                };
            }

            return new { nodes, pins, warnings = pose.Warnings };
        }

        static void ScanArt()
        {
            Art.Clear();
            var root = ArtRoot();
            if (!Directory.Exists(root))
                return;

            foreach (var directory in Directory.GetDirectories(root))
            {
                var folder = Path.GetFileName(directory);
                if (!MonsterRigCatalog.TryFolder(folder, out _))
                    continue;

                foreach (var png in Directory.GetFiles(directory, "*.png"))
                {
                    var leaf = Path.GetFileNameWithoutExtension(png);
                    var id = MonsterRigCatalog.PartId(folder, leaf);
                    TryPngSize(png, out var width, out var height);
                    Art[id] = new ArtFile
                    {
                        Folder = folder,
                        Leaf = leaf,
                        AbsolutePath = png,
                        AssetPath = "Assets/Arts/Others/怪物部位/" + folder + "/" + leaf + ".png",
                        Width = width,
                        Height = height,
                    };
                }
            }
        }

        static void MergeScan()
        {
            file.parts ??= new List<MonsterRigPart>();
            var known = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < file.parts.Count; i++)
            {
                var part = file.parts[i];
                if (part == null || string.IsNullOrEmpty(part.id))
                    continue;

                known.Add(part.id);
                part.excluded ??= new List<string>();
                part.sockets ??= new List<MonsterRigSocket>();
                part.swingDegrees = Mathf.Clamp(part.swingDegrees, 0f, 40f);
            }

            foreach (var pair in Art)
            {
                if (known.Contains(pair.Key))
                    continue;

                MonsterRigCatalog.TryFolder(pair.Value.Folder, out var spec);
                file.parts.Add(MonsterRigEdits.CreatePart(pair.Key, pair.Value.Width, pair.Value.Height, spec != null && spec.Kind == MonsterPartKind.Head));
            }

            file.parts.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            EnsurePieces();
        }

        static void EnsurePieces()
        {
            pieces = new List<MonsterRigPiece>();
            for (var i = 0; i < file.parts.Count; i++)
            {
                var data = file.parts[i];
                if (data == null || !MonsterRigCatalog.TrySplitId(data.id, out var folder, out _))
                    continue;

                if (!MonsterRigCatalog.TryFolder(folder, out var spec))
                    continue;

                if (spec.Role != MonsterPartRole.Primary)
                    data.sockets = new List<MonsterRigSocket>();

                var width = 1;
                var height = 1;
                var missing = true;
                if (Art.TryGetValue(data.id, out var art))
                {
                    width = art.Width;
                    height = art.Height;
                    missing = false;
                }

                pieces.Add(new MonsterRigPiece
                {
                    Data = data,
                    Folder = folder,
                    Width = width,
                    Height = height,
                    Role = spec.Role,
                    Kind = spec.Kind,
                    Color = spec.Color,
                    Missing = missing,
                });
            }
        }

        static MonsterRigPiece Find(string id)
        {
            EnsurePieces();
            for (var i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].Data != null && pieces[i].Data.id == id)
                    return pieces[i];
            }

            return null;
        }

        static MonsterRigPiece Owner(string socketId)
        {
            EnsurePieces();
            for (var i = 0; i < pieces.Count; i++)
            {
                var sockets = pieces[i].Data.sockets;
                if (sockets == null)
                    continue;

                for (var s = 0; s < sockets.Count; s++)
                {
                    if (sockets[s].id == socketId)
                        return pieces[i];
                }
            }

            return null;
        }

        static MonsterRigPiece FirstBody()
        {
            for (var i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].Kind == MonsterPartKind.Body && !pieces[i].Missing)
                    return pieces[i];
            }

            return null;
        }

        static string WithPiece(string id, Func<MonsterRigPiece, string> edit)
        {
            var piece = Find(string.IsNullOrEmpty(id) ? selectedPartId : id);
            if (piece == null)
                return "没有选中的部件";

            return edit(piece);
        }

        static string SelectedId(JObject payload)
        {
            var id = Str(payload, "partId");
            return string.IsNullOrEmpty(id) ? selectedPartId : id;
        }

        static void PersistTransient()
        {
            SessionState.SetString(WorkingKey, JsonUtility.ToJson(file));
            SessionState.SetString(BaselineKey, Hash(baselineJson));
            SessionState.SetString(RecoverKey, recoverJson ?? "");
        }

        static string ReadDisk()
        {
            var path = RigPath();
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
        }

        static MonsterRigFile ParseOrEmpty(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new MonsterRigFile();

            try
            {
                var parsed = JsonUtility.FromJson<MonsterRigFile>(json);
                return parsed ?? new MonsterRigFile();
            }
            catch (Exception)
            {
                return new MonsterRigFile();
            }
        }

        static string RigPath()
        {
            return Path.Combine(Application.dataPath, "Resources/MonsterRig.json");
        }

        static string ArtRoot()
        {
            return Path.Combine(Application.dataPath, "Arts/Others/怪物部位");
        }

        static double NextUnit()
        {
            return random.NextDouble();
        }

        static string Hash(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""));
                var text = new StringBuilder(bytes.Length * 2);
                for (var i = 0; i < bytes.Length; i++)
                    text.Append(bytes[i].ToString("x2"));
                return text.ToString();
            }
        }

        static bool BytesEqual(string left, string right)
        {
            var a = File.ReadAllBytes(left);
            var b = File.ReadAllBytes(right);
            if (a.Length != b.Length)
                return false;

            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        static bool TryPngSize(string path, out int width, out int height)
        {
            width = 1;
            height = 1;
            using (var stream = File.OpenRead(path))
            {
                var header = new byte[24];
                if (stream.Read(header, 0, 24) < 24)
                    return false;

                width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            }

            return width > 0 && height > 0;
        }

        static string Leaf(string id)
        {
            MonsterRigCatalog.TrySplitId(id, out _, out var leaf);
            return leaf ?? id;
        }

        static string Hex(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        static string Str(JObject payload, string key)
        {
            return payload.Value<string>(key) ?? "";
        }

        static int Int(JObject payload, string key)
        {
            var token = payload[key];
            if (token == null || token.Type == JTokenType.Null)
                return 0;

            return token.Type == JTokenType.Integer ? token.Value<int>() : Mathf.RoundToInt(token.Value<float>());
        }

        static float Float(JObject payload, string key)
        {
            var token = payload[key];
            return token == null ? 0f : token.Value<float>();
        }

        static bool Bool(JObject payload, string key)
        {
            return payload.Value<bool?>(key) ?? false;
        }

        static MonsterFacing Facing(JObject payload)
        {
            return Enum.TryParse(Str(payload, "facing"), out MonsterFacing facing) ? facing : MonsterFacing.None;
        }

        sealed class ArtFile
        {
            public string Folder;
            public string Leaf;
            public string AbsolutePath;
            public string AssetPath;
            public int Width;
            public int Height;
            public bool Missing;
        }
    }
}
#endif
