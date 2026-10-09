using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    /// <summary>
    /// 技能图鉴。左边按稀有度列名字，右边显示和悬停技能栏相同的五项。
    /// </summary>
    public sealed class SkillCodexView : MonoBehaviour
    {
        static readonly Color WhiteInk = new Color32(242, 242, 242, 255);
        static readonly Color BlueInk = new Color32(126, 196, 255, 255);
        static readonly Color GoldInk = new Color32(240, 196, 96, 255);
        static readonly Color IdleInk = Color.white;
        static readonly Color PickedInk = new Color(0.72f, 0.95f, 0.82f, 1f);

        public Button closeButton;
        public Button shadeButton;
        public ScrollRect scroll;
        public RectTransform listContent;
        public Button rowTemplate;
        public TMP_Text headerTemplate;
        public TMP_Text nameLabel;
        public TMP_Text useLabel;
        public TMP_Text rarityLabel;
        public TMP_Text functionLabel;
        public TMP_Text sentenceLabel;

        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<Row> _rows = new List<Row>();
        IReadOnlyList<MonsterSkillDetail> _skills = Array.Empty<MonsterSkillDetail>();
        int _selected = -1;

        public bool IsOpen => gameObject.activeSelf;

        void Awake()
        {
            if (rowTemplate != null)
                rowTemplate.gameObject.SetActive(false);
            if (headerTemplate != null)
                headerTemplate.gameObject.SetActive(false);
        }

        public void Show(IReadOnlyList<MonsterSkillDetail> skills)
        {
            gameObject.SetActive(true);
            _skills = skills ?? Array.Empty<MonsterSkillDetail>();
            Rebuild();
            if (_rows.Count > 0)
                Select(_rows[0].Index);
            else
                ClearDetail();

            StopAllCoroutines();
            StartCoroutine(PinToTop());
        }

        public void Hide()
        {
            if (!gameObject.activeSelf)
                return;

            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        void Rebuild()
        {
            ClearSpawned();
            AddGroup(Rarity.White, "白");
            AddGroup(Rarity.Blue, "蓝");
            AddGroup(Rarity.Gold, "金");
        }

        void AddGroup(Rarity rarity, string title)
        {
            var any = false;
            for (var i = 0; i < _skills.Count; i++)
            {
                if (_skills[i] != null && _skills[i].Rarity == rarity)
                    any = true;
            }

            if (!any || listContent == null)
                return;

            if (headerTemplate != null)
            {
                var header = Instantiate(headerTemplate, listContent);
                header.gameObject.SetActive(true);
                header.text = title;
                header.color = RarityInk(rarity);
                _spawned.Add(header.gameObject);
            }

            for (var i = 0; i < _skills.Count; i++)
            {
                var skill = _skills[i];
                if (skill == null || skill.Rarity != rarity || rowTemplate == null)
                    continue;

                var index = i;
                var row = Instantiate(rowTemplate, listContent);
                row.gameObject.SetActive(true);
                var label = row.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = skill.Name;
                    label.color = RarityInk(skill.Rarity);
                }

                row.onClick.AddListener(() => Select(index));
                _rows.Add(new Row(index, row.targetGraphic as Image));
                _spawned.Add(row.gameObject);
            }
        }

        void Select(int index)
        {
            if (index < 0 || index >= _skills.Count || _skills[index] == null)
                return;

            _selected = index;
            var skill = _skills[index];
            if (nameLabel != null)
            {
                nameLabel.text = skill.Name;
                nameLabel.color = RarityInk(skill.Rarity);
            }

            if (useLabel != null)
                useLabel.text = UseText(skill.Kind);
            if (rarityLabel != null)
            {
                rarityLabel.text = RarityText(skill.Rarity);
                rarityLabel.color = RarityInk(skill.Rarity);
            }

            if (functionLabel != null)
                functionLabel.text = skill.Function;
            if (sentenceLabel != null)
            {
                sentenceLabel.richText = true;
                sentenceLabel.text = skill.DisplaySentence;
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].Plate == null)
                    continue;

                _rows[i].Plate.color = _rows[i].Index == _selected ? PickedInk : IdleInk;
            }
        }

        void ClearDetail()
        {
            _selected = -1;
            if (nameLabel != null)
                nameLabel.text = "";
            if (useLabel != null)
                useLabel.text = "";
            if (rarityLabel != null)
                rarityLabel.text = "";
            if (functionLabel != null)
                functionLabel.text = "";
            if (sentenceLabel != null)
                sentenceLabel.text = "";
        }

        void ClearSpawned()
        {
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                    Destroy(_spawned[i]);
            }

            _spawned.Clear();
            _rows.Clear();
            _selected = -1;
        }

        IEnumerator PinToTop()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (listContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
        }

        static string UseText(SkillUse kind)
        {
            switch (kind)
            {
                case SkillUse.Active: return "主动";
                case SkillUse.Passive: return "被动";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        static string RarityText(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.White: return "白";
                case Rarity.Blue: return "蓝";
                case Rarity.Gold: return "金";
                default: throw new ArgumentOutOfRangeException(nameof(rarity));
            }
        }

        static Color RarityInk(Rarity rarity)
        {
            switch (rarity)
            {
                case Rarity.White: return WhiteInk;
                case Rarity.Blue: return BlueInk;
                case Rarity.Gold: return GoldInk;
                default: throw new ArgumentOutOfRangeException(nameof(rarity));
            }
        }

        readonly struct Row
        {
            public Row(int index, Image plate)
            {
                Index = index;
                Plate = plate;
            }

            public int Index { get; }

            public Image Plate { get; }
        }
    }
}
