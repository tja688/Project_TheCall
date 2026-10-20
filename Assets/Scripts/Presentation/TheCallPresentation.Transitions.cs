using System;
using System.Collections;
using UnityEngine;

namespace TheCall
{
    public sealed partial class TheCallPresentation
    {
        MonoBehaviour _visibleScreen;
        Coroutine _transition;
        ScreenAssembly _leavingAssembly;
        ScreenAssembly _arrivingAssembly;
        GameObject _transitionShield;
        bool _refreshAfterTransition;
        public bool Transitioning { get; private set; }

        void Show(MonoBehaviour screen, Action render)
        {
            if (!Application.isPlaying || _visibleScreen == null || _visibleScreen == screen || screen == null)
            {
                ShowImmediate(screen);
                _visibleScreen = screen;
                render();
                return;
            }
            _arrivingAssembly = screen.GetComponent<ScreenAssembly>();
            if (_arrivingAssembly == null)
            {
                ShowImmediate(screen);
                _visibleScreen = screen;
                render();
                return;
            }
            _leavingAssembly = _visibleScreen.GetComponent<ScreenAssembly>();
            Transitioning = true;
            Shield(true);
            var panorama = _operation != null ? _operation.pointer?.panorama : null;
            if (panorama != null) panorama.Locked = true;
            _transition = StartCoroutine(ChangeScreen(screen, render));
        }

        IEnumerator ChangeScreen(MonoBehaviour screen, Action render)
        {
            if (_leavingAssembly != null)
            {
                _leavingAssembly.Prepare(false);
                yield return AnimateAssembly(_leavingAssembly);
                _leavingAssembly.Restore();
            }
            ShowImmediate(screen);
            _visibleScreen = screen;
            render();
            Canvas.ForceUpdateCanvases();
            _arrivingAssembly.Prepare(true);
            yield return AnimateAssembly(_arrivingAssembly);
            _arrivingAssembly.Restore();
            _leavingAssembly = _arrivingAssembly = null;
            _transition = null;
            Transitioning = false;
            Shield(false);
            var panorama = _operation != null ? _operation.pointer?.panorama : null;
            if (panorama != null) panorama.Locked = false;
            if (_refreshAfterTransition)
            {
                _refreshAfterTransition = false;
                Refresh();
            }
        }

        static IEnumerator AnimateAssembly(ScreenAssembly assembly)
        {
            var time = 0f;
            while (assembly != null && time < assembly.Duration)
            {
                assembly.Sample(time);
                yield return null;
                time += Time.unscaledDeltaTime;
            }
            if (assembly != null) assembly.Sample(assembly.Duration);
        }

        void CancelTransition()
        {
            if (_transition != null) StopCoroutine(_transition);
            _transition = null;
            if (_leavingAssembly != null) _leavingAssembly.Restore();
            if (_arrivingAssembly != null) _arrivingAssembly.Restore();
            _leavingAssembly = _arrivingAssembly = null;
            Transitioning = false;
            _refreshAfterTransition = false;
            Shield(false);
        }

        void Shield(bool active)
        {
            if (active && _transitionShield == null && _opening != null)
            {
                var node = new GameObject("ScreenTransitionShield", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                node.hideFlags = HideFlags.DontSave;
                var rect = (RectTransform)node.transform;
                rect.SetParent(_opening.transform.parent, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                node.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
                _transitionShield = node;
            }
            if (_transitionShield != null)
            {
                _transitionShield.SetActive(active);
                if (active) _transitionShield.transform.SetAsLastSibling();
            }
            if (active && UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
