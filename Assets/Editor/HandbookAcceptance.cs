using System;
using System.Collections.Generic;
using System.Linq;
using QFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheCall.Editor
{
    /// <summary>在真实 Play Mode 中复测入口、分类、搜索、返回和对局不变。</summary>
    public static class HandbookAcceptance
    {
        static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException("HANDBOOK_FAIL: " + message); }
        static SettingsChromeView Chrome => UnityEngine.Object.FindAnyObjectByType<SettingsChromeView>(FindObjectsInactive.Include);
        public static string Run()
        {
            Check(Application.isPlaying, "请在 Play Mode 中运行。");
            var chrome = Chrome;
            Check(chrome != null, "设置层缺失");
            var app = TheCallApp.Interface; var run = app.GetModel<RunModel>();
            var phase = run.Phase; var gold = run.Gold; var tech = run.TechPoints;
            var monsters = string.Join(",", run.Cage.Select(m => m.Id));
            var entries = app.SendQuery(new HandbookIndexQuery());
            chrome.HideAll();
            foreach (var button in chrome.openButtons)
            {
                // 所有阶段的绑定契约，包含当前未激活界面；实际可见入口另由 ClickVisible 检查。
                button.onClick.Invoke(); Check(chrome.SettingsOpen, button.transform.parent.name + "入口无响应或重复绑定");
                chrome.codexButton.onClick.Invoke(); Check(chrome.CodexOpen && !chrome.SettingsOpen, "手册入口");
                chrome.codex.closeButton.onClick.Invoke(); Check(chrome.SettingsOpen && !chrome.CodexOpen, "关闭手册返回设置");
                chrome.settingsCloseButton.onClick.Invoke(); Check(!chrome.SettingsOpen, "关闭设置返回游戏");
            }
            chrome.openButtons.First(b => b.gameObject.activeInHierarchy).onClick.Invoke(); chrome.codexButton.onClick.Invoke();
            var book = chrome.codex;
            for (var category = 0; category < book.categoryButtons.Length; category++)
            {
                book.categoryButtons[category].onClick.Invoke();
                Check(book.VisibleCount == entries.Count(e => (int)e.Category == category), "分类数量 " + category);
                Check(!string.IsNullOrWhiteSpace(book.ruleLabel.text), "分类正文 " + category);
            }
            book.categoryButtons[(int)HandbookCategory.Personality].onClick.Invoke(); book.rarityButtons[4].onClick.Invoke();
            Check(book.VisibleCount == entries.Count(e => e.Category == HandbookCategory.Personality && e.Rarity == Rarity.Negative), "负面性格筛选");
            book.categoryButtons[(int)HandbookCategory.Skill].onClick.Invoke(); book.search.text = "产生5点能量";
            Check(book.VisibleCount == entries.Count(e => e.Category == HandbookCategory.Skill && e.Matches("产生5点能量")), "效果全文搜索");
            book.search.text = "missing-entry-001"; Check(book.VisibleCount == 0 && book.emptyLabel.gameObject.activeSelf, "空结果反馈");
            book.clearButton.onClick.Invoke(); Check(book.VisibleCount == entries.Count(e => e.Category == HandbookCategory.Skill), "清空搜索");
            Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(book.listContent);
            book.scroll.Rebuild(CanvasUpdate.PostLayout);
            var beforeScroll = book.listContent.anchoredPosition;
            book.scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -3) });
            Check(book.listContent.anchoredPosition != beforeScroll, "技能列表滚轮无响应");
            book.categoryButtons[(int)HandbookCategory.Guide].onClick.Invoke();
            var link = book.relatedContent.GetComponentsInChildren<Button>().First(b => b != book.relatedTemplate);
            link.onClick.Invoke(); Check(book.Category != HandbookCategory.Guide, "关联条目导航");
            var selected = book.SelectedId; book.closeButton.onClick.Invoke(); chrome.codexButton.onClick.Invoke(); Check(book.SelectedId == selected, "关闭后保留阅读位置");
            Check(run.Phase == phase && run.Gold == gold && run.TechPoints == tech && string.Join(",", run.Cage.Select(m => m.Id)) == monsters, "阅读修改了对局");
            book.categoryButtons[(int)HandbookCategory.Guide].onClick.Invoke();
            chrome.CloseCodex(); chrome.HideAll();
            return "PASS: 6 stage bindings, 8 categories / " + entries.Count + " entries, negative rarity, effect search, empty state, clear, wheel scrolling, related navigation, return/reopen, read-only run state. frame=" + Time.frameCount;
        }

        public static string ClickVisible(string name)
        {
            var button = UnityEngine.Object.FindObjectsByType<Button>().First(b => b.name == name);
            var rect = (RectTransform)button.transform;
            var position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, "按钮被遮挡 " + name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            return "PASS visible raycast+pointer click: " + name + " at " + position;
        }

        public static string ValidateSaved()
        {
            var chrome = Chrome;
            Check(chrome != null && chrome.settingsWindow != null && chrome.codex != null, "保存场景缺少设置或手册");
            Check(chrome.openButtons.Length == 6 && chrome.openButtons.Distinct().Count() == 6 && chrome.openButtons.All(b => b != null), "入口数量或重复绑定");
            Check(chrome.settingsCloseButton != null && chrome.settingsShadeButton != null && chrome.logButton != null && chrome.log != null, "设置返回与记录引用");
            Check(chrome.masterVolume != null && chrome.musicVolume != null && chrome.sfxVolume != null, "音量引用");
            var book = chrome.codex;
            Check(book.categoryButtons.Length == 8 && book.rarityButtons.Length == 5 && book.search != null, "搜索筛选引用");
            foreach (var scroll in new[] { book.scroll, book.detailScroll })
                Check(scroll != null && scroll.content != null && scroll.viewport.GetComponent<Mask>() != null && scroll.verticalScrollbar != null && scroll.content.GetComponent<ContentSizeFitter>() != null, "滚动结构不完整");
            Check(book.closeButton != null && book.shadeButton != null && book.ruleLabel != null && book.relatedTemplate != null, "详情引用");
            return "PASS saved Main scene: six entries, settings, handbook, scroll/mask/scrollbars, search, links, audio, close/backdrop and log references.";
        }
    }
}
