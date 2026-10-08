using UnityEngine;
using UnityEngine.EventSystems;

namespace TheCall
{
    public sealed class OperationPayloadDrag : MonoBehaviour,
        IPointerDownHandler,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerUpHandler
    {
        public PayloadKind payloadKind;
        public OperationPointer session;

        public DropPayload ReadPayload()
        {
            if (payloadKind == PayloadKind.Monster)
            {
                var slot = GetComponent<MonsterSlotView>();
                return DropPayload.Monster(slot != null ? slot.monsterId : null);
            }

            var chip = GetComponent<SkillChipView>();
            var index = chip != null ? chip.index : -1;
            if (payloadKind == PayloadKind.SkillChip)
                return DropPayload.SkillChip(index);

            return DropPayload.BreedingSkill(index);
        }

        public bool HasPayload()
        {
            if (payloadKind == PayloadKind.Monster)
            {
                var slot = GetComponent<MonsterSlotView>();
                return slot != null && !string.IsNullOrEmpty(slot.monsterId);
            }

            var chip = GetComponent<SkillChipView>();
            if (chip == null)
                return false;

            if (payloadKind == PayloadKind.SkillChip)
                return chip.button != null && chip.button.interactable;

            return chip.label != null && chip.label.text != "投入技能";
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!TrySession(out var pointer))
                return;

            pointer.Press(this, eventData);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) =>
            eventData.useDragThreshold = true;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!TrySession(out var pointer))
                return;

            pointer.BeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!TrySession(out var pointer))
                return;

            pointer.MoveGhost(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!TrySession(out var pointer))
                return;

            pointer.Finish(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!TrySession(out var pointer))
                return;

            pointer.Finish(eventData);
        }

        bool TrySession(out OperationPointer pointer)
        {
            if (session == null)
                session = FindAnyObjectByType<OperationPointer>();

            pointer = session;
            return pointer != null;
        }
    }
}
