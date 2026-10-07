using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TheCall
{
    public sealed class MonsterSlotView : MonoBehaviour
    {
        public Button button;
        public MonsterPortrait portrait;
        public TMP_Text title;
        public TMP_Text subtitle;
        public GameObject emptyMark;
        public GameObject selection;
        public int index;
        public string monsterId;
    }
}
