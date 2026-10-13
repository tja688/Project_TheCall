using System;
using System.Collections.Generic;
using UnityEngine.Events;

namespace TheCall
{
    // Remove only callbacks owned by this controller. Inspector animation callbacks survive.
    internal sealed class ButtonBindings : IDisposable
    {
        readonly List<(UnityEngine.UI.Button button, UnityAction action)> _listeners = new();

        public void Listen(UnityEngine.UI.Button button, UnityAction action)
        {
            if (button == null || action == null) return;
            button.onClick.AddListener(action);
            _listeners.Add((button, action));
        }

        public void Dispose()
        {
            foreach (var listener in _listeners)
                if (listener.button != null) listener.button.onClick.RemoveListener(listener.action);
            _listeners.Clear();
        }
    }
}
