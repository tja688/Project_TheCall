using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>
    /// 各界面共用的设置层。按钮在每个界面里，窗口盖在所有界面上面。
    /// </summary>
    public sealed class SettingsChromeView : MonoBehaviour
    {
        public Button[] openButtons;
        public GameObject settingsWindow;
        public Button quitButton;
        public Button codexButton;
        public Button settingsCloseButton;
        public Button settingsShadeButton;
        public SkillCodexView codex;

        public bool SettingsOpen => settingsWindow != null && settingsWindow.activeSelf;

        public bool CodexOpen => codex != null && codex.IsOpen;

        void Awake()
        {
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

        public void ShowSettings()
        {
            if (codex != null)
                codex.Hide();
            if (settingsWindow != null)
                settingsWindow.SetActive(true);
        }

        public void HideAll()
        {
            if (codex != null)
                codex.Hide();
            if (settingsWindow != null)
                settingsWindow.SetActive(false);
        }

        public void ShowCodex(IReadOnlyList<MonsterSkillDetail> skills)
        {
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
    }
}
