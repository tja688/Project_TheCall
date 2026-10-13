using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>分类、搜索和关联阅读只改变界面选择，不写入对局。</summary>
    public sealed class HandbookView : MonoBehaviour
    {
        public Button closeButton, shadeButton, clearButton;
        public TMP_InputField search;
        public Button[] categoryButtons, rarityButtons;
        public ScrollRect scroll, detailScroll;
        public RectTransform listContent, relatedContent;
        public Button rowTemplate, relatedTemplate;
        public TMP_Text countLabel, emptyLabel, nameLabel, metadataLabel, ruleLabel, adviceLabel, locationLabel;
        public Image icon;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly Dictionary<string, Button> _rows = new Dictionary<string, Button>();
        readonly List<GameObject> _links = new List<GameObject>();
        IReadOnlyList<HandbookEntry> _entries = Array.Empty<HandbookEntry>();
        HandbookCategory _category;
        Rarity? _rarity;
        string _selectedId;
        bool _wired;
        PresentationSpriteCatalog _art;
        static readonly Color Idle = new Color32(36, 52, 66, 255);
        static readonly Color Picked = new Color32(32, 99, 82, 255);
        static readonly Color Ink = new Color32(232, 244, 240, 255);
        static readonly Rarity[] Rarities = { Rarity.White, Rarity.Blue, Rarity.Gold, Rarity.Negative };
        public static readonly string[] Categories = { "入门与玩法", "技能", "性格", "种族", "心情", "工具", "科技", "词条" };

        public bool IsOpen => gameObject.activeSelf;
        public int VisibleCount => _rows.Count;
        public string SelectedId => _selectedId;
        public HandbookCategory Category => _category;

        void Prepare()
        {
            if (_wired) return;
            _wired = true;
            _art = Resources.Load<PresentationSpriteCatalog>("PresentationSpriteCatalog");
            rowTemplate.gameObject.SetActive(false);
            relatedTemplate.gameObject.SetActive(false);
            for (var i = 0; i < categoryButtons.Length; i++)
            {
                var category = (HandbookCategory)i;
                categoryButtons[i].onClick.AddListener(() => ChooseCategory(category));
            }
            for (var i = 0; i < rarityButtons.Length; i++)
            {
                var index = i;
                rarityButtons[i].onClick.AddListener(() => { _rarity = index == 0 ? (Rarity?)null : Rarities[index - 1]; Rebuild(); });
            }
            search.onValueChanged.AddListener(_ => Rebuild());
            clearButton.onClick.AddListener(() => search.text = "");
        }

        public void Show(IReadOnlyList<HandbookEntry> entries)
        {
            Prepare();
            _entries = entries ?? Array.Empty<HandbookEntry>();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Rebuild();
        }

        public void Hide()
        {
            if (search.isFocused) search.DeactivateInputField();
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        void ChooseCategory(HandbookCategory category)
        {
            _category = category;
            _rarity = null;
            search.SetTextWithoutNotify("");
            Rebuild();
        }

        void Rebuild()
        {
            Clear(_spawned);
            _rows.Clear();
            var categoryCount = 0;
            HandbookEntry first = null, selected = null;
            foreach (var entry in _entries)
            {
                if (entry.Category != _category) continue;
                categoryCount++;
                if ((_rarity.HasValue && entry.Rarity != _rarity) || !entry.Matches(search.text)) continue;
                var row = Instantiate(rowTemplate, listContent);
                row.name = "Entry_" + entry.Id;
                row.gameObject.SetActive(true);
                var label = row.GetComponentInChildren<TMP_Text>(true);
                label.text = entry.Name + (entry.Rarity.HasValue ? "  <size=70%>" + HandbookIndexQuery.RarityName(entry.Rarity.Value) + "</size>" : "");
                label.color = RarityInk(entry.Rarity);
                row.onClick.AddListener(() => Select(entry));
                _spawned.Add(row.gameObject); _rows.Add(entry.Id, row);
                if (first == null) first = entry;
                if (entry.Id == _selectedId) selected = entry;
            }
            var hasRarity = _category == HandbookCategory.Skill || _category == HandbookCategory.Personality || _category == HandbookCategory.Tool;
            foreach (var button in rarityButtons) button.gameObject.SetActive(hasRarity);
            for (var i = 0; i < rarityButtons.Length; i++)
                rarityButtons[i].targetGraphic.color = (i == 0 ? !_rarity.HasValue : _rarity == Rarities[i - 1]) ? Picked : Idle;
            for (var i = 0; i < categoryButtons.Length; i++)
            {
                var count = 0;
                foreach (var entry in _entries) if ((int)entry.Category == i) count++;
                categoryButtons[i].GetComponentInChildren<TMP_Text>().text = Categories[i] + "   <size=75%>" + count + "</size>";
                categoryButtons[i].targetGraphic.color = (int)_category == i ? Picked : Idle;
            }
            countLabel.text = Categories[(int)_category] + "   " + _rows.Count + " / " + categoryCount + " 条";
            emptyLabel.gameObject.SetActive(_rows.Count == 0);
            clearButton.gameObject.SetActive(!string.IsNullOrEmpty(search.text));
            if (selected != null || first != null) Select(selected ?? first);
            else
            {
                _selectedId = null; Clear(_links); icon.enabled = false;
                nameLabel.text = "没有匹配条目"; metadataLabel.text = "试试清空搜索或更换筛选";
                ruleLabel.text = "搜索会匹配名称和效果说明。"; adviceLabel.text = ""; locationLabel.text = Categories[(int)_category];
            }
            StopAllCoroutines();
            StartCoroutine(PinToTop());
        }

        void Select(HandbookEntry entry)
        {
            _selectedId = entry.Id;
            nameLabel.text = entry.Name; nameLabel.color = RarityInk(entry.Rarity);
            metadataLabel.text = entry.Metadata;
            ruleLabel.text = entry.Rule; adviceLabel.text = entry.Advice;
            locationLabel.text = "玩家手册  /  " + Categories[(int)entry.Category];
            foreach (var row in _rows) row.Value.targetGraphic.color = row.Key == entry.Id ? Picked : Idle;
            icon.sprite = Icon(entry); icon.enabled = icon.sprite != null; icon.gameObject.SetActive(icon.sprite != null);
            Clear(_links);
            foreach (var id in entry.Related)
            {
                var target = Find(id);
                if (target == null) continue;
                var link = Instantiate(relatedTemplate, relatedContent);
                link.gameObject.SetActive(true); link.name = "Related_" + target.Id;
                link.GetComponentInChildren<TMP_Text>().text = Categories[(int)target.Category] + " · " + target.Name + "  ›";
                link.onClick.AddListener(() => Navigate(target));
                _links.Add(link.gameObject);
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(detailScroll.content);
            detailScroll.verticalNormalizedPosition = 1f;
        }

        void Navigate(HandbookEntry target)
        {
            _category = target.Category; _rarity = null; _selectedId = target.Id;
            search.SetTextWithoutNotify(""); Rebuild();
        }

        HandbookEntry Find(string id)
        {
            foreach (var entry in _entries) if (entry.Id == id) return entry;
            return null;
        }

        Sprite Icon(HandbookEntry entry)
        {
            if (_art == null) return null;
            switch (entry.Category)
            {
                case HandbookCategory.Personality: return _art.GetPersonalityIcon(entry.Name);
                case HandbookCategory.Race: return _art.GetRaceIcon(entry.Name);
                case HandbookCategory.Mood: return _art.GetMoodIcon(entry.Name);
                case HandbookCategory.Tool: return _art.FindTool(entry.Name)?.icon;
                default: return null;
            }
        }

        static void Clear(List<GameObject> objects)
        {
            foreach (var obj in objects) { obj.SetActive(false); Destroy(obj); }
            objects.Clear();
        }

        IEnumerator PinToTop()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            scroll.verticalNormalizedPosition = 1f;
            if (_selectedId != null && _rows.TryGetValue(_selectedId, out var row) && listContent.rect.height > scroll.viewport.rect.height)
            {
                var rect = (RectTransform)row.transform;
                var top = -rect.anchoredPosition.y - rect.rect.height * rect.pivot.y - listContent.GetComponent<VerticalLayoutGroup>().padding.top;
                scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(top / (listContent.rect.height - scroll.viewport.rect.height));
            }
        }

        static Color RarityInk(Rarity? rarity) => rarity switch
        {
            Rarity.Blue => new Color32(126, 196, 255, 255), Rarity.Gold => new Color32(240, 196, 96, 255),
            Rarity.Negative => new Color32(255, 146, 158, 255), _ => Ink
        };
    }
}
