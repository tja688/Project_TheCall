#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        public int Recipe, Palette, HeadAccessory, BackAccessory, Variation;
        public string Personality, Mood, Race;
        public List<string> Skills = new List<string>();
        public int Pose;
        public bool Duplicates;
        public int ParentCount;

        public MonsterAppearance Appearance => new MonsterAppearance(
            Recipe, Palette, HeadAccessory, BackAccessory, Variation);

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
        const string SessionKey = "TheCall.MonsterGallery.Session.v2";
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
                    case "preview":
                        Preview(payload);
                        return true;
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
            EnsureSeeded();
            var items = new List<object>(Items.Count);
            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                items.Add(new { id = item.Id, summary = item.Summary, channel = item.ChannelLabel });
            }

            return new
            {
                webSockets = MonsterGalleryWorkbench.SupportsWebSockets,
                options = new { templates = MonsterArtCatalog.Runtime.bodies.OrderBy(b=>(int)b.id).Select(b=>new { id=(int)b.id, name=b.name, pairs=Enumerable.Range(0,12).Where(code=>MonsterAppearance.Compatible((int)b.id,code/3,code%3)).ToArray() }).ToArray(),
                    palettes = MonsterArtCatalog.Runtime.palettes.OrderBy(p=>(int)p.id).Select(p=>new { id=(int)p.id,name=p.name,body=ColorUtility.ToHtmlStringRGB(p.body),pattern=ColorUtility.ToHtmlStringRGB(p.pattern),eye=ColorUtility.ToHtmlStringRGB(p.eye),mouth=ColorUtility.ToHtmlStringRGB(p.mouth) }).ToArray(),
                    personalities = ContentGate.Current.Personalities.Select(p=>p.Name).ToArray(), moods = new[] { "倦怠","疲倦","普通","热情","热爱" } },
                selection = Items.Count == 0 ? null : new { template=Find(_currentId).Recipe,palette=Find(_currentId).Palette,head=Find(_currentId).HeadAccessory,back=Find(_currentId).BackAccessory,personality=Find(_currentId).Personality,mood=Find(_currentId).Mood },
                filter = _filter,
                currentId = _currentId,
                error = _error ?? "",
                items,
                specimen = _view == null ? null : _view.Payload(),
            };
        }

        static void Preview(Newtonsoft.Json.Linq.JObject payload)
        {
            EnsureSeeded(); var item=Find(_currentId);
            var template=payload.Value<int?>("template")??item.Recipe; var palette=payload.Value<int?>("palette")??item.Palette;
            var head=payload.Value<int?>("head")??item.HeadAccessory; var back=payload.Value<int?>("back")??item.BackAccessory;
            if(template<0||template>=12||palette<0||palette>=6||!MonsterAppearance.Compatible(template,head,back)) throw new InvalidOperationException("该模板不支持这组饰品，请选择无饰品或其他组合。");
            var personality=payload.Value<string>("personality")??item.Personality; var mood=payload.Value<string>("mood")??item.Mood;
            if(ContentGate.Current.FindPersonality(personality)==null||ContentGate.Current.FindMood(mood)==null) throw new InvalidOperationException("未知性格或心情。");
            item.Recipe=template;item.Palette=palette;item.HeadAccessory=head;item.BackAccessory=back;item.Personality=personality;item.Mood=mood;
            item.Detail="固定模板预览，不修改对局。";item.MoodNote="抽查台手动预览状态。";item.Summary=MonsterArtCatalog.Runtime.Body(template).name+" · "+personality+" · "+mood;
            _view=null;EnsureView();Save();
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
        public static SpecimenView Render(Specimen specimen)
        {
            var catalog=MonsterArtCatalog.Runtime; var traits=MonsterVisualTraits.Create(specimen.Appearance,specimen.Personality,specimen.Mood,specimen.Skills);
            var pieces=new List<MonsterComposition.Piece>();MonsterComposition.Compose(catalog,traits,pieces);
            var palette=catalog.Colorway(specimen.Palette);
            return new SpecimenView { Id=specimen.Id,Summary=specimen.Summary,Png=Convert.ToBase64String(MonsterPortraitCapture.Render(traits,specimen.PoseSeconds)),
                Facts=new List<object> { Fact("来源",specimen.ChannelLabel),Fact("模板",catalog.Body(specimen.Recipe).name),Fact("色卡",palette.name),
                    Fact("头饰",new[]{"无附加头饰","短角","纹角","蝴蝶结"}[specimen.HeadAccessory]),Fact("背饰",new[]{"无附加背饰","蝙蝠翼","羽翼"}[specimen.BackAccessory]),
                    Fact("性格与眼型",specimen.Personality+" / "+new[]{"焦虑","冷漠","狂躁","怠惰","渴爱"}[(int)traits.EyeFamily]),Fact("心情",specimen.Mood),Fact("心情来源",specimen.MoodNote),Fact("技能",string.Join("、",specimen.Skills)),
                    Fact("外观规则","成品结构固定；花纹同色略深，眼嘴异色；技能不改造身体。"),Fact("本次来源",specimen.Detail) },
                Parts=pieces.Select((p,i)=>(object)new {order=i+1,name=p.Name,art=p.Art.id,at=p.Position.ToString(),turn=p.Angle.ToString(CultureInfo.InvariantCulture),mirror=p.Mirror}).ToList() };
        }
        static object Fact(string label,string value)=>new {label,value};
    }
}
#endif
