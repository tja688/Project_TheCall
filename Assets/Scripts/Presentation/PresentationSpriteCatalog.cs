using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheCall
{
    [Serializable]
    public sealed class ToolArtDef
    {
        public string name;
        public Sprite icon;
        public string description;
        public Rarity rarity;
        public int price;
    }

    [Serializable]
    public sealed class NamedSpriteDef
    {
        public string name;
        public Sprite icon;
    }

    [CreateAssetMenu(fileName = "PresentationSpriteCatalog", menuName = "TheCall/Presentation Sprite Catalog")]
    public sealed class PresentationSpriteCatalog : ScriptableObject
    {
        public List<ToolArtDef> tools = new List<ToolArtDef>();
        public List<NamedSpriteDef> races = new List<NamedSpriteDef>();
        public List<NamedSpriteDef> moods = new List<NamedSpriteDef>();
        public List<NamedSpriteDef> personalities = new List<NamedSpriteDef>();

        readonly Dictionary<string, ToolArtDef> _toolMap = new Dictionary<string, ToolArtDef>(StringComparer.Ordinal);
        readonly Dictionary<string, Sprite> _raceMap = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        readonly Dictionary<string, Sprite> _moodMap = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        readonly Dictionary<string, Sprite> _personalityMap = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        bool _initialized;

        void Init()
        {
            if (_initialized) return;
            _initialized = true;
            _toolMap.Clear();
            foreach (var t in tools)
                if (t != null && !string.IsNullOrEmpty(t.name))
                    _toolMap[t.name] = t;

            _raceMap.Clear();
            foreach (var r in races)
                if (r != null && !string.IsNullOrEmpty(r.name))
                    _raceMap[r.name] = r.icon;

            _moodMap.Clear();
            foreach (var m in moods)
                if (m != null && !string.IsNullOrEmpty(m.name))
                    _moodMap[m.name] = m.icon;

            _personalityMap.Clear();
            foreach (var p in personalities)
                if (p != null && !string.IsNullOrEmpty(p.name))
                    _personalityMap[p.name] = p.icon;
        }

        public ToolArtDef FindTool(string name)
        {
            Init();
            return _toolMap.TryGetValue(name ?? "", out var def) ? def : null;
        }

        public Sprite GetRaceIcon(string race)
        {
            Init();
            if (string.IsNullOrEmpty(race)) return null;
            if (_raceMap.TryGetValue(race, out var s)) return s;
            var element = race switch
            {
                "森林" => "木",
                "天空" => "火",
                "沙漠" => "土",
                "山脉" => "金",
                "海生" => "水",
                _ => null
            };
            return element != null && _raceMap.TryGetValue(element, out s) ? s : null;
        }

        public Sprite GetMoodIcon(string mood)
        {
            Init();
            if (string.IsNullOrEmpty(mood)) return null;
            if (_moodMap.TryGetValue(mood, out var s)) return s;
            var alias = mood switch
            {
                "热爱" => "极好",
                "热情" => "良好",
                "普通" => "普通",
                "疲倦" => "糟糕",
                "倦怠" => "崩溃",
                _ => null
            };
            return alias != null && _moodMap.TryGetValue(alias, out s) ? s : null;
        }

        public Sprite GetPersonalityIcon(string personality)
        {
            Init();
            return _personalityMap.TryGetValue(personality ?? "", out var s) ? s : null;
        }
    }
}
