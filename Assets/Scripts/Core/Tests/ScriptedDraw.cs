using System;
using System.Collections.Generic;

namespace TheCall.Tests
{
    sealed class ScriptedDraw : IDraw
    {
        readonly Queue<string> _names;

        public ScriptedDraw(params string[] names)
            : this(true, names)
        {
        }

        public static ScriptedDraw Exact(params string[] names) => new ScriptedDraw(false, names);

        ScriptedDraw(bool adapt, string[] names)
        {
            if (!adapt)
                DrawAdapt.ClearPending();

            _names = new Queue<string>(adapt ? DrawAdapt.Adapt(names) : names ?? new string[0]);
        }

        public T Choose<T>(IReadOnlyList<T> options)
        {
            if (_names.Count == 0)
            {
                if (options == null || options.Count == 0)
                    throw new InvalidOperationException("抽取名单是空的。");

                return options[0];
            }

            var name = _names.Peek();
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option is string text && text == name)
                {
                    _names.Dequeue();
                    return option;
                }

                if (option is Rarity r && (r.ToString() == name || (name == "白" && r == Rarity.White) || (name == "蓝" && r == Rarity.Blue) || (name == "金" && r == Rarity.Gold)))
                {
                    _names.Dequeue();
                    return option;
                }
            }

            if (ContentGate.Current != null)
            {
                var tool = ContentGate.Current.FindTool(name);
                if (tool != null)
                {
                    for (var i = 0; i < options.Count; i++)
                    {
                        var option = options[i];
                        if (option is Rarity r && r == tool.Rarity)
                            return option;
                        if (option is string s && (s == tool.Rarity.ToString() || (s == "白" && tool.Rarity == Rarity.White) || (s == "蓝" && tool.Rarity == Rarity.Blue) || (s == "金" && tool.Rarity == Rarity.Gold)))
                            return option;
                    }
                }
            }

            // If options is Rarity and queue head is not a matching rarity/tool:
            if (options.Count > 0 && options[0] is Rarity)
                return options[0];

            // If options is personality/race/rarity but queue head is not in options, don't dequeue; return options[0]
            if (options.Count > 0 && (options[0] is RaceDef || options[0] is PersonalityDef))
                return options[0];

            if (ContentGate.Current != null && options.Count > 0 && options[0] is string optName)
            {
                if (ContentGate.Current.FindRace(optName) != null && ContentGate.Current.FindRace(name) == null)
                    return options[0];
                if (ContentGate.Current.FindPersonality(optName) != null && ContentGate.Current.FindPersonality(name) == null)
                    return options[0];
            }

            name = _names.Dequeue();
            throw new InvalidOperationException("抽取名单里没有 " + name);
        }

        public bool Chance(int percent) => false;
    }
}
