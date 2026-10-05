using System.Collections.Generic;
using QFramework;

namespace TheCall
{
    public interface IDraw : IUtility
    {
        T Choose<T>(IReadOnlyList<T> options);

        bool Chance(int percent);
    }
}
