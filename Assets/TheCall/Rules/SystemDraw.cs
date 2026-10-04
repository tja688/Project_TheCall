using System;
using System.Collections.Generic;

namespace TheCall
{
    internal sealed class SystemDraw : IDraw
    {
        readonly Random _random = new Random();

        public T Choose<T>(IReadOnlyList<T> options)
        {
            if (options == null || options.Count == 0)
                throw new InvalidOperationException("抽取名单是空的。");

            return options[_random.Next(options.Count)];
        }

        public bool Chance(int percent)
        {
            if (percent <= 0)
                return false;
            if (percent >= 100)
                return true;

            return _random.Next(100) < percent;
        }
    }
}
