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

            var name = _names.Dequeue();
            foreach (var option in options)
            {
                if (option is string text && text == name)
                    return option;
            }

            throw new InvalidOperationException("抽取名单里没有 " + name);
        }

        public bool Chance(int percent) => false;
    }
}
