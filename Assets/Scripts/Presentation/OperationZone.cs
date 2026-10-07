using UnityEngine;
using UnityEngine.EventSystems;

namespace TheCall
{
    public sealed class OperationZone : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public LandingPlace Place;
        public MonsterSlotView monsterView;
        public SkillChipView chipView;

        public DropLanding ReadLanding()
        {
            if (monsterView != null)
                return new DropLanding(Place, monsterView.index, monsterView.monsterId);

            if (chipView != null)
                return new DropLanding(Place, chipView.index, null);

            return new DropLanding(Place, 0, null);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (GetComponent<OperationPayloadDrag>() != null)
                return;

            var session = GetComponentInParent<OperationPointer>();
            if (session != null)
                session.Press(null, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (GetComponent<OperationPayloadDrag>() != null)
                return;

            var session = GetComponentInParent<OperationPointer>();
            if (session != null)
                session.Finish(eventData);
        }
    }
}
