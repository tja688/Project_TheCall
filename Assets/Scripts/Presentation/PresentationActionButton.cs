using UnityEngine;

namespace TheCall
{
    public enum PresentationAction
    {
        None = 0, BeginLevel = 1, Settle = 2, ReturnToTitle = 3, OpenResearch = 4,
        CloseResearch = 5, Research = 6, BuyTab = 7, SellTab = 8, RefreshShelf = 9,
        LeaveShop = 10, AddGeneSlot = 11, ToggleSettings = 12, OpenHandbook = 13,
        ToggleLog = 14, Quit = 15, OpenSettingsLog = 16
    }

    // A new art prefab can declare its intent without adding gameplay to its animation script.
    [RequireComponent(typeof(UnityEngine.UI.Button))]
    [DisallowMultipleComponent]
    public sealed class PresentationActionButton : MonoBehaviour
    {
        [SerializeField] TheCallPresentation _presentation;
        [SerializeField] PresentationAction _action;
        UnityEngine.UI.Button _button;

        public PresentationAction Action => _action;

        public void Configure(TheCallPresentation presentation, PresentationAction action)
        {
            _presentation = presentation;
            _action = action;
            // Runtime compatibility controls may replace their cloned UnityEvent.
            if (_button != null)
            {
                _button.onClick.RemoveListener(Invoke);
                if (isActiveAndEnabled) _button.onClick.AddListener(Invoke);
            }
        }

        void OnEnable()
        {
            _button = GetComponent<UnityEngine.UI.Button>();
            _button.onClick.AddListener(Invoke);
        }

        void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(Invoke);
        }

        void Invoke()
        {
            if (_action == PresentationAction.None) return;
            if (_presentation == null)
                _presentation = GetComponentInParent<TheCallPresentation>(true)
                    ?? GetComponentInParent<Canvas>()?.GetComponentInChildren<TheCallPresentation>(true);
            // Main keeps its controller beside UICanvas as a separate scene root.
            if (_presentation == null && gameObject.scene.IsValid())
            {
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    foreach (var candidate in root.GetComponentsInChildren<TheCallPresentation>(true))
                    {
                        if (_presentation != null) { _presentation = null; return; }
                        _presentation = candidate;
                    }
                }
            }
            if (_presentation != null && _presentation.isActiveAndEnabled)
                _presentation.Perform(_action);
        }
    }
}
