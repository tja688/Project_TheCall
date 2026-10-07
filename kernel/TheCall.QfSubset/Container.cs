using System;
using System.Collections.Generic;
using System.Linq;

namespace QFramework
{
    public class IOCContainer
    {
        readonly Dictionary<Type, object> mInstances = new Dictionary<Type, object>();

        public void Register<T>(T instance)
        {
            var key = typeof(T);
            mInstances[key] = instance;
        }

        public T Get<T>() where T : class
        {
            object instance;
            if (mInstances.TryGetValue(typeof(T), out instance))
                return instance as T;

            return null;
        }

        public IEnumerable<T> GetInstancesByType<T>()
        {
            var type = typeof(T);
            return mInstances.Values.Where(instance => type.IsInstanceOfType(instance)).Cast<T>();
        }

        public void Clear() => mInstances.Clear();
    }
}
