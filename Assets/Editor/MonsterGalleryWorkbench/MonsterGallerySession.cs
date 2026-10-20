#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace TheCall.Editor
{
    sealed class Specimen
    {
        public int Id;
        public string Channel;
        public string Summary;
        public string Detail;
        public string MoodNote;
        public int Seed;
        public int Recipe, Palette, Head, Mouth, Arms, Legs, Crest, Pattern, Variation;
        public string Personality, Mood, Race;
        public List<string> Skills = new List<string>();
        public int Pose;
        public bool Duplicates;
        public int ParentCount;

        public MonsterAppearance Appearance => new MonsterAppearance(
            Recipe, Palette, Head, Mouth, Arms, Legs, Crest, Pattern, Variation);

        public float PoseSeconds => 0.35f + Pose * 0.72f;

        public string ChannelLabel
        {
            get
            {
                switch (Channel)
                {
                    case "opening-candidate": return "开局候选";
                    case "opening-produce": return "开局生产";
                    case "opening-support": return "开局辅助";
                    case "shop": return "商店";
                    case "breed": return "培育";
                    case "spawned": return "技能生成";
                    default: return Channel;
                }
            }
        }
    }

    static class MonsterGallerySession
    {
        const string SessionKey = "TheCall.MonsterGallery.Session.v1";
        const int HistoryLimit = 36;

        static readonly List<Specimen> Items = new List<Specimen>();
        static int _nextId = 1;
        static int _currentId;
        static string _filter = "any";
        static string _error = "";
        static SpecimenView _view;
        static int _viewCatalogRevision = -1;

        public static string Filter => _filter;

        public static void EnsureSeeded()
        {
            Load();
            if (Items.Count == 0) Roll("any");
            else EnsureView();
        }

        public static bool TryCommand(string command, Newtonsoft.Json.Linq.JObject payload, out string error)
        {
            error = "";
            try
            {
                switch (command)
                {
                    case "roll":
                        Roll(Source(payload));
                        return true;
                    case "show":
                        Show(RequireId(payload));
                        return true;
                    case "pose":
                        Pose(payload);
                        return true;
                    default:
                        error = "未知命令。";
                        return false;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static object SnapshotPayload()
        {
            EnsureView();
            var items = new List<object>(Items.Count);
            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                items.Add(new { id = item.Id, summary = item.Summary, channel = item.ChannelLabel });
            }

            return new
            {
                filter = _filter,
                currentId = _currentId,
                error = _error ?? "",
                items,
                specimen = _view == null ? null : _view.Payload(),
            };
        }

        static void Roll(string source)
        {
            _filter = source;
            var specimen = MonsterGallerySampler.Roll(source);
            specimen.Id = _nextId++;
            specimen.Pose = 0;
            var view = MonsterGalleryPresenter.Render(specimen);
            Items.Add(specimen);
            while (Items.Count > HistoryLimit)
                Items.RemoveAt(0);
            _currentId = specimen.Id;
            _view = view;
            _viewCatalogRevision = CatalogRevision();
            _error = "";
            Save();
        }

        static void Show(int id)
        {
            var specimen = Find(id);
            _currentId = specimen.Id;
            _view = MonsterGalleryPresenter.Render(specimen);
            _viewCatalogRevision = CatalogRevision();
            _error = "";
            Save();
        }

        static void Pose(Newtonsoft.Json.Linq.JObject payload)
        {
            var specimen = payload != null && payload["id"] != null ? Find(RequireId(payload)) : Find(_currentId);
            specimen.Pose = (specimen.Pose + 1) % 4;
            _currentId = specimen.Id;
            _view = MonsterGalleryPresenter.Render(specimen);
            _viewCatalogRevision = CatalogRevision();
            _error = "";
            Save();
        }

        static void EnsureView()
        {
            if (Items.Count == 0)
            {
                _view = null;
                return;
            }

            if (FindOrNull(_currentId) == null) _currentId = Items[Items.Count - 1].Id;
            var catalogRevision = CatalogRevision();
            if (_view != null && _view.Id == _currentId && _viewCatalogRevision == catalogRevision) return;
            try
            {
                _view = MonsterGalleryPresenter.Render(Find(_currentId));
                _viewCatalogRevision = catalogRevision;
                _error = "";
            }
            catch (Exception exception)
            {
                _view = null;
                _error = exception.Message;
            }
        }

        static Specimen Find(int id)
        {
            var found = FindOrNull(id);
            if (found == null) throw new InvalidOperationException("这只怪物不在当前抽查记录里。");
            return found;
        }

        static Specimen FindOrNull(int id)
        {
            for (var i = 0; i < Items.Count; i++)
                if (Items[i].Id == id) return Items[i];
            return null;
        }

        static int CatalogRevision()
        {
            var catalog = MonsterArtCatalog.Runtime;
            return catalog == null ? -1 : catalog.Revision;
        }

        static string Source(Newtonsoft.Json.Linq.JObject payload)
        {
            var source = payload?.Value<string>("source");
            if (string.IsNullOrEmpty(source)) source = _filter ?? "any";
            if (source != "any" && source != "opening" && source != "shop" && source != "breed")
                throw new InvalidOperationException("来源只能是 any、opening、shop 或 breed。");
            return source;
        }

        static int RequireId(Newtonsoft.Json.Linq.JObject payload)
        {
            var token = payload?["id"];
            if (token == null || token.Type != Newtonsoft.Json.Linq.JTokenType.Integer)
                throw new InvalidOperationException("需要整数编号。");
            return token.ToObject<int>();
        }

        static void Load()
        {
            if (Items.Count > 0) return;
            var json = SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var saved = JsonConvert.DeserializeObject<SavedSession>(json);
                if (saved == null) return;
                _filter = string.IsNullOrEmpty(saved.filter) ? "any" : saved.filter;
                _nextId = Math.Max(1, saved.nextId);
                _currentId = saved.currentId;
                if (saved.items != null) Items.AddRange(saved.items);
            }
            catch (Exception)
            {
                Items.Clear();
            }
        }

        static void Save()
        {
            var saved = new SavedSession
            {
                filter = _filter,
                nextId = _nextId,
                currentId = _currentId,
                items = Items,
            };
            SessionState.SetString(SessionKey, JsonConvert.SerializeObject(saved));
        }

        sealed class SavedSession
        {
            public string filter;
            public int nextId;
            public int currentId;
            public List<Specimen> items;
        }
    }

    sealed class SpecimenView
    {
        public int Id;
        public string Summary;
        public string Png;
        public List<object> Facts;
        public List<object> Parts;

        public object Payload() => new
        {
            id = Id,
            summary = Summary,
            png = Png,
            facts = Facts,
            parts = Parts,
        };
    }

    static class MonsterGalleryPresenter
    {
        static readonly string[] HeadNames = { "圆头", "扁头", "星头" };
        static readonly string[] ArmNames = { "前手", "细长手", "手指手", "触手" };
        static readonly string[] LegNames = { "手脚通用", "短脚", "手脚通用", "小爪" };
        static readonly string[] CrestNames = { "无", "单角", "纹角", "短角" };
        static readonly string[] PatternNames = { "无", "缝线", "斑纹", "星纹" };
        static readonly string[] ClosedMouths = { "奸笑", "厌恶", "缝合嘴", "奸笑", "讨好", "厌恶" };

        public static SpecimenView Render(Specimen specimen)
        {
            var traits = MonsterVisualTraits.Create(specimen.Appearance, specimen.Personality, specimen.Mood, specimen.Skills);
            var png = MonsterPortraitCapture.Render(traits, specimen.PoseSeconds);
            var catalog = MonsterArtCatalog.Runtime;
            var pieces = new List<MonsterComposition.Piece>(36);
            MonsterComposition.Compose(catalog, traits, pieces);
            return new SpecimenView
            {
                Id = specimen.Id,
                Summary = specimen.Summary,
                Png = Convert.ToBase64String(png),
                Facts = Facts(specimen, traits, catalog, pieces),
                Parts = PartRows(pieces),
            };
        }

        static List<object> Facts(Specimen specimen, MonsterVisualTraits traits, MonsterArtCatalog catalog, List<MonsterComposition.Piece> pieces)
        {
            var body = catalog.bodies[specimen.Recipe];
            var palette = catalog.palettes[specimen.Palette];
            var head = HeadNames[specimen.Head];
            if (body.integratedHead) head += "（胖体不换头图）";
            else if (specimen.Head == 2) head += "（星头，顶饰会下移）";
            return new List<object>
            {
                Fact("来源", specimen.ChannelLabel),
                Fact("这只为什么会出现", specimen.Detail),
                Fact("心情怎么来的", specimen.MoodNote),
                Fact("体型", body.name),
                Fact("配色", palette.name),
                Fact("头", head),
                Fact("嘴", MouthLabel(specimen, traits, body)),
                Fact("手", LimbLabel(ArmNames[specimen.Arms], Find(pieces, "FrontArm"), "前肢")),
                Fact("脚", LimbLabel(LegNames[specimen.Legs], Find(pieces, "FrontFoot"), "前脚")),
                Fact("顶饰", specimen.Crest == 0 ? "无" : CrestNames[specimen.Crest]),
                Fact("花纹", specimen.Pattern == 0 ? "无" : PatternNames[specimen.Pattern]),
                Fact("性格", specimen.Personality),
                Fact("眼型", EyeLabel(traits.EyeFamily)),
                Fact("心情", specimen.Mood),
                Fact("种族", specimen.Race + "（种族不改变拼图）"),
                Fact("技能", specimen.Skills.Count == 0 ? "无" : string.Join("、", specimen.Skills)),
                Fact("可见改造", traits.MutationDescription),
                Fact("部件数", pieces.Count.ToString(CultureInfo.InvariantCulture)),
            };
        }

        static string MouthLabel(Specimen specimen, MonsterVisualTraits traits, MonsterArtCatalog.BodyPlan body)
        {
            var stitched = (traits.Mutations & MonsterMutation.StitchedMouth) != 0;
            var tongue = (traits.Mutations & (MonsterMutation.LongTongue | MonsterMutation.ForkTongue)) != 0;
            var open = body.integratedHead && (specimen.Mouth == 0 || specimen.Mouth == 1 || tongue) && !stitched;
            if (open) return specimen.Mouth == 1 ? "开口 B" : "开口 A";
            if (stitched) return "缝合嘴";
            if (traits.MoodTier <= 1) return "不爽（心情盖过嘴基因）";
            if (traits.MoodTier >= 3) return "讨好（心情盖过嘴基因）";
            return ClosedMouths[specimen.Mouth];
        }

        static string LimbLabel(string gene, MonsterComposition.Piece piece, string slot)
        {
            if (string.IsNullOrEmpty(piece.Name)) return gene;
            var actual = piece.Art == null ? gene : piece.Art.id;
            return gene + "（" + slot + "实际图 " + actual + "）";
        }

        static MonsterComposition.Piece Find(List<MonsterComposition.Piece> pieces, string name)
        {
            for (var i = 0; i < pieces.Count; i++)
                if (pieces[i].Name == name) return pieces[i];
            return default;
        }

        static string EyeLabel(MonsterEyeFamily family)
        {
            switch (family)
            {
                case MonsterEyeFamily.Anxious: return "焦虑";
                case MonsterEyeFamily.Indifferent: return "冷漠";
                case MonsterEyeFamily.Manic: return "狂躁";
                case MonsterEyeFamily.Slothful: return "怠惰";
                case MonsterEyeFamily.Affectionate: return "渴爱";
                default: return family.ToString();
            }
        }

        static List<object> PartRows(List<MonsterComposition.Piece> pieces)
        {
            var rows = new List<object>(pieces.Count);
            for (var i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                rows.Add(new
                {
                    order = i + 1,
                    name = PartName(piece.Name),
                    art = piece.Art == null ? "" : piece.Art.id,
                    at = piece.Position.x.ToString("0", CultureInfo.InvariantCulture) + ", " +
                         piece.Position.y.ToString("0", CultureInfo.InvariantCulture),
                    turn = Mathf.Abs(piece.Angle) < 0.5f ? "" : piece.Angle.ToString("0", CultureInfo.InvariantCulture) + "°",
                    mirror = piece.Mirror,
                });
            }

            return rows;
        }

        static string PartName(string name)
        {
            if (name.StartsWith("Body_", StringComparison.Ordinal)) return "身体";
            if (name.StartsWith("EyeL", StringComparison.Ordinal)) return "左眼";
            if (name.StartsWith("EyeR", StringComparison.Ordinal)) return "右眼";
            if (name.StartsWith("Mouth", StringComparison.Ordinal)) return "嘴";
            if (name.StartsWith("BlushL", StringComparison.Ordinal)) return "左腮红";
            if (name.StartsWith("BlushR", StringComparison.Ordinal)) return "右腮红";
            switch (name)
            {
                case "WingL": return "左后翼";
                case "WingR": return "右后翼";
                case "ExtraNeck": return "副颈";
                case "BudNeck": return "芽颈";
                case "RearArm": return "后手";
                case "RearFoot": return "后脚";
                case "OralCavity": return "口腔";
                case "InnerTongue": return "内舌";
                case "UpperTeeth": return "上牙";
                case "FrontFoot": return "前脚";
                case "FrontArm": return "前手";
                case "GraftedTentacle": return "嫁接触手";
                case "ExtraArmL": return "左增生手";
                case "ExtraArmR": return "右增生手";
                case "BackSpineL": return "左尖刺";
                case "BackSpineR": return "右尖刺";
                case "ExtraHead": return "副头";
                case "BudHead": return "芽头";
                case "CrownBrain": return "开盖脑";
                case "CrownHorn": return "顶角";
                case "SideMouth": return "侧嘴";
                case "GraftedTongue": return "嫁接舌";
                case "OuterTongue": return "外舌";
                case "EyePatch": return "眼侧饰";
                case "BodyStitch": return "缝线";
                case "BodyStitchSecondary": return "次缝线";
                case "BodyStitchTertiary": return "第三缝线";
                case "BodyStitchInherited": return "遗传缝线";
                case "BodySpots": return "斑纹";
                case "BodySpotsSecondary": return "次斑纹";
                case "BodySpotsTertiary": return "第三斑纹";
                case "BodySpotsInherited": return "遗传斑纹";
                case "BodyStar": return "星纹";
                case "BodyStarSecondary": return "次星纹";
                case "BodyStarTertiary": return "第三星纹";
                case "BodyStarInherited": return "遗传星纹";
                default: return name;
            }
        }

        static object Fact(string label, string value) => new { label, value };
    }
}
#endif
