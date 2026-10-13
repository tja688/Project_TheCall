using UnityEngine;
using UnityEngine.UI;
using QFramework;

namespace TheCall
{
    public sealed class OpeningScreenView : MonoBehaviour
    {
        public MonsterSlotView[] candidates;
        public Button exitButton;

        public void Render(IArchitecture architecture, PresentationSpriteCatalog catalog)
        {
            var monsters = architecture.SendQuery(new OpeningCandidatesQuery());
            if (candidates == null) return;
            for (var i = 0; i < candidates.Length; i++)
            {
                var slot = candidates[i];
                if (slot == null) continue;
                var monster = i < monsters.Count ? monsters[i] : null;
                PresentationReadout.Monster(slot, monster, false, catalog);
                slot.gameObject.SetActive(monster != null);
            }
        }
    }
}
