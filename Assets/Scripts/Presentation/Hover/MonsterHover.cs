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
        static readonly Color WhiteInk = new Color32(242, 242, 242, 255);
        static readonly Color BlueInk = new Color32(126, 196, 255, 255);
        static readonly Color GoldInk = new Color32(240, 196, 96, 255);

        [SerializeField] RectTransform _window;
        [SerializeField] RectTransform[] _subpanels;
        [SerializeField] TMP_Text[] _subNames;
        [SerializeField] TMP_Text[] _subUses;
        [SerializeField] TMP_Text[] _subRarities;
        [SerializeField] TMP_Text[] _subFunctions;
        [SerializeField] TMP_Text[] _subSentences;
        [SerializeField] Vector2 _subpanelSize = new Vector2(300f, 156f);
        [SerializeField] float _slotGap = 12f;
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
                _subUses[i].text = UseText(skill.Kind);
                _subRarities[i].text = RarityText(skill.Rarity);
                _subRarities[i].color = RarityInk(skill.Rarity);
                _subFunctions[i].text = skill.Function;
                _subSentences[i].text = skill.DisplaySentence;
            }

            var graphics = _window.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        RectTransform Canvas => (RectTransform)transform.parent;

        HoverMetrics ReadMetrics() =>
            new HoverMetrics(_subpanelSize, _slotGap, _subpanelGap, Canvas.rect);

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
    }
}
