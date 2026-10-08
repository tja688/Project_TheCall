using System;
using System.Collections.Generic;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class MonsterHover : MonoBehaviour, IController
    {
        [SerializeField] RectTransform _window;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _skillLine;
        [SerializeField] TMP_Text _modifier;
        [SerializeField] TMP_Text _capacity;
        [SerializeField] GameObject _immovable;
        [SerializeField] MonsterPortrait _portrait;
        [SerializeField] RectTransform[] _subpanels;
        [SerializeField] TMP_Text[] _subNames;
        [SerializeField] TMP_Text[] _subMetas;
        [SerializeField] TMP_Text[] _subSentences;
        [SerializeField] Vector2 _cardSize = new Vector2(320f, 400f);
        [SerializeField] Vector2 _subpanelSize = new Vector2(300f, 156f);
        [SerializeField] float _slotGap = 12f;
        [SerializeField] float _stackGap = 12f;
        [SerializeField] float _subpanelGap = 8f;

        PressLatch _latch;
        readonly List<RaycastResult> _hits = new List<RaycastResult>();

        public IArchitecture GetArchitecture() => TheCallApp.Interface;

        void Update()
        {
            var group = _window.GetComponent<CanvasGroup>();
            if (group != null)
                group.blocksRaycasts = false;

            var pointer = HoverSample.ReadPointer();
            _latch = HoverSample.NextLatch(_latch, pointer);
            if (!HoverSample.TryRead(pointer, _latch, _hits, Canvas, out var target))
            {
                Hide();
                return;
            }

            var details = this.SendQuery(new MonsterDetailsQuery(target.MonsterId));
            if (details == null)
            {
                Hide();
                return;
            }

            var placement = HoverLayout.Place(
                target.AnchorInCanvas,
                target.PointerScreenX,
                Screen.width,
                details.Skills.Count,
                ReadMetrics());
            Show(details, placement);
        }

        void Hide()
        {
            if (!_window.gameObject.activeSelf)
                return;

            _window.gameObject.SetActive(false);
        }

        void Show(MonsterDetails details, in HoverPlacement placement)
        {
            _window.gameObject.SetActive(true);
            _title.text = details.DisplayName;
            _skillLine.text = SkillNames(details);
            _modifier.text = ModifierText(details.Modifier);
            _capacity.text = "产能 " + details.Capacity;
            _immovable.SetActive(details.Immovable);
            _immovable.GetComponent<TMP_Text>().text = "不动";
            _portrait.Show(details.Appearance);
            _window.anchoredPosition = placement.WindowAnchoredPosition;

            for (var i = 0; i < _subpanels.Length; i++)
            {
                var open = i < details.Skills.Count;
                _subpanels[i].gameObject.SetActive(open);
                if (!open)
                    continue;

                _subpanels[i].anchoredPosition = SubpanelLocal(placement, i);
                var skill = details.Skills[i];
                _subNames[i].text = skill.Name;
                _subMetas[i].text = MetaLine(skill);
                _subSentences[i].text = skill.Sentence;
            }

            var graphics = _window.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        RectTransform Canvas => (RectTransform)transform.parent;

        HoverMetrics ReadMetrics() =>
            new HoverMetrics(_cardSize, _subpanelSize, _slotGap, _stackGap, _subpanelGap, Canvas.rect);

        static Vector2 SubpanelLocal(in HoverPlacement placement, int index)
        {
            switch (index)
            {
                case 0: return placement.Sub0;
                case 1: return placement.Sub1;
                case 2: return placement.Sub2;
                default: return placement.Sub3;
            }
        }

        static string SkillNames(MonsterDetails details)
        {
            if (details.Skills.Count == 0)
                return "";

            var text = details.Skills[0].Name;
            for (var i = 1; i < details.Skills.Count; i++)
                text += " · " + details.Skills[i].Name;

            return text;
        }

        static string ModifierText(int modifier)
        {
            if (modifier > 0)
                return "修正 +" + modifier;

            return "修正 " + modifier;
        }

        static string MetaLine(MonsterSkillDetail skill)
        {
            var use = skill.Kind switch
            {
                SkillUse.Active => "主动",
                SkillUse.Passive => "被动",
                _ => throw new ArgumentOutOfRangeException(nameof(skill)),
            };
            var rarity = skill.Rarity switch
            {
                Rarity.White => "白",
                Rarity.Blue => "蓝",
                Rarity.Gold => "金",
                _ => throw new ArgumentOutOfRangeException(nameof(skill)),
            };
            return use + " " + rarity;
        }
    }
}
