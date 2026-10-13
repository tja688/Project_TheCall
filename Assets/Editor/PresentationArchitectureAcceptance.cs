using System;
using System.Linq;
using System.Reflection;
using QFramework;
using TheCall.Scoring;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TheCall.Editor
{
    public static class PresentationArchitectureAcceptance
    {
        static TheCallPresentation Presentation => Object.FindAnyObjectByType<TheCallPresentation>();
        static OperationScreenView Operation => Object.FindAnyObjectByType<OperationScreenView>(FindObjectsInactive.Include);
        static Button _adapterOriginal;
        static Button _adapterClone;
        static int _animationCalls;
        static void Need(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("PRESENTATION_FAIL: " + message);
        }

        public static void PrepareButtonAdapter()
        {
            var chrome = Object.FindAnyObjectByType<SettingsChromeView>(FindObjectsInactive.Include);
            chrome.HideAll();
            _adapterOriginal = chrome.openButtons.First(button => button.gameObject.activeInHierarchy);
            _adapterClone = Object.Instantiate(_adapterOriginal, _adapterOriginal.transform.parent);
            _adapterClone.name = "ArtSettingsButton";
            _adapterClone.onClick = new Button.ButtonClickedEvent();
            _animationCalls = 0;
            _adapterClone.onClick.AddListener(() => _animationCalls++);
            var action = _adapterClone.gameObject.AddComponent<PresentationActionButton>();
            action.Configure(null, PresentationAction.ToggleSettings);
            _adapterOriginal.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
        }

        public static string CheckButtonAdapter()
        {
            var chrome = Object.FindAnyObjectByType<SettingsChromeView>(FindObjectsInactive.Include);
            Need(_adapterClone != null, "Prepare the new art button and let it render first");
            try
            {
                HandbookAcceptance.ClickVisible(_adapterClone.name);
                Need(chrome.SettingsOpen && _animationCalls == 1, "Declared button or artist callback missing/duplicated");
                _adapterClone.gameObject.SetActive(false);
                _adapterClone.gameObject.SetActive(true);
                _adapterClone.onClick.Invoke();
                Need(!chrome.SettingsOpen && _animationCalls == 2, "Button reenable accumulated bindings");
            }
            finally
            {
                _adapterOriginal.gameObject.SetActive(true);
                chrome.HideAll();
                Object.DestroyImmediate(_adapterClone.gameObject);
                _adapterClone = null;
            }
            return "PASS new art button, actual raycast click, enable/disable, artist callback retained";
        }

        public static string CheckLegacyButtonAdapter()
        {
            var chrome = Object.FindAnyObjectByType<SettingsChromeView>(FindObjectsInactive.Include);
            var button = chrome.openButtons.First(item => item.gameObject.activeInHierarchy);
            var adapter = button.gameObject.AddComponent<PresentationActionButton>();
            try
            {
                foreach (var state in new[] { 0, 1, 2, 3, 1 })
                {
                    chrome.HideAll();
                    adapter.enabled = state != 2;
                    adapter.Configure(null, state == 0 || state == 3 ? PresentationAction.None : PresentationAction.ToggleSettings);
                    HandbookAcceptance.ClickVisible(button.name);
                    Need(chrome.SettingsOpen, "Legacy/new adapter double-dispatched or lost input at state " + state);
                }
            }
            finally
            {
                chrome.HideAll();
                Object.DestroyImmediate(adapter);
            }
            return "PASS existing button switches None/enabled/disabled without double dispatch";
        }

        public static string CheckControllerReenable()
        {
            var chrome = Object.FindAnyObjectByType<SettingsChromeView>(FindObjectsInactive.Include);
            var presentation = Presentation;
            chrome.HideAll();
            presentation.enabled = false;
            presentation.enabled = true;
            var visible = chrome.openButtons.First(button => button.gameObject.activeInHierarchy);
            visible.onClick.Invoke();
            Need(chrome.SettingsOpen, "Reenabled controller has duplicate/missing listeners");
            visible.onClick.Invoke();
            Need(!chrome.SettingsOpen, "Second click should close settings");
            return "PASS controller reenable has exactly one gameplay listener";
        }

        public static string CheckRefreshDuringPlayback()
        {
            Need(ScoringShow.IsPlaying, "Start a real settlement first");
            var phase = TheCallApp.Interface.SendQuery(new RunPhaseQuery());
            var show = Object.FindAnyObjectByType<ScoringShow>();
            Presentation.Refresh();
            Presentation.Refresh();
            Need(Operation.gameObject.activeInHierarchy && ScoringShow.IsPlaying && show != null, "Refresh hid live playback");
            Need(TheCallApp.Interface.SendQuery(new RunPhaseQuery()) == phase, "Refresh changed rules");
            return "PASS repeated Refresh keeps the operation stage visible until playback completes";
        }

        public static string CheckResetDuringPlayback()
        {
            Need(ScoringShow.IsPlaying, "Start playback before reset");
            Presentation.Perform(PresentationAction.ReturnToTitle);
            Need(!ScoringShow.IsPlaying, "Reset left a live playback");
            Need(TheCallApp.Interface.SendQuery(new RunPhaseQuery()) == RunPhase.Opening, "Reset failed to reopen opening");
            Need(Object.FindAnyObjectByType<OpeningScreenView>(FindObjectsInactive.Include).gameObject.activeInHierarchy, "Opening not visible");
            return "PASS playback canceled before Architecture reset; new opening visible";
        }

        public static string CheckOwnerDisableDuringPlayback()
        {
            Need(ScoringShow.IsPlaying, "Start playback before disabling owner");
            var presentation = Presentation;
            var phase = TheCallApp.Interface.SendQuery(new RunPhaseQuery());
            presentation.enabled = false;
            Need(!ScoringShow.IsPlaying, "Disabled owner retained playback");
            presentation.enabled = true;
            Need(TheCallApp.Interface.SendQuery(new RunPhaseQuery()) == phase, "Disabling owner changed settlement result");
            Need(!Operation.transform.GetComponentsInChildren<Transform>(true).Any(t => t.name == "ScoringBlocker" && t.gameObject.activeInHierarchy), "Blocker remains active");
            return "PASS owner disable cancels playback and reenable presents committed phase";
        }

        public static string CheckOptionalOperationControls()
        {
            Need(TheCallApp.Interface.SendQuery(new RunPhaseQuery()) == RunPhase.Operation, "Enter operation first");
            var operation = Operation;
            var fields = typeof(OperationScreenView).GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => typeof(TMPro.TMP_Text).IsAssignableFrom(field.FieldType)
                    || typeof(Button).IsAssignableFrom(field.FieldType)).ToArray();
            var previous = fields.Select(field => field.GetValue(operation)).ToArray();
            var state = M5PlayModeAutomation.State();
            try
            {
                foreach (var field in fields) field.SetValue(operation, null);
                Presentation.Refresh();
                Need(M5PlayModeAutomation.State() == state, "Optional view removal changed rules");
            }
            finally
            {
                for (var i = 0; i < fields.Length; i++) fields[i].SetValue(operation, previous[i]);
                Presentation.Refresh();
            }
            return "PASS operation labels/buttons can be absent without changing run state";
        }
    }
}
