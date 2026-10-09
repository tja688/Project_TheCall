using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>
    /// 各界面共用的设置层。按钮在每个界面里，窗口盖在所有界面上面。
    /// 图鉴和结算记录都从这里再打开一层。
    /// </summary>
    public sealed class SettingsChromeView : MonoBehaviour
    {
        public Button[] openButtons;
        public GameObject settingsWindow;
        public Button quitButton;
        public Button codexButton;
        public Button logButton;
        public Button settingsCloseButton;
        public Button settingsShadeButton;
        public SkillCodexView codex;
        public ProductionLogView log;

        bool _logFromSettings;
        bool _logHooked;

        public bool SettingsOpen => settingsWindow != null && settingsWindow.activeSelf;

        public bool CodexOpen => codex != null && codex.IsOpen;

        public bool LogOpen => log != null && log.IsOpen;

        public bool LogOpenedFromSettings => _logFromSettings;

        void Awake()
        {
            Prepare();
            HideAll();
            if (settingsCloseButton != null)
                settingsCloseButton.onClick.AddListener(HideAll);
            if (settingsShadeButton != null)
                settingsShadeButton.onClick.AddListener(HideAll);
            if (codex == null)
                return;

            if (codex.closeButton != null)
                codex.closeButton.onClick.AddListener(CloseCodex);
            if (codex.shadeButton != null)
                codex.shadeButton.onClick.AddListener(CloseCodex);
        }

        public void Prepare()
        {
            if (log == null)
                log = FindAnyObjectByType<ProductionLogView>(FindObjectsInactive.Include);

            EnsureLogButton();
            HookLog();
        }

        public void ShowSettings()
        {
            _logFromSettings = false;
            if (codex != null)
                codex.Hide();
            if (log != null)
                log.Hide();
            if (settingsWindow != null)
                settingsWindow.SetActive(true);
        }

        public void HideAll()
        {
            _logFromSettings = false;
            if (codex != null)
                codex.Hide();
            if (log != null)
                log.Hide();
            if (settingsWindow != null)
                settingsWindow.SetActive(false);
        }

        public void ShowCodex(IReadOnlyList<MonsterSkillDetail> skills)
        {
            _logFromSettings = false;
            if (log != null)
                log.Hide();
            if (settingsWindow != null)
                settingsWindow.SetActive(false);
            if (codex != null)
                codex.Show(skills);
        }

        public void CloseCodex()
        {
            if (codex != null)
                codex.Hide();
            if (settingsWindow != null)
                settingsWindow.SetActive(true);
        }

        public void ShowLog(string text)
        {
            _logFromSettings = true;
            if (codex != null)
                codex.Hide();
            if (settingsWindow != null)
                settingsWindow.SetActive(false);
            if (log != null)
                log.Show(text);
        }

        public void CloseLog()
        {
            if (log != null && log.IsOpen)
                log.Hide();
            else
                ReturnFromLog();
        }

        void EnsureLogButton()
        {
            if (logButton != null || codexButton == null)
                return;

            var parent = codexButton.transform.parent;
            var existing = parent != null ? parent.Find("LogButton") : null;
            if (existing != null)
            {
                logButton = existing.GetComponent<Button>();
                return;
            }

            var clone = Instantiate(codexButton.gameObject, parent);
            clone.name = "LogButton";
            clone.transform.SetSiblingIndex(codexButton.transform.GetSiblingIndex() + 1);
            logButton = clone.GetComponent<Button>();
            var labels = clone.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < labels.Length; i++)
                labels[i].text = "结算记录";

            var panel = codexButton.transform.parent as RectTransform;
            if (panel != null && panel.sizeDelta.y < 540f)
                panel.sizeDelta = new Vector2(panel.sizeDelta.x, 560f);
        }

        void HookLog()
        {
            if (_logHooked || log == null)
                return;

            log.Closed += OnLogClosed;
            _logHooked = true;
        }

        void OnLogClosed() => ReturnFromLog();

        void ReturnFromLog()
        {
            if (!_logFromSettings)
                return;

            _logFromSettings = false;
            if (settingsWindow != null)
                settingsWindow.SetActive(true);
        }
    }
}
